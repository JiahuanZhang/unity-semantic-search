using System.Threading;
using System.Threading.Tasks;

namespace SemanticSearch.Editor.Core.Pipeline
{
    public class AssetProcessResult
    {
        public string Caption { get; set; }
        public float[] Vector { get; set; }
        public bool Success { get; set; }
        public string ErrorMessage { get; set; }

        public static AssetProcessResult Fail(string error) =>
            new AssetProcessResult { Success = false, ErrorMessage = error };
    }

    public interface IAssetProcessor
    {
        string[] SupportedExtensions { get; }

        bool CanProcess(string assetPath);

        byte[] GetAssetData(string assetPath);

        Task<AssetProcessResult> ProcessAsync(string assetPath, CancellationToken ct);
    }
}
