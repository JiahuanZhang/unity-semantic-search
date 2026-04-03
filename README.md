# Semantic Search - Unity Semantic Asset Retrieval

[中文文档](README_CN.md)

Integrate LLMs (Vision + Embedding) to provide natural-language asset search capabilities for Unity developers. Supports multiple provider configurations, allowing simultaneous management of several API services. Compatible with both OpenAI-compatible format and Google Gemini native API.

## Features

- **Natural Language Search**: Locate assets by typing descriptions in the search window
- **Multi-Provider Support**: Configure multiple LLM providers and switch between them with one click
- **Admin Mode**: Disabled by default; state is persisted under `Library/SemanticSearch/` (project-level, excluded from Git, survives Unity restarts). A confirmation dialog is shown when enabling. Admins see all configuration panels (Prompt, Workflow, Filter, Database); non-admins only see LLM Provider settings. Auto-indexing only takes effect in Admin Mode
- **Multiple API Formats**: Supports OpenAI-compatible format (Alibaba DashScope, OpenAI, Ollama, etc.) and Google Gemini native API
- **Asset Filter Rules**: Include/Exclude glob patterns to precisely control which assets participate in indexing
- **Auto-Indexing**: Automatically indexes assets upon import (disabled by default, only effective in Admin Mode)
- **Manual Scan**: One-click full-project scan and index update
- **Indexed Asset Browser**: Open a dedicated window via `Window > Semantic Search > Asset View` to browse all indexed assets, with path/description search, status filtering, and thumbnail preview
- **Enhanced Search**: The Search Results window provides an Enhanced toggle. When enabled, the LLM optimizes search keywords (expanding into richer bilingual descriptions) to improve vector matching accuracy; falls back to the original query on failure
- **Quick Submit**: Pressing Enter or losing focus in the Search Results input immediately triggers a search
- **Selective Re-indexing**: Check assets in the Asset View window and click Re-index to reprocess only selected assets
- **Settings Panel**: Manage providers, model selection, etc. via `Project Settings > Semantic Search` or `Window > Semantic Search > Settings`
- **Custom Prompts**: Customize Vision description prompts and search enhancement prompts; leaving them empty automatically uses the current language's defaults
- **LLM Connectivity Test**: Click **Test LLM** in the settings panel to quickly verify the current provider configuration
- **Team Sharing**: The embedding database is committed to version control so team members don't need to re-process assets

## Installation

### Option 1: Local Path Reference

Add to your project's `Packages/manifest.json`:

```json
{
  "dependencies": {
    "com.vectorz.semantic-search": "file:../../Terminal/Semantic Search"
  }
}
```

### Option 2: Git URL (if pushed to a repository)

```json
{
  "dependencies": {
    "com.vectorz.semantic-search": "https://your-repo-url.git?path=Semantic Search"
  }
}
```

## Requirements

- Unity 2021.3+
- A supported API key (must support Vision + Embedding)

## Supported Providers (Examples)

### OpenAI-Compatible Format (Provider Type: OpenAI)

| Provider | Base URL | Vision Model | Embedding Model |
|---|---|---|---|
| Alibaba DashScope | `https://dashscope.aliyuncs.com/compatible-mode/v1` | `qwen-vl-plus` | `text-embedding-v3` |
| OpenAI | `https://api.openai.com/v1` | `gpt-4o` | `text-embedding-3-small` |
| Ollama (local) | `http://localhost:11434/v1` | `llava` | `nomic-embed-text` |

> Any API following OpenAI's `/chat/completions` and `/embeddings` format can be integrated.

### Google Gemini (Provider Type: Gemini)

| Provider | Base URL | Vision Model | Embedding Model |
|---|---|---|---|
| Google Gemini | `https://generativelanguage.googleapis.com/v1beta` | `gemini-2.0-flash` | `gemini-embedding-001` |

> Uses the Gemini native API (`generateContent` + `embedContent`), authenticated via `x-goog-api-key` header.

## Data Storage

| Content | Path | Version Controlled |
|---|---|---|
| User config (provider list, endpoints, etc.) | `UserSettings/SemanticSearch/Settings.json` | No |
| API Key | Local machine EditorPrefs by default; written to `UserSettings/SemanticSearch/Settings.json` when Save is checked | No |
| Embedding index database | `ProjectSettings/SemanticSearch/Index.db` | **Yes** |

> Not all team members have API key access, so configuration is not version-controlled. Processed embedding data is stored in `ProjectSettings/`, allowing team members to use semantic search immediately after pulling without re-indexing.

## Configuration

