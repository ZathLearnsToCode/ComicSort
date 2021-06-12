using ComicSort.DataAccess;
using ComicSort.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
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

        private ComicSortLibraries _selectedItems;
        public ComicSortLibraries SelectedItems
        {
            get { return _selectedItems; }
            set { SetProperty(ref _selectedItems, value); }
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

        private DelegateCommand _okCommand;
        public DelegateCommand OKCommand =>
            _okCommand ?? (_okCommand = new DelegateCommand(ExecuteOKCommand));

        void ExecuteOKCommand()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.OK));
        }

        private DelegateCommand _cancelCommand;
        public DelegateCommand CancelCommand =>
            _cancelCommand ?? (_cancelCommand = new DelegateCommand(ExecuteCancelCommand));

        void ExecuteCancelCommand()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
        }

        private DelegateCommand _deleteCommand;
        public DelegateCommand DeleteCommand =>
            _deleteCommand ?? (_deleteCommand = new DelegateCommand(ExecuteDeleteCommand));

        void ExecuteDeleteCommand()
        {
            using(var context = new LibraryDBContext())
            {
                var test = context.Libraries.Where(e => e.Id == _selectedItems.Id).FirstOrDefault();

                var dir = Directory.GetParent(_selectedItems.LibraryPath);

                if(Directory.Exists(dir.ToString()))
                    Directory.Delete(dir.ToString(), true);
                else
                    System.Windows.Forms.MessageBox.Show("Test");

                context.Remove(test);
                context.SaveChangesAsync();

                _list = context.Libraries.ToList();
                Libraries = new(_list);
            }
        }

        public string Title => "Manage your Libraries";

        public event Action<IDialogResult> RequestClose;

        public bool CanCloseDialog()
        {
            return true;
        }

        public void OnDialogClosed()
        {
            using (var context = new LibraryDBContext())
            {
                context.SaveChangesAsync();
            }
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
