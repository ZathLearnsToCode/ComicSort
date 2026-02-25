using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ComicSort.UI.ViewModels.Controls;

namespace ComicSort.UI.Views.Controls;

public partial class TopToolbarView : UserControl
{
    private bool _appendPrimaryFilterSelection;

    public TopToolbarView()
    {
        InitializeComponent();
    }

    private void PrimaryFilterButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control button || button.ContextMenu is null)
        {
            return;
        }

        button.ContextMenu.Open(button);
    }

    private void PrimaryFilterMenuItem_OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        _appendPrimaryFilterSelection = e.KeyModifiers.HasFlag(KeyModifiers.Control);
    }

    private void PrimaryFilterMenuItem_OnClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not TopToolbarViewModel viewModel || sender is not MenuItem menuItem)
        {
            _appendPrimaryFilterSelection = false;
            return;
        }

        var filterName = menuItem.Tag?.ToString() ?? menuItem.Header?.ToString();
        if (!string.IsNullOrWhiteSpace(filterName))
        {
            viewModel.ApplyPrimaryFilterSelection(filterName, _appendPrimaryFilterSelection);
        }

        _appendPrimaryFilterSelection = false;
    }
}
