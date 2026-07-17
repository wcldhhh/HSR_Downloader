using System;
using System.Text;
using Newtonsoft.Json;

namespace HSR_DataDownloader;

public class HotfixParser
{
    private readonly HttpClient _client;
    private readonly Logger _logger;
    private readonly HotfixJson _hotfixJson;
    private readonly string _platform;
    private BlockV _blockV;
    private LuaIndex _luaIndex;
    private DesignIndex _designIndex;

    /// <summary>Desired languages for filtering (e.g. "cn","en","jp"). Empty = download all.</summary>
    public List<string> Languages { get; set; } = new();

    /// <summary>Download items with URL and subdirectory info.</summary>
    public List<DownloadItem> asbItems = new();
    public List<DownloadItem> luaItems = new();
    public List<DownloadItem> designItems = new();

    /// <summary>Backward-compatible URL lists.</summary>
    public List<string> asbLinks => asbItems.Select(i => i.Url).ToList();
    public List<string> luaLinks => luaItems.Select(i => i.Url).ToList();
    public List<string> exResourceLinks => designItems.Select(i => i.Url).ToList();

    /// <summary>Get the parsed DesignIndex (available after ParseDesignDatasAsync).</summary>
    public DesignIndex? GetDesignIndex() => _designIndex;
    /// <summary>Get the parsed LuaIndex (available after ParseLuaDatasAsync).</summary>
    public LuaIndex? GetLuaIndex() => _luaIndex;

