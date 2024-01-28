using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

namespace qshDownloader
{
    /// <summary>
    /// Folder enumerator for server.
    /// </summary>
    public interface IFolderEnumerator
    {
		Task<IEnumerable<string>> Enumerate(Uri url, CancellationToken cancellationToken);
    }
}
