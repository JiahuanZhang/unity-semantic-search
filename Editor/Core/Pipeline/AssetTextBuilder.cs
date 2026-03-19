using System.IO;
using System.Text;
using SemanticSearch.Editor.Core.LLM;
using SemanticSearch.Editor.Core.Localization;

namespace SemanticSearch.Editor.Core.Pipeline
{
    static class AssetTextBuilder
    {
        public static string BuildVisionPrompt(string assetPath, string assetType)
        {
            var fileNameHint = BuildFileNameHint(assetPath);
            if (string.IsNullOrEmpty(fileNameHint))
                return VisionConstants.DefaultPrompt;

            return $"{VisionConstants.DefaultPrompt}\n{L10n.VisionPromptContext(assetType, fileNameHint)}";
        }

        public static string BuildEmbeddingText(string assetPath, string caption, string assetType)
        {
            var fileName = BuildFileNameHint(assetPath);
            return $"Asset type: {assetType}, File name: {fileName}. Description: {caption}";
        }

        static string BuildFileNameHint(string assetPath)
        {
            if (string.IsNullOrWhiteSpace(assetPath))
                return string.Empty;

            var name = Path.GetFileNameWithoutExtension(assetPath);
            if (string.IsNullOrWhiteSpace(name))
                return string.Empty;

            return name
                .Replace('_', ' ')
                .Replace('-', ' ')
                .Trim();
        }
    }
}
