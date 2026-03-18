# Semantic Search - Unity 语义化资产检索

通过集成大模型（Vision + Embedding），为 Unity 开发者提供自然语言资产搜索能力。支持多 Provider 配置，可同时管理多个 API 服务。支持 OpenAI 兼容格式和 Google Gemini 原生 API。

## 功能

- **自然语言搜索**：在搜索窗口输入描述即可定位资产
- **多 Provider 支持**：可配置多个 LLM 服务商，一键切换
- **管理员模式**：区分管理员与普通用户角色，各自绑定独立的 LLM Provider；管理员可见 Workflow Control 和 Database Maintenance 面板
- **多 API 格式**：支持 OpenAI 兼容格式（阿里 DashScope、OpenAI、Ollama 等）和 Google Gemini 原生 API
- **资产筛选规则**：支持 Include/Exclude Glob 模式，精确控制哪些资产参与索引（Admin 模式下配置）
- **自动索引**：资产导入时自动入库并触发索引（默认关闭）
- **手动扫描**：一键扫描全项目并更新索引
- **已索引资源浏览**：通过 `Window > Semantic Search > Asset View` 打开独立窗口，浏览所有已索引资源，支持按路径/描述搜索、按状态过滤、缩略图预览
- **搜索框快速提交**：Search Results 窗口输入后按回车或失去焦点会立即触发搜索
- **选择性重新索引**：在 Asset View 窗口中勾选资源后点击 Re-index 按钮，仅重新处理选中的资源
- **配置面板**：通过 `Project Settings > Semantic Search` 或 `Window > Semantic Search > Settings` 管理 Provider、模型选择等
- **LLM 连通性测试**：在配置面板点击 **Test LLM**，快速验证当前 Provider 配置是否可用
- **团队共享**：embed 数据库入库，团队成员无需重复处理

## 安装

### 方式一：本地路径引用

在项目的 `Packages/manifest.json` 中添加：

```json
{
  "dependencies": {
    "com.vectorz.semantic-search": "file:../../Terminal/Semantic Search"
  }
}
```

### 方式二：Git URL（如已推送至仓库）

```json
{
  "dependencies": {
    "com.vectorz.semantic-search": "https://your-repo-url.git?path=Semantic Search"
  }
}
```

## 要求

- Unity 2021.3+
- 支持的 API Key（需支持 Vision + Embedding）

## 支持的服务商（示例）

### OpenAI 兼容格式（Provider Type: OpenAI）

| 服务商 | Base URL | Vision 模型 | Embedding 模型 |
|---|---|---|---|
| 阿里 DashScope | `https://dashscope.aliyuncs.com/compatible-mode/v1` | `qwen-vl-plus` | `text-embedding-v3` |
| OpenAI | `https://api.openai.com/v1` | `gpt-4o` | `text-embedding-3-small` |
| Ollama (本地) | `http://localhost:11434/v1` | `llava` | `nomic-embed-text` |

> 只要 API 遵循 OpenAI 的 `/chat/completions` 和 `/embeddings` 格式即可接入。

### Google Gemini（Provider Type: Gemini）

| 服务商 | Base URL | Vision 模型 | Embedding 模型 |
|---|---|---|---|
| Google Gemini | `https://generativelanguage.googleapis.com/v1beta` | `gemini-2.0-flash` | `gemini-embedding-001` |

> 使用 Gemini 原生 API（`generateContent` + `embedContent`），鉴权方式为 `x-goog-api-key` header。

## 数据存储

| 内容 | 路径 | 是否入库 |
|---|---|---|
| 用户配置（Provider 列表、端点等） | `UserSettings/SemanticSearch/Settings.json` | 否 |
| API Key | 本机 EditorPrefs | 否 |
| Embed 索引数据库 | `ProjectSettings/SemanticSearch/Index.db` | **是** |

> 不是所有团队成员都有 API Key 权限，因此配置信息不入库。已处理的 embed 数据存入 `ProjectSettings/` 目录，团队成员拉取后即可直接使用语义搜索，无需重新索引。

## 配置

1. 打开 `Edit > Project Settings > Semantic Search`，或通过 `Window > Semantic Search > Settings`
2. 勾选 **Admin Mode** 进入管理员模式（可查看 Workflow Control 和 Database Maintenance）
3. 通过 **+** 按钮添加 Provider，或编辑默认 Provider
4. 选择 **Provider Type**（OpenAI 或 Gemini），切换时会自动填充默认值
5. 填入 Base URL、API Key、Vision Model、Embedding Model
6. 点击 **Test LLM** 测试当前 Provider 连通性（基于 Embedding 请求）
7. 在 **Role Provider Assignment** 中为管理员和普通用户分别指定使用的 Provider
8. 如需自动索引，开启 **Auto-Index On Import**（默认关闭，开启后新导入资产会自动从 Pending 进入 Indexed）
9. 在 **Asset Filter Rules** 中配置包含/排除规则，控制哪些资产参与 Embedding：
   - **Include Rules**：资产路径须匹配至少一条规则才会被索引，默认 `Assets/**`（全部包含）
   - **Exclude Rules**：匹配的资产将被排除（优先级高于 Include）
   - 支持 Glob 模式：`**`（递归匹配）、`*`（单层匹配），例如 `Assets/UI/**`、`*.png`、`Assets/Temp/**`
