using Avalonia.Threading;
using Avalonia.Media.Imaging;
using ComicSort.Engine.Models;
using ComicSort.Engine.Services;
using ComicSort.Engine.Settings;
using ComicSort.UI.Models;
using ComicSort.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ComicSort.UI.ViewModels.Controls;

public sealed partial class ComicGridViewModel : ViewModelBase
{
    private const int InitialLoadSize = 200;
    private const string ArrangeByNotSorted = "Not Sorted";
    private const string ArrangeBySeries = "Series";
    private const string ArrangeByPosition = "Position";
    private const string ArrangeByFilePath = "File Path";
    private static readonly Regex IssueNumberRegex = new(@"#\s*(?<num>\d+(?:\.\d+)?)", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex AnyNumberRegex = new(@"(?<num>\d+(?:\.\d+)?)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly IComicDatabaseService _comicDatabaseService;
    private readonly IScanRepository _scanRepository;
    private readonly ISmartListExecutionService _smartListExecutionService;
    private readonly ISmartListEvaluator _smartListEvaluator;
    private readonly ISmartListExpressionService _smartListExpressionService;
    private readonly IComicMetadataService _comicMetadataService;
    private readonly IComicConversionService _comicConversionService;
    private readonly IDialogService _dialogService;
    private readonly ISettingsService _settingsService;
    private readonly Dictionary<string, ComicTileModel> _itemIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly List<string> _activeGrouping = ["Not Grouped"];
    private readonly object _savedItemsLock = new();
    private readonly List<ComicLibraryItem> _pendingSavedItems = [];
    private readonly HashSet<string> _pendingRemovedPaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _thumbnailCacheLock = new();
    private readonly Dictionary<string, Bitmap> _thumbnailBitmapCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<string> _thumbnailCacheOrder = new();
    private readonly Dictionary<string, LinkedListNode<string>> _thumbnailCacheNodes = new(StringComparer.OrdinalIgnoreCase);
    private int _initialized;
    private bool _smartListFilterActive;
    private MatcherGroupNode? _activeSmartListExpression;
    private string _activeSmartListName = "All Comics";
    private string _activeArrangement = ArrangeByNotSorted;
    private int _infoRequestVersion;
    private bool _savedItemsDrainScheduled;

    private const int SavedItemsUiBatchDelayMs = 150;
    private const int ThumbnailCacheCapacity = 512;

    public ComicGridViewModel(
        IComicDatabaseService comicDatabaseService,
        IScanRepository scanRepository,
        ISmartListExecutionService smartListExecutionService,
        ISmartListEvaluator smartListEvaluator,
        ISmartListExpressionService smartListExpressionService,
        IComicMetadataService comicMetadataService,
        IComicConversionService comicConversionService,
        IDialogService dialogService,
        ISettingsService settingsService,
        IScanService scanService)
    {
        _comicDatabaseService = comicDatabaseService;
        _scanRepository = scanRepository;
        _smartListExecutionService = smartListExecutionService;
        _smartListEvaluator = smartListEvaluator;
        _smartListExpressionService = smartListExpressionService;
        _comicMetadataService = comicMetadataService;
        _comicConversionService = comicConversionService;
        _dialogService = dialogService;
        _settingsService = settingsService;
        scanService.ComicFileSaved += OnComicFileSaved;
        scanService.ComicFileRemoved += OnComicFileRemoved;
    }

    public ObservableCollection<ComicTileModel> Items { get; } = [];
    public ObservableCollection<ComicGroupModel> Groups { get; } = [];
    public ObservableCollection<ComicTileModel> SelectedItems { get; } = [];

    [ObservableProperty]
    private ComicTileModel? selectedItem;

    [ObservableProperty]
    private bool isGroupedView;

    [ObservableProperty]
    private string filterSummary = "Filter: All Comics";

    [ObservableProperty]
    private bool isConvertingToCbz;

    [ObservableProperty]
    private bool isDeletingFromLibrary;

    [ObservableProperty]
    private bool isInfoPanelOpen;

    [ObservableProperty]
    private bool isInfoLoading;

    [ObservableProperty]
    private bool hasInfoError;

    [ObservableProperty]
    private string infoError = string.Empty;

    [ObservableProperty]
    private ComicInfoPanelModel? infoPanel;

    public bool IsFlatView => !IsGroupedView;

    public event EventHandler<ComicTileModel?>? SelectedItemChanged;
    public event EventHandler<string>? FilterSummaryChanged;

    public async Task InitializeAsync()
    {
        if (Interlocked.Exchange(ref _initialized, 1) == 1)
        {
            return;
        }

        await ReloadAsync();
    }

    public async Task ReloadAsync()
    {
        await _loadLock.WaitAsync();
        try
        {
            const int maxAttempts = 4;
            for (var attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    await LoadInitialAsync();

                    // Retry briefly if startup ordering returns no rows while DB initialization is still settling.
                    if (Items.Count > 0 || attempt == maxAttempts)
                    {
                        return;
                    }
                }
                catch
                {
                    if (attempt == maxAttempts)
                    {
                        Interlocked.Exchange(ref _initialized, 0);
                        return;
                    }
                }

                await Task.Delay(250);
            }
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private async Task LoadInitialAsync()
    {
        await _comicDatabaseService.InitializeAsync();
        if (_smartListFilterActive && _activeSmartListExpression is not null)
        {
            var smartResult = await _smartListExecutionService.ExecuteAsync(
                _activeSmartListExpression,
                InitialLoadSize);
            await PopulateItemsAsync(smartResult.Items);
            SetFilterSummary($"Filter: {_activeSmartListName} ({smartResult.LoadedCount})");
            return;
        }

        var items = await _scanRepository.GetLibraryItemsAsync(InitialLoadSize);
        await PopulateItemsAsync(items);
        SetFilterSummary($"Filter: All Comics ({items.Count})");
    }

    private void OnComicFileSaved(object? sender, ComicFileSavedEventArgs eventArgs)
    {
        lock (_savedItemsLock)
        {
            _pendingSavedItems.Add(eventArgs.Item);
            if (_savedItemsDrainScheduled)
            {
                return;
            }

            _savedItemsDrainScheduled = true;
        }

        _ = DrainSavedItemsAsync();
    }

    private void OnComicFileRemoved(object? sender, ComicFileRemovedEventArgs eventArgs)
    {
        if (string.IsNullOrWhiteSpace(eventArgs.FilePath))
        {
            return;
        }

        lock (_savedItemsLock)
        {
            _pendingRemovedPaths.Add(eventArgs.FilePath);
            if (_savedItemsDrainScheduled)
            {
                return;
            }

            _savedItemsDrainScheduled = true;
        }

        _ = DrainSavedItemsAsync();
    }

    private async Task DrainSavedItemsAsync()
    {
        await Task.Delay(SavedItemsUiBatchDelayMs);

        List<ComicLibraryItem> snapshot;
        List<string> removedPathsSnapshot;
        lock (_savedItemsLock)
        {
            snapshot = _pendingSavedItems
                .OrderBy(x => x.SequenceNumber)
                .ToList();
            removedPathsSnapshot = _pendingRemovedPaths
                .ToList();
            _pendingSavedItems.Clear();
            _pendingRemovedPaths.Clear();
            _savedItemsDrainScheduled = false;
        }

        if (snapshot.Count == 0 && removedPathsSnapshot.Count == 0)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            ApplySavedItemsBatch(snapshot, removedPathsSnapshot);
        });

        lock (_savedItemsLock)
        {
            if ((_pendingSavedItems.Count == 0 && _pendingRemovedPaths.Count == 0) || _savedItemsDrainScheduled)
            {
                return;
            }

            _savedItemsDrainScheduled = true;
        }

        _ = DrainSavedItemsAsync();
    }

    private void ApplySavedItemsBatch(IReadOnlyList<ComicLibraryItem> items, IReadOnlyList<string> removedPaths)
    {
        var hasChanges = false;
        var selectionChanged = false;

        foreach (var path in removedPaths)
        {
            if (!RemovePathFromUi(path))
            {
                continue;
            }

            hasChanges = true;
            selectionChanged = true;
        }

        foreach (var item in items)
        {
            if (_smartListFilterActive && _activeSmartListExpression is not null)
            {
                var projection = ToProjection(item);
                var isMatch = _smartListEvaluator.IsMatch(_activeSmartListExpression, projection);
                if (!isMatch)
                {
                    if (RemovePathFromUi(item.FilePath))
                    {
                        hasChanges = true;
                        selectionChanged = true;
                    }

                    continue;
                }
            }

            if (_itemIndex.TryGetValue(item.FilePath, out var existingTile))
            {
                existingTile.DisplayTitle = item.DisplayTitle;
                existingTile.FilePath = item.FilePath;
                existingTile.FileDirectory = item.FileDirectory;
                existingTile.Series = CoalesceGroupValue(item.Series);
                existingTile.Publisher = CoalesceGroupValue(item.Publisher);
                existingTile.IsThumbnailReady = item.IsThumbnailReady;
                existingTile.FileTypeTag = item.FileTypeTag;
                existingTile.LastScannedUtc = item.LastScannedUtc;
                ApplyThumbnailUpdate(existingTile, item.ThumbnailPath);
                hasChanges = true;
                continue;
            }

            var newTile = ToTile(item);
            _itemIndex[item.FilePath] = newTile;
            Items.Add(newTile);
            ApplyThumbnailUpdate(newTile, item.ThumbnailPath);
            SelectedItem ??= newTile;
            if (SelectedItems.Count == 0 && SelectedItem is not null)
            {
                SelectedItems.Add(SelectedItem);
                selectionChanged = true;
            }

            hasChanges = true;
        }

        if (hasChanges)
        {
            ApplyVisualOrdering();
            SetFilterSummary($"Filter: {(_smartListFilterActive ? _activeSmartListName : "All Comics")} ({Items.Count})");
        }

        if (selectionChanged)
        {
            ConvertToCbzCommand.NotifyCanExecuteChanged();
            DeleteFromLibraryCommand.NotifyCanExecuteChanged();
        }
    }

    private bool RemovePathFromUi(string filePath)
    {
        if (!_itemIndex.TryGetValue(filePath, out var existing))
        {
            return false;
        }

        Items.Remove(existing);
        _itemIndex.Remove(filePath);
        SelectedItems.Remove(existing);
        SetTileThumbnailImage(existing, null);
        if (ReferenceEquals(SelectedItem, existing))
        {
            SelectedItem = Items.FirstOrDefault();
            SetSelectedItems(SelectedItem is null ? [] : [SelectedItem]);
        }

        return true;
    }

    public async Task ApplySmartListAsync(ComicListItem listModel, CancellationToken cancellationToken = default)
    {
        var expression = _smartListExpressionService.ResolveExpression(listModel);
        _smartListFilterActive = true;
        _activeSmartListExpression = expression;
        _activeSmartListName = string.IsNullOrWhiteSpace(listModel.Name) ? "Smart List" : listModel.Name.Trim();

        await _loadLock.WaitAsync(cancellationToken);
        try
        {
            var result = await _smartListExecutionService.ExecuteAsync(
                expression,
                InitialLoadSize,
                cancellationToken);

            await PopulateItemsAsync(result.Items);
            SetFilterSummary($"Filter: {_activeSmartListName} ({result.LoadedCount})");
        }
        finally
        {
            _loadLock.Release();
        }
    }

    public async Task ClearSmartListAsync(CancellationToken cancellationToken = default)
    {
        _smartListFilterActive = false;
        _activeSmartListExpression = null;
        _activeSmartListName = "All Comics";

        await ReloadAsync();
        SetFilterSummary($"Filter: All Comics ({Items.Count})");
    }

    public void ApplyGrouping(IReadOnlyList<string> grouping)
    {
        var normalized = NormalizeGrouping(grouping);
        if (_activeGrouping.SequenceEqual(normalized, StringComparer.Ordinal))
        {
            return;
        }

        _activeGrouping.Clear();
        _activeGrouping.AddRange(normalized);
        RebuildGroupingView();
    }

    public void ApplyArrangement(string arrangeBy)
    {
        var normalized = NormalizeArrangement(arrangeBy);
        if (string.Equals(_activeArrangement, normalized, StringComparison.Ordinal))
        {
            return;
        }

        _activeArrangement = normalized;
        ApplyVisualOrdering();
    }

    public void SetSelectedItems(IReadOnlyList<ComicTileModel> selectedItems)
    {
        SelectedItems.Clear();
        foreach (var item in selectedItems
                     .Where(x => x is not null)
                     .Distinct())
        {
            SelectedItems.Add(item);
        }

        ConvertToCbzCommand.NotifyCanExecuteChanged();
        DeleteFromLibraryCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanConvertToCbz))]
    private async Task ConvertToCbzAsync(ComicTileModel? contextItem)
    {
        var conversionTargets = ResolveConversionTargets(contextItem);
        if (conversionTargets.Count == 0)
        {
            return;
        }

        await _settingsService.InitializeAsync();

        var settings = _settingsService.CurrentSettings;
        var sendOriginalToRecycleBin = settings.SendOriginalToRecycleBinOnCbzConversion;

        if (settings.ConfirmCbzConversion)
        {
            var confirmation = await _dialogService.ShowCbzConversionConfirmationDialogAsync(
                conversionTargets.Count,
                sendOriginalToRecycleBin);
            if (confirmation is null)
            {
                return;
            }

            sendOriginalToRecycleBin = confirmation.SendOriginalToRecycleBin;
            settings.SendOriginalToRecycleBinOnCbzConversion = confirmation.SendOriginalToRecycleBin;
            if (confirmation.DontAskAgain)
            {
                settings.ConfirmCbzConversion = false;
            }

            await _settingsService.SaveAsync();
        }

        IsConvertingToCbz = true;
        try
        {
            var sourcePaths = conversionTargets
                .Select(x => x.FilePath)
                .ToArray();
            var conversionResult = await _comicConversionService.ConvertToCbzAsync(
                sourcePaths,
                new CbzConversionOptions
                {
                    SendOriginalToRecycleBin = sendOriginalToRecycleBin
                });

            foreach (var convertedFile in conversionResult.Files.Where(x => x.Success))
            {
                if (convertedFile.DestinationPath is null)
                {
                    continue;
                }

                var sourceTile = conversionTargets.FirstOrDefault(x =>
                    string.Equals(x.FilePath, convertedFile.SourcePath, StringComparison.OrdinalIgnoreCase));
                if (sourceTile is null)
                {
                    continue;
                }

                ApplyConversionSuccess(sourceTile, convertedFile.DestinationPath, convertedFile.OriginalRemoved);
            }

            SetFilterSummary(
                $"Filter: {(_smartListFilterActive ? _activeSmartListName : "All Comics")} ({Items.Count}) | Converted {conversionResult.SuccessCount}, Failed {conversionResult.FailureCount}");
        }
        finally
        {
            IsConvertingToCbz = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanDeleteFromLibrary))]
    private async Task DeleteFromLibraryAsync(ComicTileModel? contextItem)
    {
        var deleteTargets = ResolveDeleteTargets(contextItem);
        if (deleteTargets.Count == 0)
        {
            return;
        }

        await _settingsService.InitializeAsync();

        var settings = _settingsService.CurrentSettings;
        var sendToRecycleBin = settings.SendDeletedToRecycleBinOnLibraryDelete;

        if (settings.ConfirmDeleteFromLibrary)
        {
            var confirmation = await _dialogService.ShowLibraryDeleteConfirmationDialogAsync(
                deleteTargets.Count,
                sendToRecycleBin);
            if (confirmation is null)
            {
                return;
            }

            sendToRecycleBin = confirmation.SendToRecycleBin;
            settings.SendDeletedToRecycleBinOnLibraryDelete = confirmation.SendToRecycleBin;
            if (confirmation.DontAskAgain)
            {
                settings.ConfirmDeleteFromLibrary = false;
            }

            await _settingsService.SaveAsync();
        }

        if (sendToRecycleBin && !OperatingSystem.IsWindows())
        {
            var filterName = _smartListFilterActive ? _activeSmartListName : "All Comics";
            SetFilterSummary($"Filter: {filterName} ({Items.Count}) | Recycle Bin delete is only supported on Windows.");
            return;
        }

        IsDeletingFromLibrary = true;
        try
        {
            var normalizedPaths = deleteTargets
                .Select(x => NormalizeFilePath(x.FilePath))
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (normalizedPaths.Length == 0)
            {
                return;
            }

            var pathsForDatabaseDelete = new List<string>(normalizedPaths.Length);
            var failedRecycleCount = 0;

            foreach (var normalizedPath in normalizedPaths)
            {
                if (!sendToRecycleBin)
                {
                    pathsForDatabaseDelete.Add(normalizedPath);
                    continue;
                }

                if (!File.Exists(normalizedPath))
                {
                    pathsForDatabaseDelete.Add(normalizedPath);
                    continue;
                }

                try
                {
                    MoveFileToRecycleBin(normalizedPath);
                    pathsForDatabaseDelete.Add(normalizedPath);
                }
                catch
                {
                    failedRecycleCount++;
                }
            }

            var removedPaths = pathsForDatabaseDelete.Count == 0
                ? []
                : await _scanRepository.DeleteByNormalizedPathsAsync(pathsForDatabaseDelete);

            if (removedPaths.Count > 0)
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ApplySavedItemsBatch([], removedPaths);
                });
            }

            if (removedPaths.Count > 0 || failedRecycleCount > 0)
            {
                var filterName = _smartListFilterActive ? _activeSmartListName : "All Comics";
                var resultSuffix = failedRecycleCount > 0
                    ? $" | Removed {removedPaths.Count}, Failed {failedRecycleCount}"
                    : $" | Removed {removedPaths.Count}";
                SetFilterSummary($"Filter: {filterName} ({Items.Count}){resultSuffix}");
            }
        }
        finally
        {
            IsDeletingFromLibrary = false;
        }
    }

    private bool CanConvertToCbz(ComicTileModel? contextItem)
    {
        if (IsConvertingToCbz)
        {
            return false;
        }

        if (contextItem is not null)
        {
            return true;
        }

        return SelectedItems.Count > 0 || SelectedItem is not null;
    }

    private bool CanDeleteFromLibrary(ComicTileModel? contextItem)
    {
        if (IsDeletingFromLibrary || IsConvertingToCbz)
        {
            return false;
        }

        if (contextItem is not null)
        {
            return true;
        }

        return SelectedItems.Count > 0 || SelectedItem is not null;
    }

    [RelayCommand]
    private async Task ShowInfoAsync(ComicTileModel? contextItem)
    {
        var targetItem = contextItem ?? SelectedItem;
        if (targetItem is null)
        {
            return;
        }

        if (!ReferenceEquals(SelectedItem, targetItem))
        {
            SelectedItem = targetItem;
        }

        SetSelectedItems([targetItem]);

        IsInfoPanelOpen = true;
        await LoadInfoPanelAsync(targetItem);
    }

    [RelayCommand]
    private void CloseInfoPanel()
    {
        Interlocked.Increment(ref _infoRequestVersion);
        IsInfoPanelOpen = false;
        IsInfoLoading = false;
        HasInfoError = false;
        InfoError = string.Empty;
    }

    private List<ComicTileModel> ResolveConversionTargets(ComicTileModel? contextItem)
    {
        if (contextItem is not null)
        {
            if (SelectedItems.Any(x => string.Equals(x.FilePath, contextItem.FilePath, StringComparison.OrdinalIgnoreCase)))
            {
                return SelectedItems
                    .Distinct()
                    .ToList();
            }

            return [contextItem];
        }

        if (SelectedItems.Count > 0)
        {
            return SelectedItems
                .Distinct()
                .ToList();
        }

        return SelectedItem is null ? [] : [SelectedItem];
    }

    private List<ComicTileModel> ResolveDeleteTargets(ComicTileModel? contextItem)
    {
        if (contextItem is not null)
        {
            if (SelectedItems.Any(x => string.Equals(x.FilePath, contextItem.FilePath, StringComparison.OrdinalIgnoreCase)))
            {
                return SelectedItems
                    .Distinct()
                    .ToList();
            }

            return [contextItem];
        }

        if (SelectedItems.Count > 0)
        {
            return SelectedItems
                .Distinct()
                .ToList();
        }

        return SelectedItem is null ? [] : [SelectedItem];
    }

    private void ApplyConversionSuccess(ComicTileModel sourceTile, string destinationPath, bool originalRemoved)
    {
        var normalizedDestinationPath = Path.GetFullPath(destinationPath).Trim();
        var destinationDirectory = Path.GetDirectoryName(normalizedDestinationPath) ?? string.Empty;

        if (originalRemoved)
        {
            var oldPath = sourceTile.FilePath;
            _itemIndex.Remove(oldPath);

            sourceTile.FilePath = normalizedDestinationPath;
            sourceTile.FileDirectory = destinationDirectory;
            sourceTile.DisplayTitle = Path.GetFileNameWithoutExtension(normalizedDestinationPath);
            sourceTile.FileTypeTag = "CBZ";
            sourceTile.LastScannedUtc = DateTimeOffset.UtcNow;

            _itemIndex[normalizedDestinationPath] = sourceTile;
            ApplyVisualOrdering();
            return;
        }

        if (_itemIndex.ContainsKey(normalizedDestinationPath))
        {
            return;
        }

        var duplicateTile = new ComicTileModel
        {
            FilePath = normalizedDestinationPath,
            FileDirectory = destinationDirectory,
            DisplayTitle = Path.GetFileNameWithoutExtension(normalizedDestinationPath),
            Series = sourceTile.Series,
            Publisher = sourceTile.Publisher,
            ThumbnailPath = sourceTile.ThumbnailPath,
            ThumbnailImage = sourceTile.ThumbnailImage,
            IsThumbnailReady = sourceTile.IsThumbnailReady,
            FileTypeTag = "CBZ",
            LastScannedUtc = DateTimeOffset.UtcNow
        };

        Items.Insert(0, duplicateTile);
        _itemIndex[normalizedDestinationPath] = duplicateTile;
        ApplyVisualOrdering();
    }

    private ComicTileModel ToTile(ComicLibraryItem item)
    {
        return new ComicTileModel
        {
            FilePath = item.FilePath,
            FileDirectory = string.IsNullOrWhiteSpace(item.FileDirectory)
                ? (Path.GetDirectoryName(item.FilePath) ?? string.Empty)
                : item.FileDirectory,
            DisplayTitle = item.DisplayTitle,
            Series = CoalesceGroupValue(item.Series, item.DisplayTitle),
            Publisher = CoalesceGroupValue(item.Publisher),
            ThumbnailPath = item.ThumbnailPath,
            ThumbnailImage = null,
            IsThumbnailReady = item.IsThumbnailReady,
            FileTypeTag = item.FileTypeTag,
            LastScannedUtc = item.LastScannedUtc
        };
    }

    private async Task PopulateItemsAsync(IReadOnlyList<ComicLibraryItem> items)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            foreach (var existing in Items)
            {
                SetTileThumbnailImage(existing, null);
            }

            Items.Clear();
            _itemIndex.Clear();
            SelectedItems.Clear();
            ClearThumbnailCache();

            foreach (var item in items)
            {
                var tile = ToTile(item);
                _itemIndex[item.FilePath] = tile;
                Items.Add(tile);
                ApplyThumbnailUpdate(tile, item.ThumbnailPath);
            }

            ApplyVisualOrdering();
            SelectedItem = Items.FirstOrDefault();
            SetSelectedItems(SelectedItem is null ? [] : [SelectedItem]);

            ConvertToCbzCommand.NotifyCanExecuteChanged();
            DeleteFromLibraryCommand.NotifyCanExecuteChanged();
        });
    }

    private void RebuildGroupingView()
    {
        var isGrouped = !_activeGrouping.Contains("Not Grouped", StringComparer.Ordinal);
        IsGroupedView = isGrouped;
        OnPropertyChanged(nameof(IsFlatView));

        Groups.Clear();
        if (!isGrouped)
        {
            return;
        }

        var groupIndex = new Dictionary<string, ComicGroupModel>(StringComparer.Ordinal);
        foreach (var tile in Items)
        {
            var values = _activeGrouping
                .Select(filter => ResolveGroupValue(tile, filter))
                .ToArray();

            var groupKey = string.Join("||", values);
            if (!groupIndex.TryGetValue(groupKey, out var group))
            {
                group = new ComicGroupModel
                {
                    Header = $"{string.Join(" - ", values)} ({0})"
                };

                groupIndex[groupKey] = group;
                Groups.Add(group);
            }

            group.Items.Add(tile);
            group.Header = $"{string.Join(" - ", values)} ({group.Items.Count})";
        }
    }

    private void ApplyVisualOrdering()
    {
        if (Items.Count > 1)
        {
            var arranged = GetArrangedItemsSnapshot();
            if (!Items.SequenceEqual(arranged))
            {
                var selectedItemSnapshot = SelectedItem;
                var selectedItemsSnapshot = SelectedItems.ToArray();

                Items.Clear();
                foreach (var tile in arranged)
                {
                    Items.Add(tile);
                }

                if (selectedItemSnapshot is not null && Items.Contains(selectedItemSnapshot))
                {
                    SelectedItem = selectedItemSnapshot;
                }
                else
                {
                    SelectedItem = Items.FirstOrDefault();
                }

                var restoredSelection = selectedItemsSnapshot
                    .Where(x => Items.Contains(x))
                    .ToArray();
                if (restoredSelection.Length == 0 && SelectedItem is not null)
                {
                    restoredSelection = [SelectedItem];
                }

                SetSelectedItems(restoredSelection);
            }
        }

        RebuildGroupingView();
    }

    private IReadOnlyList<ComicTileModel> GetArrangedItemsSnapshot()
    {
        return _activeArrangement switch
        {
            ArrangeBySeries => Items
                .OrderBy(GetSeriesSortValue, StringComparer.OrdinalIgnoreCase)
                .ThenBy(GetIssueSortKey)
                .ThenBy(x => x.DisplayTitle, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.FilePath, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            ArrangeByPosition => Items
                .OrderBy(GetIssueSortKey)
                .ThenBy(GetSeriesSortValue, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.DisplayTitle, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.FilePath, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            ArrangeByFilePath => Items
                .OrderBy(x => x.FilePath, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            _ => Items.ToArray()
        };
    }

    private static IReadOnlyList<string> NormalizeGrouping(IReadOnlyList<string> grouping)
    {
        if (grouping.Count == 0 ||
            grouping.Any(x => string.Equals(x, "Not Grouped", StringComparison.Ordinal)))
        {
            return ["Not Grouped"];
        }

        var normalized = grouping
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return normalized.Length == 0 ? ["Not Grouped"] : normalized;
    }

    private static string ResolveGroupValue(ComicTileModel tile, string filterName)
    {
        return filterName switch
        {
            "Series" => CoalesceGroupValue(tile.Series, tile.DisplayTitle),
            "Publisher" => CoalesceGroupValue(tile.Publisher),
            "File Directory" => CoalesceGroupValue(tile.FileDirectory),
            "Folder" => CoalesceGroupValue(Path.GetFileName(tile.FileDirectory)),
            "Smart List" => "Unspecified",
            "Import Source" => "Unspecified",
            _ => "Unspecified"
        };
    }

    private static string NormalizeArrangement(string? arrangeBy)
    {
        return arrangeBy switch
        {
            ArrangeBySeries => ArrangeBySeries,
            ArrangeByPosition => ArrangeByPosition,
            ArrangeByFilePath => ArrangeByFilePath,
            _ => ArrangeByNotSorted
        };
    }

    private static string GetSeriesSortValue(ComicTileModel tile)
    {
        return CoalesceGroupValue(tile.Series, tile.DisplayTitle);
    }

    private static IssueSortKey GetIssueSortKey(ComicTileModel tile)
    {
        var titleCandidate = tile.DisplayTitle ?? string.Empty;
        if (TryParseIssueNumber(titleCandidate, out var issueNumber))
        {
            return new IssueSortKey(true, issueNumber);
        }

        var fileNameCandidate = Path.GetFileNameWithoutExtension(tile.FilePath) ?? string.Empty;
        if (TryParseIssueNumber(fileNameCandidate, out issueNumber))
        {
            return new IssueSortKey(true, issueNumber);
        }

        return IssueSortKey.Missing;
    }

    private static bool TryParseIssueNumber(string value, out decimal issueNumber)
    {
        issueNumber = 0;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var issueMatch = IssueNumberRegex.Match(value);
        if (issueMatch.Success &&
            decimal.TryParse(
                issueMatch.Groups["num"].Value,
                NumberStyles.Number,
                CultureInfo.InvariantCulture,
                out issueNumber))
        {
            return true;
        }

        var anyNumberMatch = AnyNumberRegex.Match(value);
        return anyNumberMatch.Success &&
               decimal.TryParse(
                   anyNumberMatch.Groups["num"].Value,
                   NumberStyles.Number,
                   CultureInfo.InvariantCulture,
                   out issueNumber);
    }

    private static string CoalesceGroupValue(string? value, string? displayTitle = null)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            return value.Trim();
        }

        if (string.IsNullOrWhiteSpace(displayTitle))
        {
            return "Unspecified";
        }

        var title = displayTitle.Trim();
        var hashIndex = title.IndexOf('#');
        if (hashIndex > 0)
        {
            return title[..hashIndex].Trim();
        }

        var yearIndex = title.IndexOf('(');
        if (yearIndex > 0)
        {
            return title[..yearIndex].Trim();
        }

        return title;
    }

    private readonly record struct IssueSortKey(bool HasValue, decimal Value) : IComparable<IssueSortKey>
    {
        public static IssueSortKey Missing => new(false, decimal.MaxValue);

        public int CompareTo(IssueSortKey other)
        {
            if (HasValue != other.HasValue)
            {
                return HasValue ? -1 : 1;
            }

            return Value.CompareTo(other.Value);
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

    private static void MoveFileToRecycleBin(string path)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Sending files to recycle bin is only supported on Windows.");
        }

        FileSystem.DeleteFile(
            path,
            UIOption.OnlyErrorDialogs,
            RecycleOption.SendToRecycleBin,
            UICancelOption.ThrowException);
    }

    private void ApplyThumbnailUpdate(ComicTileModel tile, string? thumbnailPath)
    {
        tile.ThumbnailPath = thumbnailPath;
        tile.ThumbnailVersion++;

        if (string.IsNullOrWhiteSpace(thumbnailPath))
        {
            SetTileThumbnailImage(tile, null);
            return;
        }

        if (!tile.IsThumbnailReady)
        {
            SetTileThumbnailImage(tile, null);
            return;
        }

        if (TryGetCachedThumbnail(thumbnailPath, out var cachedBitmap))
        {
            SetTileThumbnailImage(tile, cachedBitmap);
            return;
        }

        SetTileThumbnailImage(tile, null);
        _ = LoadThumbnailBitmapAsync(tile, thumbnailPath, tile.ThumbnailVersion);
    }

    private bool TryGetCachedThumbnail(string thumbnailPath, out Bitmap bitmap)
    {
        lock (_thumbnailCacheLock)
        {
            if (_thumbnailBitmapCache.TryGetValue(thumbnailPath, out bitmap!))
            {
                if (_thumbnailCacheNodes.TryGetValue(thumbnailPath, out var node))
                {
                    _thumbnailCacheOrder.Remove(node);
                    _thumbnailCacheOrder.AddLast(node);
                }

                return true;
            }
        }

        bitmap = null!;
        return false;
    }

    private void CacheThumbnail(string thumbnailPath, Bitmap bitmap)
    {
        List<Bitmap>? evictedBitmaps = null;
        Bitmap? replacedBitmap = null;

        lock (_thumbnailCacheLock)
        {
            if (_thumbnailBitmapCache.TryGetValue(thumbnailPath, out var existingBitmap))
            {
                _thumbnailBitmapCache[thumbnailPath] = bitmap;
                if (_thumbnailCacheNodes.TryGetValue(thumbnailPath, out var existingNode))
                {
                    _thumbnailCacheOrder.Remove(existingNode);
                    _thumbnailCacheOrder.AddLast(existingNode);
                }

                replacedBitmap = existingBitmap;
            }
            else
            {
                var node = _thumbnailCacheOrder.AddLast(thumbnailPath);
                _thumbnailCacheNodes[thumbnailPath] = node;
                _thumbnailBitmapCache[thumbnailPath] = bitmap;
            }

            while (_thumbnailBitmapCache.Count > ThumbnailCacheCapacity)
            {
                var oldestNode = _thumbnailCacheOrder.First;
                if (oldestNode is null)
                {
                    break;
                }

                _thumbnailCacheOrder.RemoveFirst();
                var oldestKey = oldestNode.Value;
                _thumbnailCacheNodes.Remove(oldestKey);
                if (_thumbnailBitmapCache.TryGetValue(oldestKey, out var evictedBitmap))
                {
                    _thumbnailBitmapCache.Remove(oldestKey);
                    evictedBitmaps ??= [];
                    evictedBitmaps.Add(evictedBitmap);
                }
                else
                {
                    _thumbnailBitmapCache.Remove(oldestKey);
                }
            }
        }

        if (replacedBitmap is not null && !ReferenceEquals(replacedBitmap, bitmap))
        {
            ReleaseBitmapIfUnused(replacedBitmap);
        }

        if (evictedBitmaps is null)
        {
            return;
        }

        foreach (var evictedBitmap in evictedBitmaps.Distinct())
        {
            ReleaseBitmapIfUnused(evictedBitmap);
        }
    }

    private void SetTileThumbnailImage(ComicTileModel tile, Bitmap? bitmap)
    {
        if (ReferenceEquals(tile.ThumbnailImage, bitmap))
        {
            return;
        }

        var previous = tile.ThumbnailImage;
        tile.ThumbnailImage = bitmap;
        ReleaseBitmapIfUnused(previous);
    }

    private void ReleaseBitmapIfUnused(Bitmap? bitmap)
    {
        if (bitmap is null)
        {
            return;
        }

        if (IsBitmapReferencedByTile(bitmap))
        {
            return;
        }

        lock (_thumbnailCacheLock)
        {
            if (_thumbnailBitmapCache.Values.Any(x => ReferenceEquals(x, bitmap)))
            {
                return;
            }
        }

        bitmap.Dispose();
    }

    private bool IsBitmapReferencedByTile(Bitmap bitmap)
    {
        foreach (var tile in Items)
        {
            if (ReferenceEquals(tile.ThumbnailImage, bitmap))
            {
                return true;
            }
        }

        return false;
    }

    private async Task LoadThumbnailBitmapAsync(ComicTileModel tile, string thumbnailPath, int thumbnailVersion)
    {
        Bitmap? bitmap;
        try
        {
            bitmap = await Task.Run(() => new Bitmap(thumbnailPath));
        }
        catch
        {
            bitmap = null;
        }

        if (bitmap is null)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            if (tile.ThumbnailVersion != thumbnailVersion ||
                !string.Equals(tile.ThumbnailPath, thumbnailPath, StringComparison.OrdinalIgnoreCase))
            {
                bitmap.Dispose();
                return;
            }

            CacheThumbnail(thumbnailPath, bitmap);
            SetTileThumbnailImage(tile, bitmap);
        });
    }

    private void ClearThumbnailCache()
    {
        List<Bitmap> cachedBitmaps;
        lock (_thumbnailCacheLock)
        {
            cachedBitmaps = _thumbnailBitmapCache.Values
                .Distinct()
                .ToList();
            _thumbnailBitmapCache.Clear();
            _thumbnailCacheNodes.Clear();
            _thumbnailCacheOrder.Clear();
        }

        foreach (var bitmap in cachedBitmaps)
        {
            ReleaseBitmapIfUnused(bitmap);
        }
    }

    partial void OnSelectedItemChanged(ComicTileModel? value)
    {
        SelectedItemChanged?.Invoke(this, value);
        ConvertToCbzCommand.NotifyCanExecuteChanged();
        DeleteFromLibraryCommand.NotifyCanExecuteChanged();

        if (IsInfoPanelOpen && value is not null)
        {
            _ = LoadInfoPanelAsync(value);
        }
    }

    partial void OnIsConvertingToCbzChanged(bool value)
    {
        ConvertToCbzCommand.NotifyCanExecuteChanged();
        DeleteFromLibraryCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsDeletingFromLibraryChanged(bool value)
    {
        DeleteFromLibraryCommand.NotifyCanExecuteChanged();
    }

    private void SetFilterSummary(string summary)
    {
        FilterSummary = summary;
        FilterSummaryChanged?.Invoke(this, summary);
    }

    private static ComicLibraryProjection ToProjection(ComicLibraryItem item)
    {
        return new ComicLibraryProjection
        {
            FilePath = item.FilePath,
            FileName = Path.GetFileName(item.FilePath),
            FileDirectory = string.IsNullOrWhiteSpace(item.FileDirectory)
                ? (Path.GetDirectoryName(item.FilePath) ?? string.Empty)
                : item.FileDirectory,
            DisplayTitle = item.DisplayTitle,
            Extension = Path.GetExtension(item.FilePath),
            Series = item.Series,
            Publisher = item.Publisher,
            ThumbnailPath = item.ThumbnailPath,
            HasThumbnail = item.IsThumbnailReady,
            LastScannedUtc = item.LastScannedUtc
        };
    }

    private async Task LoadInfoPanelAsync(ComicTileModel tile)
    {
        var requestVersion = Interlocked.Increment(ref _infoRequestVersion);
        IsInfoLoading = true;
        HasInfoError = false;
        InfoError = string.Empty;

        InfoPanel = ComicInfoPanelModel.From(tile, new ComicMetadata
        {
            FilePath = tile.FilePath,
            FileName = Path.GetFileName(tile.FilePath),
            DisplayTitle = tile.DisplayTitle,
            Title = tile.DisplayTitle,
            Series = tile.Series,
            Publisher = tile.Publisher,
            Source = ComicMetadataSource.FileNameFallback
        });

        try
        {
            var metadata = await _comicMetadataService.GetMetadataAsync(tile.FilePath);
            if (requestVersion != _infoRequestVersion)
            {
                return;
            }

            InfoPanel = ComicInfoPanelModel.From(tile, metadata);
        }
        catch (Exception ex)
        {
            if (requestVersion != _infoRequestVersion)
            {
                return;
            }

            HasInfoError = true;
            InfoError = $"Unable to load metadata: {ex.Message}";
        }
        finally
        {
            if (requestVersion == _infoRequestVersion)
            {
                IsInfoLoading = false;
            }
        }
    }
}
