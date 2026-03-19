using SemanticSearch.Editor.Core.LLM;

namespace SemanticSearch.Editor.Core.Pipeline
{
    public class ModelAssetProcessor : PreviewBasedAssetProcessor
    {
        static readonly string[] Extensions = { ".fbx", ".obj", ".blend", ".dae", ".3ds", ".gltf", ".glb" };

        public override string[] SupportedExtensions => Extensions;
        protected override string AssetType => "model";

        public ModelAssetProcessor(IVisionClient vlClient, IEmbeddingClient embeddingClient)
            : base(vlClient, embeddingClient) { }
    }
}
