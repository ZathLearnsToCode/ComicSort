using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicSort.Core
{
    public static class FileUtilities
    {
        public static string CreateDirectory(string path, string fileName)
        {
            string fullPath = null;
            if(!Directory.Exists(path + fileName))
            {
                var dir = Directory.CreateDirectory(path + fileName);
                fullPath = dir.FullName;
            }

            return fullPath;

        }

        public static FileInfo GetFileInfos(string file)
        {
            FileInfo fileInfo = new FileInfo(file);

            return fileInfo;

        }
    }
}
