# 变更日志

[English Changelog](CHANGELOG.md)

## [0.1.8] - 2026-04-03

### Added

- 恢复管理员模式开关（Admin Mode），默认关闭
- Admin Mode 状态持久化到 `Library/SemanticSearch/AdminMode`，仅当前项目生效、不会上传 Git、关闭 Unity 后保持
- 非管理员设置面板仅显示 LLM Configuration；管理员可见 Prompt、Workflow、Filter、Database 全部面板
- 从非管理员切换到管理员时弹出确认对话框，告知该模式面向开发者并说明影响范围
- 仅管理员身份下资源导入时自动索引（`AutoIndexOnImport`）才会生效

## [0.1.7] - 2026-03-19

### Changed

- 移除 Admin/User 角色 Provider 分配机制，所有身份统一使用当前激活的 Provider
- 移除管理员模式开关，设置面板始终显示全部配置项

## [0.1.6] - 2026-03-19

### Added

- 提示词配置面板：管理员模式下可自定义 Vision 描述提示词和搜索增强提示词
- 提示词默认值根据用户系统语言自动填充（中/英/日）
- 自定义提示词清空后自动回退到当前语言的默认提示词
- 提示词配置持久化到 `UserSettings/SemanticSearch/Settings.json`

## [0.1.0] - 2026-03-17

### Added

- SQLite 数据存储层（AssetRecord / SemanticSearchDB / VectorSerializer）
- 文件监听与 MD5 比对（AssetScanner / MD5Helper / SemanticAssetPostprocessor）
- LLM API 通信层（Qwen-VL / Qwen-Embedding / LLMHttpClient）
- 资产索引流水线（IndexPipeline / BatchProgress）
- 向量检索引擎（CosineSimilarity / VectorSearchEngine）
- 配置中心面板（SemanticSearchSettingsProvider）
- Console 搜索注入与结果展示窗口
