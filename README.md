# Semantic Search - Unity 语义化资产检索

通过集成 OpenAI 兼容格式的大模型（Vision + Embedding），为 Unity 开发者提供自然语言资产搜索能力。支持多 Provider 配置，可同时管理多个 API 服务。

## 功能

- **自然语言搜索**：在搜索窗口输入描述即可定位资产
- **多 Provider 支持**：可配置多个 LLM 服务商，一键切换
- **OpenAI 兼容格式**：支持任何 OpenAI 格式的 API（阿里 DashScope、OpenAI、Ollama 等）
- **自动索引**：资产导入时自动生成描述与向量（默认关闭）
- **手动扫描**：一键扫描全项目并更新索引
- **配置面板**：通过 `Project Settings > Semantic Search` 管理 Provider、模型选择等
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
- 任意 OpenAI 兼容 API 的 Key（需支持 Vision + Embedding）

## 支持的服务商（示例）

| 服务商 | Base URL | Vision 模型 | Embedding 模型 |
|---|---|---|---|
| 阿里 DashScope | `https://dashscope.aliyuncs.com/compatible-mode/v1` | `qwen-vl-plus` | `text-embedding-v3` |
| OpenAI | `https://api.openai.com/v1` | `gpt-4o` | `text-embedding-3-small` |
| Ollama (本地) | `http://localhost:11434/v1` | `llava` | `nomic-embed-text` |

> 只要 API 遵循 OpenAI 的 `/chat/completions` 和 `/embeddings` 格式即可接入。

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
3. 填入 Base URL、API Key、Vision Model、Embedding Model
4. 使用下拉菜单切换当前活跃的 Provider
5. 如需自动索引，开启 **Auto-Index On Import**（默认关闭）
6. 点击 **Scan & Update** 开始索引
