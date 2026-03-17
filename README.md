# Semantic Search - Unity 语义化资产检索

通过集成大模型（通义千问 Qwen-VL / Embedding），为 Unity 开发者提供自然语言资产搜索能力。

## 功能

- **自然语言搜索**：在 Console 窗口输入描述即可定位资产
- **自动索引**：资产导入时自动生成描述与向量
- **手动扫描**：一键扫描全项目并更新索引
- **配置面板**：通过 `Project Settings > Semantic Search` 管理 API Key、模型选择等

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
- 通义千问 API Key（Qwen-VL + Embedding）

## 配置

1. 打开 `Edit > Project Settings > Semantic Search`
2. 填入 API Key 与选择模型
3. 点击 **Scan & Update** 开始索引
