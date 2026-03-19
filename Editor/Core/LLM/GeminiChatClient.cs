using System;
using System.Threading.Tasks;

namespace SemanticSearch.Editor.Core.LLM
{
    public class GeminiChatClient : IChatClient
    {
        readonly LLMApiConfig _config;
        readonly LLMHttpClient _http;

        public GeminiChatClient(LLMApiConfig config, LLMHttpClient httpClient)
        {
            _config = config;
            _http = httpClient;
        }

        public async Task<string> ChatAsync(string systemPrompt, string userMessage)
        {
            var body = SimpleJson.Serialize(SimpleJson.Obj(
                ("system_instruction", SimpleJson.Obj(
                    ("parts", SimpleJson.Arr(
                        SimpleJson.Obj(("text", (object)systemPrompt))
                    ))
                )),
                ("contents", SimpleJson.Arr(
                    SimpleJson.Obj(
                        ("parts", SimpleJson.Arr(
                            SimpleJson.Obj(("text", (object)userMessage))
                        ))
                    )
                ))
            ));

            string url = $"{_config.BaseUrl.TrimEnd('/')}/models/{_config.VLModel}:generateContent";
            string response = await _http.PostJsonAsync(url, body, _config.ApiKey);
            return ParseContent(response);
        }

        static string ParseContent(string json)
        {
            var root = SimpleJson.DeserializeObject(json);
            var content = SimpleJson.GetString(root, "candidates", "0", "content", "parts", "0", "text");
            if (string.IsNullOrEmpty(content))
                throw new Exception($"Failed to parse Gemini chat response: {json}");
            return content.Trim();
        }
    }
}
