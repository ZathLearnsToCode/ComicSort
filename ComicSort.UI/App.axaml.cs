using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using ComicSort.Engine.Services;
using ComicSort.UI.Services;
using ComicSort.UI.ViewModels;
using ComicSort.UI.ViewModels.Controls;
using ComicSort.UI.Views;
using ComicSort.UI.Views.Controls;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System.Linq;
using System.Threading.Tasks;

namespace ComicSort.UI
{
    public partial class App : Application
    {
        public static IHost? AppHost { get; private set; }
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            AppHost = Host.CreateDefaultBuilder()
                .ConfigureServices((context, services) =>
                {
                    services.AddSingleton<ISettingsService, SettingsService>();
                    services.AddSingleton<IThemeService, ThemeService>();
                    services.AddSingleton<IDialogService, DialogService>();
                    services.AddSingleton<IComicDbContextFactory, ComicDbContextFactory>();
                    services.AddSingleton<IComicDatabaseService, ComicDatabaseService>();
                    services.AddSingleton<IScanRepository, ScanRepository>();
                    services.AddSingleton<IArchiveImageService, SevenZipArchiveImageService>();
                    services.AddSingleton<IThumbnailCacheService, ThumbnailCacheService>();
                    services.AddSingleton<IScanService, ScanService>();
                    services.AddSingleton<IComicRackImportService, ComicRackImportService>();


                    services.AddTransient<MainWindowViewModel>();
                    services.AddTransient<MainWindow>();
                    services.AddSingleton<LibraryActionsBarViewModel>();
                    services.AddSingleton<ComicGridViewModel>();
                    services.AddSingleton<SidebarViewModel>();
                    services.AddSingleton<StatusBarViewModel>();
                    services.AddTransient<LibraryActionsBarView>();

                    services.AddHostedService<StartupService>();
                })
            .Build();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
                // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
                DisableAvaloniaDataAnnotationValidation();

                var settingsService = AppHost.Services.GetRequiredService<ISettingsService>();
                Task.Run(() => settingsService.InitializeAsync()).GetAwaiter().GetResult();
                var themeService = AppHost.Services.GetRequiredService<IThemeService>();

                var originalDefaultTheme = settingsService.CurrentSettings.DefaultTheme;
                var originalCurrentTheme = settingsService.CurrentSettings.CurrentTheme;

                var defaultTheme = themeService.NormalizeThemeName(originalDefaultTheme);
                var currentTheme = themeService.NormalizeThemeName(originalCurrentTheme, defaultTheme);
                settingsService.CurrentSettings.DefaultTheme = defaultTheme;
                settingsService.CurrentSettings.CurrentTheme = currentTheme;
                themeService.ApplyTheme(currentTheme);

                if (!string.Equals(originalDefaultTheme, defaultTheme, System.StringComparison.Ordinal) ||
                    !string.Equals(originalCurrentTheme, currentTheme, System.StringComparison.Ordinal))
                {
                    Task.Run(() => settingsService.SaveAsync()).GetAwaiter().GetResult();
                }

                var mainWindow = AppHost.Services.GetRequiredService<MainWindow>();
                mainWindow.DataContext = AppHost.Services.GetRequiredService<MainWindowViewModel>();
                if (mainWindow.DataContext is MainWindowViewModel mainWindowViewModel)
                {
                    _ = mainWindowViewModel.ComicGrid.InitializeAsync();
                }

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
