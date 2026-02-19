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
    }
}
