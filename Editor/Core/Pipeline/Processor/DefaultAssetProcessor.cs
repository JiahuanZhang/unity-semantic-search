using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SemanticSearch.Editor.Core.LLM;

namespace SemanticSearch.Editor.Core.Pipeline
{
    /// <summary>
    /// Fallback processor that indexes any asset using only file name, path, and extension.
    /// No Vision call — generates embedding directly from metadata text.
    /// </summary>
    public class DefaultAssetProcessor : IAssetProcessor
    {
        static readonly string[] Extensions = { };

        readonly IEmbeddingClient _embeddingClient;

        public AssetKind Kind => AssetKind.Text;
        public string[] SupportedExtensions => Extensions;

        public DefaultAssetProcessor(IEmbeddingClient embeddingClient)
        {
            _embeddingClient = embeddingClient;
        }

        public bool CanProcess(string assetPath) => true;

        public byte[] GetAssetData(string assetPath) => null;

        public async Task<AssetProcessResult> ProcessAsync(string assetPath, CancellationToken ct)
        {
            if (string.IsNullOrEmpty(assetPath))
                return AssetProcessResult.Fail("Asset path is empty");

            ct.ThrowIfCancellationRequested();

            var ext = Path.GetExtension(assetPath).TrimStart('.').ToLowerInvariant();
            var assetType = string.IsNullOrEmpty(ext) ? "unknown" : ext;
            var caption = $"Unity asset file at {assetPath}";

            var embeddingText = AssetTextBuilder.BuildEmbeddingText(assetPath, caption, assetType);
            var vector = await _embeddingClient.RequestEmbeddingAsync(embeddingText);
            ct.ThrowIfCancellationRequested();

            if (vector == null || vector.Length == 0)
                return AssetProcessResult.Fail($"Empty embedding returned: {assetPath}");

            return new AssetProcessResult
            {
                Success = true,
                Caption = caption,
                Vector = vector
            };
        }
    }
}
