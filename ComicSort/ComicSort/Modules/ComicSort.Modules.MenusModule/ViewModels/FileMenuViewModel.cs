using ComicSort.DataAccess;
using ComicSort.Domain.Models;
using Microsoft.Win32;
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

        private DelegateCommand _libraryManagementCommand;
        public DelegateCommand LibraryManagementCommand =>
            _libraryManagementCommand ?? (_libraryManagementCommand = new DelegateCommand(ExecuteLibraryManagementCommand));

        void ExecuteLibraryManagementCommand()
        {
            _dialogService.ShowDialog("LibraryManagementDialog", null, null);
        }

        private DelegateCommand _addFilesCommand;
        public DelegateCommand AddFilesCommand =>
            _addFilesCommand ?? (_addFilesCommand = new DelegateCommand(ExecuteAddFilesCommand));

        void ExecuteAddFilesCommand()
        {
            
            
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
