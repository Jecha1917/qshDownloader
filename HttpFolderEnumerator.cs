using Microsoft.Extensions.Logging;
using System;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.Collections;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using System.Threading;

namespace qshDownloader
{
    internal class HttpFolderEnumerator : IFolderEnumerator
	{
		private IEnumerable _dirs;
		private ILogger _logger;
		private DownloaderConfig _config;

		/// <summary>
		/// Match
		/// Group[1] = date
		/// </summary>
		private Regex parseDates;

		public HttpFolderEnumerator(ILogger<HttpFolderEnumerator> logger, IOptions<DownloaderConfig> config)
		{
			_logger = logger ?? throw new ArgumentNullException(nameof(logger));
			_config = config?.Value ?? throw new ArgumentNullException(nameof(config));
			parseDates = new Regex(_config.ParseDate);
		}

		public async Task<IEnumerable<string>> Enumerate(Uri url, CancellationToken cancellationToken)
		{

			IEnumerable<string> _dirs = null;

            using var httpClient = new HttpClient();
			var stream = await httpClient.GetStreamAsync(url, cancellationToken);
			using var reader = new StreamReader(stream);
			var data = await reader.ReadToEndAsync();

            var m = parseDates.Matches(data);
            _dirs = m.Select(k => k.Groups[1].Value).ToList();

			if (_dirs?.Count() is null or 0)
			{
				_logger.LogInformation($"\tFiles not found for url:{url.AbsoluteUri}");
			}
			return _dirs;
		}
	}
}
