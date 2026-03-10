using ComicSort.Engine.Models;
using ComicSort.UI.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ComicSort.UI.Services;

public sealed class ComicGridPresentationService
{
    private readonly IComicGridArrangementService _arrangementService;
    private readonly IComicGridThumbnailService _thumbnailService;
    private readonly ComicGridSelectionService _selectionService;

    public ComicGridPresentationService(
        IComicGridArrangementService arrangementService,
        IComicGridThumbnailService thumbnailService,
        ComicGridSelectionService selectionService)
    {
        _arrangementService = arrangementService;
        _thumbnailService = thumbnailService;
        _selectionService = selectionService;
    }

    public ComicGridPresentationResult Populate(
        IReadOnlyList<ComicLibraryItem> sourceItems,
        ObservableCollection<ComicTileModel> items,
        ObservableCollection<ComicGroupModel> groups,
        ObservableCollection<ComicTileModel> selectedItems,
        IDictionary<string, ComicTileModel> itemIndex,
        ComicTileModel? selectedItem,
        string arrangement,
        IReadOnlyList<string> grouping)
    {
        _thumbnailService.Clear(items);
        items.Clear();
        itemIndex.Clear();
        selectedItems.Clear();
        foreach (var item in sourceItems)
        {
            var tile = _arrangementService.CreateTile(item);
            itemIndex[item.FilePath] = tile;
            items.Add(tile);
            _thumbnailService.ApplyThumbnail(tile, item.ThumbnailPath, items);
        }

        selectedItem = items.FirstOrDefault();
        _selectionService.SetSelectedItems(selectedItems, selectedItem is null ? [] : [selectedItem]);
        return ApplyVisualOrdering(items, groups, selectedItems, selectedItem, arrangement, grouping);
    }

    public ComicGridPresentationResult ApplyVisualOrdering(
        ObservableCollection<ComicTileModel> items,
        ObservableCollection<ComicGroupModel> groups,
        ObservableCollection<ComicTileModel> selectedItems,
        ComicTileModel? selectedItem,
        string arrangement,
        IReadOnlyList<string> grouping)
    {
        selectedItem = ReorderItemsIfNeeded(items, selectedItems, selectedItem, arrangement);
        var isGrouped = !grouping.Contains("Not Grouped", StringComparer.Ordinal);
        groups.Clear();
        if (isGrouped)
        {
            foreach (var group in _arrangementService.BuildGroups(items, grouping))
            {
                groups.Add(group);
            }
        }

        return new ComicGridPresentationResult(selectedItem, isGrouped);
    }

    private ComicTileModel? ReorderItemsIfNeeded(
        ObservableCollection<ComicTileModel> items,
        ObservableCollection<ComicTileModel> selectedItems,
        ComicTileModel? selectedItem,
        string arrangement)
    {
        if (items.Count <= 1)
        {
            return selectedItem;
        }

        var arranged = _arrangementService.ArrangeItems(items, arrangement);
        if (items.SequenceEqual(arranged))
        {
            return selectedItem;
        }

        var restoreResult = _selectionService.RestoreSelection(items, selectedItems.ToArray(), selectedItem);
        items.Clear();
        foreach (var tile in arranged)
        {
            items.Add(tile);
        }

        _selectionService.SetSelectedItems(selectedItems, restoreResult.SelectedItems);
        return restoreResult.SelectedItem;
    }
}

public sealed class ComicGridPresentationResult
{
    public ComicGridPresentationResult(ComicTileModel? selectedItem, bool isGrouped)
    {
        SelectedItem = selectedItem;
        IsGrouped = isGrouped;
    }

    public ComicTileModel? SelectedItem { get; }

    public bool IsGrouped { get; }
}
