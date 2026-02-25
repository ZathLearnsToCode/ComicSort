namespace ComicSort.Engine.Services;

public interface IComicDatabaseService
{
    string DatabasePath { get; }

    Task InitializeAsync(CancellationToken cancellationToken = default);
}
