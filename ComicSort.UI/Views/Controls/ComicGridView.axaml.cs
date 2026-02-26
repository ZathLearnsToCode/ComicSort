using Avalonia.Controls;
using Avalonia.LogicalTree;
using ComicSort.UI.Models;
using ComicSort.UI.ViewModels.Controls;
using System.Collections.Generic;
using System.Linq;

namespace ComicSort.UI.Views.Controls;

public partial class ComicGridView : UserControl
{
    public ComicGridView()
    {
        InitializeComponent();
    }

    private void FlatListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not ComicGridViewModel viewModel ||
            sender is not ListBox listBox)
        {
            return;
        }

        var selectedTiles = listBox.SelectedItems?
            .OfType<ComicTileModel>()
            .ToArray() ?? [];
        viewModel.SetSelectedItems(selectedTiles);

        viewModel.SelectedItem = listBox.SelectedItem as ComicTileModel ?? selectedTiles.FirstOrDefault();
    }

    private void GroupedListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not ComicGridViewModel viewModel ||
            sender is not ListBox listBox)
        {
            return;
        }

        var selectedTiles = new List<ComicTileModel>();
        foreach (var groupedList in this.GetLogicalDescendants().OfType<ListBox>())
        {
            if (!groupedList.IsVisible || groupedList.SelectedItems is null)
            {
                continue;
            }

            selectedTiles.AddRange(groupedList.SelectedItems.OfType<ComicTileModel>());
        }

        viewModel.SetSelectedItems(selectedTiles);

        viewModel.SelectedItem = listBox.SelectedItem as ComicTileModel ?? selectedTiles.FirstOrDefault();
    }
}
