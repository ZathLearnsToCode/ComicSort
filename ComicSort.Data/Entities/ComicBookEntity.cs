using System;
using System.Collections.Generic;
using System.Text;

namespace ComicSort.Data.Entities
{
    public class ComicBookEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string FilePath { get; set; } = default!;

        public DateTime DateAdded { get; set; } = DateTime.UtcNow;

        public long FileSize { get; set; }

        public DateTime CreationDate { get; set; }

        public DateTime ModifiedDate { get; set; }

        public ComicInfoEntity? ComicInfo { get; set; }
    }
}
