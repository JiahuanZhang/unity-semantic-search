using System;

namespace SemanticSearch.Editor.Core.Database
{
    public static class VectorSerializer
    {
        public static byte[] Serialize(float[] vector)
        {
            if (vector == null) return null;
            var bytes = new byte[vector.Length * sizeof(float)];
            Buffer.BlockCopy(vector, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        public static float[] Deserialize(byte[] data)
        {
            if (data == null || data.Length == 0) return null;
            var floats = new float[data.Length / sizeof(float)];
            Buffer.BlockCopy(data, 0, floats, 0, data.Length);
            return floats;
        }

        public static float[] Deserialize(byte[] data, int expectedDim)
        {
            var result = Deserialize(data);
            if (result != null && result.Length != expectedDim)
                throw new ArgumentException(
                    $"Vector dimension mismatch: expected {expectedDim}, got {result.Length}");
            return result;
        }
    }
}
