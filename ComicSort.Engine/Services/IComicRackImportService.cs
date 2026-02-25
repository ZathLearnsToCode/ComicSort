namespace ComicSort.Engine.Services;

public interface IComicRackImportService
{
    Task ImportFromXmlAsync(string xmlFilePath, CancellationToken cancellationToken = default);
}
