# HSR_Downloader
某回合制二游的 Asb/DesignData/Lua 热更新资源下载器

[English](README.md) | [中文](README.zh-CN.md)

> **已适配最新 DesignData 和 Lua 数据格式**

# 使用方法
- 使用 Visual Studio 2022 构建
- 将 `hotfix.json` 作为参数运行程序，可通过 [FetchHotfix](https://github.com/Hiro420/FetchHotfix) 获取
- 程序将自动下载 hotfix 中的所有 Asb/DesignData/Lua 资源

# 命令行选项
- `--lang, -l <langs>` — 按语言过滤下载（逗号分隔），例如 `--lang cn,en`
- `--help, -h` — 显示帮助信息

![示例](image.png)

基于 [Hiro](https://github.com/Hiro420/HSR_Downloader) 的 HSR_Downloader 项目。