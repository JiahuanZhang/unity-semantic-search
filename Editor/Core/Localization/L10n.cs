using UnityEngine;

namespace SemanticSearch.Editor.Core.Localization
{
    public enum AppLanguage { English, Chinese, Japanese }

    public static class L10n
    {
        static readonly AppLanguage Lang;

        static L10n()
        {
            Lang = DetectLanguage();
        }

        static AppLanguage DetectLanguage()
        {
            switch (Application.systemLanguage)
            {
                case SystemLanguage.Chinese:
                case SystemLanguage.ChineseSimplified:
                case SystemLanguage.ChineseTraditional:
                    return AppLanguage.Chinese;
                case SystemLanguage.Japanese:
                    return AppLanguage.Japanese;
                default:
                    return AppLanguage.English;
            }
        }

        public static AppLanguage CurrentLanguage => Lang;

        static string S(string en, string zh, string ja) => Lang switch
        {
            AppLanguage.Chinese => zh,
            AppLanguage.Japanese => ja,
            _ => en
        };

        // ===== Common =====
        public static string Cancel => S("Cancel", "取消", "キャンセル");
        public static string Delete => S("Delete", "删除", "削除");
        public static string OK => S("OK", "确定", "OK");
        public static string Confirm => S("Confirm", "确认", "確認");
        public static string Save => S("Save", "保存", "保存");
        public static string Refresh => S("Refresh", "刷新", "更新");

        // ===== Search Result Window =====
        public static string SearchWindowTitle => S("Semantic Search Results", "语义搜索结果", "セマンティック検索結果");
        public static string Enhanced => S("Enhanced", "增强", "拡張");
        public static string EnhancedTooltip => S(
            "Use LLM to optimize search keywords for better semantic matching",
            "使用大模型优化搜索关键词，提高语义匹配准确度",
            "LLMを使用して検索キーワードを最適化し、セマンティックマッチングの精度を向上");
        public static string Search => S("Search", "搜索", "検索");
        public static string EnhancingAndSearching => S("Enhancing & Searching...", "增强搜索中...", "拡張検索中...");
        public static string Searching => S("Searching...", "搜索中...", "検索中...");
        public static string NoResultsFound => S("No results found.", "未找到结果。", "結果が見つかりません。");
        public static string LoadMore => S("Load More", "加载更多", "もっと読み込む");

        public static string FoundResults(int count, float time) =>
            string.Format(S(
                "Found {0} results ({1:F2}s)",
                "找到 {0} 个结果（{1:F2}秒）",
                "{0} 件の結果（{1:F2}秒）"), count, time);

        public static string EnhancedQuery(string query) =>
            string.Format(S("Enhanced: {0}", "增强: {0}", "拡張: {0}"), query);

        // ===== Settings — Admin =====
        public static string AdminMode => S("Admin Mode", "管理员模式", "管理者モード");
        public static string AdminModeDialogTitle => S("Admin Mode", "管理员模式", "管理者モード");
        public static string AdminModeWarning => S(
            "Warning: Admin mode is intended only for developers.\nAre you sure you want to enable it?",
            "警告：管理员模式仅供开发者使用。\n确定要启用吗？",
            "警告：管理者モードは開発者専用です。\n有効にしますか？");
        public static string RoleAdmin => S("Admin", "管理员", "管理者");
        public static string RoleUser => S("User", "用户", "ユーザー");

        // ===== Settings — Role Provider =====
        public static string RoleProviderAssignment => S("Role Provider Assignment", "角色供应商分配", "ロールプロバイダー割り当て");
        public static string AdminProvider => S("Admin Provider", "管理员供应商", "管理者プロバイダー");
        public static string UserProvider => S("User Provider", "用户供应商", "ユーザープロバイダー");

        public static string CurrentRoleInfo(string role, string provider) =>
            string.Format(S(
                "Current role: {0}, using provider: {1}",
                "当前角色: {0}，使用供应商: {1}",
                "現在のロール: {0}、プロバイダー: {1}"), role, provider);

        // ===== Settings — LLM Configuration =====
        public static string LLMConfiguration => S("LLM Configuration", "LLM 配置", "LLM 設定");
        public static string ActiveProvider => S("Active Provider", "当前供应商", "アクティブプロバイダー");
        public static string ProviderLabel => S("Provider", "供应商", "プロバイダー");
        public static string DeleteProviderTitle => S("Delete Provider", "删除供应商", "プロバイダーを削除");

