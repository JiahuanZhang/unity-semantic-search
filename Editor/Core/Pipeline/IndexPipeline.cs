using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using SemanticSearch.Editor.Core.Database;
using SemanticSearch.Editor.Core.LLM;
using SemanticSearch.Editor.Core.Utils;

namespace SemanticSearch.Editor.Core.Pipeline
{
    public enum PipelineState
    {
        Idle,
        Running,
        Cancelled
    }

    public class IndexPipeline
    {
        static readonly string[] ImageExtensions = { ".png", ".jpg", ".jpeg", ".tga" };
        const int DbWriteBatchSize = 16;
        const int ProgressReportIntervalMs = 100;

        readonly SemanticSearchDB _db;
        readonly LLMApiConfig _config;
        readonly IVisionClient _vlClient;
        readonly IEmbeddingClient _embeddingClient;

        CancellationTokenSource _cts;

        public PipelineState State { get; private set; } = PipelineState.Idle;

        private class ProgressCounters
        {
            public int Total;
            public int Completed;
            public int Succeeded;
            public int Failed;
            public int Skipped;
            public string CurrentAsset;

            public BatchProgress Snapshot(string currentAsset = null)
            {
                return new BatchProgress(
                    Volatile.Read(ref Total),
                    Volatile.Read(ref Completed),
                    Volatile.Read(ref Succeeded),
                    Volatile.Read(ref Failed),
                    Volatile.Read(ref Skipped),
                    currentAsset ?? Volatile.Read(ref CurrentAsset)
                );
            }
        }

        class IndexSingleResult
        {
            public AssetRecord Record;
            public bool Success;
        }

        public IndexPipeline(SemanticSearchDB db, LLMApiConfig config)
        {
            _db = db;
            _config = config;
            var http = new LLMHttpClient(config);
            _vlClient = LLMClientFactory.CreateVisionClient(config, http);
            _embeddingClient = LLMClientFactory.CreateEmbeddingClient(config, http);
        }

        public async Task<bool> IndexSingleAsync(string assetPath, CancellationToken ct)
        {
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogError($"[SemanticSearch] Invalid asset path: {assetPath}");
                return false;
            }

            var existing = _db.GetByGuid(guid);
            var pendingRecord = new AssetRecord
            {
                Guid = guid,
                AssetPath = assetPath,
                Md5 = existing?.Md5 ?? "",
                Caption = existing?.Caption,
                Vector = existing?.Vector,
                VectorDim = existing?.VectorDim ?? 0,
                Status = Database.AssetStatus.Pending,
                UpdatedAt = DateTime.UtcNow.ToString("o")
            };

            var result = await BuildRecordForPendingAsync(pendingRecord, ct);
            _db.Upsert(result.Record);
            return result.Success;
        }

