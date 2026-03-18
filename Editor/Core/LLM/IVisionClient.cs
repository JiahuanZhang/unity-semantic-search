using System.Threading.Tasks;

namespace SemanticSearch.Editor.Core.LLM
{
    public interface IVisionClient
    {
        Task<string> RequestCaptionAsync(byte[] imageData, string prompt = null);
    }
}
