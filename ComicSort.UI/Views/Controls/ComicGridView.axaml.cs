using Avalonia.Controls;
using ComicSort.UI.Models;
using ComicSort.UI.ViewModels.Controls;

namespace ComicSort.UI.Views.Controls;

public partial class ComicGridView : UserControl
{
    public ComicGridView()
    {
        InitializeComponent();
    }

    private void GroupedListBox_OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not ComicGridViewModel viewModel ||
            sender is not ListBox listBox ||
            listBox.SelectedItem is not ComicTileModel selectedTile)
        {
            return;
        }

        viewModel.SelectedItem = selectedTile;
    }
}
