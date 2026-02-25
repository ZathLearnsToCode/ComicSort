using ComicSort.Engine.Services;
using Microsoft.Extensions.Hosting;
using System.Threading;
using System.Threading.Tasks;

namespace ComicSort.UI.Services;

public class StartupService : IHostedService
{
    private readonly ISettingsService _settingsService;

    public StartupService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await _settingsService.InitializeAsync(cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
        => Task.CompletedTask;
}