        public static string DeleteProviderMessage(string name) =>
            string.Format(S(
                "Delete \"{0}\"?",
                "删除 \"{0}\"？",
                "\"{0}\" を削除しますか？"), name);

        public static string NewProviderName(int index) =>
            string.Format(S("Provider {0}", "供应商 {0}", "プロバイダー {0}"), index);

        public static string ProviderName => S("Provider Name", "供应商名称", "プロバイダー名");
        public static string ProviderType => S("Provider Type", "供应商类型", "プロバイダータイプ");
        public static string ApiKey => S("API Key", "API Key", "API Key");
        public static string SaveApiKeyTooltip => S(
            "Save API Key to Settings.json when checked",
            "勾选后会将 API Key 写入 Settings.json",
            "チェックするとAPI KeyをSettings.jsonに保存します");
        public static string BaseUrlGemini => S("Base URL (Gemini API)", "Base URL (Gemini API)", "Base URL (Gemini API)");
        public static string BaseUrlOpenAI => S("Base URL (OpenAI-compatible)", "Base URL (OpenAI 兼容)", "Base URL (OpenAI互換)");
        public static string VisionModel => S("Vision Model", "视觉模型", "ビジョンモデル");
        public static string EmbeddingModel => S("Embedding Model", "嵌入模型", "埋め込みモデル");

        // ===== Settings — LLM Test =====
        public static string TestingBtn => S("Testing...", "测试中...", "テスト中...");
        public static string TestLLM => S("Test LLM", "测试 LLM", "LLM テスト");
        public static string TestingProvider => S("Testing current provider...", "正在测试当前供应商...", "現在のプロバイダーをテスト中...");
        public static string ApiKeyEmpty => S("API Key is empty.", "API Key 为空。", "API Keyが空です。");
        public static string BaseUrlEmpty => S("Base URL is empty.", "Base URL 为空。", "Base URLが空です。");
        public static string EmbeddingModelEmpty => S("Embedding Model is empty.", "嵌入模型为空。", "埋め込みモデルが空です。");

        public static string LLMAvailable(int dims) =>
            string.Format(S(
                "LLM is available. Embedding dims: {0}.",
                "LLM 可用。嵌入维度: {0}。",
                "LLM 利用可能。埋め込み次元: {0}。"), dims);

        public static string LLMTestFailed(string error) =>
            string.Format(S(
                "LLM test failed: {0}",
                "LLM 测试失败: {0}",
                "LLM テスト失敗: {0}"), error);

        // ===== Settings — Workflow =====
        public static string WorkflowControl => S("Workflow Control", "工作流控制", "ワークフロー制御");
        public static string AutoIndexOnImport => S("Auto-Index On Import", "导入时自动索引", "インポート時に自動インデックス");
        public static string MaxConcurrentRequests => S("Max Concurrent Requests", "最大并发请求数", "最大同時リクエスト数");

        // ===== Settings — Asset Filter =====
        public static string AssetFilterRules => S("Asset Filter Rules", "资产过滤规则", "アセットフィルタールール");
        public static string IncludeRules => S(
            "Include Rules (match at least one)",
            "包含规则（至少匹配一个）",
            "包含ルール（少なくとも1つ一致）");
        public static string ExcludeRules => S(
            "Exclude Rules (excluded if matched)",
            "排除规则（匹配则排除）",
            "除外ルール（一致すると除外）");
        public static string GlobPatternHint => S(
            "Glob patterns: ** (recursive match), * (single level)\nExamples: Assets/UI/**, *.png, Assets/Textures/Icons/*",
            "Glob 模式: **（递归匹配）, *（单层匹配）\n示例: Assets/UI/**, *.png, Assets/Textures/Icons/*",
            "Glob パターン: **（再帰マッチ）, *（単一レベル）\n例: Assets/UI/**, *.png, Assets/Textures/Icons/*");
        public static string AddRule => S("+ Add Rule", "+ 添加规则", "+ ルール追加");

        // ===== Settings — Database Maintenance =====
        public static string DatabaseMaintenance => S("Database Maintenance", "数据库维护", "データベースメンテナンス");
        public static string IndexedAssets => S("Indexed Assets", "已索引资产", "インデックス済みアセット");
        public static string PendingAssets => S("Pending Assets", "待索引资产", "保留中アセット");
        public static string ScanAndUpdate => S("Scan & Update", "扫描并更新", "スキャン＆更新");
        public static string ClearDatabase => S("Clear Database", "清空数据库", "データベースをクリア");
        public static string OpenDatabaseFolder => S("Open Database Folder", "打开数据库文件夹", "データベースフォルダーを開く");
        public static string ScanningAssets => S("Scanning assets...", "扫描资产中...", "アセットをスキャン中...");

