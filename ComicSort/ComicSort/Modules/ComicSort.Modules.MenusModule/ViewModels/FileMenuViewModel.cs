using Prism.Commands;
using Prism.Mvvm;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace ComicSort.Modules.MenusModule.ViewModels
{
    public class FileMenuViewModel : BindableBase
    {
        public FileMenuViewModel()
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
