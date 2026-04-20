using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;

namespace SemanticSearch.Editor.Core.LLM
{
    public class EmbeddingClient : IEmbeddingClient
    {
        readonly LLMApiConfig _config;
        readonly LLMHttpClient _http;

        public EmbeddingClient(LLMApiConfig config, LLMHttpClient httpClient)
        {
            _config = config;
            _http = httpClient;
        }

        public async Task<float[]> RequestEmbeddingAsync(string text)
        {
            var body = SimpleJson.Serialize(SimpleJson.Obj(
                ("model", _config.EmbeddingModel),
                ("input", (object)text),
                ("encoding_format", (object)"float")
            ));

            string url = $"{_config.BaseUrl.TrimEnd('/')}/embeddings";
            string response = await _http.PostJsonAsync(url, body, _config.ApiKey);
            return ParseSingleEmbedding(response);
        }

        public async Task<List<float[]>> RequestEmbeddingBatchAsync(List<string> texts)
        {
            var inputArr = new List<object>();
            foreach (var t in texts) inputArr.Add(t);

            var body = SimpleJson.Serialize(SimpleJson.Obj(
                ("model", _config.EmbeddingModel),
                ("input", (object)inputArr),
                ("encoding_format", (object)"float")
            ));

            string url = $"{_config.BaseUrl.TrimEnd('/')}/embeddings";
            string response = await _http.PostJsonAsync(url, body, _config.ApiKey);
            return ParseBatchEmbedding(response);
        }

        static float[] ParseSingleEmbedding(string json)
        {
            var root = SimpleJson.DeserializeObject(json);
            var embeddingArr = SimpleJson.GetArray(root, "data", "0", "embedding");
            if (embeddingArr == null)
                throw new Exception($"Failed to parse embedding from response: {json}");
            return ToFloatArray(embeddingArr);
        }

        static List<float[]> ParseBatchEmbedding(string json)
        {
            var root = SimpleJson.DeserializeObject(json);
            var dataArr = SimpleJson.GetArray(root, "data");
            if (dataArr == null)
                throw new Exception($"Failed to parse batch embedding from response: {json}");

            var results = new List<float[]>();
            foreach (var item in dataArr)
            {
                if (item is Dictionary<string, object> dict &&
                    dict.TryGetValue("embedding", out var embObj) &&
                    embObj is List<object> embList)
                {
                    results.Add(ToFloatArray(embList));
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
