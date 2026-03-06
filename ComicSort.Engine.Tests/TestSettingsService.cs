using ComicSort.Engine.Services;
using ComicSort.Engine.Settings;

namespace ComicSort.Engine.Tests;

internal sealed class TestSettingsService : ISettingsService
{
    public TestSettingsService(AppSettings settings)
    {
        CurrentSettings = settings;
    }

    public AppSettings CurrentSettings { get; }

    public event EventHandler? SettingsChanged;

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task SaveAsync(CancellationToken cancellationToken = default)
    {
        SettingsChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public Task SavetoSettings(string folder)
    {
        return Task.CompletedTask;
    }
}
