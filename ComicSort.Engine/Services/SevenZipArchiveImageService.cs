using ComicSort.Engine.Models;
using System.Diagnostics;

namespace ComicSort.Engine.Services;

public sealed class SevenZipArchiveImageService : IArchiveImageService
{
    private static readonly string[] SupportedImageExtensions =
    [
        ".jpg",
        ".jpeg",
        ".png",
        ".webp",
        ".gif",
        ".bmp"
    ];

    public async Task<ArchiveImageResult> TryGetFirstImageAsync(string archivePath, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
        {
            return new ArchiveImageResult
            {
                Success = false,
                Error = "Archive file was not found."
            };
        }

        var sevenZipPath = ResolveSevenZipPath();
        if (string.IsNullOrWhiteSpace(sevenZipPath))
        {
            return new ArchiveImageResult
            {
                Success = false,
                Error = "7z executable was not found. Place 7z.exe in Tools/7zip or install 7-Zip in PATH."
            };
        }

        var listResult = await ExecuteTextCommandAsync(
            sevenZipPath,
            ["l", "-slt", "-ba", archivePath],
            cancellationToken);

        if (listResult.ExitCode != 0)
        {
            return new ArchiveImageResult
            {
                Success = false,
                Error = $"7z list failed: {listResult.ErrorText}"
            };
        }

        var firstImageEntry = FindFirstImageEntry(listResult.OutputText, archivePath);
        if (string.IsNullOrWhiteSpace(firstImageEntry))
        {
            return new ArchiveImageResult
            {
                Success = false,
                Error = "No image entry found in archive."
            };
        }

        var extractResult = await ExecuteBinaryCommandAsync(
            sevenZipPath,
            ["e", "-so", "-y", archivePath, firstImageEntry],
            cancellationToken);

        if (extractResult.ExitCode != 0 || extractResult.OutputBytes.Length == 0)
        {
            return new ArchiveImageResult
            {
                Success = false,
                Error = $"7z extract failed: {extractResult.ErrorText}"
            };
        }

        return new ArchiveImageResult
        {
            Success = true,
            ImageBytes = extractResult.OutputBytes
        };
    }

    private static string? ResolveSevenZipPath()
    {
        var bundledPath = Path.Combine(AppContext.BaseDirectory, "Tools", "7zip", "7z.exe");
        if (File.Exists(bundledPath))
        {
            return bundledPath;
        }

        return "7z";
    }

    private static string? FindFirstImageEntry(string outputText, string archivePath)
    {
        string? currentPath = null;
        bool currentIsFolder = false;
        var normalizedArchivePath = Path.GetFullPath(archivePath);

        foreach (var rawLine in outputText.Split('\n'))
        {
            var line = rawLine.TrimEnd('\r');

            if (line.StartsWith("Path = ", StringComparison.Ordinal))
            {
                if (ShouldUseEntry(currentPath, currentIsFolder, normalizedArchivePath))
                {
                    return currentPath;
                }

                currentPath = line["Path = ".Length..].Trim();
                currentIsFolder = false;
                continue;
            }

            if (line.StartsWith("Folder = ", StringComparison.Ordinal))
            {
                currentIsFolder = line.EndsWith('+');
            }
        }

        return ShouldUseEntry(currentPath, currentIsFolder, normalizedArchivePath)
            ? currentPath
            : null;
    }

    private static bool ShouldUseEntry(string? entryPath, bool isFolder, string normalizedArchivePath)
    {
        if (string.IsNullOrWhiteSpace(entryPath) || isFolder)
        {
            return false;
        }

        if (Path.IsPathRooted(entryPath))
        {
            var normalizedEntry = Path.GetFullPath(entryPath);
            if (string.Equals(normalizedArchivePath, normalizedEntry, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        var extension = Path.GetExtension(entryPath);
        return SupportedImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<(int ExitCode, string OutputText, string ErrorText)> ExecuteTextCommandAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            using var process = BuildProcess(fileName, arguments);
            process.Start();

            var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);

            await process.WaitForExitAsync(cancellationToken);
            var outputText = await outputTask;
            var errorText = await errorTask;

            return (process.ExitCode, outputText, errorText);
        }
        catch (Exception ex)
        {
            return (-1, string.Empty, ex.Message);
        }
    }

    private static async Task<(int ExitCode, byte[] OutputBytes, string ErrorText)> ExecuteBinaryCommandAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        CancellationToken cancellationToken)
    {
        try
        {
            using var process = BuildProcess(fileName, arguments);
            process.Start();

            var errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            await using var outputStream = new MemoryStream();
            await process.StandardOutput.BaseStream.CopyToAsync(outputStream, cancellationToken);

            await process.WaitForExitAsync(cancellationToken);
            var errorText = await errorTask;

            return (process.ExitCode, outputStream.ToArray(), errorText);
        }
        catch (Exception ex)
        {
            return (-1, [], ex.Message);
        }
    }

    private static Process BuildProcess(string fileName, IReadOnlyList<string> arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = fileName,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        return new Process
        {
            StartInfo = startInfo
        };
    }
}
