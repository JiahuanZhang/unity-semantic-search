using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SemanticSearch.Editor.Core.LLM;
using UnityEngine;

namespace SemanticSearch.Editor.Core.Pipeline
{
    public class ImageAssetProcessor : IAssetProcessor
    {
        static readonly string[] Extensions = { ".png", ".jpg", ".jpeg", ".tga" };

        readonly IVisionClient _vlClient;
        readonly IEmbeddingClient _embeddingClient;

        public AssetKind Kind => AssetKind.Visual;
        public string[] SupportedExtensions => Extensions;

        public ImageAssetProcessor(IVisionClient vlClient, IEmbeddingClient embeddingClient)
        {
            _vlClient = vlClient;
            _embeddingClient = embeddingClient;
        }

        public bool CanProcess(string assetPath)
        {
            var ext = Path.GetExtension(assetPath).ToLowerInvariant();
            return Array.IndexOf(Extensions, ext) >= 0;
        }

        public byte[] GetAssetData(string assetPath)
        {
            var fullPath = Path.GetFullPath(assetPath);
            return File.Exists(fullPath) ? File.ReadAllBytes(fullPath) : null;
        }

        public async Task<AssetProcessResult> ProcessAsync(string assetPath, CancellationToken ct)
        {
            var imageBytes = GetAssetData(assetPath);
            if (imageBytes == null || imageBytes.Length == 0)
                return AssetProcessResult.Fail($"Failed to read image file: {assetPath}");

            ct.ThrowIfCancellationRequested();

            var prompt = AssetTextBuilder.BuildVisionPrompt(assetPath, "image");
            var caption = await _vlClient.RequestCaptionAsync(imageBytes, prompt);
            ct.ThrowIfCancellationRequested();

            var embeddingText = AssetTextBuilder.BuildEmbeddingText(assetPath, caption, "image");
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
