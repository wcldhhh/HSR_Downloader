# HSR_Downloader

Asb/DesignData/Lua hotfix downloader for some anime game with stars and rails — **Supporting both Beta and Rel servers**

[English](README.md) | [中文](README.zh-CN.md)

### Key Features

- **Dual server mode** — Select Beta or Rel at startup (interactive or via `--mode`)
- **Language filtering** — `--lang` option with mode-specific defaults
- **Retry & verify** — Downloads retry up to 3 times with file size verification
- **Organized output** — Files saved to `Beta/` or `Rel/` subdirectory based on selected mode

### Format Differences

| Feature | Beta | Rel |
|---------|------|-----|
| Default languages | cn, en, jp, kr | cn, en, jp, kr, cht |

### Choosing Beta or Rel

Choose based on which **server** your `hotfix.json` comes from:

- **Beta** — For the **Beta (test) server**, version **4.4.51 and later**.
- **Rel** — For the **Release (live) server**, or for **Beta server versions before 4.4.51**.

> **Note:** If you are unsure, the program auto-detects the index format at runtime, so the mode mainly affects M_DesignV/M_LuaV metadata parsing and default language lists.

## Usage

- Build via Visual Studio 2022 (.NET 8.0)
- Run the .exe with `hotfix.json` as an argument, you can get it using [FetchHotfix](https://github.com/Hiro420/FetchHotfix)
- The program will download all Asb/DesignData/Lua from the hotfix

## Options

- `--mode, -m <beta|rel>` — Server mode (if omitted, interactive selection)
- `--lang, -l <langs>` — Filter downloads by language (comma-separated)
  - Beta default: `cn,en,jp,kr`
  - Rel default: `cn,en,jp,kr,cht`
- `--help, -h` — Show help message

## Examples

```bash
# Interactive mode selection
HSR_Downloader hotfix.json

# Beta mode with default languages (cn,en,jp,kr)
HSR_Downloader hotfix.json --mode beta

# Rel mode with default languages (cn,en,jp,kr,cht)
HSR_Downloader hotfix.json -m rel

# Rel mode, Chinese and English only
HSR_Downloader hotfix.json -m rel -l cn,en

# Beta mode, Chinese only
HSR_Downloader hotfix.json --mode beta --lang cn
```

Based on [HSR_Downloader](https://github.com/Hiro420/HSR_Downloader) by Hiro.

![Example](image.png)