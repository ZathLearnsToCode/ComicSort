using System.Buffers;
using System.IO.Hashing;
using System.Text;

namespace ComicSort.Engine.Services;

public sealed class FileHashService : IFileHashService
{
    public async Task<string> ComputeXxHash64HexAsync(string filePath, CancellationToken ct)
    {
        // SequentialScan helps Windows for large files
        await using var fs = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 1024 * 128,
            options: FileOptions.Asynchronous | FileOptions.SequentialScan);

        var hasher = new XxHash64();

        byte[] rented = ArrayPool<byte>.Shared.Rent(1024 * 128);
        try
        {
            int read;
            while ((read = await fs.ReadAsync(rented.AsMemory(0, rented.Length), ct)) > 0)
            {
                hasher.Append(rented.AsSpan(0, read));
            }

            var hashBytes = hasher.GetCurrentHash(); // 8 bytes
            return ToHex(hashBytes);
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(rented);
        }
    }

    private static string ToHex(byte[] bytes)
    {
        // fast-ish hex
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes)
            sb.Append(b.ToString("x2"));
        return sb.ToString();
    }
}
