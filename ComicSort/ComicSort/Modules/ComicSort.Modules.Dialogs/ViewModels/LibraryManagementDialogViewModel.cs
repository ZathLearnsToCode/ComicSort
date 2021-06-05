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
    public class LibraryManagementDialogViewModel : BindableBase, IDialogAware
    {
        private List<ComicSortLibraries> _list = new();
        
        private ObservableCollection<ComicSortLibraries> _libraries;
        public ObservableCollection<ComicSortLibraries> Libraries
        {
            get { return _libraries; }
            set { SetProperty(ref _libraries, value); }
        }
        public LibraryManagementDialogViewModel(IDialogService dialogService)
        {
            
            _dialogService = dialogService;
        }

        private DelegateCommand _newCommand;
        
        private readonly IDialogService _dialogService;

        public DelegateCommand NewCommand =>
            _newCommand ?? (_newCommand = new DelegateCommand(ExecuteNewCommand));

        void ExecuteNewCommand()
        {
            _dialogService.ShowDialog("NewLibraryDialog",null, r =>
            {
                if(r.Result == ButtonResult.OK)
                {
                    using (var context = new LibraryDBContext())
                    {
                        _list = context.Libraries.ToList();
                        Libraries = new(_list);
                    }
                }
            });
        }

        public string Title => "Manage your Libraries";

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
                _list = context.Libraries.ToList();
                Libraries = new(_list);
            }
        }
    }
}
