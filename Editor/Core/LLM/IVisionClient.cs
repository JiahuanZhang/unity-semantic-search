using System.Threading.Tasks;
using SemanticSearch.Editor.UI;

namespace SemanticSearch.Editor.Core.LLM
{
    public static class VisionConstants
    {
        public static string DefaultPrompt => SemanticSearchSettings.Instance.GetEffectiveVisionPrompt();
    }

    public interface IVisionClient
    {
        Task<string> RequestCaptionAsync(byte[] imageData, string prompt = null);
    }
}
