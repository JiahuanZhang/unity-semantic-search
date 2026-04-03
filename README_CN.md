# Semantic Search - Unity 语义化资产检索

[English](README.md) | [环境要求](#环境要求) | [安装方法](#安装方法) | [支持的服务商](#支持的服务商)

集成大模型（Vision + Embedding），为 Unity 开发者提供自然语言资产搜索能力。支持多服务商配置，可同时管理多个 API 服务。

## 🌟 核心特性

- **自然语言搜索**: 直接输入描述（如“红色按钮”、“写实风格角色头像”）即可定位资产。
- **智能索引**: 
  - **视觉识别**: 通过视觉模型描述图片、预制体、模型及材质。
  - **上下文注入**: 将文件名作为辅助信息注入索引，大幅提升检索的稳定性和召回率。
- **管理员模式**: 专为开发者设计的面板。启用后可自定义提示词（Prompt）、控制自动索引工作流、设置过滤规则以及维护数据库。
- **增强搜索**: 开启 Enhanced 后，LLM 会将搜索关键词优化为更完整的中英文描述，从而提高向量匹配的准确度。
- **团队友好**: 向量数据库存储在 `ProjectSettings/`，支持版本管理，团队成员拉取后即可直接使用，无需重新索引。

## 🛠 安装与配置

### 环境要求
- Unity 2021.3+
- 具备 **Vision + Embedding** 能力的 API Key（如 GPT-4o + Text-Embedding-3, Gemini 1.5/2.0 Flash）。

### 安装方法
在项目的 `Packages/manifest.json` 中添加：
```json
"com.vectorz.semantic-search": "https://github.com/JiahuanZhang/unity-semantic-search.git"
```

### 快速开始
1. **配置**: `Window > Semantic Search > Settings`，添加 Provider 并填写 API Key。
2. **启用 Admin**: 在设置中勾选 **Admin Mode** 以解锁索引与维护工具。
3. **扫描**: 在 **Database** 面板点击 **Scan & Update** 建立初始索引。
4. **搜索**: 通过 `Window > Semantic Search > Asset View` 浏览，或在搜索窗口开启 **Enhanced** 增强检索。

## 📦 支持的资产类型
| 类型 | 扩展名 | 索引策略 |
|---|---|---|
| **图片** | .png, .jpg, .jpeg, .tga | Vision 描述 + Embedding |
| **预制体/模型** | .prefab, .fbx, .obj, .gltf... | 缩略图预览 → Vision + Embedding |
| **材质** | .mat | 材质球预览 → Vision + Embedding |
| **脚本** | .cs | 正则提取类/方法/注释 → 文本 Embedding |
| **通用 Fallback** | * | 路径 + 文件名 + 类型 → 文本 Embedding |

## 🌐 支持的服务商
| 类型 | 示例服务商 | 推荐模型 (Vision / Embedding) |
|---|---|---|
| **OpenAI** | 阿里 DashScope, OpenAI, Ollama | qwen-vl-plus / text-embedding-v3, gpt-4o / text-embedding-3-small |
| **Gemini** | Google Gemini | gemini-2.0-flash / text-embedding-004 |

<details>
<summary><b>🚀 大规模资产 (10k+) 性能优化</b></summary>

- **并发控制**: 采用 Worker Queue 模式，避免内存峰值。
- **批量写入**: 数据库 Upsert 事务优化。
- **向量缓存**: 内存 LRU 缓存加速搜索响应。
- **增量扫描**: 基于文件哈希 (MD5) 的秒级差异扫描，包含元数据缓存。
</details>

<details>
<summary><b>🏗 架构设计与扩展</b></summary>

采用 **Processor Pattern**：
- `IAssetProcessor`: 定义不同资产的处理逻辑。
- `AssetProcessorRegistry`: 根据扩展名自动路由至对应的处理器。
- `SearchQueryEnhancer`: 处理搜索词的自然语言扩充，优化向量匹配。

**自定义扩展**: 实现 `IAssetProcessor` 接口并注册至 Registry 即可支持新格式。
</details>

## 📂 数据存储
| 内容 | 路径 | 建议 Git 管理 |
|---|---|---|
| 个人配置 | `UserSettings/SemanticSearch/Settings.json` | 否 |
| API Key | EditorPrefs (本地存储) | 否 |
| 索引数据库 | `ProjectSettings/SemanticSearch/Index.db` | **是** |

---
*本地化：支持简体中文、繁体中文、英文及日文，根据系统语言自动切换。*
