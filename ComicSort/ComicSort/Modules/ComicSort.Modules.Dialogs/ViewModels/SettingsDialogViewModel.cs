using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ComicSort.Modules.Dialogs.ViewModels
{
    public class SettingsDialogViewModel : BindableBase, IDialogAware
    {
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
            
        }
    }
}
