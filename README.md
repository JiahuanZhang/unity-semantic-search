# Semantic Search - Unity 语义化资产检索

通过集成大模型（Vision + Embedding），为 Unity 开发者提供自然语言资产搜索能力。支持多 Provider 配置，可同时管理多个 API 服务。支持 OpenAI 兼容格式和 Google Gemini 原生 API。

## 功能

- **自然语言搜索**：在搜索窗口输入描述即可定位资产
- **多 Provider 支持**：可配置多个 LLM 服务商，一键切换
- **多 API 格式**：支持 OpenAI 兼容格式（阿里 DashScope、OpenAI、Ollama 等）和 Google Gemini 原生 API
- **自动索引**：资产导入时自动生成描述与向量（默认关闭）
- **手动扫描**：一键扫描全项目并更新索引
- **已索引资源浏览**：通过 `Window > Semantic Search > Asset View` 打开独立窗口，浏览所有已索引资源，支持按路径/描述搜索、按状态过滤、缩略图预览
- **搜索框快速提交**：Search Results 窗口输入后按回车或失去焦点会立即触发搜索
- **选择性重新索引**：在 Asset View 窗口中勾选资源后点击 Re-index 按钮，仅重新处理选中的资源
- **配置面板**：通过 `Project Settings > Semantic Search` 管理 Provider、模型选择等
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

1. 打开 `Edit > Project Settings > Semantic Search`
2. 通过 **+** 按钮添加 Provider，或编辑默认 Provider
3. 选择 **Provider Type**（OpenAI 或 Gemini），切换时会自动填充默认值
4. 填入 Base URL、API Key、Vision Model、Embedding Model
5. 点击 **Test LLM** 测试当前 Provider 连通性（基于 Embedding 请求）
6. 使用下拉菜单切换当前活跃的 Provider
7. 如需自动索引，开启 **Auto-Index On Import**（默认关闭）
8. 点击 **Scan & Update** 开始索引
9. 点击 **Open Asset View** 或通过菜单 `Window > Semantic Search > Asset View` 打开资源浏览窗口
10. 在 Asset View 窗口中可按路径/描述搜索、按状态筛选，勾选资源后点击 **Re-index Selected** 重新生成描述和向量
