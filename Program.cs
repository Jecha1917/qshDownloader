using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace qshDownloader
{
	public class Program
	{
		public static void Main(string[] args)
		{
			CreateHostBuilder(args).Build().Run();
		}

		public static IHostBuilder CreateHostBuilder(string[] args) =>
			Host.CreateDefaultBuilder(args)
				.ConfigureServices((hostContext, services) =>
				{
					IConfiguration config = hostContext.Configuration;

					services.AddOptions();
					services.AddHostedService<Worker>();
					services.AddTransient<IDownloader, Downloader>();
					services.AddTransient<IFolderEnumerator, HttpFolderEnumerator>();

					services.AddMemoryCache();
					services.Configure<WorkerConfig>(config.GetSection("WorkerConfig"));
					services.Configure<DownloaderConfig>(config.GetSection("DownloaderConfig"));
				});
	}
}
