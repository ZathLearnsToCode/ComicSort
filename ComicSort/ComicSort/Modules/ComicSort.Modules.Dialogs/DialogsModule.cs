using Prism.Ioc;
using Prism.Modularity;
using Prism.Regions;

namespace ComicSort.Modules.Dialogs
{
    public class DialogsModule : IModule
    {
        private readonly IRegionManager _regionManager;

        public DialogsModule(IRegionManager regionManager)
        {
            _regionManager = regionManager;
        }
        public void OnInitialized(IContainerProvider containerProvider)
        {

        }

        public void RegisterTypes(IContainerRegistry containerRegistry)
        {

        }
    }
}