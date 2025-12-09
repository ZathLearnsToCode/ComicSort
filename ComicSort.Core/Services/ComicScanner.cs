using ComicSort.Core.DTO;
using System;
using System.Collections.Generic;
using System.Text;

namespace ComicSort.Core.Services
{
    public class ComicScanner : IComicScanner
    {
        private static readonly string[] Extensions = { ".cbr", ".cbz" };
        public Task<List<ComicBookDTO>> ScanFoldersAsync(
            IEnumerable<string> folderPaths,
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default)
        {
            return Task.Run(() =>
            {
                var results = new List<ComicBookDTO>();
                int processed = 0;

                foreach (var folder in folderPaths)
                {
                    if (!Directory.Exists(folder))
                        continue;

                    foreach (var file in Directory.EnumerateFiles(folder, "*.*", SearchOption.AllDirectories))
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        var ext = Path.GetExtension(file);
                        if (!Extensions.Contains(ext, StringComparer.OrdinalIgnoreCase))
                            continue;

                        var info = new FileInfo(file);

                        results.Add(new ComicBookDTO
                        {
                            Id = Guid.NewGuid(),
                            FilePath = file,
                            FileSize = info.Length,
                            CreationDate = info.CreationTimeUtc,
                            ModifiedDate = info.LastWriteTimeUtc,
                            DateAdded = DateTime.UtcNow
                        });

                        processed++;
                        progress?.Report(processed);
                    }
                }

                return results;
            }, cancellationToken);
        }
    }
}

