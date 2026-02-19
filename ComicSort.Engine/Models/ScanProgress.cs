using System;
using System.Collections.Generic;
using System.Text;

namespace ComicSort.Engine.Models
{
    public sealed class ScanProgress
    {
        public int Processed { get; init; }
        public int Added { get; init; }
        public string? CurrentFile { get; init; }
    }
}
