using ComicSort.Engine.Services;
using ComicSort.Engine.Settings;
using ComicSort.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ComicSort.UI.ViewModels.Dialogs;

public sealed partial class SettingsDialogViewModel : ViewModelBase
{
    private const string GeneralSection = "General";
    private const string LibrarySection = "Library";

    private readonly ISettingsService _settingsService;
    private readonly IDialogService _dialogService;
    private readonly IThemeService _themeService;
    private bool _isLoadingThemeSelection;
    private string _themeAtDialogOpen = "Soft Neutral Pro";

    public SettingsDialogViewModel(
        ISettingsService settingsService,
        IDialogService dialogService,
        IThemeService themeService)
    {
        _settingsService = settingsService;
        _dialogService = dialogService;
        _themeService = themeService;

        ThemeOptions = [.. _themeService.AvailableThemes];
    }

    public ObservableCollection<string> Sections { get; } =
    [
        GeneralSection,
        LibrarySection
    ];

    public ObservableCollection<string> ThemeOptions { get; }

    public ObservableCollection<LibraryFolderEntryViewModel> LibraryFolders { get; } = [];

    [ObservableProperty]
    private string selectedSection = GeneralSection;

    [ObservableProperty]
    private string selectedCurrentTheme = "Soft Neutral Pro";

    [ObservableProperty]
    private string selectedDefaultTheme = "Soft Neutral Pro";

    [ObservableProperty]
    private LibraryFolderEntryViewModel? selectedLibraryFolder;

    [ObservableProperty]
    private string statusText = "Configure application preferences.";

    public bool IsGeneralSectionSelected => string.Equals(SelectedSection, GeneralSection, StringComparison.Ordinal);

    public bool IsLibrarySectionSelected => string.Equals(SelectedSection, LibrarySection, StringComparison.Ordinal);

    public event EventHandler<SettingsDialogCloseRequestedEventArgs>? CloseRequested;

    public async Task InitializeAsync()
    {
        await _settingsService.InitializeAsync();
        LoadFromSettings(_settingsService.CurrentSettings);
    }

    [RelayCommand]
    private void OpenGeneralSection()
    {
        SelectedSection = GeneralSection;
    }

    [RelayCommand]
    private void OpenLibrarySection()
    {
        SelectedSection = LibrarySection;
    }

    [RelayCommand]
    private async Task AddLibraryFolderAsync()
    {
        var selectedPath = await _dialogService.ShowOpenFolderDialogAsync("Select library folder");
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        var normalizedPath = selectedPath.Trim();
        if (LibraryFolders.Any(x => string.Equals(x.Path, normalizedPath, StringComparison.OrdinalIgnoreCase)))
        {
            StatusText = "Folder already exists in library.";
            return;
        }

        var entry = new LibraryFolderEntryViewModel
        {
            Path = normalizedPath,
            Watched = false
        };

        LibraryFolders.Add(entry);
        SelectedLibraryFolder = entry;
        OpenLibraryFolderCommand.NotifyCanExecuteChanged();
        StatusText = $"Added: {normalizedPath}";
    }

    [RelayCommand(CanExecute = nameof(CanModifySelectedLibraryFolder))]
    private async Task ChangeLibraryFolderAsync()
    {
        if (SelectedLibraryFolder is null)
        {
            return;
        }

        var selectedPath = await _dialogService.ShowOpenFolderDialogAsync("Select replacement folder");
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        var normalizedPath = selectedPath.Trim();
        if (LibraryFolders.Any(x =>
                !ReferenceEquals(x, SelectedLibraryFolder) &&
                string.Equals(x.Path, normalizedPath, StringComparison.OrdinalIgnoreCase)))
        {
            StatusText = "Folder already exists in library.";
            return;
        }

        SelectedLibraryFolder.Path = normalizedPath;
        OpenLibraryFolderCommand.NotifyCanExecuteChanged();
        StatusText = $"Changed to: {normalizedPath}";
    }

    [RelayCommand(CanExecute = nameof(CanModifySelectedLibraryFolder))]
    private void RemoveLibraryFolder()
    {
        if (SelectedLibraryFolder is null)
        {
            return;
        }

        var removedPath = SelectedLibraryFolder.Path;
        LibraryFolders.Remove(SelectedLibraryFolder);
        SelectedLibraryFolder = LibraryFolders.FirstOrDefault();
        OpenLibraryFolderCommand.NotifyCanExecuteChanged();
        StatusText = $"Removed: {removedPath}";
    }

    [RelayCommand(CanExecute = nameof(CanOpenSelectedLibraryFolder))]
    private void OpenLibraryFolder()
    {
        if (SelectedLibraryFolder is null)
        {
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = SelectedLibraryFolder.Path,
                UseShellExecute = true
            });

