using Avalonia.Threading;
using Avalonia.Media.Imaging;
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

    private readonly IComicDatabaseService _comicDatabaseService;
    private readonly IScanRepository _scanRepository;
    private readonly ISmartListExecutionService _smartListExecutionService;
    private readonly ISmartListEvaluator _smartListEvaluator;
    private readonly ISmartListExpressionService _smartListExpressionService;
    private readonly IComicConversionService _comicConversionService;
    private readonly IDialogService _dialogService;
    private readonly ISettingsService _settingsService;
    private readonly Dictionary<string, ComicTileModel> _itemIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly List<string> _activeGrouping = ["Not Grouped"];
    private int _initialized;
    private bool _smartListFilterActive;
    private MatcherGroupNode? _activeSmartListExpression;
    private string _activeSmartListName = "All Comics";

    public ComicGridViewModel(
        IComicDatabaseService comicDatabaseService,
        IScanRepository scanRepository,
        ISmartListExecutionService smartListExecutionService,
        ISmartListEvaluator smartListEvaluator,
        ISmartListExpressionService smartListExpressionService,
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
        _comicConversionService = comicConversionService;
        _dialogService = dialogService;
        _settingsService = settingsService;
        scanService.ComicFileSaved += OnComicFileSaved;
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
        Dispatcher.UIThread.Post(() =>
        {
            var item = eventArgs.Item;
            if (_smartListFilterActive && _activeSmartListExpression is not null)
            {
                var projection = ToProjection(item);
                var isMatch = _smartListEvaluator.IsMatch(_activeSmartListExpression, projection);
                if (!isMatch)
                {
                    if (_itemIndex.TryGetValue(item.FilePath, out var existing))
                    {
                        Items.Remove(existing);
                        _itemIndex.Remove(item.FilePath);
                        SelectedItems.Remove(existing);
                        if (ReferenceEquals(SelectedItem, existing))
                        {
                            SelectedItem = Items.FirstOrDefault();
                            SetSelectedItems(SelectedItem is null ? [] : [SelectedItem]);
                        }
                    }

                    RebuildGroupingView();
                    SetFilterSummary($"Filter: {_activeSmartListName} ({Items.Count})");
                    return;
                }
            }

            if (_itemIndex.TryGetValue(item.FilePath, out var existingTile))
            {
                existingTile.DisplayTitle = item.DisplayTitle;
                existingTile.FilePath = item.FilePath;
                existingTile.FileDirectory = item.FileDirectory;
                existingTile.Series = CoalesceGroupValue(item.Series);
                existingTile.Publisher = CoalesceGroupValue(item.Publisher);
                existingTile.ThumbnailPath = item.ThumbnailPath;
                existingTile.ThumbnailImage = LoadThumbnailBitmap(item.ThumbnailPath);
                existingTile.IsThumbnailReady = item.IsThumbnailReady;
                existingTile.FileTypeTag = item.FileTypeTag;
                existingTile.LastScannedUtc = item.LastScannedUtc;
                RebuildGroupingView();
                return;
            }

            var newTile = ToTile(item);
            _itemIndex[item.FilePath] = newTile;
            Items.Add(newTile);
            SelectedItem ??= newTile;
            if (SelectedItems.Count == 0 && SelectedItem is not null)
            {
                SelectedItems.Add(SelectedItem);
                ConvertToCbzCommand.NotifyCanExecuteChanged();
            }

            RebuildGroupingView();
            SetFilterSummary($"Filter: {(_smartListFilterActive ? _activeSmartListName : "All Comics")} ({Items.Count})");
        });
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
            RebuildGroupingView();
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
        RebuildGroupingView();
    }

    private static ComicTileModel ToTile(ComicLibraryItem item)
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
            ThumbnailImage = LoadThumbnailBitmap(item.ThumbnailPath),
            IsThumbnailReady = item.IsThumbnailReady,
            FileTypeTag = item.FileTypeTag,
            LastScannedUtc = item.LastScannedUtc
        };
    }

    private async Task PopulateItemsAsync(IReadOnlyList<ComicLibraryItem> items)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Items.Clear();
            _itemIndex.Clear();
            SelectedItems.Clear();

            foreach (var item in items)
            {
                var tile = ToTile(item);
                _itemIndex[item.FilePath] = tile;
                Items.Add(tile);
            }

            RebuildGroupingView();
            SelectedItem = Items.FirstOrDefault();
            if (SelectedItem is not null)
            {
                SelectedItems.Add(SelectedItem);
            }

            ConvertToCbzCommand.NotifyCanExecuteChanged();
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

    private static Bitmap? LoadThumbnailBitmap(string? thumbnailPath)
    {
        if (string.IsNullOrWhiteSpace(thumbnailPath) || !File.Exists(thumbnailPath))
        {
            return null;
        }

        try
        {
            return new Bitmap(thumbnailPath);
        }
        catch
        {
            return null;
        }
    }

    partial void OnSelectedItemChanged(ComicTileModel? value)
    {
        SelectedItemChanged?.Invoke(this, value);
        ConvertToCbzCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsConvertingToCbzChanged(bool value)
    {
        ConvertToCbzCommand.NotifyCanExecuteChanged();
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
}
