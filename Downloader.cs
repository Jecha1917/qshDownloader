using Microsoft.Extensions.Logging;
using System;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using System.Net;

namespace qshDownloader
{

    internal class Downloader : IDownloader
	{
		private readonly ILogger _logger;
		private readonly DownloaderConfig _config;

		/// <summary>
		/// Match
		/// Group[1] = relative url to file
		/// Group[2] = file name
		/// </summary>
		private readonly Regex _parseFileUrls;

		/// <summary>
		/// Match
		/// Group[1] = date
		/// </summary>
		private readonly Regex _parseDates;

		public Downloader(ILogger<Downloader> logger, IOptions<DownloaderConfig> config)
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			_config = config?.Value ?? throw new ArgumentNullException(nameof(config));

			if (string.IsNullOrWhiteSpace(_config.ParseDate))
			{
				throw new InvalidOperationException($"Configuration not setted for {nameof(_config.ParseDate)}");
			}

			if (string.IsNullOrWhiteSpace(_config.ParseFileUrls))
			{
                throw new InvalidOperationException($"Configuration not setted for {nameof(_config.ParseFileUrls)}");
            }

            _parseDates = new Regex(_config.ParseDate);
            _parseFileUrls = new Regex(_config.ParseFileUrls);
        }

		public void Download()
		{
			throw new NotImplementedException();
		}
	}
}
