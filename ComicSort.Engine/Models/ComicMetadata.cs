using System;
using System.Collections.Generic;
using System.Text;

namespace ComicSort.Engine.Models
{
    public sealed class ComicMetadata
    {
        public string? Series { get; set; }
        public string? Title { get; set; }
        public int? Year { get; set; }

        public string? Number { get; set; }   // often "001", "12.5", etc.
        public int? Volume { get; set; }

        public string? Summary { get; set; }
    }
}
