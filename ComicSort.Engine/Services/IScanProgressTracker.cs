using ComicSort.Engine.Models;

namespace ComicSort.Engine.Services;

public interface IScanProgressTracker
{
    void Reset();

    void SetStage(string stage);

    void SetCurrentFile(string filePath);

    void IncrementEnumerated();

    void IncrementQueued();

    void IncrementInserted(int count);

    void IncrementUpdated(int count);

    void IncrementSkipped();

    void IncrementFailed();

    long NextSequenceNumber();

    bool ShouldPublish(bool force = false);

    ScanProgressUpdate CreateUpdate();
}
