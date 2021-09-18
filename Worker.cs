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

namespace qshDownloader
{
	public class WorkerConfig
	{
		public string HistoryPath { get; set; }
		public string[] UrlQshServers { get; set; }
		public string ParseQshUrls { get; set; }
		public string ParseDate { get; set; }
		public int HoursInterval { get; set; }
	}

	public class Worker : BackgroundService
	{
		private readonly IConfiguration _configuration;
		private WorkerConfig config;
		private IMemoryCache _cache;

		/// <summary>
		/// Match
		/// Group[1] = relative url to file
		/// Group[2] = file name
		/// </summary>
		private Regex parseQshUrls;

		/// <summary>
		/// Match
		/// Group[1] = date
		/// </summary>
		private Regex parseDates;

		private readonly ILogger<Worker> _logger;

		public Worker(ILogger<Worker> logger, IConfiguration configuration, IMemoryCache cache)
		{
			_logger = logger;
			_configuration = configuration;

			config = _configuration.GetSection("WorkerConfig").Get<WorkerConfig>();

			parseDates = new Regex(config.ParseDate);
			parseQshUrls = new Regex(config.ParseQshUrls);
			_cache = cache;
		}

		protected override async Task ExecuteAsync(CancellationToken stoppingToken)
		{
			while (!stoppingToken.IsCancellationRequested)
			{
				DateTime startTime = DateTime.Now;
				_logger.LogInformation("Загрузчик запущен: {time}", DateTimeOffset.Now);
				foreach (var urlQshServer in config.UrlQshServers)
				{
					_logger.LogInformation("Сервер: {string}", urlQshServer);
					var url = new Uri(urlQshServer);
					List<string> dirs;

					if (!_cache.TryGetValue("dirs", out dirs))
					{
						dirs = Directory.EnumerateDirectories(config.HistoryPath).Where(k => !k.EndsWith(".tmp")).Select(k => k.Substring(k.Length - 10)).ToList();
						_cache.Set("dirs", dirs);

						dirs.AsParallel().ForAll(k =>
							{
								var files = Directory.EnumerateFiles($"{config.HistoryPath}\\{k}");
								_cache.Set(k, files.ToList());
								files.AsParallel().ForAll(k => _cache.Set(k, (int?)1));
							});

					}

					IEnumerable<string> availableDays = null;

					using (var stream = WebRequest.Create(url).GetResponse().GetResponseStream())
					{
						using (var streamReader = new StreamReader(stream))
						{
							var data = await streamReader.ReadToEndAsync();
							var m = parseDates.Matches(data);
							availableDays = m.Select(k => k.Groups[1].Value).ToList();
						}
					}

					if (availableDays?.Count() > 0)
					{
						IEnumerable<string> newDays = availableDays.Except(dirs);
						_logger.LogInformation("\tНайдено {int} новых дней к загрузке", newDays.Count());

						foreach (var day in newDays)
						{
							_logger.LogInformation("\t\tЗагрузка дня {string}", day);
							var dayPath = $"{config.HistoryPath}\\{day}";
							var dayPathTmp = $"{config.HistoryPath}\\{day}.tmp";

							if (Directory.Exists(dayPathTmp))
								Directory.Delete(dayPathTmp, true);

							Directory.CreateDirectory(dayPathTmp);
							List<string> files = new List<string>();

							using (var stream = WebRequest.Create(new Uri(baseUri: url, day)).GetResponse().GetResponseStream())
							{
								using (var streamReader = new StreamReader(stream))
								{
									var data = await streamReader.ReadToEndAsync();
									var listFiles = parseQshUrls.Matches(data);

									listFiles.AsParallel().ForAll(k =>
									{
										WebClient webClient = new WebClient() { BaseAddress = url.OriginalString };
										string fileName = k.Groups[2].Value;
										for (int i = 0; i < 10; i++)
											try
											{
												webClient.DownloadFile(new Uri(url, k.Groups[1].Value), $"{dayPathTmp}\\{fileName}");

												files.Add(fileName);
												_cache.Set(fileName, (int?)1 );

												break;
											}
											catch (Exception ex)
											{
												if (i == 10)
													throw ex;

												Task.Delay(3000, stoppingToken);
												_logger.LogInformation("\t\tПопытка №{int} загрузки {string}", i, fileName);
											}
									});
								}
							}

							Directory.Move(dayPathTmp, dayPath);

							dirs.Add(day);
							_cache.Set(day, files);
							_logger.LogInformation("\tУспешно загружено в {string}", dayPath);
						}
					}
					else
					{
						_logger.LogInformation("\tНет новых данных.");
					}
				}

				await Task.Delay(config.HoursInterval * 24 * 3600000 - (int)(DateTime.Now.TimeOfDay.TotalMilliseconds - startTime.TimeOfDay.TotalMilliseconds), stoppingToken);
			}
		}
	}
}
