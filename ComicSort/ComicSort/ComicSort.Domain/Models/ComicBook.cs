using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicSort.Domain.Models
{
    public class ComicBook
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string File { get; set; }
        public string Series { get; set; }
        public string IssueNumber { get; set; }
        public string Volume { get; set; }
        public int PageCount { get; set; }
        public Page Pages { get; set; }
        public string DateAdded { get; set; }
        public long FileSize { get; set; }
        public string DateModified { get; set; }
        public string DateCreated { get; set; }
    }
}
