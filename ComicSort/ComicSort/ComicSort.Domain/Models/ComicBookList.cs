using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicSort.Domain.Models
{
    public class ComicBookList
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public List<ComicBook> ComicBooks { get; set; } = new List<ComicBook>();
    }
}
