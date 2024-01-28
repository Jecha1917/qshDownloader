using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Net;
using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Net.Http;
using System.Collections.Concurrent;

namespace qshDownloader
{
	public class Worker : BackgroundService
	{
		private readonly WorkerConfig _config;
        private readonly DownloaderConfig _downloaderConfig;
        private IMemoryCache _cache;
        private readonly ILogger _logger;
        private readonly IEnumerable<IDownloader> _downloaders;
		private readonly IEnumerable<IFolderEnumerator> _folderEnumerators;

		/// <summary>
		/// Match
		/// Group[1] = relative url to fileMatch
		/// Group[2] = fileMatch name
		/// </summary>
		private readonly Regex _parseFileUrls;

		/// <summary>
		/// Match
		/// Group[1] = date
		/// </summary>
		private readonly Regex _parseDates;

		private readonly int _retryCount;
        private readonly int _retryDelay;

        /// <summary>
        /// Service Worker.
        /// </summary>
        /// <param name="logger">logger.</param>
        /// <param name="cache">cache.</param>
        /// <param name="config">worker config.</param>
        /// <param name="downloaderConfig">downloader config.</param>
        /// <param name="folderEnumerators">folder enumerators/</param>
        /// <param name="downloaders">downloaders.</param>
        /// <exception cref="ArgumentNullException">exception.</exception>
        /// <exception cref="InvalidOperationException">exception.</exception>
        public Worker(
			ILogger<Worker> logger,
			IMemoryCache cache,
			IOptions<WorkerConfig> config,
			IOptions<DownloaderConfig> downloaderConfig,
			IEnumerable<IFolderEnumerator> folderEnumerators,
			IEnumerable<IDownloader> downloaders)
		{
			_downloaderConfig = downloaderConfig?.Value ?? throw new ArgumentNullException(nameof(downloaderConfig));
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			this._config = config?.Value ?? throw new ArgumentNullException(nameof(config));
			_parseDates = new Regex(_downloaderConfig.ParseDate);
			_parseFileUrls = new Regex(_downloaderConfig.ParseFileUrls);
			_retryCount = _downloaderConfig.RetryCount > 0 ? _downloaderConfig.RetryCount : 1;
			_retryDelay = _downloaderConfig.RetryDelay > 0 ? _downloaderConfig.RetryDelay : 3000;

            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
			_folderEnumerators = folderEnumerators ?? throw new ArgumentNullException(nameof(folderEnumerators));

			if(folderEnumerators.Count() == 0)
			{
				throw new InvalidOperationException("No folder enumerators are registered.");
			}
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				DateTime startTime = DateTime.Now;
				_logger.LogInformation("Downloader started at: {time}", DateTimeOffset.Now);

				await Task.WhenAll(_config.ServerUrls.Select(async urlServer =>
				{
					IEnumerable<string> availableDays = await _folderEnumerators.First().Enumerate(new Uri(urlServer), CancellationToken.None);

					_logger.LogInformation("Server: {string}", urlServer);
					var url = new Uri(urlServer);
					List<string> dirs;

					if (!_cache.TryGetValue("dirs", out dirs))
					{
						dirs = Directory.EnumerateDirectories(_config.HistoryPath)
							.Where(dir => !dir.EndsWith(".tmp"))
							.Select(dir => dir.Substring(dir.Length - 10))
							.ToList();

						_cache.Set("dirs", dirs);

						await Task.WhenAll(dirs.Select(async dir =>
						{
							var files = await Task.Run(() =>
								Directory.EnumerateFiles($"{_config.HistoryPath}\\{dir}"));

							_cache.Set(dir, files.ToList());
							files.ToList().ForEach(file =>
								_cache.Set(file, FileDownloadStatusType.Success));
						}));
					}

					if (availableDays.Count() > 0)
					{
						IEnumerable<string> newDays = availableDays.Except(dirs);
						_logger.LogInformation("\tFounded {int} new folder to downloading", newDays.Count());

						foreach (var day in newDays)
						{
							_logger.LogInformation("\t\tLoading folder {string}", day);
							var dayPath = $"{_config.HistoryPath}\\{day}";
							var dayPathTmp = $"{_config.HistoryPath}\\{day}.tmp";

							if (Directory.Exists(dayPathTmp))
								Directory.Delete(dayPathTmp, true);

							Directory.CreateDirectory(dayPathTmp);
							var files = new ConcurrentBag<string>();


							using var client = new HttpClient();
							var data = await client.GetStringAsync(new Uri($"{url}/{day}"), stoppingToken);
							var listFiles = _parseFileUrls.Matches(data);

							await Task.WhenAll(listFiles.Select(async fileMatch =>
							{
								string fileName = fileMatch.Groups[2].Value;
								for (int i = 0; i < _retryCount; i++)
									try
									{
										using var response = await client.GetAsync(new Uri(url, fileMatch.Groups[1].Value), stoppingToken);
										if(!response.IsSuccessStatusCode)
										{
											break;
										}

										response.EnsureSuccessStatusCode();

										using var content = response.Content;
										using var fileStream = new FileStream($"{dayPathTmp}\\{fileName}", FileMode.CreateNew);

										await content.CopyToAsync(fileStream, stoppingToken);

										files.Add(fileName);
										_cache.Set(fileName, FileDownloadStatusType.Success);

										break;
									}
									catch (Exception)
									{
										if (i == _retryCount || stoppingToken.IsCancellationRequested)
										{
											throw;
										}

										await Task.Delay(_retryDelay, stoppingToken);
										_logger.LogInformation("\t\tTry N:{int} loading {string}", i, fileName);
									}
							}));

							if (files.Count() > 0)
							{
								Directory.Move(dayPathTmp, dayPath);

								dirs.Add(day);
								_cache.Set(day, files.ToList());
								_logger.LogInformation("\tLoading success to {string}", dayPath);
							}
							else
							{
                                _logger.LogInformation("\t\tFolder {string} not loaded.", day);
                                Directory.Delete(dayPathTmp);
                            }
						}
					}
				}));
				_logger.LogInformation("\tSleep...");

                await Task.Delay(_config.HoursInterval * 24 * 3600000 - (int)(DateTime.Now.TimeOfDay.TotalMilliseconds - startTime.TimeOfDay.TotalMilliseconds), stoppingToken);
			}
		}
	}
}
