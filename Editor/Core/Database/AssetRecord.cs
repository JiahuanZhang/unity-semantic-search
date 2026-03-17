using System;

namespace SemanticSearch.Editor.Core.Database
{
    public enum AssetStatus
    {
        Pending,
        Indexed,
        Error
    }

    public class AssetRecord
    {
        public string Guid { get; set; }
        public string AssetPath { get; set; }
        public string Md5 { get; set; }
        public string Caption { get; set; }
        public float[] Vector { get; set; }
        public int VectorDim { get; set; }
        public AssetStatus Status { get; set; } = AssetStatus.Pending;
        public string UpdatedAt { get; set; }

        public AssetRecord() { }

        public AssetRecord(string guid, string assetPath, string md5)
        {
            Guid = guid;
            AssetPath = assetPath;
            Md5 = md5;
            UpdatedAt = DateTime.UtcNow.ToString("o");
        }
    }
}
