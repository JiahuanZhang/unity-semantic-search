using System;
using SemanticSearch.Editor.UI;

namespace SemanticSearch.Editor.Core.LLM
{
    public enum LLMProviderType
    {
        OpenAI = 0,
        Gemini = 1,
    }

    [Serializable]
    public class LLMProviderConfig
    {
        public string Name = "Default";
        public LLMProviderType ProviderType = LLMProviderType.OpenAI;
        public string ApiKey = "";
        public bool SaveApiKeyToJson = false;
        public string BaseUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1";
        public string VLModel = "qwen-vl-plus";
        public string EmbeddingModel = "text-embedding-v3";

        public LLMProviderConfig() { }

        public LLMProviderConfig(LLMProviderConfig other)
        {
            Name = other.Name;
            ProviderType = other.ProviderType;
            ApiKey = other.ApiKey;
            SaveApiKeyToJson = other.SaveApiKeyToJson;
            BaseUrl = other.BaseUrl;
            VLModel = other.VLModel;
            EmbeddingModel = other.EmbeddingModel;
        }
    }

    public class LLMApiConfig
    {
        public LLMProviderType ProviderType = LLMProviderType.OpenAI;
        public string ApiKey = "";
        public string VLModel = "qwen-vl-plus";
        public string EmbeddingModel = "text-embedding-v3";
        public string BaseUrl = "https://dashscope.aliyuncs.com/compatible-mode/v1";
        public int MaxConcurrent = 3;
        public int TimeoutSeconds = 30;
        public int MaxRetries = 3;

        public static LLMApiConfig Load()
        {
            return SemanticSearchSettings.Instance.ToLLMApiConfig();
        }
    }
}
