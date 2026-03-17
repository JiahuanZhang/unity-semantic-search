using System;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using SemanticSearch.Editor.Core.Database;
using SemanticSearch.Editor.Core.LLM;
using SemanticSearch.Editor.Core.Search;
using Object = UnityEngine.Object;

namespace SemanticSearch.Editor.UI
{
    public class SearchResultWindow : EditorWindow
    {
        string _queryText = "";
        List<SearchResult> _results = new List<SearchResult>();
        bool _isSearching;
        float _searchTime;
        Vector2 _scrollPosition;
        readonly Dictionary<string, Texture2D> _thumbnailCache = new Dictionary<string, Texture2D>();

        const int PageSize = 20;
        int _displayCount = PageSize;

        GUIStyle _boldLabel;
        GUIStyle _grayMiniLabel;
        GUIStyle _italicLabel;
        GUIStyle _greenLabel;

        SemanticSearchDB _db;
        VectorSearchEngine _searchEngine;
        LLMApiConfig _cachedConfig;

        public static void Show(string queryText)
        {
            var window = GetWindow<SearchResultWindow>("Semantic Search Results");
            window.minSize = new Vector2(400, 300);
            window._queryText = queryText ?? "";
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
        }

        void OnGUI()
        {
            InitStyles();
            DrawSearchBar();
            DrawStatusLine();
            DrawResults();
        }

        void DrawSearchBar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            GUI.SetNextControlName("SearchField");
            _queryText = EditorGUILayout.TextField(_queryText, EditorStyles.toolbarSearchField);

            if (GUILayout.Button("Search", EditorStyles.toolbarButton, GUILayout.Width(60))
                || (Event.current.type == EventType.KeyDown
                    && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter)
                    && GUI.GetNameOfFocusedControl() == "SearchField"))
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
                EditorGUILayout.HelpBox("Searching...", MessageType.Info);
            }
            else if (_results.Count > 0)
            {
                EditorGUILayout.LabelField($"Found {_results.Count} results ({_searchTime:F2}s)");
            }
            else if (!string.IsNullOrEmpty(_queryText) && _searchTime > 0)
            {
                EditorGUILayout.HelpBox("No results found.", MessageType.Warning);
            }
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
                if (GUILayout.Button("Load More", GUILayout.Height(28)))
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
            EditorGUILayout.LabelField(fileName, _boldLabel);
            EditorGUILayout.LabelField(result.AssetPath, _grayMiniLabel);

            if (!string.IsNullOrEmpty(result.Caption))
            {
                string caption = result.Caption.Length > 80
                    ? result.Caption.Substring(0, 80) + "..."
                    : result.Caption;
                EditorGUILayout.LabelField(caption, _italicLabel);
            }

            EditorGUILayout.LabelField($"{result.Similarity * 100f:F1}%", _greenLabel);

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

        VectorSearchEngine GetOrCreateSearchEngine()
        {
            var config = LLMApiConfig.Load();
            bool configChanged = _cachedConfig == null
                || _cachedConfig.ApiKey != config.ApiKey
                || _cachedConfig.BaseUrl != config.BaseUrl
                || _cachedConfig.EmbeddingModel != config.EmbeddingModel;

            if (configChanged)
            {
                _db?.Dispose();
                _db = null;
                _searchEngine = null;
                _cachedConfig = config;
            }

            if (_db == null)
            {
                _db = new SemanticSearchDB();
                _db.Open();
            }

            if (_searchEngine == null)
            {
                var http = new LLMHttpClient(config);
                var embedding = new QwenEmbeddingClient(config, http);
                _searchEngine = new VectorSearchEngine(_db, embedding);
            }

            return _searchEngine;
        }

        void OnDestroy()
        {
            _db?.Dispose();
            _db = null;
            _searchEngine = null;
        }

        async void ExecuteSearch(string queryText)
        {
            _isSearching = true;
            _results.Clear();
            _displayCount = PageSize;
            Repaint();

            try
            {
                var sw = Stopwatch.StartNew();
                var engine = GetOrCreateSearchEngine();
                _results = await engine.SearchAsync(queryText);
                sw.Stop();
                _searchTime = (float)sw.Elapsed.TotalSeconds;
            }
            catch (Exception ex)
            {
                try { UnityEngine.Debug.LogError($"[SemanticSearch] Search failed: {ex.Message}"); }
                catch (Exception) { }
            }
            finally
            {
                try
                {
                    _isSearching = false;
                    Repaint();
                }
                catch (Exception) { }
            }
        }

        Texture2D GetThumbnail(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;

            if (_thumbnailCache.TryGetValue(assetPath, out var cached))
                return cached;

            var asset = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
            if (asset == null) return null;

            var tex = asset as Texture2D ?? AssetPreview.GetMiniThumbnail(asset);
            _thumbnailCache[assetPath] = tex;
            return tex;
        }
    }
}
