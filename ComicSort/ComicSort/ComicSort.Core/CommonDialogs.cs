using Microsoft.Win32;
using Ookii.Dialogs.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicSort.Core
{
    public static class CommonDialogs
    {
        public static string ShowFolderBrowserDialog()
        {
            string fullPath = null;
            var folderBrowserDialog = new VistaFolderBrowserDialog();
            folderBrowserDialog.ShowDialog();
            fullPath = folderBrowserDialog.SelectedPath;

            return fullPath;
        }

        public static string ShowComicOpenDialog(string title)
        {
            string str = null;

            OpenFileDialog ofd = new();
            ofd.Title = title.Replace("_", "");
            ofd.Filter = "All Files (*.*)|*.*|CBR files (*.cbr)|*.cbr|CBZ files (*.cbz)|*.cbz";
            ofd.CheckFileExists = true;
            ofd.ShowDialog();
            str = ofd.FileName;


            return str;
        }
    }
}
