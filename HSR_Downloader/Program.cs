using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using Newtonsoft.Json;

namespace HSR_DataDownloader
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintUsage();
                return;
            }

            // Parse arguments
            string? hotfixPath = null;
            List<string> languages = new();

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--lang":
                    case "-l":
                        if (i + 1 < args.Length)
                        {
                            languages = args[++i].Split(',').Select(l => l.Trim().ToLowerInvariant()).ToList();
                        }
                        break;
                    case "--help":
                    case "-h":
                        PrintUsage();
                        return;
                    default:
                        if (hotfixPath == null && !args[i].StartsWith("-"))
                            hotfixPath = args[i];
                        break;
                }
            }

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

            HttpClient client = new HttpClient();
            Logger logger = new Logger();
            HotfixJson hotfixJson = JsonConvert.DeserializeObject<HotfixJson>(File.ReadAllText(hotfixPath))!;
            string platform = "Windows";
            BlockV blockV = new BlockV();
            DesignIndex designIndex = new DesignIndex();
            LuaIndex luaIndex = new LuaIndex();

            HotfixParser hotfixParser = new HotfixParser(client, logger, hotfixJson, platform, blockV, designIndex, luaIndex);
            hotfixParser.Languages = languages;

            // Parse metadata
            await hotfixParser.ParseAsbDatasAsync();
            await hotfixParser.ParseLuaDatasAsync();
            await hotfixParser.ParseDesignDatasAsync();

            // Download files
            // Ensure base directories exist
            foreach (var dir in new[] { "Asb", "DesignData", "Lua", "Links" })
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

            // Write link files for reference
            var asbLinksFilePath = Path.Combine("Links", "asbLinks.txt");
            File.WriteAllLines(asbLinksFilePath, hotfixParser.asbLinks.Distinct());
            logger.LogInfo($"Asb links written to {asbLinksFilePath}");

            var luaLinksFilePath = Path.Combine("Links", "luaLinks.txt");
            File.WriteAllLines(luaLinksFilePath, hotfixParser.luaLinks.Distinct());
            logger.LogInfo($"Lua links written to {luaLinksFilePath}");

            var exResourceLinksFilePath = Path.Combine("Links", "exResourceLinks.txt");
            File.WriteAllLines(exResourceLinksFilePath, hotfixParser.exResourceLinks.Distinct());
            logger.LogInfo($"DesignData links written to {exResourceLinksFilePath}");

            // Download using DownloadItem (supports subdirectories)
            var downloader = new Downloader(logger, ".");

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
HSR Data Downloader - Star Rail Resource Downloader

Usage: HSR_Downloader <hotfix.json> [options]

Options:
  --lang, -l <langs>     Filter downloads by language (comma-separated)
                         Examples: --lang cn,en,jp  --lang cn
  --help, -h             Show this help message

Examples:
  HSR_Downloader hotfix.json                     Download all resources
  HSR_Downloader hotfix.json --lang cn,en        Download CN and EN resources only
");
        }
    }
}