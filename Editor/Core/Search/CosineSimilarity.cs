using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SemanticSearch.Editor.Core.Search
{
    public static class CosineSimilarity
    {
        public static float Compute(float[] a, float[] b)
        {
            if (a == null || b == null || a.Length != b.Length)
                return 0f;

            float dot = 0f, normA = 0f, normB = 0f;
            for (int i = 0; i < a.Length; i++)
            {
                dot += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }

            float denom = (float)(Math.Sqrt(normA) * Math.Sqrt(normB));
            return denom == 0f ? 0f : dot / denom;
        }

        public static float[] ComputeBatch(float[] query, List<float[]> targets)
        {
            if (targets == null || targets.Count == 0)
                return Array.Empty<float>();

            var results = new float[targets.Count];
            Parallel.For(0, targets.Count, i =>
            {
                results[i] = Compute(query, targets[i]);
            });
            return results;
        }
    }
}
