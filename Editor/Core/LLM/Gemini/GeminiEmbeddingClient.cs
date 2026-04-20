using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace SemanticSearch.Editor.Core.LLM
{
    public class GeminiEmbeddingClient : IEmbeddingClient
    {
        readonly LLMApiConfig _config;
        readonly LLMHttpClient _http;

        public GeminiEmbeddingClient(LLMApiConfig config, LLMHttpClient httpClient)
        {
            _config = config;
            _http = httpClient;
        }

        public async Task<float[]> RequestEmbeddingAsync(string text)
        {
            var body = SimpleJson.Serialize(SimpleJson.Obj(
                ("model", (object)$"models/{_config.EmbeddingModel}"),
                ("content", SimpleJson.Obj(
                    ("parts", SimpleJson.Arr(
                        SimpleJson.Obj(("text", (object)text))
                    ))
                ))
            ));

            string url = $"{_config.BaseUrl.TrimEnd('/')}/models/{_config.EmbeddingModel}:embedContent";
            string response = await _http.PostJsonAsync(url, body, _config.ApiKey);
            return ParseSingleEmbedding(response);
        }

        public async Task<List<float[]>> RequestEmbeddingBatchAsync(List<string> texts)
        {
            var requests = new List<object>();
            foreach (var t in texts)
            {
                requests.Add(SimpleJson.Obj(
                    ("model", (object)$"models/{_config.EmbeddingModel}"),
                    ("content", SimpleJson.Obj(
                        ("parts", SimpleJson.Arr(
                            SimpleJson.Obj(("text", (object)t))
                        ))
                    ))
                ));
            }

            var body = SimpleJson.Serialize(SimpleJson.Obj(
                ("requests", (object)requests)
            ));

            string url = $"{_config.BaseUrl.TrimEnd('/')}/models/{_config.EmbeddingModel}:batchEmbedContents";
            string response = await _http.PostJsonAsync(url, body, _config.ApiKey);
            return ParseBatchEmbedding(response);
        }

        static float[] ParseSingleEmbedding(string json)
        {
            var root = SimpleJson.DeserializeObject(json);
            var embeddingArr = SimpleJson.GetArray(root, "embedding", "values");
            if (embeddingArr == null)
                throw new Exception($"Failed to parse embedding from Gemini response: {json}");
            return ToFloatArray(embeddingArr);
        }

        static List<float[]> ParseBatchEmbedding(string json)
        {
            var root = SimpleJson.DeserializeObject(json);
            var dataArr = SimpleJson.GetArray(root, "embeddings");
            if (dataArr == null)
                throw new Exception($"Failed to parse batch embedding from Gemini response: {json}");

            var results = new List<float[]>();
            foreach (var item in dataArr)
            {
                if (item is Dictionary<string, object> dict &&
                    dict.TryGetValue("values", out var valObj) &&
                    valObj is List<object> valList)
                {
                    results.Add(ToFloatArray(valList));
                }
            }
            return results;
        }

        static float[] ToFloatArray(List<object> list)
        {
            var arr = new float[list.Count];
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] is double d)
                    arr[i] = (float)d;
                else if (list[i] is string s)
                    float.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out arr[i]);
            }
            return arr;
        }
    }
}
