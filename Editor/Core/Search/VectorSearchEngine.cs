using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SemanticSearch.Editor.Core.Database;
using SemanticSearch.Editor.Core.LLM;

namespace SemanticSearch.Editor.Core.Search
{
    public class VectorSearchEngine
    {
        class CachedVector
        {
            public string Guid;
            public float[] Vector;
            public float Norm;
        }

        static readonly object CacheLock = new object();
        static List<CachedVector> s_cachedVectors;
        static (int count, string maxUpdatedAt) s_cachedSignature;
        static bool s_hasCache;

        private readonly SemanticSearchDB _db;
        private readonly IEmbeddingClient _embeddingClient;

        public VectorSearchEngine(SemanticSearchDB db, IEmbeddingClient embeddingClient)
        {
            _db = db;
            _embeddingClient = embeddingClient;
        }

        public static void InvalidateCache()
        {
            lock (CacheLock)
            {
                s_cachedVectors = null;
                s_hasCache = false;
            }
        }

        public async Task<List<SearchResult>> SearchAsync(string queryText, int topK = 30, float minSimilarity = 0.3f)
        {
            if (topK <= 0)
                return new List<SearchResult>();

            var queryVector = await _embeddingClient.RequestEmbeddingAsync(queryText);
            var queryNorm = CosineSimilarity.ComputeNorm(queryVector);
            if (queryVector == null || queryVector.Length == 0 || queryNorm <= 0f)
                return new List<SearchResult>();

            var vectors = await Task.Run(() => GetCachedVectors());
            if (vectors.Count == 0)
                return new List<SearchResult>();

            int expectedDim = vectors[0].Vector.Length;
            if (queryVector.Length != expectedDim)
                throw new InvalidOperationException(
                    $"Query vector dimension ({queryVector.Length}) does not match database vector dimension ({expectedDim}).");

            var similarities = new float[vectors.Count];
            Parallel.For(0, vectors.Count, i =>
            {
                similarities[i] = CosineSimilarity.ComputeWithNorms(
                    queryVector,
                    queryNorm,
                    vectors[i].Vector,
                    vectors[i].Norm);
            });

            var results = SelectTopK(vectors, similarities, topK, minSimilarity);
            if (results.Count == 0)
                return results;

            var guids = new List<string>(results.Count);
            foreach (var r in results)
                guids.Add(r.Guid);
            var records = await Task.Run(() => _db.GetAssetSummariesByGuids(guids));

            foreach (var result in results)
            {
                if (records.TryGetValue(result.Guid, out var record))
                {
                    result.AssetPath = record.AssetPath;
                    result.Caption = record.Caption;
                }
            }

            return results;
        }

        List<CachedVector> GetCachedVectors()
        {
            var signature = _db.GetVectorSnapshotSignature();

            lock (CacheLock)
            {
                if (s_hasCache && s_cachedVectors != null && s_cachedSignature.Equals(signature))
                    return s_cachedVectors;
            }

            var rawVectors = _db.GetAllVectors();
            var cached = new List<CachedVector>(rawVectors.Count);
            foreach (var item in rawVectors)
            {
                if (item.vector == null || item.vector.Length == 0)
                    continue;

                float norm = CosineSimilarity.ComputeNorm(item.vector);
                if (norm <= 0f)
                    continue;

                cached.Add(new CachedVector
                {
                    Guid = item.guid,
                    Vector = item.vector,
                    Norm = norm
                });
            }

            lock (CacheLock)
            {
                s_cachedVectors = cached;
                s_cachedSignature = signature;
                s_hasCache = true;
                return s_cachedVectors;
            }
        }

        static List<SearchResult> SelectTopK(
            List<CachedVector> vectors,
            float[] similarities,
            int topK,
            float minSimilarity)
        {
            var top = new List<SearchResult>(Math.Min(topK, 64));

            for (int i = 0; i < vectors.Count; i++)
            {
                float similarity = similarities[i];
                if (similarity < minSimilarity)
                    continue;

                var candidate = new SearchResult
                {
                    Guid = vectors[i].Guid,
                    Similarity = similarity
                };

                if (top.Count < topK)
                {
                    InsertAscending(top, candidate);
                    continue;
                }

                if (similarity <= top[0].Similarity)
                    continue;

                top.RemoveAt(0);
                InsertAscending(top, candidate);
            }

            top.Sort((a, b) => b.Similarity.CompareTo(a.Similarity));
            return top;
        }

        static void InsertAscending(List<SearchResult> sorted, SearchResult item)
        {
            int left = 0;
            int right = sorted.Count;
            while (left < right)
            {
                int mid = left + (right - left) / 2;
                if (sorted[mid].Similarity < item.Similarity)
                    left = mid + 1;
                else
                    right = mid;
            }
            sorted.Insert(left, item);
        }
    }
}
