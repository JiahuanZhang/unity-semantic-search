using System.Threading.Tasks;
using SemanticSearch.Editor.Core.LLM;
using SemanticSearch.Editor.Core.Localization;

namespace SemanticSearch.Editor.Core.Search
{
    public class SearchQueryEnhancer
    {
        readonly IChatClient _chatClient;

        public SearchQueryEnhancer(IChatClient chatClient)
        {
            _chatClient = chatClient;
        }

        public async Task<string> EnhanceAsync(string queryText)
        {
            if (string.IsNullOrWhiteSpace(queryText))
                return queryText;

            var enhanced = await _chatClient.ChatAsync(L10n.SearchEnhancerSystemPrompt, queryText);
            return string.IsNullOrWhiteSpace(enhanced) ? queryText : enhanced;
        }
    }
}
