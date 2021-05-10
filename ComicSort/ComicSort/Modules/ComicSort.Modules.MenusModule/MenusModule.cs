using ComicSort.Modules.MenusModule.Views;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;
using ComicSort.Core;

namespace ComicSort.Modules.MenusModule
{
    public class MenusModule : IModule
    {
        private readonly IRegionManager _regionManager;

        public MenusModule(IRegionManager regionManager)
        {
            _regionManager = regionManager;
        }
        public void OnInitialized(IContainerProvider containerProvider)
        {
            _regionManager.RegisterViewWithRegion(RegionNames.MenuRegion, typeof(MainMenu));
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {

        }
    }
}