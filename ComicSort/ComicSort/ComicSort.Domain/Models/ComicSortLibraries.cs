using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicSort.Domain.Models
{
    public class ComicSortLibraries : DomainObject
    {
        
        public string LibraryPath { get; set; }
        public string LibraryFile { get; set; }
        public string LibraryName { get; set; }
        public string Created { get; set; }
        public string LastAccessed { get; set; }
        public string LibraryType { get; set; }
    }
}
