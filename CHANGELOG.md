# Changelog

[中文变更日志](CHANGELOG_CN.md)

## [0.1.8] - 2026-04-03

### Added

- Restored Admin Mode toggle, disabled by default
- Admin Mode state persisted to `Library/SemanticSearch/AdminMode` — project-scoped, excluded from Git, survives Unity restarts
- Non-admin settings panel shows only LLM Configuration; admins see all panels (Prompt, Workflow, Filter, Database)
- Confirmation dialog when switching from non-admin to admin, explaining the mode is for developers and its scope of impact
- Auto-indexing on asset import (`AutoIndexOnImport`) only takes effect in Admin Mode

## [0.1.7] - 2026-03-19

### Changed

- Removed Admin/User role-based provider assignment; all identities now use the currently active provider
- Removed Admin Mode toggle; settings panel always shows all configuration items

## [0.1.6] - 2026-03-19

### Added

- Prompt configuration panel: customize Vision description prompt and search enhancement prompt in Admin Mode
- Prompt defaults auto-populated based on system language (Chinese/English/Japanese)
- Empty custom prompts automatically fall back to the current language's default prompt
- Prompt configuration persisted to `UserSettings/SemanticSearch/Settings.json`

## [0.1.0] - 2026-03-17

### Added

- SQLite data storage layer (AssetRecord / SemanticSearchDB / VectorSerializer)
- File monitoring and MD5 comparison (AssetScanner / MD5Helper / SemanticAssetPostprocessor)
- LLM API communication layer (Qwen-VL / Qwen-Embedding / LLMHttpClient)
- Asset indexing pipeline (IndexPipeline / BatchProgress)
- Vector search engine (CosineSimilarity / VectorSearchEngine)
- Settings panel (SemanticSearchSettingsProvider)
- Console search injection and results display window
