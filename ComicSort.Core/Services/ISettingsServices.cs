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

        ComicSortSettings Settings { get; }
    }
}
