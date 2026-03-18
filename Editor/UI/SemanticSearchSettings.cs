using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using SemanticSearch.Editor.Core.LLM;

namespace SemanticSearch.Editor.UI
{
    [Serializable]
    public class SemanticSearchSettings
    {
        public List<LLMProviderConfig> Providers = new List<LLMProviderConfig>();
        public int ActiveProviderIndex;
        public bool AutoIndexOnImport = false;
        public int MaxConcurrent = 3;

        public List<string> IncludeFilters = new List<string> { "Assets/**" };
        public List<string> ExcludeFilters = new List<string>();

        [NonSerialized] public bool IsAdmin;
        public int AdminProviderIndex;
        public int UserProviderIndex;

        [NonSerialized] private static SemanticSearchSettings _instance;
        public static SemanticSearchSettings Instance => _instance ?? (_instance = Load());

        public LLMProviderConfig ActiveProvider
        {
            get
            {
                EnsureDefaults();
                int idx = Mathf.Clamp(ActiveProviderIndex, 0, Providers.Count - 1);
                return Providers[idx];
            }
        }

        public LLMProviderConfig GetRoleProvider()
        {
            EnsureDefaults();
            int idx = IsAdmin ? AdminProviderIndex : UserProviderIndex;
            idx = Mathf.Clamp(idx, 0, Providers.Count - 1);
            return Providers[idx];
        }

        static string SettingsPath
        {
            get
            {
                var projectRoot = Path.GetDirectoryName(Application.dataPath);
                return Path.Combine(projectRoot, "UserSettings", "SemanticSearch", "Settings.json");
            }
        }

        static string ProjectHash => Application.dataPath.GetHashCode().ToString("X8");

        static string ApiKeyPrefsPrefix => $"SemanticSearch_{ProjectHash}_Provider_";
        static string AdminPrefsKey => $"SemanticSearch_{ProjectHash}_IsAdmin";

        public static SemanticSearchSettings Load()
        {
            try
            {
                var path = SettingsPath;
                if (File.Exists(path))
                {
                    var json = File.ReadAllText(path);
                    var settings = JsonUtility.FromJson<SemanticSearchSettings>(json);
                    if (settings != null)
                    {
                        settings.EnsureDefaults();
                        settings.LoadApiKeys();
                        settings.IsAdmin = EditorPrefs.GetBool(AdminPrefsKey, false);
                        _instance = settings;
                        return settings;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SemanticSearch] Failed to load settings: {e.Message}");
            }

            _instance = new SemanticSearchSettings();
            _instance.EnsureDefaults();
            _instance.MigrateFromLegacy();
            _instance.IsAdmin = EditorPrefs.GetBool(AdminPrefsKey, false);
            return _instance;
        }

        void MigrateFromLegacy()
        {
            var legacyKey = $"SemanticSearch_{ProjectHash}_ApiKey";
            var legacyApiKey = EditorPrefs.GetString(legacyKey, "");
            if (!string.IsNullOrEmpty(legacyApiKey) && Providers.Count > 0)
            {
                Providers[0].ApiKey = legacyApiKey;
                SaveApiKeys();
                EditorPrefs.DeleteKey(legacyKey);
            }
        }

        void EnsureDefaults()
        {
            if (Providers == null || Providers.Count == 0)
            {
                Providers = new List<LLMProviderConfig>
                {
                    new LLMProviderConfig
                    {
                        Name = "Qwen (DashScope)",
                        BaseUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1",
                        VLModel = "qwen-vl-plus",
                        EmbeddingModel = "text-embedding-v3"
                    }
                };
            }

            ActiveProviderIndex = Mathf.Clamp(ActiveProviderIndex, 0, Providers.Count - 1);
            AdminProviderIndex = Mathf.Clamp(AdminProviderIndex, 0, Providers.Count - 1);
            UserProviderIndex = Mathf.Clamp(UserProviderIndex, 0, Providers.Count - 1);

            if (IncludeFilters == null)
                IncludeFilters = new List<string> { "Assets/**" };
            if (ExcludeFilters == null)
                ExcludeFilters = new List<string>();
        }

        public void Save()
        {
            try
            {
                var path = SettingsPath;
                var dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                SaveApiKeys();
                EditorPrefs.SetBool(AdminPrefsKey, IsAdmin);

                var clone = JsonUtility.FromJson<SemanticSearchSettings>(JsonUtility.ToJson(this));
                foreach (var p in clone.Providers)
                    p.ApiKey = "";

                var json = JsonUtility.ToJson(clone, true);
                File.WriteAllText(path, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SemanticSearch] Failed to save settings: {e.Message}");
            }
        }

        void LoadApiKeys()
        {
            for (int i = 0; i < Providers.Count; i++)
                Providers[i].ApiKey = EditorPrefs.GetString(ApiKeyPrefsPrefix + i, "");
        }

        void SaveApiKeys()
        {
            for (int i = 0; i < Providers.Count; i++)
                EditorPrefs.SetString(ApiKeyPrefsPrefix + i, Providers[i].ApiKey ?? "");
        }

        public void SaveApiKeyForProvider(int index)
        {
            if (index >= 0 && index < Providers.Count)
                EditorPrefs.SetString(ApiKeyPrefsPrefix + index, Providers[index].ApiKey ?? "");
        }

        public string GetApiKey() => ActiveProvider.ApiKey;

        public void SetApiKey(string key)
        {
            ActiveProvider.ApiKey = key;
            SaveApiKeys();
        }

        public LLMApiConfig ToLLMApiConfig()
        {
            var p = GetRoleProvider();
            return new LLMApiConfig
            {
                ProviderType = p.ProviderType,
                ApiKey = p.ApiKey,
                VLModel = p.VLModel,
                EmbeddingModel = p.EmbeddingModel,
                BaseUrl = p.BaseUrl,
                MaxConcurrent = MaxConcurrent,
            };
        }
    }
}
