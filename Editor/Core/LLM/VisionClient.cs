using System;
using System.Threading.Tasks;

namespace SemanticSearch.Editor.Core.LLM
{
    public class VisionClient : IVisionClient
    {
        readonly LLMApiConfig _config;
        readonly LLMHttpClient _http;

        const string DefaultPrompt =
    "Extract keywords/tags for this image in both Chinese and English. "
    + "Cover: subject,art style,key colors,notable features. "
    + "Output ONLY comma-separated keywords, no sentences. "
    + "Format: '关键词1、关键词2；keyword1, keyword2'. Max 10 keywords per language. "
    + "Example: 动漫少女、金发、蓝瞳、蝴蝶结发饰、Q版萌系;anime girl, blonde hair, blue eyes, bow accessory, chibi style";

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
