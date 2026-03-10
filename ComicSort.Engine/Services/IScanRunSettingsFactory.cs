namespace ComicSort.Engine.Services;

public interface IScanRunSettingsFactory
{
    ScanRunSettings Create(IReadOnlyCollection<string>? requestedFolders);
}
