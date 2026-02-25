using ComicSort.Engine.Services;
using ComicSort.Engine.Settings;
using ComicSort.UI.Models;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ComicSort.UI.ViewModels.Controls;

public partial class SidebarViewModel : ViewModelBase
{
    private readonly ISettingsService _settingsService;
    private readonly IScanRepository _scanRepository;
    private readonly IScanService _scanService;
    private readonly IComicDatabaseService _comicDatabaseService;
    private readonly Dictionary<SidebarItem, ComicListItem> _settingsMap = [];
    private readonly SidebarItem _allComicsItem;
    private int _libraryCountBaseline;

    public SidebarViewModel(
        ISettingsService settingsService,
        IScanRepository scanRepository,
        IScanService scanService,
        IComicDatabaseService comicDatabaseService)
    {
        _settingsService = settingsService;
        _scanRepository = scanRepository;
        _scanService = scanService;
        _comicDatabaseService = comicDatabaseService;

        _allComicsItem = new SidebarItem("All Comics", 0);

        LibraryItems =
        [
            _allComicsItem
        ];

        SmartListItems = BuildSmartListItems(settingsService.CurrentSettings);

        SelectedSidebarItem = LibraryItems.FirstOrDefault();
        SelectedSmartListItem = SmartListItems.FirstOrDefault();

        _scanService.ProgressChanged += OnScanProgressChanged;
        _scanService.StateChanged += OnScanStateChanged;
        _ = LoadLibraryCountAsync();
    }

    public ObservableCollection<SidebarItem> LibraryItems { get; }

    public ObservableCollection<SidebarItem> SmartListItems { get; }

    [ObservableProperty]
    private SidebarItem? selectedSidebarItem;

    [ObservableProperty]
    private SidebarItem? selectedSmartListItem;

    [ObservableProperty]
    private string libraryHeaderText = "My Library";

    [RelayCommand]
    private void OpenLibrarySettings()
    {
        // Placeholder action for the library settings button.
    }

    [RelayCommand]
    private async Task AddSmartList()
    {
        var target = SelectedSmartListItem;
        if (target is not null)
        {
            await AddSmartListUnderAsync(target);
            return;
        }

        var defaultParent = GetDefaultSmartListRoot();
        if (defaultParent is not null)
        {
            await AddSmartListUnderAsync(defaultParent);
        }
    }

    [RelayCommand]
    private async Task AddChildSmartList(SidebarItem? item)
    {
        if (item is null)
        {
            return;
        }

        await AddSmartListUnderAsync(item);
    }

    [RelayCommand]
    private void RenameSmartList(SidebarItem? item)
    {
        if (item is null || item.IsHeader)
        {
            return;
        }

        item.IsEditing = true;
        SelectedSmartListItem = item;
    }

    [RelayCommand]
    private async Task RemoveSmartList(SidebarItem? item)
    {
        if (item is null || item.IsHeader)
        {
            return;
        }

        var parent = FindParent(SmartListItems, item);
        if (parent is null)
        {
            return;
        }

        if (!_settingsMap.TryGetValue(parent, out var parentModel) ||
            !_settingsMap.TryGetValue(item, out var itemModel))
        {
            return;
        }

        if (!RemoveFromCollection(SmartListItems, item))
        {
            return;
        }

        parentModel.Items.RemoveAll(x => x.Id == itemModel.Id);
        DetachAndUnmap(item);

        SelectedSmartListItem = SmartListItems.FirstOrDefault();
        await _settingsService.SaveAsync();
    }

    [RelayCommand]
    private void SortSmartLists()
    {
        SortCollection(SmartListItems);
    }

    private async Task AddSmartListUnderAsync(SidebarItem parent)
    {
        if (!_settingsMap.TryGetValue(parent, out var parentModel))
        {
            return;
        }

        var newSmartList = new ComicListItem
        {
            Id = Guid.NewGuid(),
            Type = "ComicSmartListItem",
            Name = "New Smart List",
            BookCount = 0,
            NewBookCount = 0,
            NewBookCountDateUtc = DateTimeOffset.UtcNow,
            Matchers = []
        };

        parentModel.Items.Add(newSmartList);

        var newItem = CreateSidebarItem(newSmartList);
        parent.Children.Add(newItem);
        SelectedSmartListItem = newItem;
        newItem.IsEditing = true;

        await _settingsService.SaveAsync();
    }

