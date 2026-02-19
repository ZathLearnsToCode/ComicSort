using System;
using System.Collections.Generic;
using System.Text;

namespace ComicSort.Engine.Models
{
    public sealed class ScanRequest
    {
        public required string FolderPath { get; init; }
        public bool Recursive { get; init; } = true;
    }
}
