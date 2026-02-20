namespace ComicSort.Engine.Services;

public interface IFileHashService
{
    Task<string> ComputeXxHash64HexAsync(string filePath, CancellationToken ct);
}
