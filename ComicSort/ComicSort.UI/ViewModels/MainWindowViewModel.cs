using Prism.Commands;
using Prism.Mvvm;
using System.Windows;

namespace ComicSort.UI.ViewModels;

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

    private DelegateCommand _exitAppCommand;
    public DelegateCommand ExitAppCommand =>
        _exitAppCommand ?? (_exitAppCommand = new DelegateCommand(ExecuteExitAppCommand));

    void ExecuteExitAppCommand()
    {
        Application.Current.Shutdown();
    }


}
