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
using L10n = SemanticSearch.Editor.Core.Localization.L10n;
using Stopwatch = System.Diagnostics.Stopwatch;

namespace SemanticSearch.Editor.UI
{
    public class SemanticSearchSettingsProvider : SettingsProvider
    {
        static bool s_needsCountRefresh;

        SemanticSearchSettings _settings;

        bool _foldLLM = true;
        bool _foldPrompt;
        bool _foldWorkflow = true;
        bool _foldFilter = true;
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
            RefreshCountsAsync();
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
                s_needsCountRefresh = false;
                RefreshCountsAsync();
            }

            EditorGUILayout.Space(6);

            using (new EditorGUI.IndentLevelScope())
            {
                DrawAdminModeToggle();
                EditorGUILayout.Space(4);
                DrawLLMConfiguration();

                if (_settings.AdminMode)
                {
                    EditorGUILayout.Space(4);
                    DrawPromptConfiguration();
                    EditorGUILayout.Space(4);
                    DrawWorkflowControl();
                    EditorGUILayout.Space(4);
                    DrawAssetFilterRules();
                    EditorGUILayout.Space(4);
                    DrawDatabaseMaintenance();
                }
            }

            EditorGUILayout.Space(8);
            DrawAssetViewShortcut();
        }

        void DrawAdminModeToggle()
        {
            EditorGUI.BeginChangeCheck();
            var newValue = EditorGUILayout.Toggle(
                new GUIContent(L10n.AdminMode, L10n.AdminModeTooltip),
                _settings.AdminMode);
            if (EditorGUI.EndChangeCheck() && newValue != _settings.AdminMode)
            {
                if (newValue)
                {
                    if (EditorUtility.DisplayDialog(
                            L10n.AdminModeConfirmTitle,
                            L10n.AdminModeConfirmMessage,
                            L10n.Confirm, L10n.Cancel))
                    {
                        _settings.AdminMode = true;
                    }
                }
                else
                {
                    _settings.AdminMode = false;
                }
            }
        }

        void DrawLLMConfiguration()
        {
            _foldLLM = EditorGUILayout.Foldout(_foldLLM, L10n.LLMConfiguration, true, EditorStyles.foldoutHeader);
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
            int newIdx = EditorGUILayout.Popup(L10n.ActiveProvider, _settings.ActiveProviderIndex, names);
            if (EditorGUI.EndChangeCheck())
            {
                _settings.ActiveProviderIndex = newIdx;
                _settings.Save();
            }

            if (GUILayout.Button("+", GUILayout.Width(24)))
            {
                providers.Add(new LLMProviderConfig { Name = L10n.NewProviderName(providers.Count + 1) });
                _settings.ActiveProviderIndex = providers.Count - 1;
                _settings.Save();
            }

            EditorGUI.BeginDisabledGroup(providers.Count <= 1);
            if (GUILayout.Button("-", GUILayout.Width(24)))
            {
                if (EditorUtility.DisplayDialog(L10n.DeleteProviderTitle,
                        L10n.DeleteProviderMessage(providers[_settings.ActiveProviderIndex].Name),
                        L10n.Delete, L10n.Cancel))
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

            provider.Name = EditorGUILayout.TextField(L10n.ProviderName, provider.Name);

            var oldType = provider.ProviderType;
            provider.ProviderType = (LLMProviderType)EditorGUILayout.EnumPopup(L10n.ProviderType, provider.ProviderType);
            if (provider.ProviderType != oldType)
                ApplyProviderTypeDefaults(provider);

            DrawApiKeyFieldWithPersistToggle(provider, () => _settings.SetApiKey(provider.ApiKey));

            string baseUrlLabel = provider.ProviderType == LLMProviderType.Gemini
                ? L10n.BaseUrlGemini : L10n.BaseUrlOpenAI;
            provider.BaseUrl = EditorGUILayout.TextField(baseUrlLabel, provider.BaseUrl);
            provider.VLModel = EditorGUILayout.TextField(L10n.VisionModel, provider.VLModel);
            provider.EmbeddingModel = EditorGUILayout.TextField(L10n.EmbeddingModel, provider.EmbeddingModel);

            if (EditorGUI.EndChangeCheck())
                _settings.Save();

            EditorGUILayout.Space(4);
            DrawLlmTestControls(provider);
        }

        void DrawLlmTestControls(LLMProviderConfig provider)
        {
            if (!string.IsNullOrEmpty(_llmTestStatus))
                EditorGUILayout.HelpBox(_llmTestStatus, _llmTestStatusType);

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(EditorGUI.indentLevel * 15);
            EditorGUI.BeginDisabledGroup(_isTestingLlm);
            if (GUILayout.Button(_isTestingLlm ? L10n.TestingBtn : L10n.TestLLM, GUILayout.Height(22), GUILayout.Width(100)))
                TestLlmConnectionAsync(new LLMProviderConfig(provider));
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }

        void DrawApiKeyFieldWithPersistToggle(LLMProviderConfig provider, Action onApiKeyChanged)
        {
            EditorGUILayout.BeginHorizontal();
            var apiKey = EditorGUILayout.PasswordField(L10n.ApiKey, provider.ApiKey ?? "");
            var saveToJson = GUILayout.Toggle(
                provider.SaveApiKeyToJson,
                new GUIContent(L10n.Save, L10n.SaveApiKeyTooltip),
                GUILayout.Width(56));
            EditorGUILayout.EndHorizontal();

            if (apiKey != provider.ApiKey)
            {
                provider.ApiKey = apiKey;
                onApiKeyChanged?.Invoke();
            }

            provider.SaveApiKeyToJson = saveToJson;
        }

        async void TestLlmConnectionAsync(LLMProviderConfig provider)
        {
            _isTestingLlm = true;
            _llmTestStatus = L10n.TestingProvider;
            _llmTestStatusType = MessageType.Info;
            RefreshSettingsWindow();

            try
            {
                if (string.IsNullOrWhiteSpace(provider.ApiKey))
                    throw new InvalidOperationException(L10n.ApiKeyEmpty);
                if (string.IsNullOrWhiteSpace(provider.BaseUrl))
                    throw new InvalidOperationException(L10n.BaseUrlEmpty);
                if (string.IsNullOrWhiteSpace(provider.EmbeddingModel))
                    throw new InvalidOperationException(L10n.EmbeddingModelEmpty);

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

                _llmTestStatus = L10n.LLMAvailable(vector.Length);
                _llmTestStatusType = MessageType.Info;
            }
            catch (Exception e)
            {
                _llmTestStatus = L10n.LLMTestFailed(e.Message);
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

        void DrawPromptConfiguration()
        {
            _foldPrompt = EditorGUILayout.Foldout(_foldPrompt, L10n.PromptConfiguration, true, EditorStyles.foldoutHeader);
            if (!_foldPrompt) return;

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUI.BeginChangeCheck();

                DrawPromptField(
                    L10n.VisionPromptLabel,
                    ref _settings.VisionPrompt,
                    L10n.DefaultVisionPrompt);

                EditorGUILayout.Space(4);

                DrawPromptField(
                    L10n.SearchEnhancerPromptLabel,
                    ref _settings.SearchEnhancerPrompt,
                    L10n.DefaultSearchEnhancerPrompt);

                if (EditorGUI.EndChangeCheck())
                    _settings.Save();
            }
        }

        void DrawPromptField(string label, ref string value, string defaultValue)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);

            var displayText = string.IsNullOrWhiteSpace(value) ? defaultValue : value;
            var newText = EditorGUILayout.TextArea(displayText, GUILayout.MinHeight(60));

            if (newText != displayText)
                value = newText;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(EditorGUI.indentLevel * 15);
                if (GUILayout.Button(
                        new GUIContent(L10n.ResetToDefault, L10n.ResetToDefaultTooltip),
                        GUILayout.Width(60), GUILayout.Height(18)))
                {
                    value = "";
                    GUI.FocusControl(null);
                }
            }
        }

        void DrawWorkflowControl()
        {
            _foldWorkflow = EditorGUILayout.Foldout(_foldWorkflow, L10n.WorkflowControl, true, EditorStyles.foldoutHeader);
            if (!_foldWorkflow) return;

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUI.BeginChangeCheck();

                _settings.AutoIndexOnImport = EditorGUILayout.Toggle(L10n.AutoIndexOnImport, _settings.AutoIndexOnImport);
                _settings.MaxConcurrent = EditorGUILayout.IntSlider(L10n.MaxConcurrentRequests, _settings.MaxConcurrent, 1, 10);

                if (EditorGUI.EndChangeCheck())
                    _settings.Save();
            }
        }

        void DrawAssetFilterRules()
        {
            _foldFilter = EditorGUILayout.Foldout(_foldFilter, L10n.AssetFilterRules, true, EditorStyles.foldoutHeader);
            if (!_foldFilter) return;

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUI.BeginChangeCheck();

                DrawFilterList(L10n.IncludeRules, _settings.IncludeFilters);
                EditorGUILayout.Space(4);
                DrawFilterList(L10n.ExcludeRules, _settings.ExcludeFilters);

                if (EditorGUI.EndChangeCheck())
                    _settings.Save();

                EditorGUILayout.Space(2);
                EditorGUILayout.HelpBox(L10n.GlobPatternHint, MessageType.Info);
            }
        }

        void DrawFilterList(string label, List<string> filters)
        {
            EditorGUILayout.LabelField(label, EditorStyles.miniBoldLabel);

            int removeIdx = -1;
            for (int i = 0; i < filters.Count; i++)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    filters[i] = EditorGUILayout.TextField(filters[i]);
                    if (GUILayout.Button("x", GUILayout.Width(20), GUILayout.Height(18)))
                        removeIdx = i;
                }
            }

            if (removeIdx >= 0)
                filters.RemoveAt(removeIdx);

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(EditorGUI.indentLevel * 15);
                if (GUILayout.Button(L10n.AddRule, GUILayout.Width(100), GUILayout.Height(20)))
                    filters.Add("");
            }
        }

        void DrawDatabaseMaintenance()
        {
            _foldDatabase = EditorGUILayout.Foldout(_foldDatabase, L10n.DatabaseMaintenance, true, EditorStyles.foldoutHeader);
            if (!_foldDatabase) return;

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.LabelField(L10n.IndexedAssets, _indexedCount.ToString("N0"));
                EditorGUILayout.LabelField(L10n.PendingAssets, _pendingCount.ToString("N0"));

                if (!string.IsNullOrEmpty(_statusText))
                {
                    EditorGUILayout.Space(2);
                    EditorGUILayout.HelpBox(_statusText, MessageType.Info);
                }

                EditorGUILayout.Space(4);

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(EditorGUI.indentLevel * 15);
                    EditorGUI.BeginDisabledGroup(_isRunning);

                    if (GUILayout.Button(L10n.ScanAndUpdate, GUILayout.Height(24)))
                        RunScanAndIndex();

                    if (GUILayout.Button(L10n.ClearDatabase, GUILayout.Height(24)))
                        ClearDatabase();

                    EditorGUI.EndDisabledGroup();

                    if (_isRunning && GUILayout.Button(L10n.Cancel, GUILayout.Height(24)))
                        _cts?.Cancel();
                }

                using (new EditorGUILayout.HorizontalScope())
                {
                    GUILayout.Space(EditorGUI.indentLevel * 15);
                    if (GUILayout.Button(L10n.OpenDatabaseFolder, GUILayout.Height(22)))
                        OpenDatabaseFolder();
                }
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
                EditorUtility.DisplayDialog(L10n.SemanticSearch, L10n.DatabaseFolderNotExist, L10n.OK);
        }

        async void RunScanAndIndex()
        {
            _isRunning = true;
            _statusText = L10n.ScanningAssets;
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
                    _statusText = L10n.ScanningProgress($"{progress:P0}");
                });
                scanSw.Stop();

                RefreshCounts(db);
                _statusText = L10n.IndexingAssets(_pendingCount);
                int pendingBeforeIndex = _pendingCount;

                var config = _settings.ToLLMApiConfig();
                var pipeline = new IndexPipeline(db, config);
                var progress = new Progress<BatchProgress>(p =>
                {
                    _statusText = L10n.IndexingProgress(p.Completed, p.Total, p.CurrentAsset);
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

                _statusText = L10n.ScanIndexDone(scanSw.Elapsed.TotalSeconds, indexSw.Elapsed.TotalSeconds, indexThroughput);

                Debug.Log(
                    $"[SemanticSearch] Scan+Index perf: changed={changedGuids.Count}, pendingBeforeIndex={pendingBeforeIndex}, " +
                    $"indexed={batchResult.Succeeded}, failed={batchResult.Failed}, skipped={batchResult.Skipped}, " +
                    $"scanTime={scanSw.Elapsed.TotalSeconds:F2}s, indexTime={indexSw.Elapsed.TotalSeconds:F2}s, " +
                    $"totalTime={totalSw.Elapsed.TotalSeconds:F2}s, managedBefore={FormatUtils.FormatBytes(managedBefore)}, " +
                    $"managedAfter={FormatUtils.FormatBytes(managedAfter)}, managedDelta={FormatUtils.FormatBytes(managedDelta)}.");
            }
            catch (OperationCanceledException)
            {
                _statusText = L10n.Cancelled;
            }
            catch (Exception e)
            {
                try
                {
                    _statusText = L10n.ErrorMessage(e.Message);
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

        async void ClearDatabase()
        {
            if (!EditorUtility.DisplayDialog(
                    L10n.ClearDatabase,
                    L10n.ClearDatabaseMessage,
                    L10n.DeleteAll, L10n.Cancel))
                return;

            _statusText = L10n.Clearing;
            RefreshSettingsWindow();

            try
            {
                await Task.Run(() =>
                {
                    using (var db = new SemanticSearchDB())
                    {
                        db.Open();
                        db.DeleteAll();
                    }
                });
                _statusText = L10n.DatabaseCleared;
                RefreshCountsAsync();
            }
            catch (Exception e)
            {
                _statusText = L10n.ClearFailed(e.Message);
                Debug.LogError($"[SemanticSearch] Clear failed: {e}");
            }
            RefreshSettingsWindow();
        }

        void RefreshCounts(SemanticSearchDB db)
        {
            try
            {
                _indexedCount = db.GetIndexedCount();
                _pendingCount = db.GetPendingCount();
            }
            catch
            {
                _indexedCount = 0;
                _pendingCount = 0;
            }
        }

        async void RefreshCountsAsync()
        {
            try
            {
                var (indexed, pending) = await Task.Run(() =>
                {
                    using (var db = new SemanticSearchDB())
                    {
                        db.Open();
                        return (db.GetIndexedCount(), db.GetPendingCount());
                    }
                });
                _indexedCount = indexed;
                _pendingCount = pending;
            }
            catch
            {
                _indexedCount = 0;
                _pendingCount = 0;
            }
            RefreshSettingsWindow();
        }

        void DrawAssetViewShortcut()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(L10n.IndexedAssets, EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button(L10n.OpenAssetView, GUILayout.Height(22), GUILayout.Width(130)))
                    AssetViewWindow.Open();
            }
            EditorGUILayout.HelpBox(L10n.OpenAssetViewHint, MessageType.Info);
        }

        void RefreshSettingsWindow()
        {
            SettingsService.RepaintAllSettingsWindow();
        }

    }
}
