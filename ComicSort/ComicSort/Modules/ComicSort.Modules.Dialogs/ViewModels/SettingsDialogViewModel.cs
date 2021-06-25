using ComicSort.Core;
using ComicSort.DataAccess;
using ComicSort.Domain.Models;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ComicSort.Modules.Dialogs.ViewModels
{
    public class SettingsDialogViewModel : BindableBase, IDialogAware
    {

        private ObservableCollection<WatchFolder> _watchFolders = new ObservableCollection<WatchFolder>();
       

        public ObservableCollection<WatchFolder> WatchFolders
        {
            get { return _watchFolders; }
            set { SetProperty(ref _watchFolders, value); }
        }

        public SettingsDialogViewModel()
        {
            
        }

        public string Title => "Preferences";

        public event Action<IDialogResult> RequestClose;

        public bool CanCloseDialog()
        {
            return true;
        }

        public void OnDialogClosed()
        {
            
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            using (var context = new LibraryDBContext())
            {
                
            }
        }

        private DelegateCommand _addCommand;
        public DelegateCommand AddCommand =>
            _addCommand ?? (_addCommand = new DelegateCommand(ExecuteAddCommand));

        void ExecuteAddCommand()
        {
            var path = CommonDialogs.ShowFolderBrowserDialog();
            _watchFolders.Add(new WatchFolder() { FolderPath = path, IsWatched = false });

            

        }

        private DelegateCommand _cancelCommand;
        public DelegateCommand CancelCommand =>
            _cancelCommand ?? (_cancelCommand = new DelegateCommand(ExecuteCancelCommand));

        void ExecuteCancelCommand()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
        }

        
    }
}