    public HotfixParser(HttpClient client, Logger logger, HotfixJson hotfixJson, string platform, BlockV blockV, DesignIndex designIndex, LuaIndex luaIndex)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _hotfixJson = hotfixJson ?? throw new ArgumentNullException(nameof(hotfixJson));
        _platform = platform ?? throw new ArgumentNullException(nameof(platform));
        _blockV = blockV ?? throw new ArgumentNullException(nameof(blockV));
        _luaIndex = luaIndex ?? throw new ArgumentNullException(nameof(luaIndex));
        _designIndex = designIndex ?? throw new ArgumentNullException(nameof(designIndex));
    }

    private bool ShouldDownloadFile(string? lang)
    {
        // Always download shared files (no language tag)
        if (string.IsNullOrEmpty(lang))
            return true;
        // If no filter specified, download all
        if (Languages.Count == 0)
            return true;
        // Download if language matches filter
        return Languages.Contains(lang.ToLowerInvariant());
    }

    public async Task ParseAsbDatasAsync()
    {
        if (string.IsNullOrEmpty(_hotfixJson.assetBundleUrl)) return;
        try
        {
            string? baseAssetDownloadURL = null;
            string url = $"{_hotfixJson.assetBundleUrl}/client/{_platform}/Archive/M_ArchiveV.bytes";
            string response = await _client.GetStringAsync(url).ConfigureAwait(false);
            asbItems.Add(new DownloadItem(url, "Asb"));

            _logger.LogInfo("Successfully fetched M_ArchiveV data from the URL");

            foreach (string rsp in response.Split('\n'))
            {
                if (string.IsNullOrWhiteSpace(rsp)) continue;

                M_ArchiveV? item = JsonConvert.DeserializeObject<M_ArchiveV>(rsp);
                if (item == null || !item.FileName.StartsWith("M_BlockV")) continue;

                if (!string.IsNullOrEmpty(item.BaseAssetsDownloadUrl))
                    baseAssetDownloadURL = item.BaseAssetsDownloadUrl;

                string blockVurl = $"{_hotfixJson.assetBundleUrl}/client/{_platform}/Block/BlockV_{item.ContentHash}.bytes";
                byte[] blockVcontent = await _client.GetByteArrayAsync(blockVurl).ConfigureAwait(false);
                _blockV.ReadData(blockVcontent);
                asbItems.Add(new DownloadItem(blockVurl, "Asb"));

                foreach (var block in _blockV.asbBlocks)
                {
                    if (block.isStart || string.IsNullOrEmpty(baseAssetDownloadURL))
                    {
                        asbItems.Add(new DownloadItem(
                            $"{_hotfixJson.assetBundleUrl}/client/{_platform}/Block/{block.assetName}.block",
                            "Asb"));
                    }
                    else
                    {
                        var link = string.Join("/", url.Split('/').SkipLast(1));
                        string[] urlParts = _hotfixJson.assetBundleUrl.Split('/');
                        urlParts[^1] = baseAssetDownloadURL;
                        string newurl = string.Join("/", urlParts);
                        asbItems.Add(new DownloadItem(
                            $"{newurl}/client/{_platform}/Block/{block.assetName}.block",
                            "Asb"));
                    }
                }
            }
        }
        catch (HttpRequestException e)
        {
            _logger.LogError($"Request error: {e.Message}");
        }
        catch (Exception e)
        {
            _logger.LogError($"Unexpected error: {e.Message}");
        }
    }

    public async Task ParseDesignDatasAsync()
    {
        if (string.IsNullOrEmpty(_hotfixJson.exResourceUrl)) return;
        try
        {
            string url = $"{_hotfixJson.exResourceUrl}/client/{_platform}/M_DesignV.bytes";
            byte[] designBytes = await _client.GetByteArrayAsync(url).ConfigureAwait(false);
            designItems.Add(new DownloadItem(url, "DesignData"));

            _logger.LogInfo("Successfully fetched M_DesignV data from the URL");

            using var ms = new MemoryStream(designBytes);
            using var br = new EndianBinaryReader(ms, Encoding.UTF8);
            var magic = new string(br.ReadChars(4));

            br.ReadInt16();
            var MetadataInfoSize = br.ReadInt32();
            ms.Seek(0xE, SeekOrigin.Current);

            var RemoteRevisionID = br.ReadInt32();
            var IndexHash = br.ReadHash();

            var AssetListFilesize = br.ReadUInt32();
            br.ReadUInt32();
            var AssetListUnixTimestamp = br.ReadUInt64();
            var AssetListRootPath = br.ReadString();

            string indexHashUrl = $"{_hotfixJson.exResourceUrl}/client/{_platform}/DesignV_{IndexHash}.bytes";
            byte[] indexBytes = await _client.GetByteArrayAsync(indexHashUrl).ConfigureAwait(false);
            _designIndex = DesignIndex.Read(indexBytes);
            designItems.Add(new DownloadItem(indexHashUrl, "DesignData"));

            var entriesNum = _designIndex.Files.Sum(file => file.Entries.Count);
            var filesNum = _designIndex.Files.Count;
            var availableLangs = _designIndex.Files.Where(f => !string.IsNullOrEmpty(f.Lang)).Select(f => f.Lang).Distinct().OrderBy(l => l).ToList();
            _logger.LogInfo($"DesignV v{_designIndex.Version}: {filesNum} files, {entriesNum} entries, languages: [{string.Join(", ", availableLangs)}]", true);

            foreach (var file in _designIndex.Files)
            {
                if (!ShouldDownloadFile(file.Lang))
                {
                    _logger.LogInfo($"  Skipping {file.FileHash}.bytes (lang={file.Lang})");
                    continue;
                }

                // Put language-specific files in language subdirectory (both URL and local path)
                var langPath = string.IsNullOrEmpty(file.Lang) ? "" : $"/{file.Lang}";
                var subDir = string.IsNullOrEmpty(file.Lang) ? "DesignData" : $"DesignData/{file.Lang}";
                designItems.Add(new DownloadItem(
                    $"{_hotfixJson.exResourceUrl}/client/{_platform}{langPath}/{file.FileHash}.bytes",
                    subDir,
                    $"{file.FileHash}.bytes"));
            }
        }
        catch (HttpRequestException e)
        {
            _logger.LogError($"Request error: {e.Message}");
        }
        catch (Exception e)
        {
            _logger.LogError($"Unexpected error: {e.Message}");
        }
    }

    public async Task ParseLuaDatasAsync()
    {
        if (string.IsNullOrEmpty(_hotfixJson.luaUrl)) return;
        try
        {
            string url = $"{_hotfixJson.luaUrl}/client/{_platform}/M_LuaV.bytes";
            byte[] luaVBytes = await _client.GetByteArrayAsync(url).ConfigureAwait(false);
            luaItems.Add(new DownloadItem(url, "Lua"));

            _logger.LogInfo("Successfully fetched M_LuaV data from the URL");

            using var ms = new MemoryStream(luaVBytes);
            using var br = new EndianBinaryReader(ms, Encoding.UTF8);
            var magic = new string(br.ReadChars(4));

            br.ReadInt16();
            var MetadataInfoSize = br.ReadInt32();
            ms.Seek(0xE, SeekOrigin.Current);

            var RemoteRevisionID = br.ReadInt32();
            var IndexHash = br.ReadHash();

            var AssetListFilesize = br.ReadUInt32();
            br.ReadUInt32();
            var AssetListUnixTimestamp = br.ReadUInt64();
            var AssetListRootPath = br.ReadString();

            string indexHashUrl = $"{_hotfixJson.luaUrl}/client/{_platform}/LuaV_{IndexHash}.bytes";
            byte[] indexBytes = await _client.GetByteArrayAsync(indexHashUrl).ConfigureAwait(false);
            _luaIndex = LuaIndex.Read(indexBytes);
            luaItems.Add(new DownloadItem(indexHashUrl, "Lua"));

            var entriesNum = _luaIndex.Files.Sum(file => file.Entries.Count);
            var filesNum = _luaIndex.Files.Count;
            var availableLangs = _luaIndex.Files.Where(f => !string.IsNullOrEmpty(f.Lang)).Select(f => f.Lang).Distinct().OrderBy(l => l).ToList();
            _logger.LogInfo($"LuaV v{_luaIndex.Version}: {filesNum} files, {entriesNum} entries, languages: [{string.Join(", ", availableLangs)}]", true);

            foreach (var file in _luaIndex.Files)
            {
                if (!ShouldDownloadFile(file.Lang))
                {
                    _logger.LogInfo($"  Skipping {file.FileHash}.bytes (lang={file.Lang})");
                    continue;
                }

                var langPath = string.IsNullOrEmpty(file.Lang) ? "" : $"/{file.Lang}";
                var subDir = string.IsNullOrEmpty(file.Lang) ? "Lua" : $"Lua/{file.Lang}";
                luaItems.Add(new DownloadItem(
                    $"{_hotfixJson.luaUrl}/client/{_platform}{langPath}/{file.FileHash}.bytes",
                    subDir,
                    $"{file.FileHash}.bytes"));
            }
        }
        catch (HttpRequestException e)
        {
            _logger.LogError($"Request error: {e.Message}");
        }
        catch (Exception e)
        {
            _logger.LogError($"Unexpected error: {e.Message}");
        }
    }
}

/// <summary>
/// Represents a downloadable item with URL, destination subdirectory, and filename.
/// </summary>
public class DownloadItem
{
    public string Url { get; }
    public string SubDirectory { get; }
    public string FileName { get; }

    public DownloadItem(string url, string subDirectory, string? fileName = null)
    {
        Url = url;
        SubDirectory = subDirectory;
        FileName = fileName ?? new Uri(url).Segments.Last();
    }
}