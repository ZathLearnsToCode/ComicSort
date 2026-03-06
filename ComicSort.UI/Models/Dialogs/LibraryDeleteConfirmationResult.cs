namespace ComicSort.UI.Models.Dialogs;

public sealed class LibraryDeleteConfirmationResult
{
    public bool SendToRecycleBin { get; init; }

    public bool DontAskAgain { get; init; }
}
