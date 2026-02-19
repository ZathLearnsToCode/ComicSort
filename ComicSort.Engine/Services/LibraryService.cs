using ComicSort.Engine.Models;
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;

namespace ComicSort.Engine.Services
{
    public sealed class LibraryService
    {
        private readonly ComicScannerService _scanner = new();
        private readonly LibraryStorageService _storage = new();

        private readonly List<ComicBook> _books = new();

        public IReadOnlyList<ComicBook> Books => _books;

        public async Task LoadAsync(string libraryJsonPath)
        {
            _books.Clear();
            _books.AddRange(await _storage.LoadAsync(libraryJsonPath));
        }

        public async Task SaveAsync(string libraryJsonPath)
        {
            await _storage.SaveAsync(libraryJsonPath, _books);
        }
             

        public async Task<int> RemoveMissingAsync(string libraryJsonPath)
        {
            int before = _books.Count;
            _books.RemoveAll(b => !File.Exists(b.FilePath));
            int removed = before - _books.Count;

            await SaveAsync(libraryJsonPath);
            return removed;
        }

        public async Task<int> ScanAndAddAsync(
            string folderPath,
            string libraryJsonPath,
            IProgress<ScanProgress>? progress,
            CancellationToken ct)
        {
            var existingPaths = new HashSet<string>(_books.Select(b => b.FilePath), StringComparer.OrdinalIgnoreCase);

            int added = 0;
            int processed = 0;

            foreach (var file in _scanner.ScanFolder(folderPath, recursive: true))
            {
                ct.ThrowIfCancellationRequested(); // ✅ cancellation point

                processed++;

                progress?.Report(new ScanProgress
                {
                    Processed = processed,
                    Added = added,
                    CurrentFile = file
                });

                if (existingPaths.Contains(file))
                    continue;

                long size;
                try { size = new FileInfo(file).Length; }
                catch { continue; }

                _books.Add(new ComicBook
                {
                    FilePath = file,
                    FileSize = size,
                    AddedOn = DateTime.UtcNow
                });

                existingPaths.Add(file);
                added++;
            }

            await SaveAsync(libraryJsonPath);

            progress?.Report(new ScanProgress
            {
                Processed = processed,
                Added = added,
                CurrentFile = null
            });

            return added;
        }
    }
}
