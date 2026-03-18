using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using SemanticSearch.Editor.Core.Database;
using SemanticSearch.Editor.Core.LLM;
using SemanticSearch.Editor.Core.Pipeline;
using AssetStatus = SemanticSearch.Editor.Core.Database.AssetStatus;

namespace SemanticSearch.Editor.UI
{
    public class AssetViewWindow : EditorWindow
    {
        List<AssetRecord> _allRecords = new List<AssetRecord>();
        List<AssetRecord> _filteredRecords = new List<AssetRecord>();
        readonly HashSet<string> _selectedGuids = new HashSet<string>();
        string _assetFilter = "";
        string _statusFilter = "All";
        Vector2 _assetListScroll;
        const int PageSize = 50;
        const int DbLoadPageSize = 500;
        int _displayCount = PageSize;
        bool _filterDirty = true;
        readonly Dictionary<string, Texture2D> _thumbnailCache = new Dictionary<string, Texture2D>();
        readonly Queue<string> _thumbnailOrder = new Queue<string>();
        const int ThumbnailCacheLimit = 400;

        bool _isRunning;
        string _statusText;
        CancellationTokenSource _cts;

        GUIStyle _boldLabel;
        GUIStyle _grayMiniLabel;
        GUIStyle _italicLabel;
        GUIStyle _statusIndexedStyle;
        GUIStyle _statusPendingStyle;
        GUIStyle _statusErrorStyle;

        [MenuItem("Window/Semantic Search/Asset View")]
        public static void Open()
        {
            var win = GetWindow<AssetViewWindow>();
            win.titleContent = new GUIContent("Semantic Asset View");
            win.Show();
        }

        void OnEnable()
        {
            RefreshAssetList();
        }

        void OnDisable()
        {
            _cts?.Cancel();
            ClearThumbnailCache();
        }

        void OnGUI()
        {
            InitStyles();

            EditorGUILayout.Space(4);
            DrawToolbar();
            EditorGUILayout.Space(2);
            DrawStatusBar();
            EditorGUILayout.Space(2);
            DrawAssetListContent();
            EditorGUILayout.Space(4);
            DrawActions();
        }

        void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                var newFilter = EditorGUILayout.TextField(_assetFilter, EditorStyles.toolbarSearchField);
                if (newFilter != _assetFilter)
                {
                    _assetFilter = newFilter;
                    _displayCount = PageSize;
                    _filterDirty = true;
                }

