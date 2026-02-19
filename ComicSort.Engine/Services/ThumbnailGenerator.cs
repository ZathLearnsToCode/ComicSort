using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Processing;

namespace ComicSort.Engine.Services;

public sealed class ThumbnailGenerator
{
    public async Task<bool> TryGenerateJpegAsync(
        Stream imageStream,
        string outputFilePath,
        int targetHeight,
        CancellationToken ct)
    {
        try
        {
            imageStream.Position = 0;

            using var image = await Image.LoadAsync(imageStream, ct);

            // Resize while preserving aspect ratio by height
            var ratio = (double)targetHeight / image.Height;
            var targetWidth = Math.Max(1, (int)Math.Round(image.Width * ratio));

            image.Mutate(x => x.Resize(targetWidth, targetHeight));

            Directory.CreateDirectory(Path.GetDirectoryName(outputFilePath)!);

            // Write to temp then atomic move
            var tmp = outputFilePath + ".tmp";
            await image.SaveAsJpegAsync(tmp, new JpegEncoder { Quality = 80 }, ct);

            if (File.Exists(outputFilePath))
                File.Delete(outputFilePath);

            File.Move(tmp, outputFilePath);
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch
        {
            return false;
        }
    }
}
