using ComicSort.Core;
using ComicSort.Domain.Models;
using ComicSort.Services.Interfaces;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ComicSort.Modules.Dialogs.ViewModels
{
    public class NewLibraryDialogViewModel : BindableBase, IDialogAware
    {

        public List<string> LibraryTypes { get; set; } = new() { "XML", "Sqlite" };
        private readonly IDataService<ComicSortLibraries> _dataService;


        private string _selectedType;
        public string SelectedType
        {
            get { return _selectedType; }
            set { SetProperty(ref _selectedType, value); }
        }

        private string _libraryName;
        public string LibraryName
        {
            get { return _libraryName; }
            set { SetProperty(ref _libraryName, value); }
        }

        private string _libraryPath;
        public string LibraryPath
        {
            get { return _libraryPath; }
            set { SetProperty(ref _libraryPath, value); }
        }

        public NewLibraryDialogViewModel()
        {
            
        }

        public NewLibraryDialogViewModel(IDataService<ComicSortLibraries> dataService)
        {
            _dataService = dataService;
        }

        private DelegateCommand _okCommand;
        public DelegateCommand OKCommand =>
            _okCommand ?? (_okCommand = new DelegateCommand(ExecuteOKCommand));

        void ExecuteOKCommand()
        {
            if (_selectedType != "XML")
            {
                CreateSQLiteDatabase(_libraryName, _libraryPath, _selectedType);
            }
            else
            {
                CreateXmlDatabase(_libraryName, _libraryPath, _selectedType);
            }

            RequestClose?.Invoke(new DialogResult(ButtonResult.OK));
        }

        private DelegateCommand _browseCommand;
        public DelegateCommand BrowseCommand =>
            _browseCommand ?? (_browseCommand = new DelegateCommand(ExecuteBrowseCommand));

        void ExecuteBrowseCommand()
        {
            LibraryPath = CommonDialogs.ShowFolderBrowserDialog();
        }

        private DelegateCommand _cancelCommand;
        

        public DelegateCommand CancelCommand =>
            _cancelCommand ?? (_cancelCommand = new DelegateCommand(ExecuteCancelCommand));

        void ExecuteCancelCommand()
        {
            RequestClose?.Invoke(new DialogResult(ButtonResult.Cancel));
        }

        private void CreateXmlDatabase(string libraryName, string libraryPath, string selectedType)
        {
            
        }

        private void CreateSQLiteDatabase(string libraryName, string libraryPath, string selectedType)
        {
            ComicSortLibraries comicSortLibraries = new ComicSortLibraries()
            {
                LibraryPath = libraryPath,
                LibraryName = libraryName,
                LibraryType = selectedType,
                Created = DateTime.Now.ToString()
            };

            
        }

        public string Title => "New Library";

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
            
        }
    }
}
