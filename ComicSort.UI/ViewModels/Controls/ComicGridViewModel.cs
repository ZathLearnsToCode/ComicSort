using Avalonia.Threading;
using ComicSort.Engine.Models;
using ComicSort.Engine.Services;
using ComicSort.Engine.Settings;
using ComicSort.UI.Models;
using ComicSort.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ComicSort.UI.ViewModels.Controls;

public sealed partial class ComicGridViewModel : ViewModelBase
{
    private const int InitialLoadSize = 200;
    private const string NotGrouped = "Not Grouped";
    private const string AllComicsFilter = "All Comics";

    private readonly IComicDatabaseService _comicDatabaseService;
    private readonly IScanRepository _scanRepository;
    private readonly ISmartListExecutionService _smartListExecutionService;
    private readonly ISmartListEvaluator _smartListEvaluator;
    private readonly ISmartListExpressionService _smartListExpressionService;
    private readonly IComicGridArrangementService _arrangementService;
    private readonly IComicGridThumbnailService _thumbnailService;
    private readonly IComicGridFileActionService _fileActionService;
    private readonly IComicGridInfoPanelService _infoPanelService;
    private readonly ComicGridSelectionService _selectionService;
    private readonly ComicGridConversionResultService _conversionResultService;
    private readonly ComicGridSavedItemQueueService _savedItemQueueService;
    private readonly ComicGridPresentationService _presentationService;
    private readonly Dictionary<string, ComicTileModel> _itemIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly List<string> _activeGrouping = [NotGrouped];
    private int _initialized;
    private bool _smartListFilterActive;
    private MatcherGroupNode? _activeSmartListExpression;
    private string _activeSmartListName = AllComicsFilter;
    private string _activeArrangement = "Not Sorted";
    private int _infoRequestVersion;

    private const int SavedItemsUiBatchDelayMs = 150;

