using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace ComicSort.Modules.MenusModule.ViewModels
{
    public class FileMenuViewModel : BindableBase
    {
        private readonly IDialogService _dialogService;

        public FileMenuViewModel(IDialogService dialogService)
        {
            _dialogService = dialogService;
        }

        private DelegateCommand newLibraryCommand;
        public DelegateCommand NewLibraryCommand =>
            newLibraryCommand ?? (newLibraryCommand = new DelegateCommand(ExecuteNewLibraryCommand));

        void ExecuteNewLibraryCommand()
        {
            _dialogService.ShowDialog("NewLibraryDialog", null, null);
        }

        #region Exit Command
        private DelegateCommand exitCommand;
        

        public DelegateCommand ExitCommand =>
            exitCommand ?? (exitCommand = new DelegateCommand(ExecuteExitCommand));

        void ExecuteExitCommand()
        {
            Application.Current.Shutdown();
        }
        #endregion
    }
}
