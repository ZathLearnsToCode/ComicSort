using Avalonia.Controls;
using ComicSort.UI.ViewModels;
using System.Threading.Tasks;

namespace ComicSort.UI.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            Opened += OnOpened;
        }

        private async void OnOpened(object? sender, System.EventArgs e)
        {
            if (DataContext is MainWindowViewModel viewModel)
            {
                try
                {
                    await viewModel.ComicGrid.InitializeAsync();
                    await Task.Delay(400);
                    await viewModel.ComicGrid.ReloadAsync();
                }
                catch
                {
                    // Keep window responsive even if initial library load fails.
                }
            }
        }
    }
}