10. 点击 **Scan & Update** 开始索引
9. 点击 **Open Asset View** 或通过菜单 `Window > Semantic Search > Asset View` 打开资源浏览窗口
10. 在 Asset View 窗口中可按路径/描述搜索、按状态筛选，勾选资源后点击 **Re-index Selected** 重新生成描述和向量

## 万图场景性能优化（10k+ 资产）

本版本针对大规模图片索引与检索做了专项优化，重点降低内存峰值、数据库事务开销与查询延迟。

### 已落地优化

- **索引并发模型优化**：从“全量任务一次性展开”改为受控 worker 队列，避免 10k 任务对象同时创建导致的调度与 GC 压力。
- **批量写库**：索引与扫描都支持批量 `Upsert`，减少单条事务提交开销。
- **搜索链路提速**：向量数据增加内存缓存 + 快照签名失效机制；TopK 采用部分选择，减少全量排序成本。
- **数据库查询优化**：新增 `status`、`asset_path`、`updated_at` 等索引；资产列表查询支持轻量字段与分页读取。
- **UI 负载优化**：Asset View 过滤结果缓存；Asset View / Search Result 缩略图缓存加入上限与回收策略。
- **MD5 扫描优化**：文件哈希增加元数据缓存（长度 + 修改时间），减少重复全文件读取。

### 性能观测

点击 `Scan & Update` 后，Console 会输出扫描与索引性能日志，例如：

- 扫描耗时（`scanTime`）
- 索引耗时（`indexTime`）
- 索引吞吐（assets/s）
- 批处理峰值托管内存（`peakManaged`）
- 搜索耗时与查询内存变化（`Search perf` 日志）

### 万图推荐参数

- **Max Concurrent Requests**：建议 `2~4`（先小并发压测，再逐步调高）
- **执行方式**：优先分目录分批次 `Scan & Update`，避免首次一次性覆盖全项目
- **素材管理**：控制超大图比例，优先压缩纹理再做索引
- **运行时机**：尽量在编辑器空闲时执行大批量索引任务

## 架构：资源处理器（Asset Processor）

索引流水线采用**处理器模式**，将不同类型资源的处理逻辑分离，便于后续扩展新资源类型（如脚本、材质等）。

| 组件 | 职责 |
|---|---|
| `IAssetProcessor` | 处理器接口，定义 `CanProcess` / `GetAssetData` / `ProcessAsync` |
| `ImageAssetProcessor` | 处理图片资源（.png/.jpg/.jpeg/.tga），直接读取文件字节 |
| `PrefabAssetProcessor` | 处理预制体资源（.prefab），通过 AssetPreview 生成预览图 |
| `AssetProcessorRegistry` | 注册并管理所有处理器，根据文件扩展名自动路由 |
| `IndexPipeline` | 索引流水线，通过 Registry 获取对应处理器执行索引 |

### 向量化文本增强（文件名上下文）

为降低视觉识别误差，图片与预制体在索引时会将**文件名**作为辅助上下文注入：

- Vision 阶段：在提示词中追加文件名提示（辅助信息，不覆盖视觉内容）
- Embedding 阶段：将 `asset_type + file_name + caption` 组合后再生成向量

这样可提升命名语义较强资源（如角色、道具、UI 组件）在检索中的稳定性与召回率。

扩展新资源类型只需：
1. 实现 `IAssetProcessor` 接口
2. 在 `AssetProcessorRegistry` 构造函数中注册
3. 在 `AssetScanner.ExtensionToAssetType` 中添加扩展名映射

## 最近修复

- 预制体条目在 Asset View 和 Search Results 中默认显示 3D 缩略图（`AssetPreview.GetAssetPreview`），异步加载期间先显示类型图标再自动刷新，性能无阻塞。
- `Semantic Search Results` 窗口中，匹配度百分比调整为显示在文件名同一行右侧，结果信息更易扫读。
- 修复 `Semantic Search Results` 窗口保持打开时，`Open Asset View` 偶发打开慢且列表为空的问题。
- 调整搜索窗口数据库连接策略为“每次搜索按需打开并在结束后释放”，减少窗口之间的连接竞争。
- `Asset View` 刷新失败时不再静默显示空列表，会在窗口内显示失败信息并输出错误日志，便于定位问题。
- 优化万图场景索引链路：受控并发队列 + 批量写库 + 性能指标日志，降低峰值内存并提升吞吐。
- 优化万图场景搜索链路：向量缓存 + TopK 部分选择 + 轻量元数据回填，降低查询延迟。
- 优化万图场景扫描与 UI：MD5 缓存、数据库索引、列表过滤与缩略图缓存回收，提升交互稳定性。