        public static string ScanningProgress(string progress) =>
            string.Format(S("Scanning... {0}", "扫描中... {0}", "スキャン中... {0}"), progress);

        public static string IndexingAssets(int count) =>
            string.Format(S(
                "Indexing {0} assets...",
                "索引 {0} 个资产...",
                "{0} アセットをインデックス中..."), count);

        public static string IndexingProgress(int completed, int total, string current) =>
            string.Format(S(
                "Indexing {0}/{1} — {2}",
                "索引 {0}/{1} — {2}",
                "インデックス {0}/{1} — {2}"), completed, total, current);

        public static string ScanIndexDone(double scanTime, double indexTime, double throughput) =>
            string.Format(S(
                "Done. Scan {0:F2}s, Index {1:F2}s ({2:F2} assets/s).",
                "完成。扫描 {0:F2}秒，索引 {1:F2}秒（{2:F2} 资产/秒）。",
                "完了。スキャン {0:F2}秒、インデックス {1:F2}秒（{2:F2} アセット/秒）。"),
                scanTime, indexTime, throughput);

        public static string Cancelled => S("Cancelled.", "已取消。", "キャンセルされました。");

        public static string ErrorMessage(string msg) =>
            string.Format(S("Error: {0}", "错误: {0}", "エラー: {0}"), msg);

        public static string ClearDatabaseMessage => S(
            "This will delete ALL indexed data. Are you sure?",
            "这将删除所有已索引的数据。确定吗？",
            "すべてのインデックスデータが削除されます。よろしいですか？");
        public static string DeleteAll => S("Delete All", "全部删除", "すべて削除");
        public static string Clearing => S("Clearing...", "清空中...", "クリア中...");
        public static string DatabaseCleared => S("Database cleared.", "数据库已清空。", "データベースがクリアされました。");

        public static string ClearFailed(string error) =>
            string.Format(S("Clear failed: {0}", "清空失败: {0}", "クリア失敗: {0}"), error);

        public static string OpenAssetView => S("Open Asset View", "打开资产视图", "アセットビューを開く");
        public static string OpenAssetViewHint => S(
            "Open Asset browsing Window via: Window > Semantic Search > Asset View",
            "通过 Window > Semantic Search > Asset View 打开资产浏览窗口",
            "Window > Semantic Search > Asset View からアセットブラウジングウィンドウを開く");
        public static string SemanticSearch => S("Semantic Search", "语义搜索", "セマンティック検索");
        public static string DatabaseFolderNotExist => S(
            "Database folder does not exist yet.",
            "数据库文件夹尚不存在。",
            "データベースフォルダーはまだ存在しません。");

        // ===== Asset View Window =====
        public static string AssetViewTitle => S("Semantic Asset View", "语义资产视图", "セマンティックアセットビュー");
        public static string StatusAll => S("All", "全部", "すべて");
        public static string StatusIndexed => S("Indexed", "已索引", "インデックス済み");
        public static string StatusPending => S("Pending", "待处理", "保留中");
        public static string StatusError => S("Error", "错误", "エラー");

        public static string LocalizeStatus(string status)
        {
            switch (status)
            {
                case "Indexed": return StatusIndexed;
                case "Pending": return StatusPending;
                case "Error": return StatusError;
                default: return status;
            }
        }

        public static string AssetViewStatusBar(int total, int showing, int filtered, int selected) =>
            string.Format(S(
                "Total: {0}  |  Showing {1}/{2}  |  Selected: {3}",
                "总计: {0}  |  显示 {1}/{2}  |  已选: {3}",
                "合計: {0}  |  表示 {1}/{2}  |  選択: {3}"),
                total, showing, filtered, selected);

        public static string SelectAllVisible => S("Select All Visible", "全选可见", "表示中をすべて選択");
        public static string ClearSelection => S("Clear Selection", "清除选择", "選択をクリア");

        public static string LoadMoreRemaining(int remaining) =>
            string.Format(S(
                "Load More ({0} remaining)",
                "加载更多（剩余 {0}）",
                "もっと読み込む（残り {0}）"), remaining);

