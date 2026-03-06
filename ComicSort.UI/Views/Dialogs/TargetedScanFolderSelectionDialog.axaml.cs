using Avalonia.Controls;
using ComicSort.UI.ViewModels.Dialogs;
using System;

namespace ComicSort.UI.Views.Dialogs;

public partial class TargetedScanFolderSelectionDialog : Window
{
    private TargetedScanFolderSelectionDialogViewModel? _viewModel;

    public TargetedScanFolderSelectionDialog()
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

        _viewModel = DataContext as TargetedScanFolderSelectionDialogViewModel;
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested += OnCloseRequested;
        }
    }

    private void OnCloseRequested(object? sender, TargetedScanFolderSelectionCloseRequestedEventArgs e)
    {
        Close(e.SelectedFolders);
    }
}
