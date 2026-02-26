using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using ComicSort.Engine.Services;
using ComicSort.UI.Models.Dialogs;
using ComicSort.UI.ViewModels.Dialogs;
using ComicSort.UI.Views.Dialogs;
using System.Linq;
using System.Threading.Tasks;

namespace ComicSort.UI.Services;

public sealed class DialogService : IDialogService
{
    private readonly ISettingsService _settingsService;
    private readonly IThemeService _themeService;

    public DialogService(ISettingsService settingsService, IThemeService themeService)
    {
        _settingsService = settingsService;
        _themeService = themeService;
    }

    public async Task<string?> ShowOpenFolderDialogAsync(string title)
    {
        if (GetStorageProvider() is not { } provider)
        {
            return null;
        }

        var results = await provider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return results.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> ShowOpenFileDialogAsync(string title)
    {
        if (GetStorageProvider() is not { } provider)
        {
            return null;
        }

        var results = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = title,
            AllowMultiple = false
        });

        return results.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<bool> ShowSettingsDialogAsync()
    {
        if (GetActiveWindow() is not { } owner)
        {
            return false;
        }

        var dialog = new SettingsDialog
        {
            DataContext = new SettingsDialogViewModel(_settingsService, this, _themeService)
        };

        return await dialog.ShowDialog<bool>(owner);
    }

    public async Task<SmartListEditorResult?> ShowSmartListEditorDialogAsync(SmartListEditorResult initialState)
    {
        if (GetActiveWindow() is not { } owner)
        {
            return null;
        }

        var dialog = new SmartListEditorDialog
        {
            DataContext = new SmartListEditorDialogViewModel(initialState)
        };

        return await dialog.ShowDialog<SmartListEditorResult?>(owner);
    }

    public async Task<CbzConversionConfirmationResult?> ShowCbzConversionConfirmationDialogAsync(
        int fileCount,
        bool sendOriginalToRecycleBinDefault)
    {
        if (GetActiveWindow() is not { } owner)
        {
            return null;
        }

        var dialog = new CbzConversionConfirmationDialog
        {
            DataContext = new CbzConversionConfirmationDialogViewModel(
                fileCount,
                sendOriginalToRecycleBinDefault)
        };

        return await dialog.ShowDialog<CbzConversionConfirmationResult?>(owner);
    }

    private static IStorageProvider? GetStorageProvider()
    {
        return GetActiveWindow()?.StorageProvider;
    }

    private static Window? GetActiveWindow()
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
        {
            return null;
        }

        return desktop.Windows.FirstOrDefault(x => x.IsActive) ?? desktop.MainWindow;
    }
}
