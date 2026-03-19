using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SemanticSearch.Editor.Core.LLM;
using UnityEditor;
using UnityEngine;

namespace SemanticSearch.Editor.Core.Pipeline
{
    /// <summary>
    /// Base processor for assets that use AssetPreview thumbnails + Vision description.
    /// </summary>
    public abstract class PreviewBasedAssetProcessor : IAssetProcessor
    {
        const int PreviewMaxRetries = 30;
        const int PreviewRetryIntervalMs = 100;

        protected readonly IVisionClient VlClient;
        protected readonly IEmbeddingClient EmbeddingClient;

        public AssetKind Kind => AssetKind.Visual;
        public abstract string[] SupportedExtensions { get; }
        protected abstract string AssetType { get; }

        protected PreviewBasedAssetProcessor(IVisionClient vlClient, IEmbeddingClient embeddingClient)
        {
            VlClient = vlClient;
            EmbeddingClient = embeddingClient;
        }

        public bool CanProcess(string assetPath)
        {
            var ext = Path.GetExtension(assetPath).ToLowerInvariant();
            return Array.IndexOf(SupportedExtensions, ext) >= 0;
        }

        public byte[] GetAssetData(string assetPath)
        {
            var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(assetPath);
            if (asset == null) return null;

            AssetPreview.SetPreviewTextureCacheSize(256);
            var preview = AssetPreview.GetAssetPreview(asset);

            int retries = 0;
            while (preview == null
                   && AssetPreview.IsLoadingAssetPreview(asset.GetInstanceID())
                   && retries < PreviewMaxRetries)
            {
                Thread.Sleep(PreviewRetryIntervalMs);
                preview = AssetPreview.GetAssetPreview(asset);
                retries++;
            }

            if (preview == null)
                preview = AssetPreview.GetMiniThumbnail(asset);

            if (preview == null || !preview.isReadable) return null;

            return preview.EncodeToPNG();
        }

        public async Task<AssetProcessResult> ProcessAsync(string assetPath, CancellationToken ct)
        {
            var imageBytes = GetAssetData(assetPath);
            ct.ThrowIfCancellationRequested();

            string caption;
            if (imageBytes != null && imageBytes.Length > 0)
            {
                var prompt = AssetTextBuilder.BuildVisionPrompt(assetPath, AssetType);
                caption = await VlClient.RequestCaptionAsync(imageBytes, prompt);
            }
            else
            {
                caption = $"Unity {AssetType} asset file at {assetPath}";
            }

            ct.ThrowIfCancellationRequested();

            var embeddingText = AssetTextBuilder.BuildEmbeddingText(assetPath, caption, AssetType);
            var vector = await EmbeddingClient.RequestEmbeddingAsync(embeddingText);
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
