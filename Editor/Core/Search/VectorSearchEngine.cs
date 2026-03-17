using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using SemanticSearch.Editor.Core.Database;
using SemanticSearch.Editor.Core.LLM;

namespace SemanticSearch.Editor.Core.Search
{
    public class VectorSearchEngine
    {
        private readonly SemanticSearchDB _db;
        private readonly QwenEmbeddingClient _embeddingClient;

        public VectorSearchEngine(SemanticSearchDB db, QwenEmbeddingClient embeddingClient)
        {
            _db = db;
            _embeddingClient = embeddingClient;
        }

        public async Task<List<SearchResult>> SearchAsync(string queryText, int topK = 20, float minSimilarity = 0.3f)
        {
            var queryVector = await _embeddingClient.RequestEmbeddingAsync(queryText);

            var vectors = _db.GetAllVectors();
            if (vectors.Count == 0)
                return new List<SearchResult>();

            int expectedDim = vectors[0].vector.Length;
            if (queryVector.Length != expectedDim)
                throw new InvalidOperationException(
                    $"Query vector dimension ({queryVector.Length}) does not match database vector dimension ({expectedDim}).");

            var targets = new List<float[]>(vectors.Count);
            foreach (var v in vectors)
                targets.Add(v.vector);

            var similarities = CosineSimilarity.ComputeBatch(queryVector, targets);

            var results = new List<SearchResult>();
            for (int i = 0; i < vectors.Count; i++)
            {
                if (similarities[i] >= minSimilarity)
                    results.Add(new SearchResult { Guid = vectors[i].guid, Similarity = similarities[i] });
            }

            results.Sort((a, b) => b.Similarity.CompareTo(a.Similarity));
            if (results.Count > topK)
                results.RemoveRange(topK, results.Count - topK);

            foreach (var r in results)
            {
                var record = _db.GetByGuid(r.Guid);
                if (record != null)
                {
                    r.AssetPath = record.AssetPath;
                    r.Caption = record.Caption;
                }
            }

            return results;
        }

    }
}
