using ComicSort.DataAccess;
using ComicSort.Domain.Models;
using ComicSort.HostBuilders;
using ComicSort.Modules.Dialogs;
using ComicSort.Modules.Dialogs.ViewModels;
using ComicSort.Modules.Dialogs.Views;
using ComicSort.Modules.MenusModule;
using ComicSort.Modules.ModuleName;
using ComicSort.Modules.SmartList;
using ComicSort.Modules.Status;
using ComicSort.Services;
using ComicSort.Services.Interfaces;
using ComicSort.Views;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Prism.Ioc;
using Prism.Modularity;
using System.Windows;

namespace ComicSort
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App
    {
        private readonly IHost _host;

        

        public App()
        {
            _host = CreateHostBuilder().Build();
        }

        public static IHostBuilder CreateHostBuilder(string[] args = null)
        {
            return Host.CreateDefaultBuilder(args)
                .AddConfig();
                //.AddDBContext();
        }

        protected override Window CreateShell()
        {
            return Container.Resolve<MainWindow>();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterSingleton<IMessageService, MessageService>();
            
            containerRegistry.RegisterDialog<NewLibraryDialog, NewLibraryDialogViewModel>();
            containerRegistry.RegisterDialog<LibraryManagementDialog, LibraryManagementDialogViewModel>();
            containerRegistry.RegisterDialog<SettingsDialog, SettingsDialogViewModel>();
        }

        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            moduleCatalog.AddModule<ModuleNameModule>();
            moduleCatalog.AddModule<MenusModule>();
            moduleCatalog.AddModule<DialogsModule>();
            moduleCatalog.AddModule<SmartListModule>();
            moduleCatalog.AddModule<StatusModule>();
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            _host.Start();

            
                                 
            
            base.OnStartup(e);
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            await _host.StopAsync();
            _host.Dispose();
            base.OnExit(e);
        }
    }
}
