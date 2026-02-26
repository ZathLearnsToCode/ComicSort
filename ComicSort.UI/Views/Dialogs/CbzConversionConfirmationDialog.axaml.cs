using Avalonia.Controls;
using ComicSort.UI.ViewModels.Dialogs;
using System;

namespace ComicSort.UI.Views.Dialogs;

public partial class CbzConversionConfirmationDialog : Window
{
    private CbzConversionConfirmationDialogViewModel? _viewModel;

    public CbzConversionConfirmationDialog()
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

        _viewModel = DataContext as CbzConversionConfirmationDialogViewModel;
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested += OnCloseRequested;
        }
    }

    private void OnCloseRequested(object? sender, CbzConversionConfirmationCloseRequestedEventArgs e)
    {
        Close(e.Result);
    }
}
