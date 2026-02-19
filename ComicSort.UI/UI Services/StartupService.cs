using ComicSort.UI.ViewModels;
using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ComicSort.UI.UI_Services
{
    public class StartupService : IHostedService
    {
        private readonly MainWindowViewModel _vm;

        public StartupService(MainWindowViewModel vm)
        {
            _vm = vm;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            await _vm.InitializeAsync();
        }

        public Task StopAsync(CancellationToken cancellationToken)
            => Task.CompletedTask;
    }
}