            StatusText = $"Opened: {SelectedLibraryFolder.Path}";
        }
        catch (Exception ex)
        {
            StatusText = $"Failed to open folder: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        await _settingsService.InitializeAsync();

        var settings = _settingsService.CurrentSettings;
        settings.DefaultTheme = _themeService.NormalizeThemeName(SelectedDefaultTheme);
        settings.CurrentTheme = _themeService.NormalizeThemeName(SelectedCurrentTheme, settings.DefaultTheme);
        settings.LegacyThemeName = null;

        settings.LibraryFolders = LibraryFolders
            .Where(x => !string.IsNullOrWhiteSpace(x.Path))
            .GroupBy(x => x.Path.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(x => new LibraryFolderSetting
            {
                Folder = x.First().Path.Trim(),
                Watched = x.First().Watched
            })
            .ToList();

        _themeService.ApplyTheme(settings.CurrentTheme);
        await _settingsService.SaveAsync();
        _themeAtDialogOpen = settings.CurrentTheme;
        CloseRequested?.Invoke(this, new SettingsDialogCloseRequestedEventArgs(saved: true));
    }

    [RelayCommand]
    private void Cancel()
    {
        _themeService.ApplyTheme(_themeAtDialogOpen);
        CloseRequested?.Invoke(this, new SettingsDialogCloseRequestedEventArgs(saved: false));
    }

    partial void OnSelectedSectionChanged(string value)
    {
        OnPropertyChanged(nameof(IsGeneralSectionSelected));
        OnPropertyChanged(nameof(IsLibrarySectionSelected));
    }

    partial void OnSelectedLibraryFolderChanged(LibraryFolderEntryViewModel? value)
    {
        ChangeLibraryFolderCommand.NotifyCanExecuteChanged();
        RemoveLibraryFolderCommand.NotifyCanExecuteChanged();
        OpenLibraryFolderCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedCurrentThemeChanged(string value)
    {
        if (_isLoadingThemeSelection)
        {
            return;
        }

        var normalized = _themeService.NormalizeThemeName(value, SelectedDefaultTheme);
        if (!string.Equals(normalized, value, StringComparison.Ordinal))
        {
            _isLoadingThemeSelection = true;
            SelectedCurrentTheme = normalized;
            _isLoadingThemeSelection = false;
        }

        _themeService.ApplyTheme(normalized);
        StatusText = $"Previewing theme: {normalized}";
    }

    partial void OnSelectedDefaultThemeChanged(string value)
    {
        if (_isLoadingThemeSelection)
        {
            return;
        }

        var normalized = _themeService.NormalizeThemeName(value);
        if (!string.Equals(normalized, value, StringComparison.Ordinal))
        {
            _isLoadingThemeSelection = true;
            SelectedDefaultTheme = normalized;
            _isLoadingThemeSelection = false;
        }
    }

    private bool CanModifySelectedLibraryFolder()
    {
        return SelectedLibraryFolder is not null;
    }

    private bool CanOpenSelectedLibraryFolder()
    {
        return SelectedLibraryFolder is not null &&
               !string.IsNullOrWhiteSpace(SelectedLibraryFolder.Path) &&
               Directory.Exists(SelectedLibraryFolder.Path);
    }

    private void LoadFromSettings(AppSettings settings)
    {
        _isLoadingThemeSelection = true;

        var defaultTheme = _themeService.NormalizeThemeName(settings.DefaultTheme);
        var currentTheme = _themeService.NormalizeThemeName(settings.CurrentTheme, defaultTheme);

        SelectedDefaultTheme = defaultTheme;
        SelectedCurrentTheme = currentTheme;
        _themeAtDialogOpen = currentTheme;

        _isLoadingThemeSelection = false;
        _themeService.ApplyTheme(currentTheme);

        LibraryFolders.Clear();
        foreach (var folder in settings.LibraryFolders)
        {
            if (string.IsNullOrWhiteSpace(folder.Folder))
            {
                continue;
            }

            LibraryFolders.Add(new LibraryFolderEntryViewModel
            {
                Path = folder.Folder.Trim(),
                Watched = folder.Watched
            });
        }

        SelectedLibraryFolder = LibraryFolders.FirstOrDefault();
        StatusText = $"Current theme: {currentTheme} | Default theme: {defaultTheme}";
    }
}

public sealed partial class LibraryFolderEntryViewModel : ObservableObject
{
    [ObservableProperty]
    private string path = string.Empty;

    [ObservableProperty]
    private bool watched;
}

public sealed class SettingsDialogCloseRequestedEventArgs : EventArgs
{
    public SettingsDialogCloseRequestedEventArgs(bool saved)
    {
        Saved = saved;
    }

    public bool Saved { get; }
}
