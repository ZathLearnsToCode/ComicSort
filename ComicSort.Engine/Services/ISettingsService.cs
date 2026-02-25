using ComicSort.Engine.Settings;

namespace ComicSort.Engine.Services;

public interface ISettingsService
{
    AppSettings CurrentSettings { get; }
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
    Task SavetoSettings(string folder);
}
