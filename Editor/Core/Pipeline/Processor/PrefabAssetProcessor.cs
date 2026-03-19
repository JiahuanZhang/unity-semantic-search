using SemanticSearch.Editor.Core.LLM;

namespace SemanticSearch.Editor.Core.Pipeline
{
    public class PrefabAssetProcessor : PreviewBasedAssetProcessor
    {
        static readonly string[] Extensions = { ".prefab" };

        public override string[] SupportedExtensions => Extensions;
        protected override string AssetType => "prefab";

        public PrefabAssetProcessor(IVisionClient vlClient, IEmbeddingClient embeddingClient)
            : base(vlClient, embeddingClient) { }
    }
}
