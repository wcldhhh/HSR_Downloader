using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace HSR_DataDownloader;

public class Downloader
{
	private static readonly HttpClient client = new HttpClient()
	{
		Timeout = Timeout.InfiniteTimeSpan
	};
	private readonly Logger logger;
	private readonly string destinationPath;

	public Downloader(Logger logger, string destinationPath)
	{
		this.logger = logger;
		this.destinationPath = destinationPath;
	}

	public async Task DownloadFilesAsync(string[] urls)
	{
		var semaphore = new SemaphoreSlim(3); // only 3 downloads at once
		var tasks = new List<Task>();

		foreach (var url in urls)
		{
			await semaphore.WaitAsync();
			tasks.Add(Task.Run(async () =>
			{
				try { await DownloadFileAsync(url); }
				finally { semaphore.Release(); }
			}));
		}

		await Task.WhenAll(tasks);
	}

	private async Task DownloadFileAsync(string url)
	{
		try
		{
			var fileName = GetFileNameFromUrl(url);
			var filePath = Path.Combine(destinationPath, fileName);

			using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
			response.EnsureSuccessStatusCode();

			await using var stream = await response.Content.ReadAsStreamAsync();
			await using var fileStream = File.Create(filePath);
			await stream.CopyToAsync(fileStream);

			logger.LogSuccess($"Downloaded {fileName}", false);
		}
		catch (Exception ex)
		{
			logger.LogWarning($"Error downloading {url}: {ex.Message}");
		}
	}

	private string GetFileNameFromUrl(string url)
	{
		return new Uri(url).Segments.Last();
	}
}