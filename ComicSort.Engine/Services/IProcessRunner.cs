namespace ComicSort.Engine.Services;

public interface IProcessRunner
{
    Task<ProcessRunTextResult> RunTextAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        int timeoutMs,
        CancellationToken cancellationToken = default);

    Task<ProcessRunBinaryResult> RunBinaryAsync(
        string fileName,
        IReadOnlyList<string> arguments,
        int timeoutMs,
        CancellationToken cancellationToken = default);
}

public sealed class ProcessRunTextResult
{
    public int ExitCode { get; init; }

    public bool TimedOut { get; init; }

    public bool Canceled { get; init; }

    public string OutputText { get; init; } = string.Empty;

    public string ErrorText { get; init; } = string.Empty;
}

public sealed class ProcessRunBinaryResult
{
    public int ExitCode { get; init; }

    public bool TimedOut { get; init; }

    public bool Canceled { get; init; }

    public byte[] OutputBytes { get; init; } = [];

    public string ErrorText { get; init; } = string.Empty;
}
