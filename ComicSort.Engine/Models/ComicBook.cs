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


    }
}
