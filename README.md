# Semantic Search - Unity Semantic Asset Retrieval

[中文文档](README_CN.md) | [Requirements](#requirements) | [Installation](#installation) | [Supported Providers](#supported-providers)

Integrate LLMs (Vision + Embedding) to provide natural-language asset search capabilities for Unity. Supports multiple provider configurations, allowing simultaneous management of several API services.

## 🌟 Key Features

- **Natural Language Search**: Locate assets by typing descriptions (e.g., "red button", "sci-fi character avatar").
- **Smart Indexing**: 
  - **Visual Recognition**: Vision models describe images, prefabs, models, and materials.
  - **Context Injection**: Filenames are injected as auxiliary context to improve retrieval stability and recall.
- **Admin Mode**: A dedicated project-level state (Library/SemanticSearch/) that enables advanced panels: Prompt customization, Workflow control (Auto-Indexing), Filter rules, and Database maintenance.
- **Enhanced Search**: Uses LLM to optimize search keywords into richer bilingual descriptions, improving vector matching accuracy.
- **Team Friendly**: The embedding database is stored in `ProjectSettings/`, allowing team members to use semantic search immediately after pulling from Git without re-indexing.

## 🛠 Installation & Setup

### Requirements
- Unity 2021.3+
- An API Key supporting **Vision + Embedding** (e.g., GPT-4o + Text-Embedding-3, Gemini 1.5/2.0 Flash).

### Installation
Add the following to your `Packages/manifest.json`:
```json
"com.vectorz.semantic-search": "https://github.com/JiahuanZhang/unity-semantic-search.git"
```

### Quick Start
1. **Configure**: Go to `Window > Semantic Search > Settings`. Add a Provider (OpenAI or Gemini type).
2. **Enable Admin**: Click **Admin Mode** in the settings panel to reveal indexing tools.
3. **Scan**: In the **Database** panel, click **Scan & Update** to build the initial index.
4. **Search**: Open `Window > Semantic Search > Asset View` to browse, or use the search window with the **Enhanced** toggle.

## 📦 Supported Asset Types
| Category | Extensions | Indexing Strategy |
|---|---|---|
| **Image** | .png, .jpg, .jpeg, .tga | Vision description + Embedding |
| **Prefab/Model** | .prefab, .fbx, .obj, .gltf... | AssetPreview thumbnail → Vision + Embedding |
| **Material** | .mat | Material ball preview → Vision + Embedding |
| **Script** | .cs | Regex extracts class/method/comments → Text Embedding |
| **Generic** | * | Path + Filename + Type → Text Embedding |

## 🌐 Supported Providers
| Type | Example Providers | Models (Vision / Embedding) |
|---|---|---|
| **OpenAI** | Alibaba DashScope, OpenAI, Ollama | qwen-vl-plus / text-embedding-v3, gpt-4o / text-embedding-3-small |
| **Gemini** | Google Gemini | gemini-2.0-flash / text-embedding-004 |

<details>
<summary><b>🚀 Performance for Large Scales (10k+)</b></summary>

- **Concurrency Control**: Managed worker queue to prevent memory spikes.
- **Batch Processing**: SQLite batch `Upsert` for faster database writes.
- **Vector Caching**: In-memory cache for rapid search response.
- **Incremental Scan**: File hash (MD5) based differential scanning with metadata caching.
</details>

<details>
<summary><b>🏗 Architecture & Extensibility</b></summary>

Built on a **Processor Pattern**:
- `IAssetProcessor`: Interface for custom asset handling.
- `AssetProcessorRegistry`: Auto-routes assets to the correct processor based on extension.
- `SearchQueryEnhancer`: LLM-based query expansion for better vector matching.

**Extending**: To support new formats, implement `IAssetProcessor` and register it in the registry.
</details>

## 📂 Data Storage
| Content | Path | Version Controlled |
|---|---|---|
| User Config | `UserSettings/SemanticSearch/Settings.json` | No |
| API Keys | EditorPrefs (Local) | No |
| Index Database | `ProjectSettings/SemanticSearch/Index.db` | **Yes** |

---
*Localization: Supports English, Chinese (Simplified/Traditional), and Japanese based on System Language.*
