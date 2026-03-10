using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
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
                    services.AddSingleton<IComicGridArrangementService, ComicGridArrangementService>();
                    services.AddSingleton<IComicGridThumbnailService, ComicGridThumbnailService>();
                    services.AddSingleton<IComicGridFileActionService, ComicGridFileActionService>();
                    services.AddSingleton<IComicGridInfoPanelService, ComicGridInfoPanelService>();
                    services.AddSingleton<ComicGridSelectionService>();
                    services.AddSingleton<ComicGridConversionResultService>();
                    services.AddSingleton<ComicGridSavedItemQueueService>();
                    services.AddSingleton<ComicGridPresentationService>();
                    services.AddSingleton<IComicDbContextFactory, ComicDbContextFactory>();
                    services.AddSingleton<IComicDatabaseService, ComicDatabaseService>();
                    services.AddSingleton<IScanRepository, ScanRepository>();
                    services.AddSingleton<IProcessRunner, ProcessRunner>();
                    services.AddSingleton<IArchiveInspectorService, ArchiveInspectorService>();
                    services.AddSingleton<IArchiveImageService, SevenZipArchiveImageService>();
                    services.AddSingleton<IComicMetadataService, ComicMetadataService>();
                    services.AddSingleton<IThumbnailCacheService, ThumbnailCacheService>();
                    services.AddSingleton<IScanPathService, ScanPathService>();
                    services.AddSingleton<IScanProgressTracker, ScanProgressTracker>();
                    services.AddSingleton<IScanRunSettingsFactory, ScanRunSettingsFactory>();
                    services.AddSingleton<IScanLookupCacheService, ScanLookupCacheService>();
                    services.AddSingleton<IScanRelinkService, ScanRelinkService>();
                    services.AddSingleton<IScanFileProducer, ScanFileProducer>();
                    services.AddSingleton<IScanFileProcessor, ScanFileProcessor>();
                    services.AddSingleton<IScanBatchPersister, ScanBatchPersister>();
                    services.AddSingleton<IScanPipelineCoordinator, ScanPipelineCoordinator>();
                    services.AddSingleton<IScanService, ScanService>();
                    services.AddSingleton<IComicConversionService, ComicConversionService>();
                    services.AddSingleton<IComicRackImportService, ComicRackImportService>();
                    services.AddSingleton<ISmartListParser, SmartListParser>();
                    services.AddSingleton<ISmartListSerializer, SmartListSerializer>();
                    services.AddSingleton<ISmartListEvaluator, SmartListEvaluator>();
                    services.AddSingleton<ISmartListSqlCompiler, SmartListSqlCompiler>();
                    services.AddSingleton<ISmartListExpressionService, SmartListExpressionService>();
                    services.AddSingleton<ISmartListExecutionService, SmartListExecutionService>();


                    services.AddTransient<MainWindowViewModel>();
                    services.AddTransient<MainWindow>();
                    services.AddSingleton<LibraryActionsBarViewModel>();
                    services.AddSingleton<ComicGridViewModel>();
                    services.AddSingleton<SidebarViewModel>();
                    services.AddSingleton<StatusBarViewModel>();
                    services.AddTransient<LibraryActionsBarView>();

                    services.AddHostedService<StartupService>();
                    services.AddHostedService<ComicLibraryRenameWatcherService>();
                })
            .Build();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
                // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
                DisableAvaloniaDataAnnotationValidation();

                var settingsService = AppHost.Services.GetRequiredService<ISettingsService>();
                var themeService = AppHost.Services.GetRequiredService<IThemeService>();
                _ = InitializeThemeAsync(settingsService, themeService);

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

        private static async Task InitializeThemeAsync(ISettingsService settingsService, IThemeService themeService)
        {
            try
            {
                await settingsService.InitializeAsync();

                var originalDefaultTheme = settingsService.CurrentSettings.DefaultTheme;
                var originalCurrentTheme = settingsService.CurrentSettings.CurrentTheme;

                var defaultTheme = themeService.NormalizeThemeName(originalDefaultTheme);
                var currentTheme = themeService.NormalizeThemeName(originalCurrentTheme, defaultTheme);
                settingsService.CurrentSettings.DefaultTheme = defaultTheme;
                settingsService.CurrentSettings.CurrentTheme = currentTheme;

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    themeService.ApplyTheme(currentTheme);
                });

                if (!string.Equals(originalDefaultTheme, defaultTheme, System.StringComparison.Ordinal) ||
                    !string.Equals(originalCurrentTheme, currentTheme, System.StringComparison.Ordinal))
                {
                    await settingsService.SaveAsync();
                }
            }
            catch
            {
                // Keep startup resilient if settings/theme initialization fails.
            }
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
