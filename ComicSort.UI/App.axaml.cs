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
using System.Runtime.InteropServices.JavaScript;

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
                    
                    services.AddSingleton<MainWindowViewModel>();
                    services.AddSingleton<MainWindow>();
                    services.AddSingleton<IDialogServices, DialogServices>();
                    services.AddHostedService<StartupService>();
                    services.AddSingleton<LibraryService>();
                    services.AddSingleton<LibraryIndex>();
                    services.AddSingleton<SearchEngine>();
                })
                .Build();



            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
                // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
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

                _ = AppHost.StartAsync(); // <-- THIS is what people miss
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void DisableAvaloniaDataAnnotationValidation()
        {
            // Get an array of plugins to remove
            var dataValidationPluginsToRemove =
                BindingPlugins.DataValidators.OfType<DataAnnotationsValidationPlugin>().ToArray();

            // remove each entry found
            foreach (var plugin in dataValidationPluginsToRemove)
            {
                BindingPlugins.DataValidators.Remove(plugin);
            }
        }
    }
}