using ComicSort.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace ComicSort.Core.Services
{
    public interface ISettingsServices
    {
        
        void Save();
        bool TryAddComicFolder(string folderPath, out string? error);

        ComicSortSettings Settings { get; }
    }
}
