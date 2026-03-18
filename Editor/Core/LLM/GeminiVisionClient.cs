using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SemanticSearch.Editor.Core.LLM
{
    public class GeminiVisionClient : IVisionClient
    {
        readonly LLMApiConfig _config;
        readonly LLMHttpClient _http;

        public GeminiVisionClient(LLMApiConfig config, LLMHttpClient httpClient)
        {
            _config = config;
            _http = httpClient;
        }

        public async Task<string> RequestCaptionAsync(byte[] imageData, string prompt = null)
        {
            string base64 = Convert.ToBase64String(imageData);

            var body = SimpleJson.Serialize(SimpleJson.Obj(
                ("contents", SimpleJson.Arr(
                    SimpleJson.Obj(
                        ("parts", SimpleJson.Arr(
                            SimpleJson.Obj(
                                ("inline_data", SimpleJson.Obj(
                                    ("mime_type", (object)"image/png"),
                                    ("data", (object)base64)
                                ))
                            ),
                            SimpleJson.Obj(
                                ("text", (object)(prompt ?? VisionConstants.DefaultPrompt))
                            )
                        ))
                    )
                ))
            ));

            string url = $"{_config.BaseUrl.TrimEnd('/')}/models/{_config.VLModel}:generateContent";
            string response = await _http.PostJsonAsync(url, body, _config.ApiKey);
            return ParseCaption(response);
        }

        static string ParseCaption(string json)
        {
            var root = SimpleJson.DeserializeObject(json);
            var content = SimpleJson.GetString(root, "candidates", "0", "content", "parts", "0", "text");
            if (string.IsNullOrEmpty(content))
                throw new Exception($"Failed to parse caption from Gemini response: {json}");
            return content.Trim();
        }
    }
}
