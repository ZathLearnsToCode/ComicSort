using ComicSort.UI.Services;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;

namespace ComicSort.UI.ViewModels
{
    public partial class MainWindowViewModel : ViewModelBase
    {
        private readonly IDialogServices _dialogServices;
        public string Greeting { get; } = "Welcome to ComicSort!";

        public MainWindowViewModel(IDialogServices dialogServices)
        {
            _dialogServices = dialogServices;
        }

        [RelayCommand]
        private async Task AddFolder()
        {
            var folderPath = await _dialogServices.ShowOpenFolderDialogAsync("Select a folder to add");
            if (!string.IsNullOrEmpty(folderPath))
            {
                // Handle the selected folder path
            }
        }

        [RelayCommand]
        private async Task Scan()
        {

        }
    }
}
