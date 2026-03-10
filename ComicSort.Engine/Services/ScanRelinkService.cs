using ComicSort.Engine.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Globalization;

namespace ComicSort.Engine.Services;

public sealed class ScanRelinkService : IScanRelinkService
{
    private readonly object _stateLock = new();
    private readonly IScanRepository _scanRepository;
    private readonly IScanPathService _scanPathService;
    private readonly IScanLookupCacheService _lookupCacheService;
    private readonly ILogger<ScanRelinkService> _logger;
    private readonly ConcurrentDictionary<string, byte> _seenPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ComicFileLookup> _initialLookupByPath = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<ComicFileLookup>> _relinkCandidatesByNameSize = new(StringComparer.Ordinal);
    private readonly HashSet<string> _initialPathsAtStart = new(StringComparer.OrdinalIgnoreCase);

    private string[] _activeScanRootPrefixes = [];

    public ScanRelinkService(
        IScanRepository scanRepository,
        IScanPathService scanPathService,
        IScanLookupCacheService lookupCacheService,
        ILogger<ScanRelinkService> logger)
    {
        _scanRepository = scanRepository;
        _scanPathService = scanPathService;
        _lookupCacheService = lookupCacheService;
        _logger = logger;
    }

    public bool IsEnabled { get; private set; }

    public void Reset()
    {
        IsEnabled = false;
        _activeScanRootPrefixes = [];
        _seenPaths.Clear();
        lock (_stateLock)
        {
            _initialLookupByPath.Clear();
            _relinkCandidatesByNameSize.Clear();
            _initialPathsAtStart.Clear();
        }
    }

    public async Task InitializeAsync(
        bool enabled,
        IReadOnlyList<string> libraryFolders,
        CancellationToken cancellationToken)
    {
        IsEnabled = enabled;
        _activeScanRootPrefixes = _scanPathService.BuildScanRootPrefixes(libraryFolders);
        if (!enabled)
        {
            return;
        }

        var existingLookups = await _scanRepository.GetAllLookupsAsync(cancellationToken);
        InitializeSnapshot(existingLookups);
    }

    public IReadOnlyDictionary<string, ComicFileLookup> GetSnapshotLookups(IReadOnlyList<ScanFileWorkItem> prefetchBatch)
    {
        if (!IsEnabled)
        {
            return new Dictionary<string, ComicFileLookup>(StringComparer.OrdinalIgnoreCase);
        }

        var rows = new Dictionary<string, ComicFileLookup>(StringComparer.OrdinalIgnoreCase);
        lock (_stateLock)
        {
            foreach (var workItem in prefetchBatch)
            {
                if (_initialLookupByPath.TryGetValue(workItem.NormalizedPath, out var lookup))
                {
                    rows[workItem.NormalizedPath] = lookup;
                }
            }
        }

        return rows;
    }

    public void MarkSeenPath(string normalizedPath)
    {
        if (IsEnabled)
        {
            _seenPaths.TryAdd(normalizedPath, 0);
        }
    }

    public void ApplyUpsert(ComicFileLookup lookup)
    {
        if (!IsEnabled)
        {
            return;
        }

        lock (_stateLock)
        {
            _initialLookupByPath[lookup.NormalizedPath] = lookup;
        }
    }

    public async Task<ScanRelinkResult?> TryRelinkByFileNameSizeAsync(
        FileInfo fileInfo,
        string normalizedPath,
        CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return null;
        }

        var key = BuildRelinkKey(fileInfo.Name, fileInfo.Length);
        var candidates = GetCandidatesSnapshot(key);

        foreach (var candidate in candidates)
        {
            if (ShouldSkipCandidate(candidate, normalizedPath))
            {
                continue;
            }

            if (!await TryRewritePathAsync(candidate.NormalizedPath, normalizedPath, cancellationToken))
            {
                continue;
            }

            return ApplyRelinkResult(key, candidate, normalizedPath, fileInfo);
        }

