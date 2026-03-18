using System;
using System.IO;
using System.Linq;
using System.Threading;
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

            bool hasDbChanges = false;
            bool hasPendingUpdates = false;

            using (var db = OpenDB())
            {
                foreach (var path in importedAssets)
                {
                    if (!ShouldProcess(path)) continue;
                    if (ProcessImportedAsset(db, path))
                    {
                        hasDbChanges = true;
                        hasPendingUpdates = true;
                    }
                }

                foreach (var path in deletedAssets)
                {
                    if (!ShouldProcess(path)) continue;
                    if (ProcessDeletedAsset(db, path))
                        hasDbChanges = true;
                }

                for (int i = 0; i < movedAssets.Length; i++)
                {
                    var newPath = movedAssets[i];
                    var oldPath = movedFromAssetPaths[i];

                    if (ShouldProcess(oldPath) && !ShouldProcess(newPath))
                    {
                        if (ProcessDeletedAsset(db, oldPath))
                            hasDbChanges = true;
                    }
                    else if (ShouldProcess(newPath))
                    {
                        if (ProcessImportedAsset(db, newPath))
                        {
                            hasDbChanges = true;
                            hasPendingUpdates = true;
                        }
                    }
                }
            }

            if (hasDbChanges)
                SemanticSearchSettingsProvider.RequestCountsRefresh();

            if (hasPendingUpdates)
                ScheduleAutoIndex();
        }

        private static bool ProcessImportedAsset(SemanticSearchDB db, string assetPath)
        {
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid)) return false;

            var fullPath = Path.GetFullPath(assetPath);
            if (!File.Exists(fullPath)) return false;

            var md5 = MD5Helper.ComputeFileMD5(fullPath);
            var existing = db.GetByGuid(guid);

            if (existing != null && existing.Md5 == md5)
                return false;

            db.Upsert(new AssetRecord
            {
                Guid = guid,
                AssetPath = assetPath,
                Md5 = md5,
                Status = Database.AssetStatus.Pending,
                UpdatedAt = DateTime.UtcNow.ToString("o")
            });

            return true;
        }

        private static bool ProcessDeletedAsset(SemanticSearchDB db, string assetPath)
        {
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid))
            {
                var record = db.GetByPath(assetPath);
                guid = record?.Guid;
            }

            if (string.IsNullOrEmpty(guid))
                return false;

            db.Delete(guid);
            return true;
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
