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

        public NewLibraryDialogViewModel()
        {

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
