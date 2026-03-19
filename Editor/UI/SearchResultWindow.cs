using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEditor;
using UnityEngine;
using SemanticSearch.Editor.Core.Database;
using SemanticSearch.Editor.Core.LLM;
using SemanticSearch.Editor.Core.Localization;
using SemanticSearch.Editor.Core.Search;
using SemanticSearch.Editor.Core.Utils;
using L10n = SemanticSearch.Editor.Core.Localization.L10n;
using Object = UnityEngine.Object;

namespace SemanticSearch.Editor.UI
{
    public class SearchResultWindow : EditorWindow
    {
        string _queryText = "";
        string _lastSearchedText = "";
        List<SearchResult> _results = new List<SearchResult>();
        bool _isSearching;
        float _searchTime;
        bool _enhancedSearch;
        string _enhancedQueryText;
        Vector2 _scrollPosition;
        readonly Dictionary<string, Texture2D> _thumbnailCache = new Dictionary<string, Texture2D>();
        readonly Queue<string> _thumbnailOrder = new Queue<string>();

        const int PageSize = 20;
        const int ThumbnailCacheLimit = 200;
        int _displayCount = PageSize;

        GUIStyle _boldLabel;
        GUIStyle _grayMiniLabel;
        GUIStyle _italicLabel;
        GUIStyle _greenLabel;
        GUIStyle _enhancedLabel;

        public static void Show(string queryText)
        {
            var window = GetWindow<SearchResultWindow>(L10n.SearchWindowTitle);
            window.minSize = new Vector2(400, 300);
            window._queryText = queryText ?? "";
            window._lastSearchedText = "";
            if (!string.IsNullOrEmpty(queryText))
                window.ExecuteSearch(queryText);
        }

        void InitStyles()
        {
            if (_boldLabel != null) return;

            _boldLabel = new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold };

