using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicSort.Domain.Models
{
    public class Page
    {
        public int Id { get; set; }
        public int Image { get; set; }
        public long ImageWidth { get; set; }
        public long ImageHeight { get; set; }
        public string Type { get; set; }
    }
}