        public async Task<BatchProgress> IndexBatchAsync(IProgress<BatchProgress> progress, CancellationToken ct)
        {
            State = PipelineState.Running;
            _cts?.Dispose();
            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var token = _cts.Token;

            var pending = _db.GetAllPending();
            var counters = new ProgressCounters { Total = pending.Count };

            if (pending.Count == 0)
            {
                State = PipelineState.Idle;
                return new BatchProgress(0, 0, 0, 0, 0, null);
            }

            var workerCount = Math.Max(1, Math.Min(_config.MaxConcurrent, pending.Count));
            var queue = new ConcurrentQueue<AssetRecord>(pending);
            var reportLock = new object();
            long lastReportTicks = 0;
            long reportIntervalTicks = TimeSpan.TicksPerMillisecond * ProgressReportIntervalMs;
            var startedAt = DateTime.UtcNow;
            long peakManagedMemory = GC.GetTotalMemory(false);

            void FlushBufferedRecords(List<AssetRecord> buffer)
            {
                if (buffer.Count == 0)
                    return;
                _db.UpsertBatch(buffer);
                buffer.Clear();
            }

            void ReportProgress(bool force = false)
            {
                if (progress == null)
                    return;

                if (force)
                {
                    progress.Report(counters.Snapshot());
                    return;
                }

                var now = DateTime.UtcNow.Ticks;
                bool shouldReport = false;
                lock (reportLock)
                {
                    if (now - lastReportTicks >= reportIntervalTicks)
                    {
                        lastReportTicks = now;
                        shouldReport = true;
                    }
                }

                if (shouldReport)
                    progress.Report(counters.Snapshot());
            }

            async Task WorkerAsync()
            {
                var writeBuffer = new List<AssetRecord>(DbWriteBatchSize);

                try
                {
                    while (!token.IsCancellationRequested && queue.TryDequeue(out var record))
                    {
                        Volatile.Write(ref counters.CurrentAsset, record.AssetPath);
                        ReportProgress();

                        try
                        {
                            var result = await BuildRecordForPendingAsync(record, token);
                            writeBuffer.Add(result.Record);

                            if (result.Success)
                                Interlocked.Increment(ref counters.Succeeded);
                            else
                                Interlocked.Increment(ref counters.Failed);

                            if (writeBuffer.Count >= DbWriteBatchSize)
                                FlushBufferedRecords(writeBuffer);
                        }
                        catch (OperationCanceledException)
                        {
                            Interlocked.Increment(ref counters.Skipped);
                            break;
                        }
                        catch (Exception ex)
                        {
                            Debug.LogError($"[SemanticSearch] Error indexing {record.AssetPath}: {ex.Message}");
                            writeBuffer.Add(CreateErrorRecord(record, record.Guid));
                            Interlocked.Increment(ref counters.Failed);
                        }
                        finally
                        {
                            UpdateMax(ref peakManagedMemory, GC.GetTotalMemory(false));
                            Interlocked.Increment(ref counters.Completed);
                            ReportProgress();
                        }
                    }
                }
                finally
                {
                    FlushBufferedRecords(writeBuffer);
                }
            }

            var workers = new Task[workerCount];
            for (int i = 0; i < workerCount; i++)
                workers[i] = WorkerAsync();

            try
            {
                await Task.WhenAll(workers);
            }
            catch (OperationCanceledException)
            {
                // expected
            }

            if (token.IsCancellationRequested)
            {
                int completed = Volatile.Read(ref counters.Completed);
                int remaining = counters.Total - completed;
                if (remaining > 0)
                {
                    Interlocked.Add(ref counters.Skipped, remaining);
                    Interlocked.Add(ref counters.Completed, remaining);
                }
            }

            State = token.IsCancellationRequested ? PipelineState.Cancelled : PipelineState.Idle;
            var snapshot = counters.Snapshot();
            ReportProgress(force: true);

            var elapsed = DateTime.UtcNow - startedAt;
            var throughput = elapsed.TotalSeconds > 0.01
                ? snapshot.Completed / elapsed.TotalSeconds
                : 0d;
            Debug.Log(
                $"[SemanticSearch] Batch index finished. total={snapshot.Total}, " +
                $"succeeded={snapshot.Succeeded}, failed={snapshot.Failed}, skipped={snapshot.Skipped}, " +
                $"elapsed={elapsed.TotalSeconds:F2}s, throughput={throughput:F2} assets/s, workers={workerCount}, " +
                $"peakManaged={FormatUtils.FormatBytes(peakManagedMemory)}.");

            return snapshot;
        }

        public void CancelAll()
        {
            _cts?.Cancel();
            State = PipelineState.Cancelled;
        }

        public static byte[] GetAssetImageBytes(string assetPath)
        {
            var ext = Path.GetExtension(assetPath).ToLowerInvariant();

            if (ImageExtensions.Contains(ext))
            {
                var fullPath = Path.GetFullPath(assetPath);
                return File.Exists(fullPath) ? File.ReadAllBytes(fullPath) : null;
            }

            if (ext == ".prefab")
            {
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
                if (asset == null) return null;

                AssetPreview.SetPreviewTextureCacheSize(256);
                var preview = AssetPreview.GetAssetPreview(asset);

                int retries = 0;
                const int maxRetries = 30;
                while (preview == null && AssetPreview.IsLoadingAssetPreview(asset.GetInstanceID()) && retries < maxRetries)
                {
                    System.Threading.Thread.Sleep(100);
                    preview = AssetPreview.GetAssetPreview(asset);
                    retries++;
                }

                if (preview == null)
                    preview = AssetPreview.GetMiniThumbnail(asset);

                return preview != null ? preview.EncodeToPNG() : null;
            }

            return null;
        }

