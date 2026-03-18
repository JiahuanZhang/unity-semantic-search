using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using SemanticSearch.Editor.Core.Database;
using SemanticSearch.Editor.Core.Pipeline;
using SemanticSearch.Editor.UI;
using AssetStatus = SemanticSearch.Editor.Core.Database.AssetStatus;

namespace SemanticSearch.Editor.Core.Watcher
{
    public static class AssetScanner
    {
        static readonly string[] DefaultExtensions = { ".png", ".jpg", ".jpeg", ".tga", ".prefab" };
        static readonly string[] BlacklistPrefixes = { "Packages/", "Library/" };
        const int UpsertBatchSize = 128;

        static readonly Dictionary<string, string> ExtensionToAssetType = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { ".png",  "t:Texture2D" },
            { ".jpg",  "t:Texture2D" },
            { ".jpeg", "t:Texture2D" },
            { ".tga",  "t:Texture2D" },
            { ".prefab", "t:Prefab" },
        };

        public static string[] GetSupportedExtensions() => DefaultExtensions;

        public static void RegisterExtension(string extension, string assetType)
        {
            ExtensionToAssetType[extension.ToLowerInvariant()] = assetType;
        }

        public static bool IsBlacklisted(string path)
        {
            foreach (var prefix in BlacklistPrefixes)
            {
                if (path.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public static bool IsSupported(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            return ExtensionToAssetType.ContainsKey(ext);
        }

        public static List<string> ScanAll(SemanticSearchDB db, Action<float> progressCallback = null)
        {
            var guids = new HashSet<string>();
            var assetTypes = new HashSet<string>(ExtensionToAssetType.Values);

            foreach (var type in assetTypes)
            {
                foreach (var g in AssetDatabase.FindAssets(type))
                    guids.Add(g);
            }

            return ScanGuids(db, guids, progressCallback);
        }

        public static List<string> ScanFolder(SemanticSearchDB db, string folderPath, Action<float> progressCallback = null)
        {
            var guids = new HashSet<string>();
            var assetTypes = new HashSet<string>(ExtensionToAssetType.Values);
            var folders = new[] { folderPath };

            foreach (var type in assetTypes)
            {
                foreach (var g in AssetDatabase.FindAssets(type, folders))
                    guids.Add(g);
            }

            return ScanGuids(db, guids, progressCallback);
        }

        public static List<string> ScanAssets(SemanticSearchDB db, string[] assetPaths, bool forceReindex = false)
        {
            var changedGuids = new List<string>();
            var guidMd5Map = forceReindex ? null : db.GetAllGuidMd5Map();
            var pendingRecords = new List<AssetRecord>(UpsertBatchSize);
            var settings = SemanticSearchSettings.Instance;

            void FlushPendingRecords()
            {
                if (pendingRecords.Count == 0)
                    return;
                db.UpsertBatch(pendingRecords);
                pendingRecords.Clear();
            }

            foreach (var assetPath in assetPaths)
            {
                if (string.IsNullOrEmpty(assetPath)) continue;

                if (AssetDatabase.IsValidFolder(assetPath))
                {
                    changedGuids.AddRange(ScanFolder(db, assetPath));
                    continue;
                }

                if (!IsSupported(assetPath)) continue;
                if (!AssetFilter.IsIncluded(assetPath, settings.IncludeFilters, settings.ExcludeFilters)) continue;

                var guid = AssetDatabase.AssetPathToGUID(assetPath);
                if (string.IsNullOrEmpty(guid)) continue;

                var fullPath = Path.GetFullPath(assetPath);
                if (!File.Exists(fullPath)) continue;

                var md5 = MD5Helper.ComputeFileMD5(fullPath);

                if (!forceReindex && guidMd5Map != null)
                {
                    string existingMd5;
                    guidMd5Map.TryGetValue(guid, out existingMd5);
                    if (existingMd5 != null && existingMd5 == md5) continue;
                }

                pendingRecords.Add(new AssetRecord
                {
                    Guid = guid,
                    AssetPath = assetPath,
                    Md5 = md5,
                    Status = AssetStatus.Pending,
                    UpdatedAt = DateTime.UtcNow.ToString("o")
                });
                changedGuids.Add(guid);

                if (pendingRecords.Count >= UpsertBatchSize)
                    FlushPendingRecords();
            }

            FlushPendingRecords();
            return changedGuids;
        }

        static List<string> ScanGuids(SemanticSearchDB db, HashSet<string> guids, Action<float> progressCallback)
        {
            var changedGuids = new List<string>();
            int total = guids.Count;
            int processed = 0;

            var guidMd5Map = db.GetAllGuidMd5Map();
            var pendingRecords = new List<AssetRecord>(UpsertBatchSize);

            void FlushPendingRecords()
            {
                if (pendingRecords.Count == 0)
                    return;
                db.UpsertBatch(pendingRecords);
                pendingRecords.Clear();
            }

            var settings = SemanticSearchSettings.Instance;
            foreach (var guid in guids)
            {
                var assetPath = AssetDatabase.GUIDToAssetPath(guid);

                if (string.IsNullOrEmpty(assetPath) || !IsSupported(assetPath))
                {
                    processed++;
                    continue;
                }

                if (!AssetFilter.IsIncluded(assetPath, settings.IncludeFilters, settings.ExcludeFilters))
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
                    pendingRecords.Add(new AssetRecord
                    {
                        Guid = guid,
                        AssetPath = assetPath,
                        Md5 = md5,
                        Status = AssetStatus.Pending,
                        UpdatedAt = DateTime.UtcNow.ToString("o")
                    });
                    changedGuids.Add(guid);

                    if (pendingRecords.Count >= UpsertBatchSize)
                        FlushPendingRecords();
                }

                processed++;
                progressCallback?.Invoke((float)processed / total);
            }

            FlushPendingRecords();
            return changedGuids;
        }
    }
}
