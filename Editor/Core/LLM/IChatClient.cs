using System.Threading.Tasks;

namespace SemanticSearch.Editor.Core.LLM
{
    public interface IChatClient
    {
        Task<string> ChatAsync(string systemPrompt, string userMessage);
    }
}
