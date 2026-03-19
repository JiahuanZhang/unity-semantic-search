using System;
using System.Threading.Tasks;

namespace SemanticSearch.Editor.Core.LLM
{
    public class ChatClient : IChatClient
    {
        readonly LLMApiConfig _config;
        readonly LLMHttpClient _http;

        public ChatClient(LLMApiConfig config, LLMHttpClient httpClient)
        {
            _config = config;
            _http = httpClient;
        }

        public async Task<string> ChatAsync(string systemPrompt, string userMessage)
        {
            var body = SimpleJson.Serialize(SimpleJson.Obj(
                ("model", _config.VLModel),
                ("messages", SimpleJson.Arr(
                    SimpleJson.Obj(
                        ("role", (object)"system"),
                        ("content", (object)systemPrompt)
                    ),
                    SimpleJson.Obj(
                        ("role", (object)"user"),
                        ("content", (object)userMessage)
                    )
                ))
            ));

            string url = $"{_config.BaseUrl.TrimEnd('/')}/chat/completions";
            string response = await _http.PostJsonAsync(url, body, _config.ApiKey);
            return ParseContent(response);
        }

        static string ParseContent(string json)
        {
            var root = SimpleJson.DeserializeObject(json);
            var content = SimpleJson.GetString(root, "choices", "0", "message", "content");
            if (string.IsNullOrEmpty(content))
                throw new Exception($"Failed to parse chat response: {json}");
            return content.Trim();
        }
    }
}
