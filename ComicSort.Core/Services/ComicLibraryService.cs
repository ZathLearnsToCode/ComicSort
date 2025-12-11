using ComicSort.Core.Services.Repositories;
using System;
using System.Collections.Generic;
using System.Text;

namespace ComicSort.Core.Services
{
    public class ComicLibraryService : IComicLibraryService
    {
        private readonly ISettingsServices _settings;
        private readonly IComicScanner _scanner;
        private readonly IComicRepository _repository;

        public ComicLibraryService(
            ISettingsServices settings,
            IComicScanner scanner,
            IComicRepository repository)
        {
            _settings = settings;
            _scanner = scanner;
            _repository = repository;
        }

        public async Task<int> ScanAndSaveAsync(
            IProgress<int>? progress = null,
            CancellationToken cancellationToken = default)
        {
            var folders = _settings.Settings.ComicFolders.ToList();
            if (folders.Count == 0)
                return 0;

            // Step 1 — Scan filesystem
            var scanned = await _scanner.ScanFoldersAsync(folders, progress, cancellationToken);

            if (scanned.Count == 0)
                return 0;

            // Step 2 — Save results in database
            await _repository.AddComicsAsync(scanned);

            return scanned.Count;
        }
    }
}
