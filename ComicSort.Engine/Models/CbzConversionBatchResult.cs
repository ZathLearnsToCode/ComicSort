namespace ComicSort.Engine.Models;

public sealed class CbzConversionBatchResult
{
    public IReadOnlyList<CbzConversionFileResult> Files { get; init; } = [];

    public int SuccessCount => Files.Count(x => x.Success);

    public int FailureCount => Files.Count(x => !x.Success);
}
