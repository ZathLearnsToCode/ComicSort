using Avalonia.Controls;
using ComicSort.Core.Services;
using ComicSort.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Linq;
using System.Threading.Tasks;

namespace ComicSort.UI.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly IDialogServices _dialogServices;
        private readonly ISettingsServices _settings;

        private string? _selectedFolder;
        public string Greeting { get; } = "Welcome to ComicSort!";

        [ObservableProperty]
        private string? errorMessage;

        public MainWindowViewModel(IDialogServices dialogServices, ISettingsServices settings)
        {
            _dialogServices = dialogServices;
            _settings = settings;

            _selectedFolder = _settings.Settings.ComicFolders.FirstOrDefault();
        }

        [RelayCommand]
        private async Task AddFolder()
        {
            var folderPath = await _dialogServices.ShowOpenFolderDialogAsync("Select a folder to add");
            if (folderPath is null)
                return;

            if (!_settings.TryAddComicFolder(folderPath, out var error))
            {
                // Option 1: Store error message in ViewModel (recommended)
                ErrorMessage = error;
                return;
            }

            // Clear error
            ErrorMessage = null;
        }

        [RelayCommand]
        private async Task Scan()
        {

        }
    }
}
