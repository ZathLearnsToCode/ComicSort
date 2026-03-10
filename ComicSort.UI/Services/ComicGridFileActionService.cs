using ComicSort.Engine.Models;
using ComicSort.Engine.Services;
using ComicSort.UI.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ComicSort.UI.Services;

public sealed class ComicGridFileActionService : IComicGridFileActionService
{
    private readonly IComicConversionService _comicConversionService;
    private readonly IScanRepository _scanRepository;
    private readonly ISettingsService _settingsService;
    private readonly IDialogService _dialogService;

    public ComicGridFileActionService(
        IComicConversionService comicConversionService,
        IScanRepository scanRepository,
        ISettingsService settingsService,
        IDialogService dialogService)
    {
        _comicConversionService = comicConversionService;
        _scanRepository = scanRepository;
        _settingsService = settingsService;
        _dialogService = dialogService;
    }

    public async Task<CbzConversionBatchResult?> ConvertToCbzAsync(
        IReadOnlyList<ComicTileModel> targets,
        CancellationToken cancellationToken = default)
    {
        var options = await ResolveCbzConversionOptionsAsync(targets.Count, cancellationToken);
        if (options is null)
        {
            return null;
        }

        var sourcePaths = targets.Select(x => x.FilePath).ToArray();
        return await _comicConversionService.ConvertToCbzAsync(sourcePaths, options, cancellationToken);
    }

    public async Task<ComicGridDeleteActionResult?> DeleteFromLibraryAsync(
        IReadOnlyList<ComicTileModel> targets,
        CancellationToken cancellationToken = default)
    {
        var options = await ResolveDeleteOptionsAsync(targets.Count, cancellationToken);
        if (options is null)
        {
            return null;
        }

        if (options.Value.SendToRecycleBin && !OperatingSystem.IsWindows())
        {
            return new ComicGridDeleteActionResult
            {
                WarningMessage = "Recycle Bin delete is only supported on Windows."
            };
        }

        return await DeleteByTargetsAsync(targets, options.Value.SendToRecycleBin, cancellationToken);
    }

    private async Task<CbzConversionOptions?> ResolveCbzConversionOptionsAsync(int targetCount, CancellationToken cancellationToken)
    {
        await _settingsService.InitializeAsync(cancellationToken);
        var settings = _settingsService.CurrentSettings;
        if (!settings.ConfirmCbzConversion)
        {
            return new CbzConversionOptions { SendOriginalToRecycleBin = settings.SendOriginalToRecycleBinOnCbzConversion };
        }

        var confirmation = await _dialogService.ShowCbzConversionConfirmationDialogAsync(targetCount, settings.SendOriginalToRecycleBinOnCbzConversion);
        if (confirmation is null)
        {
            return null;
        }

        settings.SendOriginalToRecycleBinOnCbzConversion = confirmation.SendOriginalToRecycleBin;
        if (confirmation.DontAskAgain)
        {
            settings.ConfirmCbzConversion = false;
        }

        await _settingsService.SaveAsync(cancellationToken);
        return new CbzConversionOptions { SendOriginalToRecycleBin = confirmation.SendOriginalToRecycleBin };
    }

    private async Task<DeleteActionOptions?> ResolveDeleteOptionsAsync(int targetCount, CancellationToken cancellationToken)
    {
        await _settingsService.InitializeAsync(cancellationToken);
        var settings = _settingsService.CurrentSettings;
        if (!settings.ConfirmDeleteFromLibrary)
        {
            return new DeleteActionOptions(settings.SendDeletedToRecycleBinOnLibraryDelete);
        }

        var confirmation = await _dialogService.ShowLibraryDeleteConfirmationDialogAsync(targetCount, settings.SendDeletedToRecycleBinOnLibraryDelete);
        if (confirmation is null)
        {
            return null;
        }

        settings.SendDeletedToRecycleBinOnLibraryDelete = confirmation.SendToRecycleBin;
        if (confirmation.DontAskAgain)
        {
            settings.ConfirmDeleteFromLibrary = false;
        }

        await _settingsService.SaveAsync(cancellationToken);
        return new DeleteActionOptions(confirmation.SendToRecycleBin);
    }

    private async Task<ComicGridDeleteActionResult> DeleteByTargetsAsync(
        IReadOnlyList<ComicTileModel> targets,
        bool sendToRecycleBin,
        CancellationToken cancellationToken)
    {
        var filePaths = targets.Select(x => x.FilePath).ToArray();
        var deletePlan = ComicGridDeletePathPlanner.BuildPlan(filePaths, sendToRecycleBin);
        if (deletePlan.PathsToDelete.Count == 0)
        {
            return new ComicGridDeleteActionResult();
        }

        var removedPaths = await _scanRepository.DeleteByNormalizedPathsAsync(deletePlan.PathsToDelete, cancellationToken);
        return new ComicGridDeleteActionResult
        {
            RemovedPaths = removedPaths,
            FailedRecycleCount = deletePlan.FailedRecycleCount
        };
    }

    private readonly record struct DeleteActionOptions(bool SendToRecycleBin);
}
