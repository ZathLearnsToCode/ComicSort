using ComicSort.UI.Models.Dialogs;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ComicSort.UI.Services;

public interface IDialogService
{
    Task<string?> ShowOpenFileDialogAsync(string title);

    Task<string?> ShowOpenFolderDialogAsync(string title);

    Task<IReadOnlyList<string>?> ShowTargetedScanFolderSelectionDialogAsync(
        IReadOnlyList<string> availableFolders);

    Task<bool> ShowSettingsDialogAsync();

    Task<SmartListEditorResult?> ShowSmartListEditorDialogAsync(SmartListEditorResult initialState);

    Task<CbzConversionConfirmationResult?> ShowCbzConversionConfirmationDialogAsync(
        int fileCount,
        bool sendOriginalToRecycleBinDefault);

    Task<LibraryDeleteConfirmationResult?> ShowLibraryDeleteConfirmationDialogAsync(
        int fileCount,
        bool sendToRecycleBinDefault);
}