        return null;
    }

    public async Task<IReadOnlyList<string>> RemoveMissingFilesAsync(CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            return [];
        }

        var missingPaths = GetMissingPaths();
        if (missingPaths.Length == 0)
        {
            return [];
        }

        var removedPaths = await _scanRepository.DeleteByNormalizedPathsAsync(missingPaths, cancellationToken);
        foreach (var path in removedPaths)
        {
            _lookupCacheService.RemoveCachedLookup(path);
        }

        return removedPaths;
    }

    private List<ComicFileLookup> GetCandidatesSnapshot(string key)
    {
        lock (_stateLock)
        {
            return _relinkCandidatesByNameSize.TryGetValue(key, out var candidates)
                ? candidates.ToList()
                : [];
        }
    }

    private bool ShouldSkipCandidate(ComicFileLookup candidate, string normalizedPath)
    {
        if (string.Equals(candidate.NormalizedPath, normalizedPath, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (_seenPaths.ContainsKey(candidate.NormalizedPath))
        {
            return true;
        }

        return File.Exists(candidate.NormalizedPath);
    }

    private async Task<bool> TryRewritePathAsync(
        string oldPath,
        string newPath,
        CancellationToken cancellationToken)
    {
        return await _scanRepository.RewritePathForFileRenameAsync(oldPath, newPath, cancellationToken);
    }

    private ScanRelinkResult ApplyRelinkResult(
        string key,
        ComicFileLookup candidate,
        string normalizedPath,
        FileInfo fileInfo)
    {
        var updatedLookup = new ComicFileLookup
        {
            NormalizedPath = normalizedPath,
            FileName = fileInfo.Name,
            SizeBytes = fileInfo.Length,
            Fingerprint = candidate.Fingerprint,
            HasThumbnail = candidate.HasThumbnail,
            ThumbnailPath = candidate.ThumbnailPath,
            HasComicInfo = candidate.HasComicInfo
        };

        lock (_stateLock)
        {
            RemoveRelinkCandidate(key, candidate.NormalizedPath);
            _initialLookupByPath.Remove(candidate.NormalizedPath);
            _initialPathsAtStart.Remove(candidate.NormalizedPath);
            _initialLookupByPath[normalizedPath] = updatedLookup;
        }

        _lookupCacheService.RemoveCachedLookup(candidate.NormalizedPath);
        _lookupCacheService.CacheLookup(updatedLookup);
        _logger.LogInformation("Relinked missing path {OldPath} to {NewPath}", candidate.NormalizedPath, normalizedPath);
        return new ScanRelinkResult(updatedLookup, candidate.NormalizedPath);
    }

    private void RemoveRelinkCandidate(string key, string normalizedPath)
    {
        if (_relinkCandidatesByNameSize.TryGetValue(key, out var candidates))
        {
            candidates.RemoveAll(candidate =>
                string.Equals(candidate.NormalizedPath, normalizedPath, StringComparison.OrdinalIgnoreCase));
        }
    }

    private string[] GetMissingPaths()
    {
        lock (_stateLock)
        {
            return _initialPathsAtStart
                .Where(path => _scanPathService.IsPathInRoots(path, _activeScanRootPrefixes))
                .Where(path => !_seenPaths.ContainsKey(path))
                .ToArray();
        }
    }

    private void InitializeSnapshot(IReadOnlyList<ComicFileLookup> existingLookups)
    {
        lock (_stateLock)
        {
            _initialLookupByPath.Clear();
            _relinkCandidatesByNameSize.Clear();
            _initialPathsAtStart.Clear();

            foreach (var lookup in existingLookups)
            {
                _initialLookupByPath[lookup.NormalizedPath] = lookup;
                _initialPathsAtStart.Add(lookup.NormalizedPath);
                var relinkKey = BuildRelinkKey(lookup.FileName, lookup.SizeBytes);
                AddRelinkCandidate(relinkKey, lookup);
            }
        }
    }

    private void AddRelinkCandidate(string key, ComicFileLookup lookup)
    {
        if (!_relinkCandidatesByNameSize.TryGetValue(key, out var candidates))
        {
            candidates = [];
            _relinkCandidatesByNameSize[key] = candidates;
        }

        candidates.Add(lookup);
    }

    private static string BuildRelinkKey(string fileName, long fileSize)
    {
        return string.Concat(fileName.Trim(), "|", fileSize.ToString(CultureInfo.InvariantCulture));
    }
}