        async Task<IndexSingleResult> BuildRecordForPendingAsync(AssetRecord pendingRecord, CancellationToken ct)
        {
            var guid = pendingRecord?.Guid;
            var assetPath = pendingRecord?.AssetPath;

            if (string.IsNullOrEmpty(assetPath))
            {
                Debug.LogError("[SemanticSearch] Invalid pending record: asset path is empty.");
                return new IndexSingleResult
                {
                    Success = false,
                    Record = CreateErrorRecord(pendingRecord, guid)
                };
            }

            if (string.IsNullOrEmpty(guid))
                guid = AssetDatabase.AssetPathToGUID(assetPath);

            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogError($"[SemanticSearch] Invalid GUID for asset path: {assetPath}");
                return new IndexSingleResult
                {
                    Success = false,
                    Record = CreateErrorRecord(pendingRecord, guid)
                };
            }

            try
            {
                ct.ThrowIfCancellationRequested();

                var imageBytes = GetAssetImageBytes(assetPath);
                if (imageBytes == null || imageBytes.Length == 0)
                {
                    Debug.LogError($"[SemanticSearch] Failed to get image data: {assetPath}");
                    return new IndexSingleResult
                    {
                        Success = false,
                        Record = CreateErrorRecord(pendingRecord, guid)
                    };
                }

                var caption = await _vlClient.RequestCaptionAsync(imageBytes);
                ct.ThrowIfCancellationRequested();

                var vector = await _embeddingClient.RequestEmbeddingAsync(caption);
                ct.ThrowIfCancellationRequested();

                if (vector == null || vector.Length == 0)
                {
                    Debug.LogError($"[SemanticSearch] Empty embedding returned: {assetPath}");
                    return new IndexSingleResult
                    {
                        Success = false,
                        Record = CreateErrorRecord(pendingRecord, guid)
                    };
                }

                return new IndexSingleResult
                {
                    Success = true,
                    Record = new AssetRecord
                    {
                        Guid = guid,
                        AssetPath = assetPath,
                        Md5 = pendingRecord?.Md5 ?? "",
                        Caption = caption,
                        Vector = vector,
                        VectorDim = vector.Length,
                        Status = Database.AssetStatus.Indexed,
                        UpdatedAt = DateTime.UtcNow.ToString("o")
                    }
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SemanticSearch] Error indexing {assetPath}: {ex.Message}");
                return new IndexSingleResult
                {
                    Success = false,
                    Record = CreateErrorRecord(pendingRecord, guid)
                };
            }
        }

        static AssetRecord CreateErrorRecord(AssetRecord pendingRecord, string guid)
        {
            string resolvedGuid = guid;
            if (string.IsNullOrEmpty(resolvedGuid))
                resolvedGuid = pendingRecord?.Guid;
            if (string.IsNullOrEmpty(resolvedGuid))
                resolvedGuid = pendingRecord?.AssetPath ?? Guid.NewGuid().ToString("N");

            return new AssetRecord
            {
                Guid = resolvedGuid,
                AssetPath = pendingRecord?.AssetPath,
                Md5 = pendingRecord?.Md5 ?? "",
                Caption = pendingRecord?.Caption,
                Vector = pendingRecord?.Vector,
                VectorDim = pendingRecord?.VectorDim ?? 0,
                Status = Database.AssetStatus.Error,
                UpdatedAt = DateTime.UtcNow.ToString("o")
            };
        }

        static void UpdateMax(ref long target, long value)
        {
            long current;
            do
            {
                current = Volatile.Read(ref target);
                if (value <= current)
                    return;
            }
            while (Interlocked.CompareExchange(ref target, value, current) != current);
        }

    }
}
