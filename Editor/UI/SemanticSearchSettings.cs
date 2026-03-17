using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using SemanticSearch.Editor.Core.LLM;

namespace SemanticSearch.Editor.UI
{
    [Serializable]
    public class SemanticSearchSettings
    {
        public string VisionModel = "qwen-vl-plus";
        public string EmbeddingModel = "text-embedding-v3";
        public string EndPoint = "https://dashscope.aliyuncs.com/compatible-mode/v1";
        public bool AutoIndexOnImport = true;
        public int MaxConcurrent = 3;

        private static SemanticSearchSettings _instance;
        public static SemanticSearchSettings Instance => _instance ?? (_instance = Load());

        static string ProjectHash => Application.dataPath.GetHashCode().ToString("X8");
        static string EditorPrefsKey => $"SemanticSearch_{ProjectHash}_ApiKey";

        static string SettingsPath
        {
            get
            {
                var projectRoot = Path.GetDirectoryName(Application.dataPath);
                return Path.Combine(projectRoot, "UserSettings", "SemanticSearch", "Settings.json");
            }
        }

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
            return _instance;
        }

        public void Save()
        {
            try
            {
                var path = SettingsPath;
                var dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                var json = JsonUtility.ToJson(this, true);
                File.WriteAllText(path, json);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SemanticSearch] Failed to save settings: {e.Message}");
            }
        }

        public string GetApiKey() => EditorPrefs.GetString(EditorPrefsKey, "");

        public void SetApiKey(string key) => EditorPrefs.SetString(EditorPrefsKey, key);

        public LLMApiConfig ToLLMApiConfig()
        {
            return new LLMApiConfig
            {
                ApiKey = GetApiKey(),
                VLModel = VisionModel,
                EmbeddingModel = EmbeddingModel,
                BaseUrl = EndPoint,
                MaxConcurrent = MaxConcurrent,
            };
        }
    }
}
