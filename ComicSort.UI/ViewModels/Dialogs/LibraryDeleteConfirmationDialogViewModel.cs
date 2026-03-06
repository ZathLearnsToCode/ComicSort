using ComicSort.UI.Models.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace ComicSort.UI.ViewModels.Dialogs;

public sealed partial class LibraryDeleteConfirmationDialogViewModel : ViewModelBase
{
    public LibraryDeleteConfirmationDialogViewModel(int fileCount, bool sendToRecycleBinDefault)
    {
        var safeCount = Math.Max(1, fileCount);
        var noun = safeCount == 1 ? "comic file" : "comic files";
        Message = $"Remove {safeCount} {noun} from your library?";
        SendToRecycleBin = sendToRecycleBinDefault;
    }

    [ObservableProperty]
    private string message = string.Empty;

    [ObservableProperty]
    private bool sendToRecycleBin;

    [ObservableProperty]
    private bool dontAskAgain;

    public event EventHandler<LibraryDeleteConfirmationCloseRequestedEventArgs>? CloseRequested;

    [RelayCommand]
    private void Confirm()
    {
        CloseRequested?.Invoke(
            this,
            new LibraryDeleteConfirmationCloseRequestedEventArgs(
                new LibraryDeleteConfirmationResult
                {
                    SendToRecycleBin = SendToRecycleBin,
                    DontAskAgain = DontAskAgain
                }));
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(this, new LibraryDeleteConfirmationCloseRequestedEventArgs(null));
    }
}

public sealed class LibraryDeleteConfirmationCloseRequestedEventArgs : EventArgs
{
    public LibraryDeleteConfirmationCloseRequestedEventArgs(LibraryDeleteConfirmationResult? result)
    {
        Result = result;
    }

    public LibraryDeleteConfirmationResult? Result { get; }
}
