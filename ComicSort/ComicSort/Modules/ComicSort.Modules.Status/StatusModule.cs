using ComicSort.Core;
using ComicSort.Modules.Status.Views;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;

namespace ComicSort.Modules.Status
{
    public class StatusModule : IModule
    {
        private readonly IRegionManager _regionManager;

        public StatusModule(IRegionManager regionManager)
        {
            _regionManager = regionManager;
        }

        public void OnInitialized(IContainerProvider containerProvider)
        {
            _regionManager.RegisterViewWithRegion(RegionNames.StatusRegion, typeof(StatusControl));
        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {

        }
    }
}