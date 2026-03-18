using System;
using System.Threading.Tasks;

namespace SemanticSearch.Editor.Core.LLM
{
    public class VisionClient
    {
        readonly LLMApiConfig _config;
        readonly LLMHttpClient _http;

        const string DefaultPrompt =
    "Please describe this image in both Chinese and English. "
    + "Include content, style, main colors, and possible usage. "
    + "Format: [CN] 中文描述 [EN] English description. Keep each under 80 words.";

        public VisionClient(LLMApiConfig config, LLMHttpClient httpClient)
        {
            _config = config;
            _http = httpClient;
        }

        public async Task<string> RequestCaptionAsync(byte[] imageData, string prompt = null)
        {
            string base64 = Convert.ToBase64String(imageData);
            string dataUrl = $"data:image/png;base64,{base64}";

            var body = SimpleJson.Serialize(SimpleJson.Obj(
                ("model", _config.VLModel),
                ("messages", SimpleJson.Arr(
                    SimpleJson.Obj(
                        ("role", (object)"user"),
                        ("content", SimpleJson.Arr(
                            SimpleJson.Obj(
                                ("type", (object)"image_url"),
                                ("image_url", SimpleJson.Obj(
                                    ("url", (object)dataUrl)
                                ))
                            ),
                            SimpleJson.Obj(
                                ("type", (object)"text"),
                                ("text", (object)(prompt ?? DefaultPrompt))
                            )
                        ))
                    )
                ))
            ));

            string url = $"{_config.BaseUrl.TrimEnd('/')}/chat/completions";
            string response = await _http.PostJsonAsync(url, body, _config.ApiKey);
            return ParseCaption(response);
        }

        static string ParseCaption(string json)
        {
            var root = SimpleJson.DeserializeObject(json);
            var content = SimpleJson.GetString(root, "choices", "0", "message", "content");
            if (string.IsNullOrEmpty(content))
                throw new Exception($"Failed to parse caption from response: {json}");
            return content.Trim();
        }
    }
}
