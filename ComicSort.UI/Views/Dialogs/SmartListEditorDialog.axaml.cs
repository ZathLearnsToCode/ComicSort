using Avalonia.Controls;
using Avalonia.Interactivity;
using ComicSort.UI.ViewModels.Dialogs;
using System;

namespace ComicSort.UI.Views.Dialogs;

public partial class SmartListEditorDialog : Window
{
    private SmartListEditorDialogViewModel? _viewModel;

    public SmartListEditorDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
        }

        _viewModel = DataContext as SmartListEditorDialogViewModel;
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested += OnCloseRequested;
        }
    }

    private void OnCloseRequested(object? sender, SmartListEditorDialogCloseRequestedEventArgs e)
    {
        Close(e.Result);
    }

    private void RuleFieldButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control ||
            control.DataContext is not SmartListRuleEditorRowViewModel row ||
            _viewModel is null)
        {
            return;
        }

        var menu = BuildFieldMenu(row, _viewModel);
        menu.Open(control);
    }

    private void RuleActionsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is not Control control ||
            control.DataContext is not SmartListRuleEditorRowViewModel row ||
            _viewModel is null)
        {
            return;
        }

        var menu = BuildRuleActionsMenu(row, _viewModel);
        menu.Open(control);
    }

    private static ContextMenu BuildFieldMenu(
        SmartListRuleEditorRowViewModel row,
        SmartListEditorDialogViewModel viewModel)
    {
        var menu = new ContextMenu();

        var topLevelItems = new System.Collections.Generic.List<MenuItem>();
        foreach (var category in viewModel.FieldCategories)
        {
            var categoryItem = new MenuItem
            {
                Header = category.Header
            };

            var childItems = new System.Collections.Generic.List<MenuItem>();
            foreach (var field in category.Fields)
            {
                var fieldItem = new MenuItem
                {
                    Header = field
                };

                fieldItem.Click += (_, __) =>
                {
                    viewModel.SelectFieldCommand.Execute(new SmartListFieldSelection(row, field));
                };

                childItems.Add(fieldItem);
            }

            categoryItem.ItemsSource = childItems;
            topLevelItems.Add(categoryItem);
        }

        menu.ItemsSource = topLevelItems;
        return menu;
    }

    private static ContextMenu BuildRuleActionsMenu(
        SmartListRuleEditorRowViewModel row,
        SmartListEditorDialogViewModel viewModel)
    {
        var menu = new ContextMenu();

        var newRule = new MenuItem { Header = "New Rule    Ctrl+R" };
        newRule.Click += (_, __) => viewModel.AddRuleAfterCommand.Execute(row);

        var newGroup = new MenuItem { Header = "New Group   Ctrl+G" };
        newGroup.Click += (_, __) => viewModel.AddGroupAfterCommand.Execute(row);

        var delete = new MenuItem { Header = "Delete" };
        delete.Click += (_, __) => viewModel.DeleteRuleCommand.Execute(row);

        var cut = new MenuItem { Header = "Cut          Ctrl+X", IsEnabled = false };
        var copy = new MenuItem { Header = "Copy         Ctrl+C", IsEnabled = false };
        var paste = new MenuItem { Header = "Paste        Ctrl+V", IsEnabled = false };

        var moveUp = new MenuItem { Header = "Move Up    Ctrl+U" };
        moveUp.Click += (_, __) => viewModel.MoveRuleUpCommand.Execute(row);

        var moveDown = new MenuItem { Header = "Move Down  Ctrl+D" };
        moveDown.Click += (_, __) => viewModel.MoveRuleDownCommand.Execute(row);

        menu.ItemsSource = new object[]
        {
            newRule,
            newGroup,
            new Separator(),
            delete,
            new Separator(),
            cut,
            copy,
            paste,
            new Separator(),
            moveUp,
            moveDown
        };

        return menu;
    }
}
