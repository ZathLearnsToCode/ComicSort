using System.Diagnostics;

namespace ComicSort.Engine.Services;

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<ProcessRunTextResult> RunTextAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        int timeoutMs,
        CancellationToken cancellationToken = default)
    {
        using var process = BuildProcess(fileName, arguments);
        using var linkedSource = BuildLinkedCancellationSource(timeoutMs, cancellationToken);

        try
        {
            process.Start();
            var outputTask = process.StandardOutput.ReadToEndAsync(linkedSource.Token);
            var errorTask = process.StandardError.ReadToEndAsync(linkedSource.Token);
            await process.WaitForExitAsync(linkedSource.Token);

            return new ProcessRunTextResult
            {
                ExitCode = process.ExitCode,
                OutputText = await outputTask,
                ErrorText = await errorTask
            };
        }
        catch (OperationCanceledException)
        {
            TerminateProcess(process);
            return new ProcessRunTextResult
            {
                ExitCode = -1,
                TimedOut = !cancellationToken.IsCancellationRequested,
                Canceled = cancellationToken.IsCancellationRequested,
                ErrorText = cancellationToken.IsCancellationRequested
                    ? "Process execution was cancelled."
                    : "Process execution timed out."
            };
        }
        catch (Exception ex)
        {
            TerminateProcess(process);
            return new ProcessRunTextResult
            {
                ExitCode = -1,
                ErrorText = ex.Message
            };
        }
    }

    public async Task<ProcessRunBinaryResult> RunBinaryAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        int timeoutMs,
        CancellationToken cancellationToken = default)
    {
        using var process = BuildProcess(fileName, arguments);
        using var linkedSource = BuildLinkedCancellationSource(timeoutMs, cancellationToken);

        try
        {
            process.Start();
            var errorTask = process.StandardError.ReadToEndAsync(linkedSource.Token);
            await using var outputStream = new MemoryStream();
            await process.StandardOutput.BaseStream.CopyToAsync(outputStream, linkedSource.Token);
            await process.WaitForExitAsync(linkedSource.Token);

            return new ProcessRunBinaryResult
            {
                ExitCode = process.ExitCode,
                OutputBytes = outputStream.ToArray(),
                ErrorText = await errorTask
            };
        }
        catch (OperationCanceledException)
        {
            TerminateProcess(process);
            return new ProcessRunBinaryResult
            {
                ExitCode = -1,
                TimedOut = !cancellationToken.IsCancellationRequested,
                Canceled = cancellationToken.IsCancellationRequested,
                ErrorText = cancellationToken.IsCancellationRequested
                    ? "Process execution was cancelled."
                    : "Process execution timed out."
            };
        }
        catch (Exception ex)
        {
            TerminateProcess(process);
            return new ProcessRunBinaryResult
            {
                ExitCode = -1,
                ErrorText = ex.Message
            };
        }
    }

    private static ProcessStartInfo BuildStartInfo(string fileName, IReadOnlyList<string> arguments)
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

        return startInfo;
    }

    private static Process BuildProcess(string fileName, IReadOnlyList<string> arguments)
    {
        return new Process
        {
            StartInfo = BuildStartInfo(fileName, arguments)
        };
    }

    private static CancellationTokenSource BuildLinkedCancellationSource(
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var safeTimeoutMs = Math.Max(1_000, timeoutMs);
        linkedSource.CancelAfter(safeTimeoutMs);
        return linkedSource;
    }

    private static void TerminateProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Ignore process cleanup failures.
        }
    }
}
