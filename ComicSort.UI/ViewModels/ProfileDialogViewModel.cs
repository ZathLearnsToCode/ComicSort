using Avalonia.Controls;
using ComicSort.UI.Models;
using ComicSort.UI.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Threading.Tasks;


namespace ComicSort.UI.ViewModels
{
    public partial class ProfileDialogViewModel : ViewModelBase
    {
        private readonly IDialogServices _dialogServices;

        public Array StorageTypes { get; } =
        Enum.GetValues(typeof(StorageType));

        [ObservableProperty]
        private StorageType selectedStorageType;

        [ObservableProperty]
        private string profileName = string.Empty;

        [ObservableProperty]
        private string selectedPath = string.Empty;

        public ProfileDialogViewModel(IDialogServices dialogServices)
        {
            _dialogServices = dialogServices;
        }

        [RelayCommand]
        private void OkButton(Window window)
        {
           window.Close(true);
        }

        [RelayCommand]
        private async Task BrowseButton()
        {    
            var folderPath = await _dialogServices.ShowOpenFolderDialogAsync("Select profile storage location");
            if (folderPath is null)
                return;
            SelectedPath = folderPath;
        }

    }
}
