using System;
using System.Collections.Generic;
using System.Text;

namespace ComicSort.Core.Services
{
    public interface IComicLibraryService
    {
        Task<int> ScanAndSaveAsync(
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default);
    }
}
