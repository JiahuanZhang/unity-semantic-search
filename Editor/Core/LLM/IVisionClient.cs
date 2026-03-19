using System.Threading.Tasks;

namespace SemanticSearch.Editor.Core.LLM
{
    public static class VisionConstants
    {
        public const string DefaultPrompt =
            "Provide a concise, natural language description for this image in both Chinese and English. "
            + "Describe the main subject, art style, key colors, and notable features. "
            + "Output should be one natural sentence for each language. "
            + "Format: '中文描述; English description'. "
            + "Example: 一个金发蓝眼的动漫少女，戴着蝴蝶结发饰，Q版萌系风格。; A blonde-haired, blue-eyed anime girl with a bow accessory in a chibi style.";
    }

    public interface IVisionClient
    {
        Task<string> RequestCaptionAsync(byte[] imageData, string prompt = null);
    }
}
