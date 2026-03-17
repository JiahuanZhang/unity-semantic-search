using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using SemanticSearch.Editor.Core.Database;

namespace SemanticSearch.Editor.Core.Watcher
{
    public static class AssetScanner
    {
        private static readonly string[] SupportedExtensions = { ".png", ".jpg", ".jpeg", ".tga", ".prefab" };
        private static readonly string[] BlacklistPrefixes = { "Packages/", "Library/" };

        public static string[] GetSupportedExtensions() => SupportedExtensions;

        public static bool IsBlacklisted(string path)
        {
            foreach (var prefix in BlacklistPrefixes)
            {
                if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 全量扫描项目资产，返回状态变更为 Pending 的 GUID 列表。
        /// </summary>
        public static List<string> ScanAll(SemanticSearchDB db, Action<float> progressCallback = null)
        {
            var guids = new HashSet<string>();

            var textureGuids = AssetDatabase.FindAssets("t:Texture2D");
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab");

            foreach (var g in textureGuids) guids.Add(g);
            foreach (var g in prefabGuids) guids.Add(g);

            var changedGuids = new List<string>();
            int total = guids.Count;
            int processed = 0;

            var guidMd5Map = db.GetAllGuidMd5Map();

            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);

                if (string.IsNullOrEmpty(assetPath) || IsBlacklisted(assetPath))
                {
                    processed++;
                    continue;
                }

                var ext = Path.GetExtension(assetPath).ToLowerInvariant();
                if (!SupportedExtensions.Contains(ext))
                {
                    processed++;
                    continue;
                }

                var fullPath = Path.GetFullPath(assetPath);
                if (!File.Exists(fullPath))
                {
                    processed++;
                    continue;
                }

                var md5 = MD5Helper.ComputeFileMD5(fullPath);
                string existingMd5;
                guidMd5Map.TryGetValue(guid, out existingMd5);

                if (existingMd5 == null || existingMd5 != md5)
                {
                    db.Upsert(new AssetRecord
                    {
                        Guid = guid,
                        AssetPath = assetPath,
                        Md5 = md5,
                        Status = Database.AssetStatus.Pending,
                        UpdatedAt = DateTime.UtcNow.ToString("o")
                    });
                    changedGuids.Add(guid);
                }

                processed++;
                progressCallback?.Invoke((float)processed / total);
            }

            return changedGuids;
        }
    }
}
