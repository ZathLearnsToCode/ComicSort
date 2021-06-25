using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicSort.Domain.Models
{
    public class ComicSortLibraries
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string LibraryPath { get; set; }
        public string LibraryFile { get; set; }
        public string LibraryName { get; set; }
        public DateTime Created { get; set; }
        public DateTime LastAccessed { get; set; }
        public string LibraryType { get; set; }
    }
}
