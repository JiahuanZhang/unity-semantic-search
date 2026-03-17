using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using SemanticSearch.Editor.Core.Database;
using SemanticSearch.Editor.Core.Pipeline;
using SemanticSearch.Editor.Core.Watcher;

namespace SemanticSearch.Editor.UI
{
    public class SemanticSearchSettingsProvider : SettingsProvider
    {
        static readonly string[] VisionModels = { "qwen-vl-plus", "qwen-vl-max" };
        static readonly string[] EmbeddingModels = { "text-embedding-v3", "text-embedding-v2" };

        SemanticSearchSettings _settings;
        string _apiKey;

        bool _foldLLM = true;
        bool _foldWorkflow = true;
        bool _foldDatabase = true;

        int _indexedCount;
        int _pendingCount;
        bool _isRunning;
        string _statusText;
        CancellationTokenSource _cts;

        SemanticSearchSettingsProvider()
            : base("Project/Semantic Search", SettingsScope.Project) { }

        [SettingsProvider]
        public static SettingsProvider Create() => new SemanticSearchSettingsProvider();

        public override void OnActivate(string searchContext, UnityEngine.UIElements.VisualElement rootElement)
        {
            _settings = SemanticSearchSettings.Load();
            _apiKey = _settings.GetApiKey();
            RefreshCounts();
        }

        public override void OnDeactivate()
        {
            _cts?.Cancel();
            _settings?.Save();
        }

        public override void OnGUI(string searchContext)
        {
            EditorGUILayout.Space(6);

            using (new EditorGUI.IndentLevelScope())
            {
                DrawLLMConfiguration();
                EditorGUILayout.Space(4);
                DrawWorkflowControl();
                EditorGUILayout.Space(4);
                DrawDatabaseMaintenance();
            }
        }

        void DrawLLMConfiguration()
        {
            _foldLLM = EditorGUILayout.Foldout(_foldLLM, "LLM Configuration", true, EditorStyles.foldoutHeader);
            if (!_foldLLM) return;

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUI.BeginChangeCheck();

                _apiKey = EditorGUILayout.PasswordField("API Key", _apiKey);

                int vlIdx = Array.IndexOf(VisionModels, _settings.VisionModel);
                if (vlIdx < 0) vlIdx = 0;
                vlIdx = EditorGUILayout.Popup("Vision Model", vlIdx, VisionModels);
                _settings.VisionModel = VisionModels[vlIdx];

                int embIdx = Array.IndexOf(EmbeddingModels, _settings.EmbeddingModel);
                if (embIdx < 0) embIdx = 0;
                embIdx = EditorGUILayout.Popup("Embed Model", embIdx, EmbeddingModels);
                _settings.EmbeddingModel = EmbeddingModels[embIdx];

                _settings.EndPoint = EditorGUILayout.TextField("EndPoint", _settings.EndPoint);

                if (EditorGUI.EndChangeCheck())
                {
                    _settings.SetApiKey(_apiKey);
                    _settings.Save();
                }
            }
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

        void Repaint()
        {
            SettingsService.RepaintAllSettingsWindow();
        }
    }
}
