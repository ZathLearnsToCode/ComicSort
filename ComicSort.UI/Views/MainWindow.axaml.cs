using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using ComicSort.UI.ViewModels;
using System.Linq;


namespace ComicSort.UI.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void ResultsGrid_OnLoadingRow(object? sender, DataGridRowEventArgs e)
        {
            if (DataContext is not MainWindowViewModel vm)
                return;

            if (e.Row.DataContext is ComicItemViewModel rowVm)
                vm.RequestThumbnailForRow(rowVm);
        }
    }
}