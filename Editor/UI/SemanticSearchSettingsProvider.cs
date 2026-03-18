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
using SemanticSearch.Editor.Core.Utils;
using SemanticSearch.Editor.Core.Watcher;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace SemanticSearch.Editor.UI
{
    public class SemanticSearchSettingsProvider : SettingsProvider
    {
        static bool s_needsCountRefresh;

        SemanticSearchSettings _settings;

        bool _foldLLM = true;
        bool _foldWorkflow = true;
        bool _foldDatabase = true;
        int _indexedCount;
        int _pendingCount;
        bool _isRunning;
        bool _isTestingLlm;
        string _statusText;
        string _llmTestStatus;
        MessageType _llmTestStatusType = MessageType.Info;
        CancellationTokenSource _cts;

        SemanticSearchSettingsProvider()
            : base("Project/Semantic Search", SettingsScope.Project) { }

        [SettingsProvider]
        public static SettingsProvider Create() => new SemanticSearchSettingsProvider();

        [MenuItem("Window/Semantic Search/Settings")]
        static void OpenSettingsFromWindowMenu()
        {
            SettingsService.OpenProjectSettings("Project/Semantic Search");
        }

        public static void RequestCountsRefresh()
        {
            s_needsCountRefresh = true;
            SettingsService.RepaintAllSettingsWindow();
        }

        public override void OnActivate(string searchContext, UnityEngine.UIElements.VisualElement rootElement)
        {
            _settings = SemanticSearchSettings.Load();
            RefreshCounts();
        }

        public override void OnDeactivate()
        {
            _cts?.Cancel();
            _settings?.Save();
        }

        public override void OnGUI(string searchContext)
        {
            if (s_needsCountRefresh && !_isRunning)
            {
                RefreshCounts();
                s_needsCountRefresh = false;
            }

            EditorGUILayout.Space(6);

            using (new EditorGUI.IndentLevelScope())
            {
                DrawAdminToggle();
                EditorGUILayout.Space(4);
                DrawLLMConfiguration();

                if (_settings.IsAdmin)
                {
                    EditorGUILayout.Space(4);
                    DrawRoleProviderSelector();
                    EditorGUILayout.Space(4);
                    DrawWorkflowControl();
                    EditorGUILayout.Space(4);
                    DrawDatabaseMaintenance();
                }
            }

            EditorGUILayout.Space(8);
            DrawAssetViewShortcut();
        }

        void DrawAdminToggle()
        {
            EditorGUI.BeginChangeCheck();
            bool newValue = EditorGUILayout.Toggle("Admin Mode", _settings.IsAdmin);
            if (EditorGUI.EndChangeCheck() && newValue != _settings.IsAdmin)
            {
                if (newValue)
                {
                    if (EditorUtility.DisplayDialog("Admin Mode",
                            "Warning: Admin mode is intended only for developers.\nAre you sure you want to enable it?",
                            "Confirm", "Cancel"))
                    {
                        _settings.IsAdmin = true;
                        _settings.Save();
                    }
                }
                else
                {
                    _settings.IsAdmin = false;
                    _settings.Save();
                }
            }
        }

        void DrawRoleProviderSelector()
        {
            var names = _settings.Providers.Select(p => p.Name).ToArray();
            if (names.Length == 0) return;

            EditorGUILayout.LabelField("Role Provider Assignment", EditorStyles.boldLabel);
            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUI.BeginChangeCheck();

                _settings.AdminProviderIndex = EditorGUILayout.Popup(
                    "Admin Provider", _settings.AdminProviderIndex, names);
                _settings.UserProviderIndex = EditorGUILayout.Popup(
                    "User Provider", _settings.UserProviderIndex, names);

                var current = _settings.IsAdmin ? "Admin" : "User";
                var currentProvider = _settings.GetRoleProvider();
                EditorGUILayout.HelpBox(
                    $"Current role: {current}, using provider: {currentProvider.Name}",
                    MessageType.Info);

                if (EditorGUI.EndChangeCheck())
                    _settings.Save();
            }
        }

        void DrawLLMConfiguration()
        {
            _foldLLM = EditorGUILayout.Foldout(_foldLLM, "LLM Configuration", true, EditorStyles.foldoutHeader);
            if (!_foldLLM) return;

            using (new EditorGUI.IndentLevelScope())
            {
                if (_settings.IsAdmin)
                {
                    DrawProviderSelector();
                    EditorGUILayout.Space(4);
                    DrawActiveProviderFields();
                }
                else
                {
                    DrawUserProviderFields();
                }
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

            var oldType = provider.ProviderType;
            provider.ProviderType = (LLMProviderType)EditorGUILayout.EnumPopup("Provider Type", provider.ProviderType);
            if (provider.ProviderType != oldType)
                ApplyProviderTypeDefaults(provider);

            var apiKey = EditorGUILayout.PasswordField("API Key", provider.ApiKey ?? "");
            if (apiKey != provider.ApiKey)
            {
                provider.ApiKey = apiKey;
                _settings.SetApiKey(apiKey);
            }

            string baseUrlLabel = provider.ProviderType == LLMProviderType.Gemini
                ? "Base URL (Gemini API)" : "Base URL (OpenAI-compatible)";
            provider.BaseUrl = EditorGUILayout.TextField(baseUrlLabel, provider.BaseUrl);
            provider.VLModel = EditorGUILayout.TextField("Vision Model", provider.VLModel);
            provider.EmbeddingModel = EditorGUILayout.TextField("Embedding Model", provider.EmbeddingModel);

            if (EditorGUI.EndChangeCheck())
                _settings.Save();

            EditorGUILayout.Space(4);
            DrawLlmTestControls(provider);
        }

        void DrawUserProviderFields()
        {
            var provider = _settings.GetRoleProvider();

            EditorGUILayout.LabelField("Provider", provider.Name, EditorStyles.boldLabel);

            EditorGUI.BeginChangeCheck();

            provider.ProviderType = (LLMProviderType)EditorGUILayout.EnumPopup("Provider Type", provider.ProviderType);

            var apiKey = EditorGUILayout.PasswordField("API Key", provider.ApiKey ?? "");
            if (apiKey != provider.ApiKey)
            {
                provider.ApiKey = apiKey;
                _settings.SaveApiKeyForProvider(_settings.UserProviderIndex);
            }

            string baseUrlLabel = provider.ProviderType == LLMProviderType.Gemini
                ? "Base URL (Gemini API)" : "Base URL (OpenAI-compatible)";
            provider.BaseUrl = EditorGUILayout.TextField(baseUrlLabel, provider.BaseUrl);
            provider.VLModel = EditorGUILayout.TextField("Vision Model", provider.VLModel);
            provider.EmbeddingModel = EditorGUILayout.TextField("Embedding Model", provider.EmbeddingModel);

            if (EditorGUI.EndChangeCheck())
                _settings.Save();

            EditorGUILayout.Space(4);
            DrawLlmTestControls(provider);
        }

        void DrawLlmTestControls(LLMProviderConfig provider)
        {
            if (!string.IsNullOrEmpty(_llmTestStatus))
                EditorGUILayout.HelpBox(_llmTestStatus, _llmTestStatusType);

            EditorGUI.BeginDisabledGroup(_isTestingLlm);
            if (GUILayout.Button(_isTestingLlm ? "Testing..." : "Test LLM", GUILayout.Height(22), GUILayout.Width(100)))
                TestLlmConnectionAsync(new LLMProviderConfig(provider));
            EditorGUI.EndDisabledGroup();
        }

        async void TestLlmConnectionAsync(LLMProviderConfig provider)
        {
            _isTestingLlm = true;
            _llmTestStatus = "Testing current provider...";
            _llmTestStatusType = MessageType.Info;
            RefreshSettingsWindow();

            try
            {
                if (string.IsNullOrWhiteSpace(provider.ApiKey))
                    throw new InvalidOperationException("API Key is empty.");
                if (string.IsNullOrWhiteSpace(provider.BaseUrl))
                    throw new InvalidOperationException("Base URL is empty.");
                if (string.IsNullOrWhiteSpace(provider.EmbeddingModel))
                    throw new InvalidOperationException("Embedding Model is empty.");

                var config = new LLMApiConfig
                {
                    ProviderType = provider.ProviderType,
                    ApiKey = provider.ApiKey,
                    BaseUrl = provider.BaseUrl,
                    VLModel = provider.VLModel,
                    EmbeddingModel = provider.EmbeddingModel,
                    MaxConcurrent = Mathf.Max(1, _settings.MaxConcurrent),
                };

                var http = new LLMHttpClient(config);
                var embeddingClient = LLMClientFactory.CreateEmbeddingClient(config, http);
                var vector = await embeddingClient.RequestEmbeddingAsync("semantic search connectivity test");

                if (vector == null || vector.Length == 0)
                    throw new Exception("Embedding response is empty.");

                _llmTestStatus = $"LLM is available. Embedding dims: {vector.Length}.";
                _llmTestStatusType = MessageType.Info;
            }
            catch (Exception e)
            {
                _llmTestStatus = $"LLM test failed: {e.Message}";
                _llmTestStatusType = MessageType.Error;
            }
            finally
            {
                _isTestingLlm = false;
                RefreshSettingsWindow();
            }
        }

        static void ApplyProviderTypeDefaults(LLMProviderConfig provider)
        {
            switch (provider.ProviderType)
            {
                case LLMProviderType.Gemini:
                    provider.BaseUrl = "https://generativelanguage.googleapis.com/v1beta";
                    provider.VLModel = "gemini-2.5-flash";
                    provider.EmbeddingModel = "gemini-embedding-001";
                    break;
                default:
                    provider.BaseUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1";
                    provider.VLModel = "qwen-vl-plus";
                    provider.EmbeddingModel = "text-embedding-v3";
                    break;
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

                if (GUILayout.Button("Open Database Folder", GUILayout.Height(22)))
                    OpenDatabaseFolder();
            }
        }

        static void OpenDatabaseFolder()
        {
            var folder = System.IO.Path.Combine(
                System.IO.Directory.GetParent(Application.dataPath).FullName,
                "ProjectSettings", "SemanticSearch");
            if (System.IO.Directory.Exists(folder))
                EditorUtility.RevealInFinder(folder);
            else
                EditorUtility.DisplayDialog("Semantic Search", "Database folder does not exist yet.", "OK");
        }

        async void RunScanAndIndex()
        {
            _isRunning = true;
            _statusText = "Scanning assets...";
            _cts?.Dispose();
            _cts = new CancellationTokenSource();

            SemanticSearchDB db = null;
            try
            {
                var totalSw = Stopwatch.StartNew();
                long managedBefore = GC.GetTotalMemory(false);

                db = new SemanticSearchDB();
                db.Open();

                var scanSw = Stopwatch.StartNew();
                var changedGuids = AssetScanner.ScanAll(db, progress =>
                {
                    _statusText = $"Scanning... {progress:P0}";
                });
                scanSw.Stop();

                RefreshCounts(db);
                _statusText = $"Indexing {_pendingCount} assets...";
                int pendingBeforeIndex = _pendingCount;

                var config = _settings.ToLLMApiConfig();
                var pipeline = new IndexPipeline(db, config);
                var progress = new Progress<BatchProgress>(p =>
                {
                    _statusText = $"Indexing {p.Completed}/{p.Total} — {p.CurrentAsset}";
                    RefreshSettingsWindow();
                });

                var indexSw = Stopwatch.StartNew();
                var batchResult = await pipeline.IndexBatchAsync(progress, _cts.Token);
                indexSw.Stop();

                RefreshCounts(db);
                totalSw.Stop();

                long managedAfter = GC.GetTotalMemory(false);
                long managedDelta = managedAfter - managedBefore;
                double indexThroughput = indexSw.Elapsed.TotalSeconds > 0.01
                    ? batchResult.Completed / indexSw.Elapsed.TotalSeconds
                    : 0d;

                _statusText =
                    $"Done. Scan {scanSw.Elapsed.TotalSeconds:F2}s, " +
                    $"Index {indexSw.Elapsed.TotalSeconds:F2}s ({indexThroughput:F2} assets/s).";

                Debug.Log(
                    $"[SemanticSearch] Scan+Index perf: changed={changedGuids.Count}, pendingBeforeIndex={pendingBeforeIndex}, " +
                    $"indexed={batchResult.Succeeded}, failed={batchResult.Failed}, skipped={batchResult.Skipped}, " +
                    $"scanTime={scanSw.Elapsed.TotalSeconds:F2}s, indexTime={indexSw.Elapsed.TotalSeconds:F2}s, " +
                    $"totalTime={totalSw.Elapsed.TotalSeconds:F2}s, managedBefore={FormatUtils.FormatBytes(managedBefore)}, " +
                    $"managedAfter={FormatUtils.FormatBytes(managedAfter)}, managedDelta={FormatUtils.FormatBytes(managedDelta)}.");
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
                catch (Exception ex2) { Debug.LogWarning($"[SemanticSearch] Cleanup: {ex2.Message}"); }
            }
            finally
            {
                try
                {
                    db?.Close();
                    _isRunning = false;
                    RefreshSettingsWindow();
                }
                catch (Exception ex2) { Debug.LogWarning($"[SemanticSearch] Cleanup: {ex2.Message}"); }
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

                _indexedCount = db.GetIndexedCount();
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

        void DrawAssetViewShortcut()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField("Indexed Assets", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Open Asset View", GUILayout.Height(22), GUILayout.Width(130)))
                    AssetViewWindow.Open();
            }
            EditorGUILayout.HelpBox(
                "Open Asset browsing Window via: Window > Semantic Search > Asset View",
                MessageType.Info);
        }

        void RefreshSettingsWindow()
        {
            SettingsService.RepaintAllSettingsWindow();
        }

    }
}
