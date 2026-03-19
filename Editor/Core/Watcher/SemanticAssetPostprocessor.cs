using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using SemanticSearch.Editor.Core.Database;
using SemanticSearch.Editor.Core.Pipeline;
using SemanticSearch.Editor.UI;

namespace SemanticSearch.Editor.Core.Watcher
{
    public class SemanticAssetPostprocessor : AssetPostprocessor
    {
        private static bool _autoIndexRunning;
        private static bool _autoIndexRerunRequested;

        private static bool IsAutoIndexEnabled =>
            SemanticSearchSettings.Instance.AutoIndexOnImport;

        private static bool ShouldProcess(string path)
        {
            if (!AssetScanner.IsSupported(path))
                return false;
            var s = SemanticSearchSettings.Instance;
            return AssetFilter.IsIncluded(path, s.IncludeFilters, s.ExcludeFilters);
        }

        private static SemanticSearchDB OpenDB()
        {
            var db = new SemanticSearchDB();
            db.Open();
            return db;
        }

        private static void OnPostprocessAllAssets(
            string[] importedAssets,
            string[] deletedAssets,
            string[] movedAssets,
            string[] movedFromAssetPaths)
        {
            if (!IsAutoIndexEnabled)
                return;

            var hasWork = importedAssets.Any(ShouldProcess)
                       || deletedAssets.Any(ShouldProcess)
                       || movedAssets.Any(ShouldProcess);

            if (!hasWork)
                return;

            var upsertItems = new List<(string guid, string path, string md5)>();
            var deletePaths = new List<(string guid, string path)>();

            foreach (var path in importedAssets)
            {
                if (!ShouldProcess(path)) continue;
                var item = CollectImportItem(path);
                if (item.HasValue)
                    upsertItems.Add(item.Value);
            }

            foreach (var path in deletedAssets)
            {
                if (!ShouldProcess(path)) continue;
                var guid = AssetDatabase.AssetPathToGUID(path);
                deletePaths.Add((guid, path));
            }

            for (int i = 0; i < movedAssets.Length; i++)
            {
                var newPath = movedAssets[i];
                var oldPath = movedFromAssetPaths[i];

                if (ShouldProcess(oldPath) && !ShouldProcess(newPath))
                {
                    var guid = AssetDatabase.AssetPathToGUID(oldPath);
                    deletePaths.Add((guid, oldPath));
                }
                else if (ShouldProcess(newPath))
                {
                    var item = CollectImportItem(newPath);
                    if (item.HasValue)
                        upsertItems.Add(item.Value);
                }
            }

            if (upsertItems.Count == 0 && deletePaths.Count == 0)
                return;

            EditorApplication.delayCall += () =>
                FlushPostprocessChangesAsync(upsertItems, deletePaths);
        }

        private static (string guid, string path, string md5)? CollectImportItem(string assetPath)
        {
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid)) return null;

            var fullPath = Path.GetFullPath(assetPath);
            if (!File.Exists(fullPath)) return null;

            var md5 = MD5Helper.ComputeFileMD5(fullPath);
            return (guid, assetPath, md5);
        }

        private static async void FlushPostprocessChangesAsync(
            List<(string guid, string path, string md5)> upsertItems,
            List<(string guid, string path)> deletePaths)
        {
            try
            {
                bool hasPending = false;
                await Task.Run(() =>
                {
                    using (var db = OpenDB())
                    {
                        foreach (var (guid, path, md5) in upsertItems)
                        {
                            var existing = db.GetByGuid(guid);
                            if (existing != null && existing.Md5 == md5)
                                continue;

                            db.Upsert(new AssetRecord
                            {
                                Guid = guid,
                                AssetPath = path,
                                Md5 = md5,
                                Status = Database.AssetStatus.Pending,
                                UpdatedAt = DateTime.UtcNow.ToString("o")
                            });
                            hasPending = true;
                        }

                        foreach (var (deleteGuid, deletePath) in deletePaths)
                        {
                            var resolvedGuid = deleteGuid;
                            if (string.IsNullOrEmpty(resolvedGuid))
                            {
                                var record = db.GetByPath(deletePath);
                                resolvedGuid = record?.Guid;
                            }
                            if (!string.IsNullOrEmpty(resolvedGuid))
                                db.Delete(resolvedGuid);
                        }
                    }
                });

                SemanticSearchSettingsProvider.RequestCountsRefresh();
                if (hasPending)
                    ScheduleAutoIndex();
            }
            catch (Exception e)
            {
                Debug.LogError($"[SemanticSearch] Post-process DB flush failed: {e}");
            }
        }

        private static void ScheduleAutoIndex()
        {
            if (_autoIndexRunning)
            {
                _autoIndexRerunRequested = true;
                return;
            }

            _autoIndexRunning = true;
            EditorApplication.delayCall += RunAutoIndexAsync;
        }

        private static async void RunAutoIndexAsync()
        {
            try
            {
                do
                {
                    _autoIndexRerunRequested = false;
                    using (var db = OpenDB())
                    {
                        var config = SemanticSearchSettings.Instance.ToLLMApiConfig();
                        var pipeline = new IndexPipeline(db, config);
                        await pipeline.IndexBatchAsync(progress: null, ct: CancellationToken.None);
                    }

                    SemanticSearchSettingsProvider.RequestCountsRefresh();
                }
                while (_autoIndexRerunRequested);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SemanticSearch] Auto-index failed: {e}");
            }
            finally
            {
                _autoIndexRunning = false;
            }
        }
    }
}
