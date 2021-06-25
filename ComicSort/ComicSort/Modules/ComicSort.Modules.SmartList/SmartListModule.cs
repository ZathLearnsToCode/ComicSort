using ComicSort.Core;
using ComicSort.Modules.SmartList.Views;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;

namespace ComicSort.Modules.SmartList
{
    public class SmartListModule : IModule
    {
        private readonly IRegionManager _regionManager;

        public SmartListModule(IRegionManager regionManager)
        {
            _regionManager = regionManager;
        }
        public void OnInitialized(IContainerProvider containerProvider)
        {
            _regionManager.RegisterViewWithRegion(RegionNames.SmartListRegion, typeof(SmartListTree));
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {

        }
    }
}