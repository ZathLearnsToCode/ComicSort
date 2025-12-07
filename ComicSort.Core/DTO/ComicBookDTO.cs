using System;
using System.Collections.Generic;
using System.Text;

namespace ComicSort.Core.DTO
{
    public class ComicBookDTO
    {
        public Guid Id { get; set; }
        public string FilePath { get; set; } = default!;
        public DateTime DateAdded { get; set; }
        public long FileSize { get; set; }
        public DateTime CreationDate { get; set; }
        public DateTime ModifiedDate { get; set; }
    }
}
