using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ComicSort.UI.Models;

namespace ComicSort.UI.Views.Controls;

public partial class SidebarView : UserControl
{
    public SidebarView()
    {
        InitializeComponent();
    }

    private void SmartListTextBox_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender is TextBox { DataContext: SidebarItem item })
        {
            item.IsEditing = false;
        }
    }

    private void SmartListTextBox_KeyDown(object? sender, KeyEventArgs e)
    {
        if (sender is not TextBox { DataContext: SidebarItem item })
        {
            return;
        }

        if (e.Key is Key.Enter or Key.Escape)
        {
            item.IsEditing = false;
            e.Handled = true;
        }
    }
}
