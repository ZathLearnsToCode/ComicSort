namespace ComicSort.Engine.Services;

public interface IScanFileProcessor
{
    Task<ScanFileProcessResult> ProcessAsync(ScanFileWorkItem workItem, CancellationToken cancellationToken);
}
