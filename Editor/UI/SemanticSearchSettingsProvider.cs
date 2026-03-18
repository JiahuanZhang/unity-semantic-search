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
using SemanticSearch.Editor.Core.Watcher;
using AssetStatus = SemanticSearch.Editor.Core.Database.AssetStatus;

namespace SemanticSearch.Editor.UI
{
    public class SemanticSearchSettingsProvider : SettingsProvider
    {
        SemanticSearchSettings _settings;

        bool _foldLLM = true;
        bool _foldWorkflow = true;
        bool _foldDatabase = true;
        int _indexedCount;
        int _pendingCount;
        bool _isRunning;
        string _statusText;
        CancellationTokenSource _cts;

        List<AssetRecord> _allRecords = new List<AssetRecord>();
        readonly HashSet<string> _selectedGuids = new HashSet<string>();
        string _assetFilter = "";
        string _statusFilter = "All";
        Vector2 _assetListScroll;
        const int AssetListPageSize = 50;
        int _assetListDisplayCount = AssetListPageSize;
        readonly Dictionary<string, Texture2D> _thumbnailCache = new Dictionary<string, Texture2D>();

        GUIStyle _boldLabel;
        GUIStyle _grayMiniLabel;
        GUIStyle _italicLabel;
        GUIStyle _statusIndexedStyle;
        GUIStyle _statusPendingStyle;
        GUIStyle _statusErrorStyle;

        SemanticSearchSettingsProvider()
            : base("Project/Semantic Search", SettingsScope.Project) { }

        [SettingsProvider]
        public static SettingsProvider Create() => new SemanticSearchSettingsProvider();

        public override void OnActivate(string searchContext, UnityEngine.UIElements.VisualElement rootElement)
        {
            _settings = SemanticSearchSettings.Load();
            RefreshCounts();
            RefreshAssetList();
        }

        public override void OnDeactivate()
        {
            _cts?.Cancel();
            _settings?.Save();
        }

        public override void OnGUI(string searchContext)
        {
            InitStyles();
            EditorGUILayout.Space(6);

            using (new EditorGUI.IndentLevelScope())
            {
                DrawLLMConfiguration();
                EditorGUILayout.Space(4);
                DrawWorkflowControl();
                EditorGUILayout.Space(4);
                DrawDatabaseMaintenance();
            }

            EditorGUILayout.Space(8);
            DrawIndexedAssetList();
        }

        void DrawLLMConfiguration()
        {
            _foldLLM = EditorGUILayout.Foldout(_foldLLM, "LLM Configuration", true, EditorStyles.foldoutHeader);
            if (!_foldLLM) return;

            using (new EditorGUI.IndentLevelScope())
            {
                DrawProviderSelector();
                EditorGUILayout.Space(4);
                DrawActiveProviderFields();
            }
        }

        void DrawProviderSelector()
        {
            var providers = _settings.Providers;
            var names = providers.Select(p => p.Name).ToArray();

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            int newIdx = EditorGUILayout.Popup("Active Provider", _settings.ActiveProviderIndex, names);
            if (EditorGUI.EndChangeCheck())
            {
                _settings.ActiveProviderIndex = newIdx;
                _settings.Save();
            }

            if (GUILayout.Button("+", GUILayout.Width(24)))
            {
                providers.Add(new LLMProviderConfig { Name = $"Provider {providers.Count + 1}" });
                _settings.ActiveProviderIndex = providers.Count - 1;
                _settings.Save();
            }

            EditorGUI.BeginDisabledGroup(providers.Count <= 1);
            if (GUILayout.Button("-", GUILayout.Width(24)))
            {
                if (EditorUtility.DisplayDialog("Delete Provider",
                        $"Delete \"{providers[_settings.ActiveProviderIndex].Name}\"?", "Delete", "Cancel"))
                {
                    providers.RemoveAt(_settings.ActiveProviderIndex);
                    _settings.ActiveProviderIndex = Mathf.Clamp(
                        _settings.ActiveProviderIndex, 0, providers.Count - 1);
                    _settings.Save();
                }
            }
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.EndHorizontal();
        }

        void DrawActiveProviderFields()
        {
            var provider = _settings.ActiveProvider;

            EditorGUI.BeginChangeCheck();

            provider.Name = EditorGUILayout.TextField("Provider Name", provider.Name);

            var apiKey = EditorGUILayout.PasswordField("API Key", provider.ApiKey ?? "");
            if (apiKey != provider.ApiKey)
            {
                provider.ApiKey = apiKey;
                _settings.SetApiKey(apiKey);
            }

            provider.BaseUrl = EditorGUILayout.TextField("Base URL (OpenAI-compatible)", provider.BaseUrl);
            provider.VLModel = EditorGUILayout.TextField("Vision Model", provider.VLModel);
            provider.EmbeddingModel = EditorGUILayout.TextField("Embedding Model", provider.EmbeddingModel);

            if (EditorGUI.EndChangeCheck())
                _settings.Save();
        }

