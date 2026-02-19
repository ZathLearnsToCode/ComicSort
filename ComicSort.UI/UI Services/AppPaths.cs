using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ComicSort.UI.UI_Services
{
    public static class AppPaths
    {
        public static string GetLibraryJsonPath()
        {
            // Cross-platform safe location
            var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(root, "ComicSort-Test");
            return Path.Combine(folder, "library.json");


        }

        public static string GetThumbCacheFolder()
        {
            var root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var folder = Path.Combine(root, "ComicSort-Test", "thumbs");
            Directory.CreateDirectory(folder);
            return folder;
        }

    }
}
