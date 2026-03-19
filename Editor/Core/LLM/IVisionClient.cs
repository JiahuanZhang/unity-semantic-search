using System.Threading.Tasks;
using SemanticSearch.Editor.Core.Localization;

namespace SemanticSearch.Editor.Core.LLM
{
    public static class VisionConstants
    {
        public static string DefaultPrompt => L10n.VisionDefaultPrompt;
    }

    public interface IVisionClient
    {
        Task<string> RequestCaptionAsync(byte[] imageData, string prompt = null);
    }
}
