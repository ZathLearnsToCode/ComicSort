using System;
using System.Collections.Generic;
using System.Text;

namespace ComicSort.Engine.Models
{
    public class ComicBook
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string FilePath { get; set; } = string.Empty;

        public string FileName => Path.GetFileNameWithoutExtension(FilePath);
        public string Extension => Path.GetExtension(FilePath);

        public long FileSize { get; set; }
        public DateTime AddedOn { get; set; } = DateTime.UtcNow;

        // Phase 3: metadata
        public ComicMetadata? Metadata { get; set; }
        public DateTime? MetadataUpdatedOnUtc { get; set; }

        public string? FileHash { get; set; }              // hex string
        public string? FileHashAlgorithm { get; set; }     // "xxHash64"
        public DateTime? FileHashUpdatedOnUtc { get; set; }
    }

}