1. Open `Edit > Project Settings > Semantic Search`, or via `Window > Semantic Search > Settings`
2. Configure LLM Provider (visible to all users): click **+** to add a provider, select **Provider Type**, fill in Base URL, API Key, Vision Model, Embedding Model, then click **Test LLM** to verify connectivity
3. To manage the index database, enable **Admin Mode** (a confirmation dialog appears explaining this mode is intended for developers; state is saved in the Library directory and persists after closing Unity)
4. With Admin Mode enabled, the following additional panels become visible:
   - **Prompt Configuration**: Customize Vision and search enhancement prompts; leave empty or click **Reset** to restore the current language's defaults
   - **Workflow Control**: Enable **Auto-Index On Import** (only effective in Admin Mode), adjust max concurrency
   - **Asset Filter Rules**: Configure Include/Exclude glob rules to control which assets participate in indexing
   - **Database Maintenance**: **Scan & Update** / **Clear Database** / **Open Database Folder**
5. Click **Open Asset View** or use the menu `Window > Semantic Search > Asset View` to open the asset browser
6. In the Asset View window, search by path/description, filter by status, check assets and click **Re-index Selected** to regenerate descriptions and vectors

## Performance Optimization for Large-Scale Assets (10k+)

This version includes targeted optimizations for large-scale image indexing and retrieval, focusing on reducing peak memory usage, database transaction overhead, and query latency.

### Implemented Optimizations

- **Indexing Concurrency Model**: Changed from "expand all tasks at once" to a controlled worker queue, avoiding scheduler and GC pressure from creating 10k task objects simultaneously
- **Batch Database Writes**: Both indexing and scanning support batch `Upsert`, reducing per-record transaction commit overhead
- **Search Pipeline Speedup**: Added in-memory cache for vector data with snapshot signature invalidation; TopK uses partial selection to reduce full-sort cost
- **Database Query Optimization**: Added indexes on `status`, `asset_path`, `updated_at`; asset list queries support lightweight field selection and paginated reads
- **UI Load Optimization**: Asset View filter result caching; thumbnail caches in Asset View / Search Result include size limits and eviction policies
- **MD5 Scan Optimization**: File hash includes metadata caching (file size + modification time) to reduce redundant full-file reads

### Performance Monitoring

After clicking `Scan & Update`, the Console outputs scan and indexing performance logs, such as:

- Scan duration (`scanTime`)
- Index duration (`indexTime`)
- Indexing throughput (assets/s)
- Batch peak managed memory (`peakManaged`)
- Search duration and query memory changes (`Search perf` logs)

### Recommended Parameters for 10k+ Assets

- **Max Concurrent Requests**: Start with `2~4` (test with low concurrency first, then increase gradually)
- **Execution Strategy**: Prefer scanning directories in batches via `Scan & Update`; avoid covering the entire project at once on first run
- **Asset Management**: Limit the proportion of oversized images; compress textures before indexing
- **Timing**: Run large batch indexing tasks when the editor is idle

## Architecture: Asset Processor

The indexing pipeline uses a **processor pattern** that separates processing logic for different asset types, making it easy to extend with new types (e.g., scripts, materials).

| Component | Responsibility |
|---|---|
| `IAssetProcessor` | Processor interface defining `Kind` / `CanProcess` / `GetAssetData` / `ProcessAsync` |
| `AssetKind` | Enum: `Visual` (requires Vision description) / `Text` (direct text Embedding) |
| `ImageAssetProcessor` | Processes image assets (.png/.jpg/.jpeg/.tga), reads file bytes → Vision + Embedding |
| `PrefabAssetProcessor` | Processes prefab assets (.prefab) via AssetPreview thumbnail → Vision + Embedding |
| `ModelAssetProcessor` | Processes 3D models (.fbx/.obj/.blend/.dae/.3ds/.gltf/.glb), thumbnail → Vision + Embedding |
| `MaterialAssetProcessor` | Processes materials (.mat), material ball preview → Vision + Embedding |
| `ScriptAssetProcessor` | Processes C# scripts (.cs), regex extracts class names/methods/comments → text Embedding |
| `DefaultAssetProcessor` | Fallback processor based on filename, path, and extension type → text Embedding |
| `PreviewBasedAssetProcessor` | Base class for AssetPreview thumbnail-based processors, shared by Prefab/Model/Material |
| `AssetProcessorRegistry` | Registers and manages all processors, auto-routes by file extension, Default as fallback |
| `IndexPipeline` | Indexing pipeline that obtains the corresponding processor via Registry |

### Enhanced Search (Query Enhancement)

When **Enhanced** is checked in the Search Results window, the search flow becomes:

