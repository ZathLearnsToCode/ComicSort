using Avalonia.Controls;
using ComicSort.UI.Models;
using ComicSort.UI.ViewModels.Controls;
using System.Collections.Generic;
using System.Linq;

namespace ComicSort.UI.Views.Controls;

public partial class ComicGridView : UserControl
{
    private readonly HashSet<ComicTileModel> _groupedSelection = [];

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
        _groupedSelection.Clear();
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

        foreach (var removed in e.RemovedItems.OfType<ComicTileModel>())
        {
            _groupedSelection.Remove(removed);
        }

        foreach (var added in e.AddedItems.OfType<ComicTileModel>())
        {
            _groupedSelection.Add(added);
        }

        var selectedTiles = _groupedSelection.ToArray();
        viewModel.SetSelectedItems(selectedTiles);

        viewModel.SelectedItem = listBox.SelectedItem as ComicTileModel ?? selectedTiles.FirstOrDefault();
    }
}
