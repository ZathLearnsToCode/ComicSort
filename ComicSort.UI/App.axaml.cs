using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core.Plugins;
using Avalonia.Markup.Xaml;
using ComicSort.Core.Services;
using ComicSort.Core.Services.Repositories;
using ComicSort.Data.Repositories;
using ComicSort.Data.SQL;
using ComicSort.UI.Services;
using ComicSort.UI.ViewModels;
using ComicSort.UI.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
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
                    ConfigureDatabase(services);

                    services.AddSingleton<IDialogServices, DialogServices>();
                    services.AddSingleton<ISettingsServices, SettingsServices>();
                    services.AddScoped<IComicScanner, ComicScanner>();
                    services.AddScoped<IComicRepository, ComicRepository>();
                    services.AddScoped<IComicLibraryService, ComicLibraryService>();

                    // Register ViewModels
                    services.AddTransient<MainWindowViewModel>();
                    services.AddTransient<ProfileDialogViewModel>();

                    // Register Windows
                    services.AddTransient<MainWindow>();
                    services.AddTransient<ProfileDialog>();
                })
                .Build();

            ApplyDatabaseMigrations();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                // Avoid duplicate validations from both Avalonia and the CommunityToolkit. 
                // More info: https://docs.avaloniaui.net/docs/guides/development-guides/data-validation#manage-validationplugins
                DisableAvaloniaDataAnnotationValidation();

                var mainWindow = AppHost.Services.GetRequiredService<MainWindow>();
                mainWindow.DataContext = AppHost.Services.GetRequiredService<MainWindowViewModel>();

                desktop.MainWindow = mainWindow;
            }

            base.OnFrameworkInitializationCompleted();
        }

        private void ApplyDatabaseMigrations()
        {
            using (var scope = AppHost.Services.CreateScope())
            {
                try
                {
                    var db = scope.ServiceProvider.GetRequiredService<ComicSortDBSQLiteContext>();
                    db.Database.Migrate(); // RUN SYNC
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Database migration failed: " + ex);
                    // Optionally show a dialog or delete corrupted DB
                }
            }
        }

        private void ConfigureDatabase(IServiceCollection services)
        {
            string dbFolder = Path.Combine(
                                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                    "ComicSort");

            Directory.CreateDirectory(dbFolder);

            string dbPath = Path.Combine(dbFolder, "ComicSort.db");
            var migrationsAssembly = typeof(ComicSortDBSQLiteContext).Assembly.FullName;

            services.AddDbContext<ComicSortDBSQLiteContext>(options =>
            {
                options.UseSqlite($"Data Source={dbPath}", sqlOptions =>
                sqlOptions.MigrationsAssembly(migrationsAssembly));
            });
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