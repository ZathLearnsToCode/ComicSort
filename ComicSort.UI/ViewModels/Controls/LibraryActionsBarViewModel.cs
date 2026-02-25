using Avalonia.Threading;
using ComicSort.Engine.Models;
using ComicSort.Engine.Services;
using ComicSort.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;

namespace ComicSort.UI.ViewModels.Controls;

public partial class LibraryActionsBarViewModel : ViewModelBase
{
    private readonly IDialogService _dialogService;
    private readonly ISettingsService _settingsService;
    private readonly IScanService _scanService;

    [ObservableProperty]
    private string addFolderButtonText = "Add Folder to Library";

    [ObservableProperty]
    private string scanLibraryButtonText = "Scan Library";

    [ObservableProperty]
    private string cancelScanButtonText = "Cancel Scan";

    [ObservableProperty]
    private string settingsButtonText = "Settings";

    [ObservableProperty]
    private string statusText = "Ready";

    [ObservableProperty]
    private bool addFolderEnabled = true;

    [ObservableProperty]
    private bool scanLibraryEnabled = true;

    [ObservableProperty]
    private bool isScanRunning;

    public LibraryActionsBarViewModel(
        IDialogService dialogService,
        ISettingsService settingsService,
        IScanService scanService)
    {
        _dialogService = dialogService;
        _settingsService = settingsService;
        _scanService = scanService;

        _scanService.ProgressChanged += OnScanProgressChanged;
        _scanService.StateChanged += OnScanStateChanged;

        IsScanRunning = _scanService.IsRunning;
    }

    partial void OnIsScanRunningChanged(bool value)
    {
        AddFolderEnabled = !value;
        ScanLibraryEnabled = !value;
    }

    [RelayCommand]
    private async Task AddFolderToLibraryAsync()
    {
        if (IsScanRunning)
        {
            return;
        }

        var folder = await _dialogService.ShowOpenFolderDialogAsync("Select a folder to add to the library");
        if (folder is null)
        {
            return;
        }

        await _settingsService.SavetoSettings(folder);
        StatusText = $"Added folder: {folder}";
    }

    [RelayCommand]
    private Task ScanLibraryAsync()
    {
        if (!ScanLibraryEnabled)
        {
            return Task.CompletedTask;
        }

        StatusText = "Starting scan...";
        _ = _scanService.StartScanAsync();
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void CancelScan()
    {
        if (!IsScanRunning)
        {
            return;
        }

        StatusText = "Cancelling scan...";
        _scanService.CancelScan();
    }

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        var saved = await _dialogService.ShowSettingsDialogAsync();
        StatusText = saved ? "Settings saved" : "Settings closed";
    }

    private void OnScanProgressChanged(object? sender, ScanProgressUpdate update)
    {
        Dispatcher.UIThread.Post(() =>
        {
            var pathText = string.IsNullOrWhiteSpace(update.CurrentFilePath)
                ? string.Empty
                : $" | {update.CurrentFilePath}";

            StatusText =
                $"{update.Stage} E:{update.FilesEnumerated} I:{update.FilesInserted} U:{update.FilesUpdated} S:{update.FilesSkipped} F:{update.FilesFailed}{pathText}";
        });
    }

    private void OnScanStateChanged(object? sender, ScanStateChangedEventArgs eventArgs)
    {
        Dispatcher.UIThread.Post(() =>
        {
            IsScanRunning = eventArgs.IsRunning;
            if (!eventArgs.IsRunning && string.IsNullOrWhiteSpace(StatusText))
            {
                StatusText = eventArgs.Stage;
            }
        });
    }
}
