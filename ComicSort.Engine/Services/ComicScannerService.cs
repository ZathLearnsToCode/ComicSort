using System;
using System.Collections.Generic;
using System.Text;

namespace ComicSort.Engine.Services
{
    public sealed class ComicScannerService
    {
        private static readonly HashSet<string> ComicExtensions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".cbr", ".cbz", ".pdf", ".cb7", ".webp"
        };

        public IEnumerable<string> ScanFolder(string folderPath, bool recursive = true)
        {
            if (string.IsNullOrWhiteSpace(folderPath))
                yield break;

            if (!Directory.Exists(folderPath))
                yield break;

            var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;

            IEnumerable<string> files;
            try
            {
                files = Directory.EnumerateFiles(folderPath, "*", option);
            }
            catch
            {
                yield break; // Phase 1: fail silently on inaccessible folders
            }

            foreach (var file in files)
            {
                var ext = Path.GetExtension(file);
                if (ComicExtensions.Contains(ext))
                    yield return file;
            }
        }
    }
}
