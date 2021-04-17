using Prism.Mvvm;

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

        public MainWindowViewModel()
        {

        }
    }
}
