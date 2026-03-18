using System.Collections.Generic;
using System.Threading.Tasks;

namespace SemanticSearch.Editor.Core.LLM
{
    public interface IEmbeddingClient
    {
        Task<float[]> RequestEmbeddingAsync(string text);
        Task<List<float[]>> RequestEmbeddingBatchAsync(List<string> texts);
    }
}