        public static string ReindexSelectedBtn(int count) =>
            string.Format(S(
                "Re-index Selected ({0})",
                "重新索引所选 ({0})",
                "選択を再インデックス ({0})"), count);

        public static string DeleteSelectedBtn(int count) =>
            string.Format(S(
                "Delete Selected ({0})",
                "删除所选 ({0})",
                "選択を削除 ({0})"), count);

        public static string Loading => S("Loading...", "加载中...", "読み込み中...");

        public static string LoadFailed(string error) =>
            string.Format(S("Load failed: {0}", "加载失败: {0}", "読み込み失敗: {0}"), error);

        public static string ReindexingAssets(int count) =>
            string.Format(S(
                "Re-indexing {0} assets...",
                "重新索引 {0} 个资产...",
                "{0} アセットを再インデックス中..."), count);

        public static string ReindexingProgress(int completed, int total, string current) =>
            string.Format(S(
                "Re-indexing {0}/{1} — {2}",
                "重新索引 {0}/{1} — {2}",
                "再インデックス {0}/{1} — {2}"), completed, total, current);

        public static string ReindexDone => S("Re-index done.", "重新索引完成。", "再インデックス完了。");
        public static string ReindexCancelled => S("Re-index cancelled.", "重新索引已取消。", "再インデックスがキャンセルされました。");

        public static string DeletingRecords(int count) =>
            string.Format(S(
                "Deleting {0} records...",
                "删除 {0} 条记录...",
                "{0} レコードを削除中..."), count);

        public static string DeletedRecords(int count) =>
            string.Format(S(
                "Deleted {0} records.",
                "已删除 {0} 条记录。",
                "{0} レコードを削除しました。"), count);

        public static string DeleteFailed(string error) =>
            string.Format(S("Delete failed: {0}", "删除失败: {0}", "削除失敗: {0}"), error);

        public static string DeleteSelectedTitle => S("Delete Selected", "删除所选", "選択を削除");

        public static string DeleteSelectedMessage(int count) =>
            string.Format(S(
                "Delete {0} selected records from database?",
                "从数据库删除 {0} 条选中的记录？",
                "データベースから選択した {0} レコードを削除しますか？"), count);

        // ===== Asset Detail Popup =====
        public static string AssetDetailTitle => S("Asset Detail", "资产详情", "アセット詳細");
        public static string NoRecord => S("No record.", "无记录。", "レコードがありません。");
        public static string FieldGUID => "GUID";
        public static string FieldPath => S("Path", "路径", "パス");
        public static string FieldStatus => S("Status", "状态", "ステータス");
        public static string FieldMD5 => "MD5";
        public static string FieldVectorDim => S("Vector Dim", "向量维度", "ベクトル次元");
        public static string FieldUpdatedAt => S("Updated At", "更新时间", "更新日時");
        public static string FieldCaption => S("Caption", "描述", "キャプション");
        public static string None => S("(none)", "（无）", "（なし）");

        // ===== Console Search Injector =====
        public static string SemanticSearchShortcut => S(
            "Semantic Search (Shift+Alt+F)",
            "语义搜索 (Shift+Alt+F)",
            "セマンティック検索 (Shift+Alt+F)");

        // ===== Asset Context Menu =====
        public static string LabelIndexing => S("Indexing", "索引", "インデックス");
        public static string LabelReindexing => S("Re-indexing", "重新索引", "再インデックス");

        public static string ProgressBarTitle(string label) =>
            string.Format(S(
                "Semantic Search — {0}",
                "语义搜索 — {0}",
                "セマンティック検索 — {0}"), label);

        public static string ScanningSelectedAssets => S(
            "Scanning selected assets...",
            "扫描所选资产...",
            "選択したアセットをスキャン中...");

        // ===== LLM Prompts — Vision =====
        public static string VisionDefaultPrompt => S(
            "Provide a concise, natural language description for this image in English. "
            + "Describe the main subject, art style, key colors, and notable features. "
            + "Output should be one natural sentence. "
            + "Example: A blonde-haired, blue-eyed anime girl with a bow accessory in a chibi style.",

            "Provide a concise, natural language description for this image in both Chinese and English. "
            + "Describe the main subject, art style, key colors, and notable features. "
            + "Output should be one natural sentence for each language. "
            + "Format: '中文描述; English description'. "
            + "Example: 一个金发蓝眼的动漫少女，戴着蝴蝶结发饰，Q版萌系风格。; "
            + "A blonde-haired, blue-eyed anime girl with a bow accessory in a chibi style.",

            "Provide a concise, natural language description for this image in both Japanese and English. "
            + "Describe the main subject, art style, key colors, and notable features. "
            + "Output should be one natural sentence for each language. "
            + "Format: '日本語説明; English description'. "
            + "Example: 金髪碧眼のアニメ少女、リボンのヘアアクセサリー付き、チビスタイル。; "
            + "A blonde-haired, blue-eyed anime girl with a bow accessory in a chibi style.");

