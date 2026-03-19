using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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
        const int DbWriteBatchSize = 16;
        const int ProgressReportIntervalMs = 100;

        readonly SemanticSearchDB _db;
        readonly AssetProcessorRegistry _registry;

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
            var http = new LLMHttpClient(config);
            var vlClient = LLMClientFactory.CreateVisionClient(config, http);
            var embeddingClient = LLMClientFactory.CreateEmbeddingClient(config, http);
            _registry = new AssetProcessorRegistry(vlClient, embeddingClient);
        }

        public IndexPipeline(SemanticSearchDB db, AssetProcessorRegistry registry)
        {
            _db = db;
            _registry = registry;
        }

        public AssetProcessorRegistry Registry => _registry;

        public async Task<bool> IndexSingleAsync(string assetPath, CancellationToken ct)
        {
            var guid = UnityEditor.AssetDatabase.AssetPathToGUID(assetPath);
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
                Status = AssetStatus.Pending,
                UpdatedAt = DateTime.UtcNow.ToString("o")
            };

            var result = await BuildRecordAsync(pendingRecord, ct);
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

            var workerCount = Math.Max(1, Math.Min(8, pending.Count));
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
                            var result = await BuildRecordAsync(record, token);
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

        async Task<IndexSingleResult> BuildRecordAsync(AssetRecord pendingRecord, CancellationToken ct)
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
                guid = UnityEditor.AssetDatabase.AssetPathToGUID(assetPath);

            if (string.IsNullOrEmpty(guid))
            {
                Debug.LogError($"[SemanticSearch] Invalid GUID for asset path: {assetPath}");
                return new IndexSingleResult
                {
                    Success = false,
                    Record = CreateErrorRecord(pendingRecord, guid)
                };
            }

            var processor = _registry.GetProcessor(assetPath);
            if (processor == null)
            {
                Debug.LogError($"[SemanticSearch] No processor found for: {assetPath}");
                return new IndexSingleResult
                {
                    Success = false,
                    Record = CreateErrorRecord(pendingRecord, guid)
                };
            }

            try
            {
                ct.ThrowIfCancellationRequested();

                var result = await processor.ProcessAsync(assetPath, ct);

                if (!result.Success)
                {
                    Debug.LogError($"[SemanticSearch] {result.ErrorMessage}");
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
                        Caption = result.Caption,
                        Vector = result.Vector,
                        VectorDim = result.Vector.Length,
                        Status = AssetStatus.Indexed,
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
                Status = AssetStatus.Error,
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
