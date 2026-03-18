using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using SemanticSearch.Editor.Core.Database;
using SemanticSearch.Editor.Core.LLM;

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

            try
            {
                ct.ThrowIfCancellationRequested();

                var imageBytes = GetAssetImageBytes(assetPath);
                if (imageBytes == null || imageBytes.Length == 0)
                {
                    Debug.LogError($"[SemanticSearch] Failed to get image data: {assetPath}");
                    MarkError(guid, assetPath);
                    return false;
                }

                var caption = await _vlClient.RequestCaptionAsync(imageBytes);
                ct.ThrowIfCancellationRequested();

                var vector = await _embeddingClient.RequestEmbeddingAsync(caption);
                ct.ThrowIfCancellationRequested();

                var existing = _db.GetByGuid(guid);
                _db.Upsert(new AssetRecord
                {
                    Guid = guid,
                    AssetPath = assetPath,
                    Md5 = existing?.Md5 ?? "",
                    Caption = caption,
                    Vector = vector,
                    VectorDim = vector.Length,
                    Status = Database.AssetStatus.Indexed,
                    UpdatedAt = DateTime.UtcNow.ToString("o")
                });

                Debug.Log($"[SemanticSearch] Indexed: {assetPath}");
                return true;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SemanticSearch] Error indexing {assetPath}: {ex.Message}");
                MarkError(guid, assetPath);
                return false;
            }
        }

        public async Task<BatchProgress> IndexBatchAsync(IProgress<BatchProgress> progress, CancellationToken ct)
        {
            State = PipelineState.Running;
            _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            var token = _cts.Token;

            var pending = _db.GetAllPending();
            var counters = new ProgressCounters { Total = pending.Count };

            if (pending.Count == 0)
            {
                State = PipelineState.Idle;
                return new BatchProgress(0, 0, 0, 0, 0, null);
            }

            var tasks = pending.Select(async record =>
            {
                if (token.IsCancellationRequested)
                {
                    Interlocked.Increment(ref counters.Skipped);
                    Interlocked.Increment(ref counters.Completed);
                    return;
                }

                try
                {
                    if (token.IsCancellationRequested)
                    {
                        Interlocked.Increment(ref counters.Skipped);
                        return;
                    }

                    Volatile.Write(ref counters.CurrentAsset, record.AssetPath);
                    progress?.Report(counters.Snapshot());

                    var ok = await IndexSingleAsync(record.AssetPath, token);
                    if (ok)
                        Interlocked.Increment(ref counters.Succeeded);
                    else
                        Interlocked.Increment(ref counters.Failed);
                }
                catch (OperationCanceledException)
                {
                    Interlocked.Increment(ref counters.Skipped);
                }
                catch
                {
                    Interlocked.Increment(ref counters.Failed);
                }
                finally
                {
                    Interlocked.Increment(ref counters.Completed);
                    progress?.Report(counters.Snapshot());
                }
            }).ToArray();

            try
            {
                await Task.WhenAll(tasks);
            }
            catch (OperationCanceledException)
            {
                // expected
            }

            State = token.IsCancellationRequested ? PipelineState.Cancelled : PipelineState.Idle;
            return counters.Snapshot();
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

        void MarkError(string guid, string assetPath)
        {
            var existing = _db.GetByGuid(guid);
            _db.Upsert(new AssetRecord
            {
                Guid = guid,
                AssetPath = assetPath,
                Md5 = existing?.Md5 ?? "",
                Caption = existing?.Caption,
                Vector = existing?.Vector,
                VectorDim = existing?.VectorDim ?? 0,
                Status = Database.AssetStatus.Error,
                UpdatedAt = DateTime.UtcNow.ToString("o")
            });
        }
    }
}