        public static string VisionPromptContext(string assetType, string fileNameHint)
        {
            var hint = S(
                "Treat this as auxiliary context and prioritize visual evidence.",
                "将此作为辅助上下文，优先以视觉内容为准。",
                "これを補助コンテキストとして扱い、視覚的証拠を優先してください。");
            return $"Asset type: {assetType}. File name hint: {fileNameHint}. {hint}";
        }

        // ===== LLM Prompts — Search Query Enhancer =====
        public static string SearchEnhancerSystemPrompt => S(
            "You are a semantic search query optimizer. Assets in the resource library are indexed in this format:\n"
            + "\"Asset type: {type}, File name: {filename}. Description: {English description}\"\n\n"
            + "Optimize the user's search keywords into a natural search sentence for vector similarity matching.\n\n"
            + "Requirements:\n"
            + "1. Expand short keywords into a natural language search sentence in English\n"
            + "2. If the user doesn't specify an asset type, output: \"Find an asset described as: {expanded description}\"\n"
            + "3. If the user specifies an asset type, output: \"Find a {type} described as: {expanded description}\"\n"
            + "4. Output only the optimized search text, no explanations\n\n"
            + "Examples:\n"
            + "Input: anime avatar\n"
            + "Output: Find an asset described as: an anime-style character avatar, 2D cartoon character portrait, possibly in chibi or Japanese art style.\n\n"
            + "Input: red button image\n"
            + "Output: Find an Image described as: a red UI button, a vibrant red-colored interface button element.",

            "你是语义搜索查询优化器。资源库中的资源以如下格式索引：\n"
            + "\"Asset type: {类型}, File name: {文件名}. Description: {中文描述}\"\n\n"
            + "请将用户的搜索关键词优化为自然语言查找句式，更有利于向量相似度匹配。\n\n"
            + "要求：\n"
            + "1. 扩展简短关键词为中文自然语言描述\n"
            + "2. 如果用户未指定资源类型，输出格式为：\"查找包含以下内容的资源：{扩展描述}\"\n"
            + "3. 如果用户指定了资源类型，输出格式为：\"查找包含以下内容的{类型}：{扩展描述}\"\n"
            + "4. 仅输出优化后的搜索文本，不要解释\n\n"
            + "示例：\n"
            + "输入：动漫头像\n"
            + "输出：查找包含以下内容的资源：动漫风格的角色头像，二次元卡通人物形象，可能是Q版或日系画风。\n\n"
            + "输入：红色按钮图片\n"
            + "输出：查找包含以下内容的图片：红色的UI按钮，鲜艳的红色调界面按钮元素。",

            "あなたはセマンティック検索クエリオプティマイザーです。リソースライブラリのアセットは以下の形式でインデックスされています：\n"
            + "\"Asset type: {タイプ}, File name: {ファイル名}. Description: {日本語説明}\"\n\n"
            + "ユーザーの検索キーワードを、ベクトル類似度マッチングに適した自然言語の検索文に最適化してください。\n\n"
            + "要件：\n"
            + "1. 短いキーワードを日本語の自然言語説明に拡張する\n"
            + "2. ユーザーがアセットタイプを指定していない場合、出力形式：\"以下の内容を含むアセットを検索：{拡張説明}\"\n"
            + "3. ユーザーがアセットタイプを指定した場合、出力形式：\"以下の内容を含む{タイプ}を検索：{拡張説明}\"\n"
            + "4. 最適化された検索テキストのみを出力し、説明は不要\n\n"
            + "例：\n"
            + "入力：アニメアバター\n"
            + "出力：以下の内容を含むアセットを検索：アニメスタイルのキャラクターアバター、2D漫画キャラクターの肖像画、チビまたは日本のアートスタイル。\n\n"
            + "入力：赤いボタン画像\n"
            + "出力：以下の内容を含む画像を検索：赤いUIボタン、鮮やかな赤色のインターフェースボタン要素。");
    }
}
