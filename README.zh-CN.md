# HSR_Downloader

某回合制二游的 Asb/DesignData/Lua 热更新资源下载器 — **同时支持 Beta 和 Rel 服务器**

[English](README.md) | [中文](README.zh-CN.md)

### 主要功能

- **双服务器** — 启动时选择 Beta 或 Rel（交互式或 `--mode` 参数））
- **语言过滤** — `--lang` 选项，按模式提供不同的默认语言列表
- **重试与校验** — 下载失败自动重试（最多 3 次），并验证文件大小
- **分类输出** — 文件按所选模式保存到 `Beta/` 或 `Rel/` 子目录

### 格式差异

| 特性 | Beta | Rel |
|------|------|-----|
| 默认语言 | cn, en, jp, kr | cn, en, jp, kr, cht |

### 选择 Beta 或 Rel

根据 `hotfix.json` 来源的**服务器**选择：

- **Beta** — 适用于 **测试(Beta)服务器**，版本 **4.4.51 及之后**。
- **Rel** — 适用于 **正式服（Release/Live）**，或 **4.4.51 之前的 Beta 服务器**。

> **注意：** 如果不确定，程序会在运行时自动检测索引格式，因此模式选择主要影响 M_DesignV/M_LuaV 元数据解析和默认语言列表。

## 使用方法

- 使用 Visual Studio 2022 构建（.NET 8.0）
- 将 `hotfix.json` 作为参数运行程序，可通过 [FetchHotfix](https://github.com/Hiro420/FetchHotfix) 获取
- 程序将自动下载 hotfix 中的所有 Asb/DesignData/Lua 资源

## 命令行选项

- `--mode, -m <beta|rel>` — 服务器模式（省略则交互式选择）
- `--lang, -l <langs>` — 按语言过滤下载（逗号分隔）
  - Beta 默认：`cn,en,jp,kr`
  - Rel 默认：`cn,en,jp,kr,cht`
- `--help, -h` — 显示帮助信息

## 示例

```bash
# 交互式选择模式
HSR_Downloader hotfix.json

# Beta 模式，使用默认语言 (cn,en,jp,kr)
HSR_Downloader hotfix.json --mode beta

# Rel 模式，使用默认语言 (cn,en,jp,kr,cht)
HSR_Downloader hotfix.json -m rel

# Rel 模式，仅下载中文和英文
HSR_Downloader hotfix.json -m rel -l cn,en

# Beta 模式，仅下载中文
HSR_Downloader hotfix.json --mode beta --lang cn
```

基于 [Hiro](https://github.com/Hiro420/HSR_Downloader) 的 HSR_Downloader 项目。

![示例](image.png)