1. The user's search keywords are sent to the LLM (reusing the VL Model), which identifies intent and expands them into a more complete bilingual description
2. The optimized text is used for vector Embedding computation and matching
3. The actual enhanced text is displayed in blue italics below the search results

For example, entering "anime avatar" would be optimized by the LLM into something like "Description: Anime-style character avatar, 2D cartoon character portrait; 动漫风格的角色头像，二次元卡通人物形象...". If the user doesn't specify an asset type, no type constraint is added; if they do (e.g., "red button image"), an asset type constraint is automatically included.

Falls back to the original query text on enhancement failure without affecting search.

| Component | Responsibility |
|---|---|
| `IChatClient` | Text chat interface defining `ChatAsync(systemPrompt, userMessage)` |
| `ChatClient` | OpenAI-compatible format implementation (`/chat/completions`) |
| `GeminiChatClient` | Gemini native API implementation (`generateContent`) |
| `SearchQueryEnhancer` | Uses IChatClient to optimize search keywords into vector-retrieval-friendly descriptive text |

### Vectorization Text Enhancement (Filename Context)

To reduce visual recognition errors, images and prefabs inject the **filename** as auxiliary context during indexing:

- Vision phase: Appends filename hints to the prompt (supplementary info, does not override visual content)
- Embedding phase: Combines `asset_type + file_name + caption` before generating the vector

This improves retrieval stability and recall for assets with semantically meaningful names (e.g., characters, props, UI components).

### Supported Asset Types

| Type | Extensions | Indexing Strategy |
|---|---|---|
| Image | .png, .jpg, .jpeg, .tga | Vision description + Embedding |
| Prefab | .prefab | AssetPreview thumbnail → Vision + Embedding |
| 3D Model | .fbx, .obj, .blend, .dae, .3ds, .gltf, .glb | AssetPreview thumbnail → Vision + Embedding |
| Material | .mat | AssetPreview material ball preview → Vision + Embedding |
| C# Script | .cs | Regex extracts class names/method signatures/comments → text Embedding |
| Other | any | DefaultAssetProcessor fallback: filename + path + type → text Embedding |

To extend with a new asset type:
1. Implement the `IAssetProcessor` interface (or inherit from `PreviewBasedAssetProcessor`)
2. Register in the `AssetProcessorRegistry` constructor (before DefaultAssetProcessor)
3. Add extension mapping in `AssetScanner.ExtensionToAssetType`

## Localization

The plugin automatically switches the UI language based on the operating system language. Supported languages:

| Language | System Language |
|---|---|
| Simplified Chinese | `Chinese` / `ChineseSimplified` / `ChineseTraditional` |
| English | All other languages (default fallback) |
| Japanese | `Japanese` |

Localization coverage:
- **UI**: All window titles, button labels, status messages, dialog text, settings panels, etc.
- **LLM Prompts**: Vision description prompts and search enhancement system prompts adapt to the current language
- **Not covered**: Code comments, log output, MenuItem paths

Language detection is based on `Application.systemLanguage`, determined at static construction time and immutable during runtime. The core localization class is `Editor/Core/Localization/L10n.cs`.

## Recent Fixes

- Fixed editor freezing when operating UI during indexing: Added `PRAGMA busy_timeout` to avoid immediate SQLite lock conflicts; `EnsureSchema` now runs only once to eliminate unnecessary DDL write contention; all DB call sites audited — `RefreshAssetList`, `RefreshCounts`, `ClearDatabase`, `DeleteSelected` converted to async; search engine's `GetCachedVectors`/`GetAssetSummariesByGuids` moved to background thread; `OnPostprocessAllAssets` DB writes deferred via `delayCall` + `Task.Run`, ensuring the main thread is never blocked by DB operations
- Prefab entries in Asset View and Search Results now display 3D thumbnails by default (`AssetPreview.GetAssetPreview`), showing a type icon during async loading then auto-refreshing, with no performance blocking
- In the Semantic Search Results window, the match percentage is now displayed on the same line as the filename (right-aligned) for better readability
- Fixed occasional slow opening and empty list when opening Asset View while the Semantic Search Results window is already open
- Adjusted the search window's database connection strategy to "open on demand per search and release after completion", reducing connection contention between windows
- Asset View now shows failure information in the window and outputs error logs on refresh failure, instead of silently displaying an empty list
- Optimized 10k+ asset indexing pipeline: controlled concurrency queue + batch writes + performance metric logging, reducing peak memory and improving throughput
- Optimized 10k+ asset search pipeline: vector caching + TopK partial selection + lightweight metadata backfill, reducing query latency
- Optimized 10k+ asset scanning and UI: MD5 caching, database indexes, list filtering and thumbnail cache eviction, improving interaction stability
