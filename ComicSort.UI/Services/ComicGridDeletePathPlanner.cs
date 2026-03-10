using Microsoft.VisualBasic.FileIO;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ComicSort.UI.Services;

internal static class ComicGridDeletePathPlanner
{
    public static DeletePathPlan BuildPlan(IReadOnlyList<string> filePaths, bool sendToRecycleBin)
    {
        var normalizedPaths = filePaths.Select(NormalizeFilePath).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var deletePaths = new List<string>(normalizedPaths.Length);
        var failedRecycleCount = 0;
        foreach (var path in normalizedPaths)
        {
            if (TryQueuePath(path, sendToRecycleBin, deletePaths))
            {
                continue;
            }

            failedRecycleCount++;
        }

        return new DeletePathPlan(deletePaths, failedRecycleCount);
    }

    private static bool TryQueuePath(string normalizedPath, bool sendToRecycleBin, ICollection<string> deletePaths)
    {
        if (!sendToRecycleBin || !File.Exists(normalizedPath))
        {
            deletePaths.Add(normalizedPath);
            return true;
        }

        try
        {
            FileSystem.DeleteFile(normalizedPath, UIOption.OnlyErrorDialogs, RecycleOption.SendToRecycleBin, UICancelOption.ThrowException);
            deletePaths.Add(normalizedPath);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static string NormalizeFilePath(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        try
        {
            return Path.GetFullPath(path).Trim();
        }
        catch
        {
            return path.Trim();
        }
    }
}

internal readonly record struct DeletePathPlan(IReadOnlyList<string> PathsToDelete, int FailedRecycleCount);
