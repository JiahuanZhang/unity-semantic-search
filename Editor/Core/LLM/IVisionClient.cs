using System.Threading.Tasks;

namespace SemanticSearch.Editor.Core.LLM
{
    public static class VisionConstants
    {
        public const string DefaultPrompt =
            "Extract keywords/tags for this image in both Chinese and English. "
            + "Cover: subject,art style,key colors,notable features. "
            + "Output ONLY comma-separated keywords, no sentences. "
            + "Format: '关键词1、关键词2；keyword1, keyword2'. Max 10 keywords per language. "
            + "Example: 动漫少女、金发、蓝瞳、蝴蝶结发饰、Q版萌系;anime girl, blonde hair, blue eyes, bow accessory, chibi style";
    }

    public interface IVisionClient
    {
        Task<string> RequestCaptionAsync(byte[] imageData, string prompt = null);
    }
}
