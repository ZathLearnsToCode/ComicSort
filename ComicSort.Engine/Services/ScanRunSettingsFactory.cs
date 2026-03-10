namespace ComicSort.Engine.Services;

public sealed class ScanRunSettingsFactory : IScanRunSettingsFactory
{
    private readonly ISettingsService _settingsService;
    private readonly IScanPathService _scanPathService;

    public ScanRunSettingsFactory(ISettingsService settingsService, IScanPathService scanPathService)
    {
        _settingsService = settingsService;
        _scanPathService = scanPathService;
    }

    public ScanRunSettings Create(IReadOnlyCollection<string>? requestedFolders)
    {
        var workerCount = Math.Clamp(
            _settingsService.CurrentSettings.ScanWorkerCount,
            1,
            Math.Max(1, Math.Min(16, Environment.ProcessorCount * 2)));
        var batchSize = Math.Clamp(_settingsService.CurrentSettings.ScanBatchSize, 1, 2_000);
        var configuredFolders = _settingsService.CurrentSettings.LibraryFolders
            .Select(x => _scanPathService.NormalizeDirectoryPath(x.Folder))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Cast<string>()
            .ToArray();

        var isTargetedScan = requestedFolders is not null;
        var folders = _scanPathService.ResolveFoldersToScan(configuredFolders, requestedFolders);
        var removeMissing = _settingsService.CurrentSettings.RemoveMissingFilesDuringScan;

        return new ScanRunSettings(workerCount, batchSize, folders, isTargetedScan, removeMissing);
    }
}
