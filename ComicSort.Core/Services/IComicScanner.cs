using ComicSort.Core.DTO;
using System;
using System.Collections.Generic;
using System.Text;

namespace ComicSort.Core.Services
{
    public interface IComicScanner
    {
        Task<List<ComicBookDTO>> ScanFoldersAsync(
            IEnumerable<string> folderPaths,
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default);
    }
}
