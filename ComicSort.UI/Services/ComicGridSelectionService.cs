using ComicSort.UI.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ComicSort.UI.Services;

public sealed class ComicGridSelectionService
{
    public IReadOnlyList<ComicTileModel> ResolveActionTargets(
        ComicTileModel? contextItem,
        IReadOnlyCollection<ComicTileModel> selectedItems,
        ComicTileModel? selectedItem)
    {
        if (contextItem is not null)
        {
            if (selectedItems.Any(x => string.Equals(x.FilePath, contextItem.FilePath, StringComparison.OrdinalIgnoreCase)))
            {
                return selectedItems.Distinct().ToArray();
            }

            return [contextItem];
        }

        if (selectedItems.Count > 0)
        {
            return selectedItems.Distinct().ToArray();
        }

        return selectedItem is null ? [] : [selectedItem];
    }

    public void SetSelectedItems(ObservableCollection<ComicTileModel> target, IReadOnlyList<ComicTileModel> selectedItems)
    {
        target.Clear();
        foreach (var item in selectedItems.Where(x => x is not null).Distinct())
        {
            target.Add(item);
        }
    }

    public SelectionRestoreResult RestoreSelection(
        IReadOnlyList<ComicTileModel> items,
        IReadOnlyList<ComicTileModel> selectedItemsSnapshot,
        ComicTileModel? selectedItemSnapshot)
    {
        var selectedItem = selectedItemSnapshot is not null && items.Contains(selectedItemSnapshot)
            ? selectedItemSnapshot
            : items.FirstOrDefault();
        var restoredSelection = selectedItemsSnapshot.Where(items.Contains).ToArray();
        if (restoredSelection.Length == 0 && selectedItem is not null)
        {
            restoredSelection = [selectedItem];
        }

        return new SelectionRestoreResult(selectedItem, restoredSelection);
    }
}

public sealed class SelectionRestoreResult
{
    public SelectionRestoreResult(ComicTileModel? selectedItem, IReadOnlyList<ComicTileModel> selectedItems)
    {
        SelectedItem = selectedItem;
        SelectedItems = selectedItems;
    }

    public ComicTileModel? SelectedItem { get; }

    public IReadOnlyList<ComicTileModel> SelectedItems { get; }
}
