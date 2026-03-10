using System.Threading.Channels;

namespace ComicSort.Engine.Services;

public interface IScanFileProducer
{
    Task ProduceAsync(
        IReadOnlyList<string> folders,
        ChannelWriter<ScanFileWorkItem> writer,
        Action pulseProgress,
        CancellationToken cancellationToken);
}
