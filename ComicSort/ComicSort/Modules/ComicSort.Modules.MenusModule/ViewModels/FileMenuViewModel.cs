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

        #region New Library Command

        private DelegateCommand newLibraryCommand;
        public DelegateCommand NewLibraryCommand =>
            newLibraryCommand ?? (newLibraryCommand = new DelegateCommand(ExecuteNewLibraryCommand));

        void ExecuteNewLibraryCommand()
        {
            _dialogService.ShowDialog("NewLibraryDialog", null, null);
        }

        #endregion

        private DelegateCommand _addFilesCommand;
        public DelegateCommand AddFilesCommand =>
            _addFilesCommand ?? (_addFilesCommand = new DelegateCommand(ExecuteAddFilesCommand));

        void ExecuteAddFilesCommand()
        {
            var comiclist = new ComicBookList();
            var comicbook = new ComicBook();
            var ofd = new OpenFileDialog();
            ofd.Multiselect = true;
            ofd.ShowDialog();
            var filelist = ofd.FileNames;

            foreach(var files in filelist)
            {
                                
            }

            using (var db = new ComicDBContext())
            {
                db.Add(comiclist);
                db.SaveChanges();
            }
            
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
