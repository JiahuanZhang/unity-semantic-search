using System.Threading.Tasks;
using SemanticSearch.Editor.Core.LLM;

namespace SemanticSearch.Editor.Core.Search
{
    public class SearchQueryEnhancer
    {
        const string SystemPrompt =
            "你是语义搜索查询优化器。资源库中的资源以如下格式索引：\n" +
            "\"Asset type: {类型}, File name: {文件名}. Description: {中文描述}; {English description}\"\n\n" +
            "请将用户的搜索关键词优化为更完整、更有利于向量相似度匹配的描述文本。\n\n" +
            "要求：\n" +
            "1. 扩展简短关键词为自然语言描述，包含中文和英文\n" +
            "2. 如果用户未指定资源类型（如图片、预制体等），不要限定类型\n" +
            "3. 如果用户指定了资源类型，在描述开头添加 \"Asset type: {类型}. \"\n" +
            "4. 仅输出优化后的搜索文本，不要解释\n\n" +
            "示例：\n" +
            "输入：动漫头像\n" +
            "输出：Description: 动漫风格的角色头像，二次元卡通人物形象，可能是Q版或日系画风。; " +
            "An anime-style character avatar, 2D cartoon character portrait, possibly in chibi or Japanese art style.\n\n" +
            "输入：红色按钮图片\n" +
            "输出：Asset type: Image. Description: 红色的UI按钮图片，鲜艳的红色调界面按钮元素。; " +
            "A red UI button image, a vibrant red-colored interface button element.";

        readonly IChatClient _chatClient;

        public SearchQueryEnhancer(IChatClient chatClient)
        {
            _chatClient = chatClient;
        }

        public async Task<string> EnhanceAsync(string queryText)
        {
            if (string.IsNullOrWhiteSpace(queryText))
                return queryText;

            var enhanced = await _chatClient.ChatAsync(SystemPrompt, queryText);
            return string.IsNullOrWhiteSpace(enhanced) ? queryText : enhanced;
        }
    }
}
