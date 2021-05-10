using ComicSort.HostBuilders;
using ComicSort.Modules.MenusModule;
using ComicSort.Modules.ModuleName;
using ComicSort.Services;
using ComicSort.Services.Interfaces;
using ComicSort.Views;
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
        }

        protected override Window CreateShell()
        {
            return Container.Resolve<MainWindow>();
        }

        protected override void RegisterTypes(IContainerRegistry containerRegistry)
        {
            containerRegistry.RegisterSingleton<IMessageService, MessageService>();
        }

        protected override void ConfigureModuleCatalog(IModuleCatalog moduleCatalog)
        {
            moduleCatalog.AddModule<ModuleNameModule>();
            moduleCatalog.AddModule<MenusModule>();
        }
    }
}
