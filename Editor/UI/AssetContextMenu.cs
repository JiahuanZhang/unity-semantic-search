using System;
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEditor;
using SemanticSearch.Editor.Core.Database;
using SemanticSearch.Editor.Core.Localization;
using SemanticSearch.Editor.Core.Pipeline;
using SemanticSearch.Editor.Core.Watcher;
using L10n = SemanticSearch.Editor.Core.Localization.L10n;

namespace SemanticSearch.Editor.UI
{
    public static class AssetContextMenu
    {
        const string MenuIndex = "Assets/Semantic Search/Index";
        const string MenuReindex = "Assets/Semantic Search/Re-index (Force)";
        const int MenuPriority = 2000;

        static bool _isRunning;
        static CancellationTokenSource _cts;

        [MenuItem(MenuIndex, false, MenuPriority)]
        static void IndexSelected()
        {
            RunIndex(forceReindex: false);
        }

        [MenuItem(MenuReindex, false, MenuPriority + 1)]
        static void ReindexSelected()
        {
            RunIndex(forceReindex: true);
        }

        [MenuItem(MenuIndex, true)]
        [MenuItem(MenuReindex, true)]
        static bool ValidateMenu()
        {
            if (_isRunning) return false;
            return Selection.assetGUIDs != null && Selection.assetGUIDs.Length > 0;
        }

        static async void RunIndex(bool forceReindex)
        {
            var assetPaths = Selection.assetGUIDs
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !string.IsNullOrEmpty(p))
                .ToArray();

            if (assetPaths.Length == 0) return;

            _isRunning = true;
            _cts = new CancellationTokenSource();
            SemanticSearchDB db = null;

            try
            {
                db = new SemanticSearchDB();
                db.Open();

                var label = forceReindex ? L10n.LabelReindexing : L10n.LabelIndexing;
                EditorUtility.DisplayProgressBar(L10n.ProgressBarTitle(label), L10n.ScanningSelectedAssets, 0f);

                var changedGuids = AssetScanner.ScanAssets(db, assetPaths, forceReindex);

                if (changedGuids.Count == 0)
                {
                    Debug.Log("[SemanticSearch] No assets need indexing.");
                    return;
                }

                var settings = SemanticSearchSettings.Load();
                var config = settings.ToLLMApiConfig();
                var pipeline = new IndexPipeline(db, config);

                var progress = new Progress<BatchProgress>(p =>
                {
                    float pct = p.Total > 0 ? (float)p.Completed / p.Total : 0f;
                    EditorUtility.DisplayProgressBar(
                        L10n.ProgressBarTitle(label),
                        $"{p.Completed}/{p.Total}  {p.CurrentAsset}",
                        pct);
                });

                var result = await pipeline.IndexBatchAsync(progress, _cts.Token);
                Debug.Log($"[SemanticSearch] Done. Succeeded: {result.Succeeded}, Failed: {result.Failed}, Skipped: {result.Skipped}");
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[SemanticSearch] Indexing cancelled.");
            }
            catch (Exception e)
            {
                Debug.LogError($"[SemanticSearch] {e}");
            }
            finally
            {
                db?.Close();
                _isRunning = false;
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
