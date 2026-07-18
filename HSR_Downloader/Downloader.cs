using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace HSR_DataDownloader;

public class Downloader
{
    private static readonly HttpClient client = new HttpClient()
    {
        Timeout = TimeSpan.FromMinutes(5)
    };
    private readonly Logger logger;
    private readonly string destinationPath;
    private const int MaxRetries = 3;

    public Downloader(Logger logger, string destinationPath)
    {
        this.logger = logger;
        this.destinationPath = destinationPath;
    }

    public async Task DownloadFilesAsync(string[] urls)
    {
        var items = urls.Select(u => new DownloadItem(u, destinationPath)).ToArray();
        await DownloadItemsAsync(items);
    }

    public async Task DownloadItemsAsync(DownloadItem[] items)
    {
        var semaphore = new SemaphoreSlim(5); // limit concurrent downloads
        var tasks = new List<Task>();

        foreach (var item in items)
        {
            await semaphore.WaitAsync();
            tasks.Add(Task.Run(async () =>
            {
                try { await DownloadWithRetryAsync(item); }
                finally { semaphore.Release(); }
            }));
        }

        await Task.WhenAll(tasks);
    }

    private async Task DownloadWithRetryAsync(DownloadItem item)
    {
        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                await DownloadItemAsync(item);
                return; // success
            }
            catch (Exception ex)
            {
                if (attempt < MaxRetries)
                {
                    logger.LogWarning($"Attempt {attempt}/{MaxRetries} failed for {item.FileName}: {ex.Message}. Retrying...");
                    await Task.Delay(TimeSpan.FromSeconds(attempt * 2));
                }
                else
                {
                    logger.LogError($"Failed to download {item.FileName} after {MaxRetries} attempts: {ex.Message}");
                }
            }
        }
    }

    private async Task DownloadItemAsync(DownloadItem item)
    {
        var dir = item.SubDirectory;
        if (!Path.IsPathRooted(dir))
            dir = Path.Combine(Directory.GetCurrentDirectory(), dir);

        if (!Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        var filePath = Path.Combine(dir, item.FileName);

        // Check if file exists and has content
        if (File.Exists(filePath))
        {
            var existingInfo = new FileInfo(filePath);
            if (existingInfo.Length > 0)
            {
                logger.LogSuccess($"Skipping {item.FileName} (exists, {existingInfo.Length / 1024.0:F1} KB)", false);
                return;
            }
            // Delete empty/corrupt file
            File.Delete(filePath);
        }

        using var response = await client.GetAsync(item.Url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        var expectedSize = response.Content.Headers.ContentLength;

        await using var stream = await response.Content.ReadAsStreamAsync();
        await using var fileStream = File.Create(filePath);
        await stream.CopyToAsync(fileStream);
        await fileStream.FlushAsync();
        fileStream.Close();

        // Verify file size
        var downloadedInfo = new FileInfo(filePath);
        if (expectedSize.HasValue && downloadedInfo.Length != expectedSize.Value)
        {
            File.Delete(filePath);
            throw new Exception($"Size mismatch: expected {expectedSize.Value} bytes, got {downloadedInfo.Length} bytes");
        }

        if (downloadedInfo.Length == 0)
        {
            File.Delete(filePath);
            throw new Exception("Downloaded file is empty");
        }

        logger.LogSuccess($"Downloaded {item.FileName} ({downloadedInfo.Length / 1024.0:F1} KB)", false);
    }

    private string GetFileNameFromUrl(string url)
    {
        return new Uri(url).Segments.Last();
    }
}