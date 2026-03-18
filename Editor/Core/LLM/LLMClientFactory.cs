namespace SemanticSearch.Editor.Core.LLM
{
    public static class LLMClientFactory
    {
        public static IVisionClient CreateVisionClient(LLMApiConfig config, LLMHttpClient http)
        {
            switch (config.ProviderType)
            {
                case LLMProviderType.Gemini:
                    return new GeminiVisionClient(config, http);
                default:
                    return new VisionClient(config, http);
            }
        }

        public static IEmbeddingClient CreateEmbeddingClient(LLMApiConfig config, LLMHttpClient http)
        {
            switch (config.ProviderType)
            {
                case LLMProviderType.Gemini:
                    return new GeminiEmbeddingClient(config, http);
                default:
                    return new EmbeddingClient(config, http);
            }
        }
    }
}
