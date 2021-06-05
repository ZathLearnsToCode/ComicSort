using ComicSort.Core;
using ComicSort.DataAccess;
using ComicSort.Domain.Models;
using ComicSort.Services.Interfaces;
using Microsoft.EntityFrameworkCore;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ComicSort.Modules.Dialogs.ViewModels
{
    public class NewLibraryDialogViewModel : BindableBase, IDialogAware
    {

        public List<string> LibraryTypes { get; set; } = new() { "XML", "Sqlite" };
        private string result;
       


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

        
        private DelegateCommand _okCommand;
        public DelegateCommand OKCommand =>
            _okCommand ?? (_okCommand = new DelegateCommand(ExecuteOKCommand));

        void ExecuteOKCommand()
        {
            
            if (_selectedType != "XML")
            {
                result = CreateSQLiteDatabase(_libraryName, _libraryPath, _selectedType);
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
            var path = FileUtilities.CreateDirectory(libraryPath, libraryName);
            Directory.SetCurrentDirectory(path);



        }

        private string CreateSQLiteDatabase(string libraryName, string libraryPath, string selectedType)
        {
            var path = FileUtilities.CreateDirectory(libraryPath, libraryName);
            
            ComicDBContext context = new();
            var libraryFile = context.CreateConnectionString(libraryName, path);
            context.Database.EnsureCreated();

            return libraryFile;
            
        }

        public string Title => "New Library";

        public event Action<IDialogResult> RequestClose;

        public bool CanCloseDialog()
        {
            return true;
        }

        public void OnDialogClosed()
        {
            if(result != null)
            {
                using (var context = new LibraryDBContext())
                {
                    var result1 = FileUtilities.GetFileInfos(result);

                    ComicSortLibraries libraries = new()
                    {
                        LibraryPath = result1.FullName,
                        LibraryName = _libraryName,
                        Created = result1.CreationTime.ToString(),
                        LastAccessed = result1.LastWriteTime.ToString(),
                        LibraryType = _selectedType,
                        LibraryFile = result1.Name


                    };


                    context.Add(libraries);
                    context.SaveChanges();
                }
            }      
        }

        public void OnDialogOpened(IDialogParameters parameters)
        {
            
        }
    }
}
