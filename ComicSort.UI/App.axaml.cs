using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using ComicSort.Engine.Services;
using ComicSort.UI.UI_Services;
using ComicSort.UI.ViewModels;
using ComicSort.UI.Views;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Linq;

namespace ComicSort.UI
{
    public partial class App : Application
    {
        public static IHost AppHost { get; private set; } = null!;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            AppHost = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {

                    // Engine thumbnail services
                    services.AddSingleton<CoverStreamService>();
                    services.AddSingleton<ThumbnailGenerator>();

                    // UI thumbnail service
                    services.AddSingleton<IThumbnailService, ThumbnailService>();
                    // UI services
                    services.AddSingleton<IDialogServices, DialogServices>();

                    // Engine/services (singletons)
                    services.AddSingleton<LibraryService>();
                    services.AddSingleton<LibraryIndex>();
                    services.AddSingleton<SearchEngine>();

                    // ScanQueueService MUST share the singleton LibraryService
                    // and use the same library path as before.
                    services.AddSingleton<ScanQueueService>(sp =>
                    {
                        var library = sp.GetRequiredService<LibraryService>();
                        var libraryPath = AppPaths.GetLibraryJsonPath();
                        return new ScanQueueService(library, libraryPath);
                    });

                    // VM + Window
                    services.AddSingleton<MainWindowViewModel>();
                    services.AddSingleton<MainWindow>();

                    // Startup (calls InitializeAsync somewhere in your StartupService)
                    services.AddHostedService<StartupService>();
                })
                .Build();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                DisableAvaloniaDataAnnotationValidation();

                var mainWindow = AppHost.Services.GetRequiredService<MainWindow>();
                mainWindow.DataContext = AppHost.Services.GetRequiredService<MainWindowViewModel>();

                desktop.MainWindow = mainWindow;

                desktop.Exit += async (_, __) =>
                {
                    if (AppHost is not null)
                    {
                        await AppHost.StopAsync();
                        AppHost.Dispose();
                    }
                };

                _ = AppHost.StartAsync();
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void DisableAvaloniaDataAnnotationValidation()
        {
            var dataValidationPluginsToRemove =
                BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            foreach (var plugin in dataValidationPluginsToRemove)
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }
    }
}
