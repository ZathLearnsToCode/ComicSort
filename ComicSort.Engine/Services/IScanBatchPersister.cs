using ComicSort.Engine.Models;
using System.Threading.Channels;

namespace ComicSort.Engine.Services;

public interface IScanBatchPersister
{
    Task PersistAsync(
        ChannelReader<ComicFileUpsertModel> reader,
        int batchSize,
        Action<ComicLibraryItem> onSaved,
        Action pulseProgress,
        CancellationToken cancellationToken);
}
