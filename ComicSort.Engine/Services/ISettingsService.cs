using ComicSort.Engine.Settings;

namespace ComicSort.Engine.Services;

public interface ISettingsService
{
    AppSettings CurrentSettings { get; }
    event EventHandler? SettingsChanged;
    Task InitializeAsync(CancellationToken cancellationToken = default);
    Task SaveAsync(CancellationToken cancellationToken = default);
    Task SavetoSettings(string folder);
}