    private async void SmartListItemOnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender is not SidebarItem item || item.IsHeader)
        {
            return;
        }

        if (e.PropertyName != nameof(SidebarItem.IsEditing) || item.IsEditing)
        {
            return;
        }

        if (!_settingsMap.TryGetValue(item, out var itemModel))
        {
            return;
        }

        var normalizedName = string.IsNullOrWhiteSpace(item.Name)
            ? itemModel.Name
            : item.Name.Trim();

        if (string.Equals(itemModel.Name, normalizedName, System.StringComparison.Ordinal))
        {
            return;
        }

        itemModel.Name = normalizedName;
        item.Name = normalizedName;
        await _settingsService.SaveAsync();
    }

    private void DetachAndUnmap(SidebarItem item)
    {
        item.PropertyChanged -= SmartListItemOnPropertyChanged;
        _settingsMap.Remove(item);

        foreach (var child in item.Children)
        {
            DetachAndUnmap(child);
        }
    }

    private static void SortCollection(ObservableCollection<SidebarItem> items)
    {
        var sorted = items
            .OrderByDescending(x => x.IsHeader)
            .ThenBy(x => x.Name, System.StringComparer.OrdinalIgnoreCase)
            .ToList();

        items.Clear();
        foreach (var item in sorted)
        {
            items.Add(item);
            if (item.Children.Count > 0)
            {
                SortCollection(item.Children);
            }
        }
    }

    private static bool RemoveFromCollection(ObservableCollection<SidebarItem> source, SidebarItem target)
    {
        if (source.Remove(target))
        {
            return true;
        }

        foreach (var item in source)
        {
            if (RemoveFromCollection(item.Children, target))
            {
                return true;
            }
        }

        return false;
    }

    private static SidebarItem? FindParent(ObservableCollection<SidebarItem> source, SidebarItem target)
    {
        foreach (var item in source)
        {
            if (item.Children.Contains(target))
            {
                return item;
            }

            var nested = FindParent(item.Children, target);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    private ObservableCollection<SidebarItem> BuildSmartListItems(AppSettings settings)
    {
        var folderLists = settings.ComicLists
            .Where(x => string.Equals(x.Type, "ComicListItemFolder", System.StringComparison.OrdinalIgnoreCase))
            .Select(x => CreateSidebarItem(x, isRoot: true))
            .ToList();

        return [.. folderLists];
    }

    private SidebarItem CreateSidebarItem(ComicListItem item, bool isRoot = false)
    {
        var isHeader = isRoot || string.Equals(item.Type, "ComicListItemFolder", System.StringComparison.OrdinalIgnoreCase);
        int? count = item.BookCount > 0 ? item.BookCount : null;
        var sidebarItem = new SidebarItem(item.Name, count, isHeader: isHeader);

        _settingsMap[sidebarItem] = item;
        sidebarItem.PropertyChanged += SmartListItemOnPropertyChanged;

        foreach (var child in item.Items)
        {
            sidebarItem.Children.Add(CreateSidebarItem(child));
        }

        return sidebarItem;
    }

    private SidebarItem? GetDefaultSmartListRoot()
    {
        var namedRoot = SmartListItems.FirstOrDefault(x =>
            x.IsHeader && string.Equals(x.Name, "Smart Lists", StringComparison.OrdinalIgnoreCase));

        if (namedRoot is not null)
        {
            return namedRoot;
        }

        var firstHeader = SmartListItems.FirstOrDefault(x => x.IsHeader);
        if (firstHeader is not null)
        {
            return firstHeader;
        }

        return SmartListItems.FirstOrDefault();
    }

    private async Task LoadLibraryCountAsync()
    {
        await _comicDatabaseService.InitializeAsync();
        var totalCount = await _scanRepository.GetTotalCountAsync();
        Interlocked.Exchange(ref _libraryCountBaseline, totalCount);

        Dispatcher.UIThread.Post(() =>
        {
            _allComicsItem.Count = totalCount;
        });
    }

    private void OnScanProgressChanged(object? sender, Engine.Models.ScanProgressUpdate update)
    {
        var baseline = Volatile.Read(ref _libraryCountBaseline);
        var currentCount = baseline + update.FilesInserted;
        if (currentCount < 0)
        {
            currentCount = 0;
        }

        Dispatcher.UIThread.Post(() =>
        {
            _allComicsItem.Count = (int)currentCount;
        });
    }

    private void OnScanStateChanged(object? sender, Engine.Models.ScanStateChangedEventArgs eventArgs)
    {
        if (eventArgs.IsRunning)
        {
            var currentCount = _allComicsItem.Count ?? 0;
            Interlocked.Exchange(ref _libraryCountBaseline, currentCount);
            return;
        }

        _ = LoadLibraryCountAsync();
    }
}