        void DrawWorkflowControl()
        {
            _foldWorkflow = EditorGUILayout.Foldout(_foldWorkflow, "Workflow Control", true, EditorStyles.foldoutHeader);
            if (!_foldWorkflow) return;

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUI.BeginChangeCheck();

                _settings.AutoIndexOnImport = EditorGUILayout.Toggle("Auto-Index On Import", _settings.AutoIndexOnImport);
                _settings.MaxConcurrent = EditorGUILayout.IntSlider("Max Concurrent Requests", _settings.MaxConcurrent, 1, 10);

                if (EditorGUI.EndChangeCheck())
                    _settings.Save();
            }
        }

        void DrawDatabaseMaintenance()
        {
            _foldDatabase = EditorGUILayout.Foldout(_foldDatabase, "Database Maintenance", true, EditorStyles.foldoutHeader);
            if (!_foldDatabase) return;

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.LabelField("Indexed Assets", _indexedCount.ToString("N0"));
                EditorGUILayout.LabelField("Pending Assets", _pendingCount.ToString("N0"));

                if (!string.IsNullOrEmpty(_statusText))
                {
                    EditorGUILayout.Space(2);
                    EditorGUILayout.HelpBox(_statusText, MessageType.Info);
                }

                EditorGUILayout.Space(4);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginDisabledGroup(_isRunning);

                    if (GUILayout.Button("Scan & Update", GUILayout.Height(24)))
                        RunScanAndIndex();

                    if (GUILayout.Button("Clear Database", GUILayout.Height(24)))
                        ClearDatabase();

                    EditorGUI.EndDisabledGroup();

                    if (_isRunning && GUILayout.Button("Cancel", GUILayout.Height(24)))
                        _cts?.Cancel();
                }
            }
        }

        async void RunScanAndIndex()
        {
            _isRunning = true;
            _statusText = "Scanning assets...";
            _cts = new CancellationTokenSource();

            SemanticSearchDB db = null;
            try
            {
                db = new SemanticSearchDB();
                db.Open();

                AssetScanner.ScanAll(db, progress =>
                {
                    _statusText = $"Scanning... {progress:P0}";
                });

                RefreshCounts(db);
                _statusText = $"Indexing {_pendingCount} assets...";

                var config = _settings.ToLLMApiConfig();
                var pipeline = new IndexPipeline(db, config);
                var progress = new Progress<BatchProgress>(p =>
                {
                    _statusText = $"Indexing {p.Completed}/{p.Total} — {p.CurrentAsset}";
                    Repaint();
                });

                await pipeline.IndexBatchAsync(progress, _cts.Token);

                RefreshCounts(db);
                _allRecords = db.GetAll();
                _statusText = "Done.";
            }
            catch (OperationCanceledException)
            {
                _statusText = "Cancelled.";
            }
            catch (Exception e)
            {
                try
                {
                    _statusText = $"Error: {e.Message}";
                    Debug.LogError($"[SemanticSearch] {e}");
                }
                catch (Exception) { }
            }
            finally
            {
                try
                {
                    db?.Close();
                    _isRunning = false;
                    Repaint();
                }
                catch (Exception) { }
            }
        }

        void ClearDatabase()
        {
            if (!EditorUtility.DisplayDialog(
                    "Clear Database",
                    "This will delete ALL indexed data. Are you sure?",
                    "Delete All", "Cancel"))
                return;

            SemanticSearchDB db = null;
            try
            {
                db = new SemanticSearchDB();
                db.Open();
                db.DeleteAll();
                RefreshCounts(db);
                _allRecords.Clear();
                _selectedGuids.Clear();
                _statusText = "Database cleared.";
            }
            catch (Exception e)
            {
                Debug.LogError($"[SemanticSearch] Clear failed: {e}");
            }
            finally
            {
                db?.Close();
            }
        }

        void RefreshCounts(SemanticSearchDB db = null)
        {
            bool ownDb = db == null;
            try
            {
                if (ownDb)
                {
                    db = new SemanticSearchDB();
                    db.Open();
                }

                _indexedCount = db.GetCount();
                _pendingCount = db.GetPendingCount();
            }
            catch
            {
                _indexedCount = 0;
                _pendingCount = 0;
            }
            finally
            {
                if (ownDb) db?.Close();
            }
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

        void DrawIndexedAssetList()
        {
            EditorGUILayout.LabelField($"Indexed Assets ({_allRecords.Count})", EditorStyles.boldLabel);
            EditorGUILayout.Space(2);
            DrawAssetListToolbar();
            EditorGUILayout.Space(2);
            DrawAssetListContent();
            EditorGUILayout.Space(4);
            DrawAssetListActions();
        }

        void DrawAssetListToolbar()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                var newFilter = EditorGUILayout.TextField(_assetFilter, EditorStyles.toolbarSearchField);
                if (newFilter != _assetFilter)
                {
                    _assetFilter = newFilter;
                    _assetListDisplayCount = AssetListPageSize;
                }

                var statusOptions = new[] { "All", "Indexed", "Pending", "Error" };
                int currentIdx = Array.IndexOf(statusOptions, _statusFilter);
                if (currentIdx < 0) currentIdx = 0;
                int newIdx = EditorGUILayout.Popup(currentIdx, statusOptions, GUILayout.Width(80));
                if (newIdx != currentIdx)
                {
                    _statusFilter = statusOptions[newIdx];
                    _assetListDisplayCount = AssetListPageSize;
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                var filtered = GetFilteredRecords();
                EditorGUILayout.LabelField($"Showing {Mathf.Min(_assetListDisplayCount, filtered.Count)}/{filtered.Count} assets, {_selectedGuids.Count} selected", _grayMiniLabel);

                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Select All Visible", EditorStyles.miniButtonLeft, GUILayout.Width(100)))
                {
                    int count = Mathf.Min(_assetListDisplayCount, filtered.Count);
                    for (int i = 0; i < count; i++)
                        _selectedGuids.Add(filtered[i].Guid);
                }

                if (GUILayout.Button("Clear Selection", EditorStyles.miniButtonRight, GUILayout.Width(100)))
                    _selectedGuids.Clear();
            }
        }

        void DrawAssetListContent()
        {
            var filtered = GetFilteredRecords();
            int count = Mathf.Min(_assetListDisplayCount, filtered.Count);

            float listHeight = Mathf.Min(count * 52f + 30f, 400f);
            _assetListScroll = EditorGUILayout.BeginScrollView(_assetListScroll, GUILayout.Height(listHeight));

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

            if (_assetListDisplayCount < filtered.Count)
            {
                if (GUILayout.Button($"Load More ({filtered.Count - _assetListDisplayCount} remaining)", GUILayout.Height(22)))
                    _assetListDisplayCount += AssetListPageSize;
            }
        }

        void DrawAssetRecord(AssetRecord record)
        {
            bool selected = _selectedGuids.Contains(record.Guid);

            var rowRect = EditorGUILayout.GetControlRect(false, 48);

            if (selected && Event.current.type == EventType.Repaint)
            {
                var highlightColor = EditorGUIUtility.isProSkin
                    ? new Color(0.24f, 0.37f, 0.59f, 0.35f)
                    : new Color(0.3f, 0.5f, 0.8f, 0.2f);
                EditorGUI.DrawRect(rowRect, highlightColor);
            }

            float x = rowRect.x + 5f;
            float y = rowRect.y;
            float rowH = rowRect.height;

            const float btnW = 24f;
            const float btnPad = 10f;
            float rightEdge = rowRect.xMax - btnW - btnPad;

            var toggleRect = new Rect(x, y + (rowH - 16f) * 0.5f, 16f, 16f);
            bool newSelected = EditorGUI.Toggle(toggleRect, selected);
            if (newSelected != selected)
            {
                if (newSelected) _selectedGuids.Add(record.Guid);
                else _selectedGuids.Remove(record.Guid);
            }
            x += 22f;

            var thumbRect = new Rect(x, y + (rowH - 40f) * 0.5f, 40f, 40f);
            var thumbnail = GetThumbnail(record.AssetPath);
            if (thumbnail != null)
                GUI.DrawTexture(thumbRect, thumbnail, ScaleMode.ScaleToFit);
            else
                EditorGUI.DrawRect(thumbRect, new Color(0.3f, 0.3f, 0.3f, 0.3f));
            x += 46f;

            float textX = x;
            float textW = rightEdge - textX;
            float lineH = 16f;

            string fileName = System.IO.Path.GetFileName(record.AssetPath);
            EditorGUI.LabelField(new Rect(textX, y, textW, lineH), fileName, _boldLabel);

            EditorGUI.LabelField(new Rect(textX, y + lineH, textW, lineH), record.AssetPath, _grayMiniLabel);

            var statusStyle = record.Status == AssetStatus.Indexed ? _statusIndexedStyle
                : record.Status == AssetStatus.Error ? _statusErrorStyle
                : _statusPendingStyle;
            EditorGUI.LabelField(new Rect(textX, y + lineH * 2, 60f, lineH), record.Status.ToString(), statusStyle);

            if (!string.IsNullOrEmpty(record.Caption))
            {
                string caption = record.Caption.Length > 60
                    ? record.Caption.Substring(0, 60) + "..."
                    : record.Caption;
                EditorGUI.LabelField(new Rect(textX + 62f, y + lineH * 2, textW - 62f, lineH), caption, _italicLabel);
            }

            var infoBtnRect = new Rect(rightEdge + btnPad, y + (rowH - btnW) * 0.5f, btnW, btnW);
            if (GUI.Button(infoBtnRect, EditorGUIUtility.IconContent("_Help"), GUIStyle.none))
                AssetDetailPopup.Show(record);

            HandleAssetRecordClick(rowRect, toggleRect, infoBtnRect, record.AssetPath);
        }

        void HandleAssetRecordClick(Rect rowRect, Rect toggleRect, Rect infoBtnRect, string assetPath)
        {
            var e = Event.current;
            if (e.type != EventType.MouseDown || !rowRect.Contains(e.mousePosition)) return;
            if (e.button != 0) return;
            if (toggleRect.Contains(e.mousePosition)) return;
            if (infoBtnRect.Contains(e.mousePosition)) return;

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

        void DrawAssetListActions()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUI.BeginDisabledGroup(_selectedGuids.Count == 0 || _isRunning);

                if (GUILayout.Button($"Re-index Selected ({_selectedGuids.Count})", GUILayout.Height(24)))
                    ReindexSelected();

                if (GUILayout.Button($"Delete Selected ({_selectedGuids.Count})", GUILayout.Height(24)))
                    DeleteSelected();

                EditorGUI.EndDisabledGroup();

                if (GUILayout.Button("Refresh", GUILayout.Height(24), GUILayout.Width(70)))
                    RefreshAssetList();
            }
        }

        List<AssetRecord> GetFilteredRecords()
        {
            IEnumerable<AssetRecord> result = _allRecords;

            if (_statusFilter != "All")
            {
                if (Enum.TryParse<AssetStatus>(_statusFilter, out var status))
                    result = result.Where(r => r.Status == status);
            }

            if (!string.IsNullOrEmpty(_assetFilter))
            {
                var filter = _assetFilter.ToLowerInvariant();
                result = result.Where(r =>
                    (r.AssetPath != null && r.AssetPath.ToLowerInvariant().Contains(filter)) ||
                    (r.Caption != null && r.Caption.ToLowerInvariant().Contains(filter)));
            }

            return result.ToList();
        }

        void RefreshAssetList()
        {
            SemanticSearchDB db = null;
            try
            {
                db = new SemanticSearchDB();
                db.Open();
                _allRecords = db.GetAll();
            }
            catch
            {
                _allRecords = new List<AssetRecord>();
            }
            finally
            {
                db?.Close();
            }
            _assetListDisplayCount = AssetListPageSize;
        }

        async void ReindexSelected()
        {
            var guidsToReindex = _selectedGuids.ToList();
            if (guidsToReindex.Count == 0) return;

            _isRunning = true;
            _statusText = $"Re-indexing {guidsToReindex.Count} assets...";
            _cts = new CancellationTokenSource();

            SemanticSearchDB db = null;
            try
            {
                db = new SemanticSearchDB();
                db.Open();

                db.ResetToPending(guidsToReindex);
                RefreshCounts(db);

                var config = _settings.ToLLMApiConfig();
                var pipeline = new IndexPipeline(db, config);
                var progress = new Progress<BatchProgress>(p =>
                {
                    _statusText = $"Re-indexing {p.Completed}/{p.Total} — {p.CurrentAsset}";
                    Repaint();
                });

                await pipeline.IndexBatchAsync(progress, _cts.Token);

                RefreshCounts(db);
                _allRecords = db.GetAll();
                _selectedGuids.Clear();
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
                catch (Exception) { }
            }
            finally
            {
                try
                {
                    db?.Close();
                    _isRunning = false;
                    Repaint();
                }
                catch (Exception) { }
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
                foreach (var guid in guidsToDelete)
                    db.Delete(guid);
                _selectedGuids.Clear();
                RefreshCounts(db);
                _allRecords = db.GetAll();
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
        }

        Texture2D GetThumbnail(string assetPath)
        {
            if (string.IsNullOrEmpty(assetPath)) return null;
            if (_thumbnailCache.TryGetValue(assetPath, out var cached)) return cached;

            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset == null) return null;

            var tex = asset as Texture2D ?? AssetPreview.GetMiniThumbnail(asset);
            _thumbnailCache[assetPath] = tex;
            return tex;
        }

        void Repaint()
        {
            SettingsService.RepaintAllSettingsWindow();
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
