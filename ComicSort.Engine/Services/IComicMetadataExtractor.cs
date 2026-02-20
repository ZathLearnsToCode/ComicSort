using ComicSort.Engine.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace ComicSort.Engine.Services
{
    public interface IComicMetadataExtractor
    {
        /// <summary>
        /// Attempts to extract metadata for a comic file.
        /// Returns null if unsupported or metadata not found.
        /// </summary>
        Task<ComicMetadata?> TryExtractAsync(string filePath, CancellationToken ct);
    }
}
