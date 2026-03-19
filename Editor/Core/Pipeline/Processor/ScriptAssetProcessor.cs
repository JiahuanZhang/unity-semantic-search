using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using SemanticSearch.Editor.Core.LLM;

namespace SemanticSearch.Editor.Core.Pipeline
{
    public class ScriptAssetProcessor : IAssetProcessor
    {
        static readonly string[] Extensions = { ".cs" };
        const int MaxCaptionLength = 2000;

        static readonly Regex NamespaceRegex = new Regex(
            @"^\s*namespace\s+([\w.]+)", RegexOptions.Multiline | RegexOptions.Compiled);

        static readonly Regex TypeDeclRegex = new Regex(
            @"^\s*(?:public\s+|internal\s+|private\s+|protected\s+)?(?:abstract\s+|sealed\s+|static\s+|partial\s+)*" +
            @"(class|struct|interface|enum)\s+(\w+)",
            RegexOptions.Multiline | RegexOptions.Compiled);

        static readonly Regex PublicMemberRegex = new Regex(
            @"^\s*public\s+(?!class\b|struct\b|interface\b|enum\b)[\w<>\[\],\s]+\s+(\w+)\s*[\({]",
            RegexOptions.Multiline | RegexOptions.Compiled);

        static readonly Regex SummaryRegex = new Regex(
            @"///\s*<summary>\s*(.*?)\s*</summary>",
            RegexOptions.Singleline | RegexOptions.Compiled);

        readonly IEmbeddingClient _embeddingClient;

        public AssetKind Kind => AssetKind.Text;
        public string[] SupportedExtensions => Extensions;

        public ScriptAssetProcessor(IEmbeddingClient embeddingClient)
        {
            _embeddingClient = embeddingClient;
        }

        public bool CanProcess(string assetPath)
        {
            var ext = Path.GetExtension(assetPath).ToLowerInvariant();
            return ext == ".cs";
        }

        public byte[] GetAssetData(string assetPath) => null;

        public async Task<AssetProcessResult> ProcessAsync(string assetPath, CancellationToken ct)
        {
            var fullPath = Path.GetFullPath(assetPath);
            if (!File.Exists(fullPath))
                return AssetProcessResult.Fail($"Script file not found: {assetPath}");

            var source = File.ReadAllText(fullPath, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(source))
                return AssetProcessResult.Fail($"Script file is empty: {assetPath}");

            ct.ThrowIfCancellationRequested();

            var caption = ExtractSummary(source);

            var embeddingText = AssetTextBuilder.BuildEmbeddingText(assetPath, caption, "script");
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

        static string ExtractSummary(string source)
        {
            var sb = new StringBuilder();

            var nsMatch = NamespaceRegex.Match(source);
            if (nsMatch.Success)
                sb.Append("Namespace: ").AppendLine(nsMatch.Groups[1].Value);

            foreach (Match m in TypeDeclRegex.Matches(source))
                sb.Append(m.Groups[1].Value).Append(": ").AppendLine(m.Groups[2].Value);

            foreach (Match m in PublicMemberRegex.Matches(source))
                sb.Append("Method: ").AppendLine(m.Groups[1].Value);

            foreach (Match m in SummaryRegex.Matches(source))
            {
                var text = m.Groups[1].Value.Trim();
                text = Regex.Replace(text, @"///\s*", " ").Trim();
                if (text.Length > 0)
                    sb.AppendLine(text);
            }

            if (sb.Length == 0)
                sb.Append("C# script");

            if (sb.Length > MaxCaptionLength)
                sb.Length = MaxCaptionLength;

            return sb.ToString().Trim();
        }
    }
}
