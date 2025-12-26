using Avalonia.Controls;
using ComicSort.UI.Services;
using CommunityToolkit.Mvvm.Input;


namespace ComicSort.UI.ViewModels
{
    public partial class ProfileDialogViewModel : ViewModelBase
    {
        private readonly IDialogServices _dialogServices;
        
        public ProfileDialogViewModel(IDialogServices dialogServices)
        {
            _dialogServices = dialogServices;
        }

        [RelayCommand]
        private void OkButton(Window window)
        {
           window.Close(true);
        }

    }
}
