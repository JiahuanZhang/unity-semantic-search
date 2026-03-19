using SemanticSearch.Editor.Core.LLM;

namespace SemanticSearch.Editor.Core.Pipeline
{
    public class MaterialAssetProcessor : PreviewBasedAssetProcessor
    {
        static readonly string[] Extensions = { ".mat" };

        public override string[] SupportedExtensions => Extensions;
        protected override string AssetType => "material";

        public MaterialAssetProcessor(IVisionClient vlClient, IEmbeddingClient embeddingClient)
            : base(vlClient, embeddingClient) { }
    }
}
