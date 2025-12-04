using ComicSort.Core.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace ComicSort.Core.Services
{
    public interface ISettingsServices
    {
        ComicSortSettings Load();
        void Save(ComicSortSettings settings);
        bool TryAddComicFolder(string folderPath, out string? error);

        ComicSortSettings Settings { get; }
    }
}
