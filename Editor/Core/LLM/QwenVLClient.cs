using System;
using System.Threading.Tasks;

namespace SemanticSearch.Editor.Core.LLM
{
    public class QwenVLClient
    {
        readonly LLMApiConfig _config;
        readonly LLMHttpClient _http;

        const string DefaultPrompt =
            "请用中文简要描述这张图片的内容、风格、主要颜色和可能的用途，控制在100字以内。";

        public QwenVLClient(LLMApiConfig config, LLMHttpClient httpClient)
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
