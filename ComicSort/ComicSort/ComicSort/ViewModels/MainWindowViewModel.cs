using ComicSort.DataAccess;
using ComicSort.Domain.Models;
using Prism.Mvvm;
using System;
using System.Linq;

namespace ComicSort.ViewModels
{
    public class MainWindowViewModel : BindableBase
    {
        private string _title = "ComicSort";
        

        public string Title
        {
            get { return _title; }
            set { SetProperty(ref _title, value); }
        }

        public string CurrentLibrary { get; set; }

        public MainWindowViewModel()
        {
            
                
        }
    }
}
