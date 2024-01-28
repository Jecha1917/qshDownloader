using Microsoft.Extensions.Configuration;

namespace qshDownloader
{
	public class WorkerConfig
	{
		public string HistoryPath { get; set; }
		public int HoursInterval { get; set; }
		public string[] ServerUrls { get; set; }
	}

	public class DownloaderConfig
	{
		public int RetryCount { get; set; }
		public int RetryDelay { get; set; }
		public int MaxParallelThreads { get; set; }
		public string ParseFileUrls { get; set; }
		public string ParseDate { get; set; }
	}
}