            _grayMiniLabel = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = Color.gray }
            };

            _italicLabel = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Italic,
                normal = { textColor = Color.gray },
                wordWrap = true
            };

            _greenLabel = new GUIStyle(EditorStyles.miniLabel)
            {
                normal = { textColor = new Color(0.3f, 0.8f, 0.3f) },
                fontStyle = FontStyle.Bold
            };

            _enhancedLabel = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Italic,
                normal = { textColor = new Color(0.4f, 0.7f, 1f) },
                wordWrap = true
            };
        }

        void OnGUI()
        {
            InitStyles();
            DrawSearchBar();
            DrawStatusLine();
            DrawResults();
        }

        void OnDisable()
        {
            ClearThumbnailCache();
        }

        void DrawSearchBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUI.SetNextControlName("SearchField");
            var committedQuery = EditorGUILayout.DelayedTextField(_queryText, EditorStyles.toolbarSearchField);
            if (committedQuery != _queryText)
            {
                _queryText = committedQuery;
                if (!string.IsNullOrEmpty(_queryText?.Trim()) && _queryText.Trim() != _lastSearchedText)
                    ExecuteSearch(_queryText.Trim());
            }

            var enhancedContent = new GUIContent(L10n.Enhanced, L10n.EnhancedTooltip);
            _enhancedSearch = GUILayout.Toggle(_enhancedSearch, enhancedContent, EditorStyles.toolbarButton, GUILayout.Width(75));

            if (GUILayout.Button(L10n.Search, EditorStyles.toolbarButton, GUILayout.Width(60)))
            {
                if (!string.IsNullOrEmpty(_queryText?.Trim()))
                    ExecuteSearch(_queryText.Trim());
            }

            EditorGUILayout.EndHorizontal();
        }

        void DrawStatusLine()
        {
            if (_isSearching)
            {
                EditorGUILayout.HelpBox(
                    _enhancedSearch ? L10n.EnhancingAndSearching : L10n.Searching,
                    MessageType.Info);
                return;
            }

            if (_results.Count > 0)
            {
                EditorGUILayout.LabelField(L10n.FoundResults(_results.Count, _searchTime));
            }
            else if (!string.IsNullOrEmpty(_queryText) && _searchTime > 0)
            {
                EditorGUILayout.HelpBox(L10n.NoResultsFound, MessageType.Warning);
            }

            if (!string.IsNullOrEmpty(_enhancedQueryText))
                EditorGUILayout.LabelField(L10n.EnhancedQuery(_enhancedQueryText), _enhancedLabel);
        }

        void DrawResults()
        {
            _scrollPosition = EditorGUILayout.BeginScrollView(_scrollPosition);

            int count = Mathf.Min(_displayCount, _results.Count);
            for (int i = 0; i < count; i++)
            {
                DrawResultItem(_results[i]);
                if (i < count - 1)
                    DrawSeparator();
            }

            EditorGUILayout.EndScrollView();

            if (_displayCount < _results.Count)
            {
                if (GUILayout.Button(L10n.LoadMore, GUILayout.Height(28)))
                    _displayCount += PageSize;
            }
        }

        void DrawResultItem(SearchResult result)
        {
            var rect = EditorGUILayout.BeginHorizontal(GUILayout.Height(68));

            var thumbnail = GetThumbnail(result.AssetPath);
            if (thumbnail != null)
                GUILayout.Box(thumbnail, GUILayout.Width(64), GUILayout.Height(64));
            else
                GUILayout.Box(GUIContent.none, GUILayout.Width(64), GUILayout.Height(64));

            EditorGUILayout.BeginVertical();

            string fileName = System.IO.Path.GetFileName(result.AssetPath);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(fileName, _boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField($"{result.Similarity * 100f:F1}%", _greenLabel, GUILayout.Width(48));
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(result.AssetPath, _grayMiniLabel);

            if (!string.IsNullOrEmpty(result.Caption))
            {
                string caption = result.Caption.Length > 80
                    ? result.Caption.Substring(0, 80) + "..."
                    : result.Caption;
                EditorGUILayout.LabelField(caption, _italicLabel);
            }

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();

            HandleItemClick(rect, result.AssetPath);
        }

        void HandleItemClick(Rect rect, string assetPath)
        {
            var e = Event.current;
            if (e.type != EventType.MouseDown || !rect.Contains(e.mousePosition)) return;

            var obj = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (obj == null) return;

            if (e.clickCount == 2)
            {
                AssetDatabase.OpenAsset(obj);
            }
            else
            {
                EditorGUIUtility.PingObject(obj);
                Selection.activeObject = obj;
            }

            e.Use();
        }

        static void DrawSeparator()
        {
            EditorGUILayout.Space(2);
            var rect = EditorGUILayout.GetControlRect(false, 1);
            EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.3f));
            EditorGUILayout.Space(2);
        }

        async void ExecuteSearch(string queryText)
        {
            _lastSearchedText = queryText;
            _isSearching = true;
            _results.Clear();
            _displayCount = PageSize;
            _enhancedQueryText = null;
            ClearThumbnailCache();
            Repaint();

            try
            {
                var sw = Stopwatch.StartNew();
                long managedBefore = GC.GetTotalMemory(false);
                var config = LLMApiConfig.Load();
                var http = new LLMHttpClient(config);

                string searchText = queryText;

                if (_enhancedSearch)
                {
                    try
                    {
                        var chatClient = LLMClientFactory.CreateChatClient(config, http);
                        var enhancer = new SearchQueryEnhancer(chatClient);
                        searchText = await enhancer.EnhanceAsync(queryText);
                        _enhancedQueryText = searchText;
                        Repaint();
                    }
                    catch (Exception enhanceEx)
                    {
                        UnityEngine.Debug.LogWarning(
                            $"[SemanticSearch] Query enhancement failed, using original: {enhanceEx.Message}");
                        searchText = queryText;
                    }
                }

                using (var db = new SemanticSearchDB())
                {
                    db.Open();
                    var embedding = LLMClientFactory.CreateEmbeddingClient(config, http);
                    var engine = new VectorSearchEngine(db, embedding);
                    _results = await engine.SearchAsync(searchText);
                }

                sw.Stop();
                _searchTime = (float)sw.Elapsed.TotalSeconds;

                long managedAfter = GC.GetTotalMemory(false);
                long managedDelta = managedAfter - managedBefore;
                UnityEngine.Debug.Log(
                    $"[SemanticSearch] Search perf: query=\"{queryText}\", " +
                    (_enhancedSearch ? $"enhanced=\"{searchText}\", " : "") +
                    $"results={_results.Count}, elapsed={_searchTime:F3}s, " +
                    $"managedBefore={FormatUtils.FormatBytes(managedBefore)}, managedAfter={FormatUtils.FormatBytes(managedAfter)}, " +
                    $"managedDelta={FormatUtils.FormatBytes(managedDelta)}.");
            }
            catch (Exception ex)
            {
                try { UnityEngine.Debug.LogError($"[SemanticSearch] Search failed: {ex.Message}"); }
                catch (Exception ex2) { UnityEngine.Debug.LogWarning($"[SemanticSearch] Cleanup: {ex2.Message}"); }
            }
            finally
            {
                _isSearching = false;
                Repaint();
            }
        }

        Texture2D GetThumbnail(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;

            if (_thumbnailCache.TryGetValue(assetPath, out var cached))
                return cached;

            var asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (asset == null) return null;

            Texture2D tex;
            if (asset is Texture2D t)
            {
                tex = t;
            }
            else if (IsPrefab(assetPath))
            {
                tex = AssetPreview.GetAssetPreview(asset);
                if (tex == null && AssetPreview.IsLoadingAssetPreview(asset.GetInstanceID()))
                {
                    EditorApplication.delayCall += Repaint;
                    return AssetPreview.GetMiniThumbnail(asset);
                }
                tex = tex ?? AssetPreview.GetMiniThumbnail(asset);
            }
            else
            {
                tex = AssetPreview.GetMiniThumbnail(asset);
            }

            _thumbnailCache[assetPath] = tex;
            _thumbnailOrder.Enqueue(assetPath);
            TrimThumbnailCache();
            return tex;
        }

        static bool IsPrefab(string assetPath)
        {
            return Path.GetExtension(assetPath).Equals(".prefab", StringComparison.OrdinalIgnoreCase);
        }

        void TrimThumbnailCache()
        {
            while (_thumbnailCache.Count > ThumbnailCacheLimit && _thumbnailOrder.Count > 0)
            {
                var oldest = _thumbnailOrder.Dequeue();
                _thumbnailCache.Remove(oldest);
            }
        }

        void ClearThumbnailCache()
        {
            _thumbnailCache.Clear();
            _thumbnailOrder.Clear();
        }

    }
}