    public ComicGridViewModel(
        IComicDatabaseService comicDatabaseService,
        IScanRepository scanRepository,
        ISmartListExecutionService smartListExecutionService,
        ISmartListEvaluator smartListEvaluator,
        ISmartListExpressionService smartListExpressionService,
        IComicGridArrangementService arrangementService,
        IComicGridThumbnailService thumbnailService,
        IComicGridFileActionService fileActionService,
        IComicGridInfoPanelService infoPanelService,
        ComicGridSelectionService selectionService,
        ComicGridConversionResultService conversionResultService,
        ComicGridSavedItemQueueService savedItemQueueService,
        ComicGridPresentationService presentationService,
        IScanService scanService)
    {
        _comicDatabaseService = comicDatabaseService;
        _scanRepository = scanRepository;
        _smartListExecutionService = smartListExecutionService;
        _smartListEvaluator = smartListEvaluator;
        _smartListExpressionService = smartListExpressionService;
        _arrangementService = arrangementService;
        _thumbnailService = thumbnailService;
        _fileActionService = fileActionService;
        _infoPanelService = infoPanelService;
        _selectionService = selectionService;
        _conversionResultService = conversionResultService;
        _savedItemQueueService = savedItemQueueService;
        _presentationService = presentationService;
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
    private string filterSummary = $"Filter: {AllComicsFilter}";

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
            await ReloadWithRetryAsync();
        }
        finally
        {
            _loadLock.Release();
        }
    }

    private async Task ReloadWithRetryAsync()
    {
        const int maxAttempts = 4;
        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            if (await TryLoadInitialAsync(attempt, maxAttempts))
            {
                return;
            }

            await Task.Delay(250);
        }
    }

    private async Task<bool> TryLoadInitialAsync(int attempt, int maxAttempts)
    {
        try
        {
            await LoadInitialAsync();
            return Items.Count > 0 || attempt == maxAttempts;
        }
        catch
        {
            if (attempt != maxAttempts)
            {
                return false;
            }

            Interlocked.Exchange(ref _initialized, 0);
            return true;
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
        SetFilterSummary($"Filter: {AllComicsFilter} ({items.Count})");
    }

    private void OnComicFileSaved(object? sender, ComicFileSavedEventArgs eventArgs)
    {
        if (_savedItemQueueService.EnqueueSaved(eventArgs.Item))
        {
            _ = DrainSavedItemsAsync();
        }
    }

    private void OnComicFileRemoved(object? sender, ComicFileRemovedEventArgs eventArgs)
    {
        if (string.IsNullOrWhiteSpace(eventArgs.FilePath))
        {
            return;
        }

        if (_savedItemQueueService.EnqueueRemoved(eventArgs.FilePath))
        {
            _ = DrainSavedItemsAsync();
        }
    }

    private async Task DrainSavedItemsAsync()
    {
        await Task.Delay(SavedItemsUiBatchDelayMs);
        var snapshot = _savedItemQueueService.TakeSnapshot();
        if (snapshot.IsEmpty)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() => ApplySavedItemsBatch(snapshot.Items, snapshot.RemovedPaths));
        if (_savedItemQueueService.ScheduleDrainIfPending())
        {
            _ = DrainSavedItemsAsync();
        }
    }

    private void ApplySavedItemsBatch(IReadOnlyList<ComicLibraryItem> items, IReadOnlyList<string> removedPaths)
    {
        var removedResult = ApplyRemovedPaths(removedPaths);
        var upsertResult = ApplySavedItems(items);
        var hasChanges = removedResult.HasChanges || upsertResult.HasChanges;
        var selectionChanged = removedResult.SelectionChanged || upsertResult.SelectionChanged;
        if (hasChanges)
        {
            ApplyVisualOrdering();
            SetFilterSummary($"Filter: {GetActiveFilterName()} ({Items.Count})");
        }

        if (!selectionChanged)
        {
            return;
        }

        ConvertToCbzCommand.NotifyCanExecuteChanged();
        DeleteFromLibraryCommand.NotifyCanExecuteChanged();
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
        _thumbnailService.ReleaseTileThumbnail(existing, Items);
        if (ReferenceEquals(SelectedItem, existing))
        {
            SelectedItem = Items.FirstOrDefault();
            SetSelectedItems(SelectedItem is null ? [] : [SelectedItem]);
        }

        return true;
    }

    private BatchApplyResult ApplyRemovedPaths(IReadOnlyList<string> removedPaths)
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

        return new BatchApplyResult(hasChanges, selectionChanged);
    }

    private BatchApplyResult ApplySavedItems(IReadOnlyList<ComicLibraryItem> items)
    {
        var hasChanges = false;
        var selectionChanged = false;
        foreach (var item in items)
        {
            var itemResult = ApplySavedItem(item);
            hasChanges |= itemResult.HasChanges;
            selectionChanged |= itemResult.SelectionChanged;
        }

        return new BatchApplyResult(hasChanges, selectionChanged);
    }

    private BatchApplyResult ApplySavedItem(ComicLibraryItem item)
    {
        if (ShouldRemoveBySmartList(item))
        {
            var removed = RemovePathFromUi(item.FilePath);
            return new BatchApplyResult(removed, removed);
        }

        if (_itemIndex.TryGetValue(item.FilePath, out var existingTile))
        {
            UpdateTile(existingTile, item);
            return new BatchApplyResult(true, false);
        }

        var selectionChanged = AddTile(item);
        return new BatchApplyResult(true, selectionChanged);
    }

    private bool ShouldRemoveBySmartList(ComicLibraryItem item)
    {
        if (!_smartListFilterActive || _activeSmartListExpression is null)
        {
            return false;
        }

        return !_smartListEvaluator.IsMatch(_activeSmartListExpression, ToProjection(item));
    }

    private void UpdateTile(ComicTileModel tile, ComicLibraryItem item)
    {
        _arrangementService.UpdateTile(tile, item);
        _thumbnailService.ApplyThumbnail(tile, item.ThumbnailPath, Items);
    }

    private bool AddTile(ComicLibraryItem item)
    {
        var tile = _arrangementService.CreateTile(item);
        _itemIndex[item.FilePath] = tile;
        Items.Add(tile);
        _thumbnailService.ApplyThumbnail(tile, item.ThumbnailPath, Items);
        SelectedItem ??= tile;
        if (SelectedItems.Count > 0 || SelectedItem is null)
        {
            return false;
        }

        SelectedItems.Add(SelectedItem);
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
        _activeSmartListName = AllComicsFilter;

        await ReloadAsync();
        SetFilterSummary($"Filter: {AllComicsFilter} ({Items.Count})");
    }

    public void ApplyGrouping(IReadOnlyList<string> grouping)
    {
        var normalized = _arrangementService.NormalizeGrouping(grouping);
        if (_activeGrouping.SequenceEqual(normalized, StringComparer.Ordinal))
        {
            return;
        }

        _activeGrouping.Clear();
        _activeGrouping.AddRange(normalized);
        ApplyVisualOrdering();
    }

    public void ApplyArrangement(string arrangeBy)
    {
        var normalized = _arrangementService.NormalizeArrangement(arrangeBy);
        if (string.Equals(_activeArrangement, normalized, StringComparison.Ordinal))
        {
            return;
        }

        _activeArrangement = normalized;
        ApplyVisualOrdering();
    }

    public void SetSelectedItems(IReadOnlyList<ComicTileModel> selectedItems)
    {
        _selectionService.SetSelectedItems(SelectedItems, selectedItems);
        NotifySelectionCommandsChanged();
    }

    [RelayCommand(CanExecute = nameof(CanConvertToCbz))]
    private async Task ConvertToCbzAsync(ComicTileModel? contextItem)
    {
        var conversionTargets = ResolveActionTargets(contextItem);
        if (conversionTargets.Count == 0)
        {
            return;
        }

        IsConvertingToCbz = true;
        try
        {
            await ApplyCbzConversionAsync(conversionTargets);
        }
        finally
        {
            IsConvertingToCbz = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanDeleteFromLibrary))]
    private async Task DeleteFromLibraryAsync(ComicTileModel? contextItem)
    {
        var deleteTargets = ResolveActionTargets(contextItem);
        if (deleteTargets.Count == 0)
        {
            return;
        }

        IsDeletingFromLibrary = true;
        try
        {
            await DeleteTargetsAsync(deleteTargets);
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

    private List<ComicTileModel> ResolveActionTargets(ComicTileModel? contextItem)
    {
        return _selectionService.ResolveActionTargets(contextItem, SelectedItems, SelectedItem).ToList();
    }

    private async Task ApplyCbzConversionAsync(IReadOnlyList<ComicTileModel> conversionTargets)
    {
        var conversionResult = await _fileActionService.ConvertToCbzAsync(conversionTargets);
        if (conversionResult is null)
        {
            return;
        }

        ApplyConversionResult(conversionTargets, conversionResult);
        SetFilterSummary($"Filter: {GetActiveFilterName()} ({Items.Count}) | Converted {conversionResult.SuccessCount}, Failed {conversionResult.FailureCount}");
    }

    private void ApplyConversionResult(IReadOnlyList<ComicTileModel> conversionTargets, CbzConversionBatchResult conversionResult)
    {
        if (_conversionResultService.Apply(conversionTargets, conversionResult, _itemIndex, Items))
        {
            ApplyVisualOrdering();
        }
    }

    private async Task DeleteTargetsAsync(IReadOnlyList<ComicTileModel> deleteTargets)
    {
        var deleteResult = await _fileActionService.DeleteFromLibraryAsync(deleteTargets);
        if (deleteResult is null)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(deleteResult.WarningMessage))
        {
            SetFilterSummary($"Filter: {GetActiveFilterName()} ({Items.Count}) | {deleteResult.WarningMessage}");
            return;
        }

        await RemoveDeletedItemsFromUiAsync(deleteResult.RemovedPaths);
        SetDeleteSummary(deleteResult.RemovedPaths.Count, deleteResult.FailedRecycleCount);
    }

    private async Task RemoveDeletedItemsFromUiAsync(IReadOnlyList<string> removedPaths)
    {
        if (removedPaths.Count == 0)
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() => ApplySavedItemsBatch([], removedPaths));
    }

    private void SetDeleteSummary(int removedCount, int failedCount)
    {
        if (removedCount == 0 && failedCount == 0)
        {
            return;
        }

        var resultSuffix = failedCount > 0
            ? $" | Removed {removedCount}, Failed {failedCount}"
            : $" | Removed {removedCount}";
        SetFilterSummary($"Filter: {GetActiveFilterName()} ({Items.Count}){resultSuffix}");
    }

    private string GetActiveFilterName()
    {
        return _smartListFilterActive ? _activeSmartListName : AllComicsFilter;
    }

    private void ResetInfoPanelState()
    {
        IsInfoPanelOpen = false;
        IsInfoLoading = false;
        HasInfoError = false;
        InfoError = string.Empty;
    }

    private void NotifySelectionCommandsChanged()
    {
        ConvertToCbzCommand.NotifyCanExecuteChanged();
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
            FileDirectory = string.IsNullOrWhiteSpace(item.FileDirectory) ? (Path.GetDirectoryName(item.FilePath) ?? string.Empty) : item.FileDirectory,
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
        InfoPanel = BuildFallbackInfoPanel(tile);
        var loadResult = await _infoPanelService.LoadAsync(tile);
        if (requestVersion == _infoRequestVersion)
        {
            InfoPanel = loadResult.Panel;
            if (!string.IsNullOrWhiteSpace(loadResult.ErrorMessage))
            {
                HasInfoError = true;
                InfoError = loadResult.ErrorMessage;
            }

            IsInfoLoading = false;
        }
    }

    private static ComicInfoPanelModel BuildFallbackInfoPanel(ComicTileModel tile)
    {
        return ComicInfoPanelModel.From(tile, new ComicMetadata
        {
            FilePath = tile.FilePath,
            FileName = Path.GetFileName(tile.FilePath),
            DisplayTitle = tile.DisplayTitle,
            Title = tile.DisplayTitle,
            Series = tile.Series,
            Publisher = tile.Publisher,
            Source = ComicMetadataSource.FileNameFallback
        });
    }

    partial void OnSelectedItemChanged(ComicTileModel? value)
    {
        SelectedItemChanged?.Invoke(this, value);
        NotifySelectionCommandsChanged();

        if (IsInfoPanelOpen && value is not null)
        {
            _ = LoadInfoPanelAsync(value);
        }
    }

    partial void OnIsConvertingToCbzChanged(bool value)
    {
        NotifySelectionCommandsChanged();
    }

    partial void OnIsDeletingFromLibraryChanged(bool value)
    {
        DeleteFromLibraryCommand.NotifyCanExecuteChanged();
    }

    private async Task PopulateItemsAsync(IReadOnlyList<ComicLibraryItem> items)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            var result = _presentationService.Populate(items, Items, Groups, SelectedItems, _itemIndex, SelectedItem, _activeArrangement, _activeGrouping);
            SelectedItem = result.SelectedItem;
            IsGroupedView = result.IsGrouped;
            OnPropertyChanged(nameof(IsFlatView));
            NotifySelectionCommandsChanged();
        });
    }

    private void ApplyVisualOrdering()
    {
        var result = _presentationService.ApplyVisualOrdering(Items, Groups, SelectedItems, SelectedItem, _activeArrangement, _activeGrouping);
        SelectedItem = result.SelectedItem;
        IsGroupedView = result.IsGrouped;
        OnPropertyChanged(nameof(IsFlatView));
    }

    private readonly record struct BatchApplyResult(bool HasChanges, bool SelectionChanged);
}
