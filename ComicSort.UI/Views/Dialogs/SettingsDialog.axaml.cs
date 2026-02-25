using Avalonia.Controls;
using ComicSort.UI.ViewModels.Dialogs;
using System.Threading.Tasks;

namespace ComicSort.UI.Views.Dialogs;

public partial class SettingsDialog : Window
{
    private SettingsDialogViewModel? _viewModel;
    private bool _initialized;

    public SettingsDialog()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Opened += OnOpened;
    }

    private async void OnOpened(object? sender, System.EventArgs e)
    {
        if (_initialized || _viewModel is null)
        {
            return;
        }

        _initialized = true;
        await _viewModel.InitializeAsync();
    }

    private void OnDataContextChanged(object? sender, System.EventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested -= OnCloseRequested;
        }

        _viewModel = DataContext as SettingsDialogViewModel;
        if (_viewModel is not null)
        {
            _viewModel.CloseRequested += OnCloseRequested;
        }
    }

    private void OnCloseRequested(object? sender, SettingsDialogCloseRequestedEventArgs e)
    {
        Close(e.Saved);
    }
}
