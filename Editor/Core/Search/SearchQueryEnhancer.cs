using System.Threading.Tasks;
using SemanticSearch.Editor.Core.LLM;
using SemanticSearch.Editor.UI;

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

            var prompt = SemanticSearchSettings.Instance.GetEffectiveSearchEnhancerPrompt();
            var enhanced = await _chatClient.ChatAsync(prompt, queryText);
            return string.IsNullOrWhiteSpace(enhanced) ? queryText : enhanced;
        }
    }
}