                var statusOptions = new[] { "All", "Indexed", "Pending", "Error" };
                int currentIdx = Array.IndexOf(statusOptions, _statusFilter);
                if (currentIdx < 0) currentIdx = 0;
                int newIdx = EditorGUILayout.Popup(currentIdx, statusOptions, EditorStyles.toolbarPopup, GUILayout.Width(80));
                if (newIdx != currentIdx)
                {
                    _statusFilter = statusOptions[newIdx];
                    _displayCount = PageSize;
                    _filterDirty = true;
                }
            }
        }

        void DrawStatusBar()
        {
            var filtered = GetFilteredRecords();

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(
                    $"Total: {_allRecords.Count}  |  Showing {Mathf.Min(_displayCount, filtered.Count)}/{filtered.Count}  |  Selected: {_selectedGuids.Count}",
                    _grayMiniLabel);

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Select All Visible", EditorStyles.miniButtonLeft, GUILayout.Width(100)))
                {
                    int count = Mathf.Min(_displayCount, filtered.Count);
                    for (int i = 0; i < count; i++)
                        _selectedGuids.Add(filtered[i].Guid);
                    Repaint();
                }

                if (GUILayout.Button("Clear Selection", EditorStyles.miniButtonRight, GUILayout.Width(100)))
                {
                    _selectedGuids.Clear();
                    Repaint();
                }
            }

            if (!string.IsNullOrEmpty(_statusText))
                EditorGUILayout.HelpBox(_statusText, MessageType.Info);
        }

        void DrawAssetListContent()
        {
            var filtered = GetFilteredRecords();
            int count = Mathf.Min(_displayCount, filtered.Count);

            _assetListScroll = EditorGUILayout.BeginScrollView(_assetListScroll);

            for (int i = 0; i < count; i++)
            {
                DrawAssetRecord(filtered[i]);
                if (i < count - 1)
                {
                    var sepRect = EditorGUILayout.GetControlRect(false, 1);
                    EditorGUI.DrawRect(sepRect, new Color(0.5f, 0.5f, 0.5f, 0.15f));
                }
            }

            EditorGUILayout.EndScrollView();

            if (_displayCount < filtered.Count)
            {
                if (GUILayout.Button($"Load More ({filtered.Count - _displayCount} remaining)", GUILayout.Height(22)))
                    _displayCount += PageSize;
            }
        }

        void DrawAssetRecord(AssetRecord record)
        {
            bool selected = _selectedGuids.Contains(record.Guid);

            var rowRect = EditorGUILayout.BeginHorizontal(GUILayout.Height(48));

            if (selected && Event.current.type == EventType.Repaint)
            {
                var highlightColor = EditorGUIUtility.isProSkin
                    ? new Color(0.24f, 0.37f, 0.59f, 0.35f)
                    : new Color(0.3f, 0.5f, 0.8f, 0.2f);
                EditorGUI.DrawRect(rowRect, highlightColor);
            }

            GUILayout.Space(4);

            bool newSelected = EditorGUILayout.Toggle(selected, GUILayout.Width(16), GUILayout.Height(48));
            if (newSelected != selected)
            {
                if (newSelected) _selectedGuids.Add(record.Guid);
                else _selectedGuids.Remove(record.Guid);
            }

            EditorGUILayout.BeginVertical(GUILayout.Width(44));
            {
                var thumbnail = GetThumbnail(record.AssetPath);
                var thumbContent = thumbnail != null ? new GUIContent(thumbnail) : GUIContent.none;
                GUILayout.Box(thumbContent, GUIStyle.none, GUILayout.Width(40), GUILayout.Height(32));
                if (thumbnail == null)
                {
                    var thumbRect = GUILayoutUtility.GetLastRect();
                    EditorGUI.DrawRect(thumbRect, new Color(0.3f, 0.3f, 0.3f, 0.3f));
                }

                var statusStyle = record.Status == AssetStatus.Indexed ? _statusIndexedStyle
                    : record.Status == AssetStatus.Error ? _statusErrorStyle
                    : _statusPendingStyle;
                GUILayout.Label(record.Status.ToString(), statusStyle);
            }
            EditorGUILayout.EndVertical();

            EditorGUILayout.BeginVertical();
            {
                string fileName = System.IO.Path.GetFileName(record.AssetPath);
                GUILayout.BeginHorizontal();
                GUILayout.Label(fileName, _boldLabel, GUILayout.ExpandWidth(false));
                GUILayout.Label(record.AssetPath, _grayMiniLabel);
                GUILayout.EndHorizontal();

                if (!string.IsNullOrEmpty(record.Caption))
                    GUILayout.Label(record.Caption, _italicLabel);
            }
            EditorGUILayout.EndVertical();

            if (GUILayout.Button(EditorGUIUtility.IconContent("_Help"), GUIStyle.none,
                    GUILayout.Width(24), GUILayout.Height(24)))
                AssetDetailPopup.Show(record);

            GUILayout.Space(4);

            EditorGUILayout.EndHorizontal();

            HandleRowClick(rowRect, record.AssetPath);
        }

        void HandleRowClick(Rect rowRect, string assetPath)
        {
            var e = Event.current;
            if (e.type != EventType.MouseDown || e.button != 0) return;
            if (!rowRect.Contains(e.mousePosition)) return;

            var obj = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (obj == null) return;

            if (e.clickCount == 2)
                AssetDatabase.OpenAsset(obj);
            else
            {
                EditorGUIUtility.PingObject(obj);
                Selection.activeObject = obj;
            }
            e.Use();
        }

        void DrawActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginDisabledGroup(_selectedGuids.Count == 0 || _isRunning);

                if (GUILayout.Button($"Re-index Selected ({_selectedGuids.Count})", GUILayout.Height(24)))
                    ReindexSelected();

                if (GUILayout.Button($"Delete Selected ({_selectedGuids.Count})", GUILayout.Height(24)))
                    DeleteSelected();

                EditorGUI.EndDisabledGroup();

                if (_isRunning && GUILayout.Button("Cancel", GUILayout.Height(24), GUILayout.Width(60)))
                    _cts?.Cancel();

                if (GUILayout.Button("Refresh", GUILayout.Height(24), GUILayout.Width(70)))
                    RefreshAssetList();
            }
        }

        List<AssetRecord> GetFilteredRecords()
        {
            if (!_filterDirty)
                return _filteredRecords;

            IEnumerable<AssetRecord> result = _allRecords;

            if (_statusFilter != "All")
            {
                if (Enum.TryParse<AssetStatus>(_statusFilter, out var status))
                    result = result.Where(r => r.Status == status);
            }

            if (!string.IsNullOrEmpty(_assetFilter))
            {
                var filter = _assetFilter.Trim();
                result = result.Where(r =>
                    ContainsIgnoreCase(r.AssetPath, filter) ||
                    ContainsIgnoreCase(r.Caption, filter));
            }

            _filteredRecords = result.ToList();
            _filterDirty = false;
            return _filteredRecords;
        }

        void RefreshAssetList()
        {
            SemanticSearchDB db = null;
            try
            {
                db = new SemanticSearchDB();
                db.Open();
                _allRecords = new List<AssetRecord>();
                int offset = 0;
                while (true)
                {
                    var page = db.QueryAssetSummaries(filter: null, status: null, limit: DbLoadPageSize, offset: offset);
                    if (page.Count == 0)
                        break;

                    _allRecords.AddRange(page);
                    offset += page.Count;
                    if (page.Count < DbLoadPageSize)
                        break;
                }
                _filterDirty = true;
                _statusText = null;
            }
            catch (Exception e)
            {
                _allRecords = new List<AssetRecord>();
                _filteredRecords = new List<AssetRecord>();
                _filterDirty = false;
                _statusText = $"Load failed: {e.Message}";
                Debug.LogError($"[SemanticSearch] Asset view refresh failed: {e}");
            }
            finally
            {
                db?.Close();
            }

            ClearThumbnailCache();
            _displayCount = PageSize;
            Repaint();
        }

        async void ReindexSelected()
        {
            var guidsToReindex = _selectedGuids.ToList();
            if (guidsToReindex.Count == 0) return;

            _isRunning = true;
            _statusText = $"Re-indexing {guidsToReindex.Count} assets...";
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            SemanticSearchDB db = null;
            try
            {
                db = new SemanticSearchDB();
                db.Open();
                db.ResetToPending(guidsToReindex);

                var settings = SemanticSearchSettings.Load();
                var config = settings.ToLLMApiConfig();
                var pipeline = new IndexPipeline(db, config);
                var progress = new Progress<BatchProgress>(p =>
                {
                    _statusText = $"Re-indexing {p.Completed}/{p.Total} — {p.CurrentAsset}";
                    Repaint();
                });

                await pipeline.IndexBatchAsync(progress, _cts.Token);

                _selectedGuids.Clear();
                RefreshAssetList();
                _statusText = "Re-index done.";
            }
            catch (OperationCanceledException)
            {
                _statusText = "Re-index cancelled.";
            }
            catch (Exception e)
            {
                try
                {
                    _statusText = $"Error: {e.Message}";
                    Debug.LogError($"[SemanticSearch] {e}");
                }
                catch (Exception ex2) { Debug.LogWarning($"[SemanticSearch] Cleanup: {ex2.Message}"); }
            }
            finally
            {
                try
                {
                    db?.Close();
                    _isRunning = false;
                    Repaint();
                }
                catch (Exception ex2) { Debug.LogWarning($"[SemanticSearch] Cleanup: {ex2.Message}"); }
            }
        }

        void DeleteSelected()
        {
            var guidsToDelete = _selectedGuids.ToList();
            if (guidsToDelete.Count == 0) return;

            if (!EditorUtility.DisplayDialog("Delete Selected",
                    $"Delete {guidsToDelete.Count} selected records from database?",
                    "Delete", "Cancel"))
                return;

            SemanticSearchDB db = null;
            try
            {
                db = new SemanticSearchDB();
                db.Open();
                db.DeleteBatch(guidsToDelete);
                _selectedGuids.Clear();
                RefreshAssetList();
                _statusText = $"Deleted {guidsToDelete.Count} records.";
            }
            catch (Exception e)
            {
                Debug.LogError($"[SemanticSearch] Delete failed: {e}");
            }
            finally
            {
                db?.Close();
            }

            Repaint();
        }

        Texture2D GetThumbnail(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;
            if (_thumbnailCache.TryGetValue(assetPath, out var cached)) return cached;

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset == null) return null;

            var tex = asset as Texture2D ?? AssetPreview.GetMiniThumbnail(asset);
            _thumbnailCache[assetPath] = tex;
            _thumbnailOrder.Enqueue(assetPath);
            TrimThumbnailCache();
            return tex;
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

        static bool ContainsIgnoreCase(string text, string value)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(value))
                return false;
            return text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        void InitStyles()
        {
            if (_boldLabel != null) return;

            _boldLabel = new GUIStyle(EditorStyles.label) { fontStyle = FontStyle.Bold, fontSize = 11 };
            _grayMiniLabel = new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = Color.gray } };
            _italicLabel = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Italic,
                normal = { textColor = Color.gray },
                wordWrap = true
            };
            _statusIndexedStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.3f, 0.8f, 0.3f) }
            };
            _statusPendingStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.9f, 0.7f, 0.2f) }
            };
            _statusErrorStyle = new GUIStyle(EditorStyles.miniLabel)
            {
                fontStyle = FontStyle.Bold,
                normal = { textColor = new Color(0.9f, 0.3f, 0.3f) }
            };
        }
    }

    public class AssetDetailPopup : EditorWindow
    {
        AssetRecord _record;
        Vector2 _scroll;
        Texture2D _preview;

        public static void Show(AssetRecord record)
        {
            var win = CreateInstance<AssetDetailPopup>();
            win.titleContent = new GUIContent("Asset Detail");
            win._record = record;
            win.minSize = new Vector2(420, 360);
            win.maxSize = new Vector2(600, 500);
            win.ShowUtility();
        }

        void OnEnable()
        {
            if (_record != null)
                LoadPreview();
        }

        void LoadPreview()
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(_record.AssetPath);
            if (asset == null) return;
            _preview = asset as Texture2D ?? AssetPreview.GetAssetPreview(asset) ?? AssetPreview.GetMiniThumbnail(asset);
        }

        void OnGUI()
        {
            if (_record == null)
            {
                EditorGUILayout.HelpBox("No record.", MessageType.Warning);
                return;
            }

            if (_preview == null) LoadPreview();

            _scroll = EditorGUILayout.BeginScrollView(_scroll);

            if (_preview != null)
            {
                var previewRect = GUILayoutUtility.GetRect(128, 128, GUILayout.ExpandWidth(false));
                previewRect.x = (position.width - 128f) * 0.5f;
                GUI.DrawTexture(previewRect, _preview, ScaleMode.ScaleToFit);
                EditorGUILayout.Space(4);
            }

            DrawField("GUID", _record.Guid);
            DrawField("Path", _record.AssetPath);
            DrawField("Status", _record.Status.ToString());
            DrawField("MD5", _record.Md5);
            DrawField("Vector Dim", _record.VectorDim.ToString());
            DrawField("Updated At", _record.UpdatedAt);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Caption", EditorStyles.boldLabel);
            EditorGUILayout.TextArea(_record.Caption ?? "(none)", EditorStyles.wordWrappedLabel);

            EditorGUILayout.EndScrollView();
        }

        static void DrawField(string label, string value)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(label, EditorStyles.boldLabel, GUILayout.Width(80));
                EditorGUILayout.SelectableLabel(value ?? "-", EditorStyles.label, GUILayout.Height(18));
            }
        }
    }
}
