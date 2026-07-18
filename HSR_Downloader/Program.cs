using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using Newtonsoft.Json;

namespace HSR_DataDownloader;

public class Program
{
    public static async Task Main(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            PrintUsage();
            return;
        }

        // Parse arguments
        ServerMode? mode = null;
        string? hotfixPath = null;
        List<string>? languages = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--mode":
                case "-m":
                    if (i + 1 < args.Length)
                    {
                        var modeStr = args[++i].ToLowerInvariant();
                        if (modeStr == "beta")
                            mode = ServerMode.Beta;
                        else if (modeStr == "rel")
                            mode = ServerMode.Rel;
                        else
                        {
                            Console.WriteLine($"Error: Invalid mode '{modeStr}'. Use 'beta' or 'rel'.");
                            return;
                        }
                    }
                    else
                    {
                        Console.WriteLine("Error: --mode requires a value (beta or rel)");
                        return;
                    }
                    break;
                case "--lang":
                case "-l":
                    if (i + 1 < args.Length)
                    {
                        languages = args[++i].Split(',').Select(l => l.Trim().ToLowerInvariant()).Where(l => !string.IsNullOrEmpty(l)).ToList();
                    }
                    else
                    {
                        Console.WriteLine("Error: --lang requires a value (e.g. cn,en,jp)");
                        return;
                    }
                    break;
                default:
                    if (hotfixPath == null && !args[i].StartsWith("-"))
                        hotfixPath = args[i];
                    break;
            }
        }

        // Interactive mode selection if not specified
        if (mode == null)
        {
            Console.WriteLine("Select server mode:");
            Console.WriteLine("  1. Beta (Test server)");
            Console.WriteLine("  2. Rel  (Release server)");
            Console.Write("Enter choice (1 or 2): ");
            var input = Console.ReadLine()?.Trim();
            if (input == "1")
                mode = ServerMode.Beta;
            else if (input == "2")
                mode = ServerMode.Rel;
            else
            {
                Console.WriteLine("Invalid choice. Exiting.");
                return;
            }
        }

        // Validate hotfix path
        if (hotfixPath == null)
        {
            Console.WriteLine("Error: Please provide the path to hotfix.json");
            PrintUsage();
            return;
        }

        if (!File.Exists(hotfixPath))
        {
            Console.WriteLine($"Error: File not found: {hotfixPath}");
            return;
        }

        // Apply default languages if not specified
        if (languages == null || languages.Count == 0)
        {
            languages = mode == ServerMode.Beta ? DefaultLanguages.Beta : DefaultLanguages.Rel;
        }

        Console.WriteLine($"Mode: {mode}");
        Console.WriteLine($"Languages: {string.Join(", ", languages)}");

        // Initialize components
        HttpClient client = new HttpClient();
        Logger logger = new Logger();
        HotfixJson hotfixJson = JsonConvert.DeserializeObject<HotfixJson>(File.ReadAllText(hotfixPath))!;
        string platform = "Windows";
        BlockV blockV = new BlockV();
        DesignIndex designIndex = new DesignIndex();
        LuaIndex luaIndex = new LuaIndex();

        HotfixParser hotfixParser = new HotfixParser(client, logger, hotfixJson, platform, mode.Value, blockV, designIndex, luaIndex);
        hotfixParser.Languages = languages;

        // Parse metadata
        await hotfixParser.ParseAsbDatasAsync();
        await hotfixParser.ParseLuaDatasAsync();
        await hotfixParser.ParseDesignDatasAsync();

        // Set output directory based on mode
        string outputDir = mode == ServerMode.Beta ? "Beta" : "Rel";

        // Ensure base directories exist
        foreach (var dir in new[] { "Asb", "DesignData", "Lua", "Links" })
        {
            var path = Path.Combine(outputDir, dir);
            if (!Directory.Exists(path))
                Directory.CreateDirectory(path);
        }

        // Write link files for reference
        var asbLinksFilePath = Path.Combine(outputDir, "Links", "asbLinks.txt");
        File.WriteAllLines(asbLinksFilePath, hotfixParser.asbLinks.Distinct());
        logger.LogInfo($"Asb links written to {asbLinksFilePath}");

        var luaLinksFilePath = Path.Combine(outputDir, "Links", "luaLinks.txt");
        File.WriteAllLines(luaLinksFilePath, hotfixParser.luaLinks.Distinct());
        logger.LogInfo($"Lua links written to {luaLinksFilePath}");

        var exResourceLinksFilePath = Path.Combine(outputDir, "Links", "exResourceLinks.txt");
        File.WriteAllLines(exResourceLinksFilePath, hotfixParser.exResourceLinks.Distinct());
        logger.LogInfo($"DesignData links written to {exResourceLinksFilePath}");

        // Download files
        var downloader = new Downloader(logger, outputDir);

        if (hotfixParser.asbItems.Count > 0)
        {
            logger.LogInfo($"\nDownloading {hotfixParser.asbItems.Count} ASB files...", true);
            await downloader.DownloadItemsAsync(hotfixParser.asbItems.DistinctBy(i => i.Url).ToArray());
        }

        if (hotfixParser.luaItems.Count > 0)
        {
            logger.LogInfo($"\nDownloading {hotfixParser.luaItems.Count} Lua files...", true);
            await downloader.DownloadItemsAsync(hotfixParser.luaItems.DistinctBy(i => i.Url).ToArray());
        }

        if (hotfixParser.designItems.Count > 0)
        {
            logger.LogInfo($"\nDownloading {hotfixParser.designItems.Count} DesignData files...", true);
            await downloader.DownloadItemsAsync(hotfixParser.designItems.DistinctBy(i => i.Url).ToArray());
        }

        logger.LogInfo("\nDone!", true);
    }

    private static void PrintUsage()
    {
        Console.WriteLine(@"
HSR Data Downloader - Star Rail Resource Downloader (Unified)

Usage: HSR_Downloader <hotfix.json> [options]

Options:
  --mode, -m <mode>     Server mode: beta or rel (required if not interactive)
  --lang, -l <langs>    Filter downloads by language (comma-separated)
                        Beta default: cn,en,jp,kr
                        Rel  default: cn,en,jp,kr,cht
  --help, -h            Show this help message

Examples:
  HSR_Downloader hotfix.json                         Interactive mode selection
  HSR_Downloader hotfix.json --mode beta             Beta mode, default languages
  HSR_Downloader hotfix.json -m rel -l cn,en         Rel mode, CN+EN only
  HSR_Downloader hotfix.json -m beta -l cn           Beta mode, CN only
");
    }
}