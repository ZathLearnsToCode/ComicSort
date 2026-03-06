using ComicSort.Engine.Services;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ComicSort.UI.Services;

public sealed class ComicLibraryRenameWatcherService : IHostedService, IDisposable
{
    private static readonly HashSet<string> SupportedArchiveExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cbr",
        ".cbz",
        ".cb7"
    };

    private readonly ISettingsService _settingsService;
    private readonly IComicDatabaseService _comicDatabaseService;
    private readonly IScanRepository _scanRepository;
    private readonly SemaphoreSlim _renameLock = new(1, 1);
    private readonly object _watchersLock = new();
    private readonly ConcurrentDictionary<string, byte> _pendingWatcherSyncs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, FileSystemWatcher> _watchers = new(StringComparer.OrdinalIgnoreCase);

    private bool _isStarted;
    private bool _disposed;

    public ComicLibraryRenameWatcherService(
        ISettingsService settingsService,
        IComicDatabaseService comicDatabaseService,
        IScanRepository scanRepository)
    {
        _settingsService = settingsService;
        _comicDatabaseService = comicDatabaseService;
        _scanRepository = scanRepository;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_isStarted)
        {
            return;
        }

        await _settingsService.InitializeAsync(cancellationToken);
        await _comicDatabaseService.InitializeAsync(cancellationToken);

        _settingsService.SettingsChanged += OnSettingsChanged;
        SyncWatchers();
        _isStarted = true;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _settingsService.SettingsChanged -= OnSettingsChanged;

        lock (_watchersLock)
        {
            foreach (var watcher in _watchers.Values)
            {
                watcher.Renamed -= OnFolderRenamed;
                watcher.Dispose();
            }

            _watchers.Clear();
        }

        _isStarted = false;
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _settingsService.SettingsChanged -= OnSettingsChanged;
        _renameLock.Dispose();

        lock (_watchersLock)
        {
            foreach (var watcher in _watchers.Values)
            {
                watcher.Renamed -= OnFolderRenamed;
                watcher.Dispose();
            }

            _watchers.Clear();
        }
    }

    private void OnSettingsChanged(object? sender, EventArgs eventArgs)
    {
        _ = Task.Run(() =>
        {
            SyncWatchers();
        });
    }

    private void SyncWatchers()
    {
        var watchedFolders = _settingsService.CurrentSettings.LibraryFolders
            .Where(x => x.Watched && !string.IsNullOrWhiteSpace(x.Folder))
            .Select(x => NormalizeDirectoryPath(x.Folder))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        lock (_watchersLock)
        {
            var foldersToRemove = _watchers.Keys
                .Where(existing => !watchedFolders.Contains(existing))
                .ToArray();

            foreach (var folder in foldersToRemove)
            {
                if (!_watchers.Remove(folder, out var watcher))
                {
                    continue;
                }

                watcher.Renamed -= OnFolderRenamed;
                watcher.Dispose();
            }

            foreach (var folder in watchedFolders)
            {
                if (_watchers.ContainsKey(folder))
                {
                    continue;
                }

                if (!Directory.Exists(folder))
                {
                    continue;
                }

                var watcher = new FileSystemWatcher(folder)
                {
                    IncludeSubdirectories = true,
                    NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName,
                    EnableRaisingEvents = true
                };

                watcher.Renamed += OnFolderRenamed;
                _watchers[folder] = watcher;
            }
        }
    }

    private void OnFolderRenamed(object sender, RenamedEventArgs eventArgs)
    {
        if (!_isStarted || _disposed)
        {
            return;
        }

        _ = HandleRenameAsync(eventArgs);
    }

    private async Task HandleRenameAsync(RenamedEventArgs eventArgs)
    {
        if (string.IsNullOrWhiteSpace(eventArgs.OldFullPath) || string.IsNullOrWhiteSpace(eventArgs.FullPath))
        {
            return;
        }

        var oldPath = NormalizeFilePath(eventArgs.OldFullPath);
        var newPath = NormalizeFilePath(eventArgs.FullPath);
        if (string.IsNullOrWhiteSpace(oldPath) || string.IsNullOrWhiteSpace(newPath))
        {
            return;
        }

        var eventKey = $"{oldPath}|{newPath}";
        if (!_pendingWatcherSyncs.TryAdd(eventKey, 0))
        {
            return;
        }

        await _renameLock.WaitAsync();
        try
        {
            if (Directory.Exists(newPath) && !File.Exists(newPath))
            {
                await _scanRepository.RewritePathsForDirectoryRenameAsync(oldPath, newPath);
                return;
            }

            if (!File.Exists(newPath))
            {
                return;
            }

            var oldExtension = Path.GetExtension(oldPath);
            var newExtension = Path.GetExtension(newPath);
            if (!string.Equals(oldExtension, newExtension, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            if (!SupportedArchiveExtensions.Contains(newExtension))
            {
                return;
            }

            await _scanRepository.RewritePathForFileRenameAsync(oldPath, newPath);
        }
        catch
        {
            // Swallow watcher exceptions so file system events cannot crash the app host.
        }
        finally
        {
            _pendingWatcherSyncs.TryRemove(eventKey, out _);
            _renameLock.Release();
        }
    }

    private static string NormalizeDirectoryPath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(path).Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch
        {
            return path.Trim().TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
    }

    private static string NormalizeFilePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(path).Trim();
        }
        catch
        {
            return path.Trim();
        }
    }
}
