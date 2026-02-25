using Avalonia.Threading;
using Avalonia.Media.Imaging;
using ComicSort.Engine.Models;
using ComicSort.Engine.Services;
using ComicSort.UI.Models;
using CommunityToolkit.Mvvm.ComponentModel;
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
    private readonly Dictionary<string, ComicTileModel> _itemIndex = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private readonly List<string> _activeGrouping = ["Not Grouped"];
    private int _initialized;

    public ComicGridViewModel(
        IComicDatabaseService comicDatabaseService,
        IScanRepository scanRepository,
        IScanService scanService)
    {
        _comicDatabaseService = comicDatabaseService;
        _scanRepository = scanRepository;
        scanService.ComicFileSaved += OnComicFileSaved;
    }

    public ObservableCollection<ComicTileModel> Items { get; } = [];
    public ObservableCollection<ComicGroupModel> Groups { get; } = [];

    [ObservableProperty]
    private ComicTileModel? selectedItem;

    [ObservableProperty]
    private bool isGroupedView;

    public bool IsFlatView => !IsGroupedView;

    public event EventHandler<ComicTileModel?>? SelectedItemChanged;

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
        var items = await _scanRepository.GetLibraryItemsAsync(InitialLoadSize);
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Items.Clear();
            _itemIndex.Clear();

            foreach (var item in items)
            {
                var tile = ToTile(item);
                _itemIndex[item.FilePath] = tile;
                Items.Add(tile);
            }

            RebuildGroupingView();

            if (SelectedItem is null && Items.Count > 0)
            {
                SelectedItem = Items[0];
            }
        });
    }

    private void OnComicFileSaved(object? sender, ComicFileSavedEventArgs eventArgs)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var item = eventArgs.Item;
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
            RebuildGroupingView();
        });
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
    }
}
