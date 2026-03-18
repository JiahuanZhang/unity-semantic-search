using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SemanticSearch.Editor.Core.LLM;
using UnityEditor;
using UnityEngine;

namespace SemanticSearch.Editor.Core.Pipeline
{
    public class PrefabAssetProcessor : IAssetProcessor
    {
        static readonly string[] Extensions = { ".prefab" };
        const int PreviewMaxRetries = 30;
        const int PreviewRetryIntervalMs = 100;

        readonly IVisionClient _vlClient;
        readonly IEmbeddingClient _embeddingClient;

        public string[] SupportedExtensions => Extensions;

        public PrefabAssetProcessor(IVisionClient vlClient, IEmbeddingClient embeddingClient)
        {
            _vlClient = vlClient;
            _embeddingClient = embeddingClient;
        }

        public bool CanProcess(string assetPath)
        {
            var ext = Path.GetExtension(assetPath).ToLowerInvariant();
            return ext == ".prefab";
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

            return preview != null ? preview.EncodeToPNG() : null;
        }

        public async Task<AssetProcessResult> ProcessAsync(string assetPath, CancellationToken ct)
        {
            var imageBytes = GetAssetData(assetPath);
            if (imageBytes == null || imageBytes.Length == 0)
                return AssetProcessResult.Fail($"Failed to get prefab preview: {assetPath}");

            ct.ThrowIfCancellationRequested();

            var prompt = AssetTextBuilder.BuildVisionPrompt(assetPath, "prefab");
            var caption = await _vlClient.RequestCaptionAsync(imageBytes, prompt);
            ct.ThrowIfCancellationRequested();

            var embeddingText = AssetTextBuilder.BuildEmbeddingText(assetPath, caption, "prefab");
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
