using ComicSort.UI.Models.Dialogs;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;

namespace ComicSort.UI.ViewModels.Dialogs;

public sealed partial class CbzConversionConfirmationDialogViewModel : ViewModelBase
{
    public CbzConversionConfirmationDialogViewModel(int fileCount, bool sendOriginalToRecycleBinDefault)
    {
        var safeCount = Math.Max(1, fileCount);
        var noun = safeCount == 1 ? "comic file" : "comic files";
        Message = $"Convert {safeCount} {noun} to CBZ?";
        SendOriginalToRecycleBin = sendOriginalToRecycleBinDefault;
    }

    [ObservableProperty]
    private string message = string.Empty;

    [ObservableProperty]
    private bool sendOriginalToRecycleBin;

    [ObservableProperty]
    private bool dontAskAgain;

    public event EventHandler<CbzConversionConfirmationCloseRequestedEventArgs>? CloseRequested;

    [RelayCommand]
    private void Confirm()
    {
        CloseRequested?.Invoke(
            this,
            new CbzConversionConfirmationCloseRequestedEventArgs(
                new CbzConversionConfirmationResult
                {
                    SendOriginalToRecycleBin = SendOriginalToRecycleBin,
                    DontAskAgain = DontAskAgain
                }));
    }

    [RelayCommand]
    private void Cancel()
    {
        CloseRequested?.Invoke(this, new CbzConversionConfirmationCloseRequestedEventArgs(null));
    }
}

public sealed class CbzConversionConfirmationCloseRequestedEventArgs : EventArgs
{
    public CbzConversionConfirmationCloseRequestedEventArgs(CbzConversionConfirmationResult? result)
    {
        Result = result;
    }

    public CbzConversionConfirmationResult? Result { get; }
}
