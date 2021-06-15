using Prism.Commands;
using Prism.Mvvm;
using Prism.Services.Dialogs;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ComicSort.Modules.MenusModule.ViewModels
{
    public class EditMenuViewModel : BindableBase
    {
        private readonly IDialogService _dialogService;

        public EditMenuViewModel(IDialogService dialogService)
        {
            _dialogService = dialogService;
        }

        private DelegateCommand _openSettingsCommand;
        public DelegateCommand OpenSettingsCommand =>
            _openSettingsCommand ?? (_openSettingsCommand = new DelegateCommand(ExecuteOpenSettingsCommand));

        void ExecuteOpenSettingsCommand()
        {
            _dialogService.ShowDialog("SettingsDialog", null, null);
        }

    }
}
