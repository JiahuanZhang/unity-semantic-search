using SemanticSearch.Editor.UI;

namespace SemanticSearch.Editor.Core.LLM
{
    public class LLMApiConfig
    {
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
