using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using SemanticSearch.Editor.Core.Database;
using SemanticSearch.Editor.UI;

namespace SemanticSearch.Editor.Core.Watcher
{
    public class SemanticAssetPostprocessor : AssetPostprocessor
    {
        private static bool IsAutoIndexEnabled =>
            SemanticSearchSettings.Instance.AutoIndexOnImport;

        private static bool IsSupportedAsset(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return AssetScanner.GetSupportedExtensions().Contains(ext);
        }

        private static bool IsBlacklisted(string path)
        {
            return AssetScanner.IsBlacklisted(path);
        }

        private static bool ShouldProcess(string path)
        {
            return IsSupportedAsset(path) && !IsBlacklisted(path);
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

            using (var db = OpenDB())
            {
                foreach (var path in importedAssets)
                {
                    if (!ShouldProcess(path)) continue;
                    ProcessImportedAsset(db, path);
                }

                foreach (var path in deletedAssets)
                {
                    if (!ShouldProcess(path)) continue;
                    ProcessDeletedAsset(db, path);
                }

                for (int i = 0; i < movedAssets.Length; i++)
                {
                    var newPath = movedAssets[i];
                    var oldPath = movedFromAssetPaths[i];

                    if (ShouldProcess(oldPath) && !ShouldProcess(newPath))
                    {
                        ProcessDeletedAsset(db, oldPath);
                    }
                    else if (ShouldProcess(newPath))
                    {
                        ProcessImportedAsset(db, newPath);
                    }
                }
            }
        }

        private static void ProcessImportedAsset(SemanticSearchDB db, string assetPath)
        {
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (string.IsNullOrEmpty(guid)) return;

            var fullPath = Path.GetFullPath(assetPath);
            if (!File.Exists(fullPath)) return;

            var md5 = MD5Helper.ComputeFileMD5(fullPath);
            var existing = db.GetByGuid(guid);

            if (existing != null && existing.Md5 == md5)
                return;

            db.Upsert(new AssetRecord
            {
                Guid = guid,
                AssetPath = assetPath,
                Md5 = md5,
                Status = Database.AssetStatus.Pending,
                UpdatedAt = DateTime.UtcNow.ToString("o")
            });
        }

        private static void ProcessDeletedAsset(SemanticSearchDB db, string assetPath)
        {
            var guid = AssetDatabase.AssetPathToGUID(assetPath);
            if (!string.IsNullOrEmpty(guid))
                db.Delete(guid);
        }
    }
}
