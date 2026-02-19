using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicSort.UI.UI_Services
{
    public  class DialogServices : IDialogServices
    {
        public async Task<string?> ShowOpenFolderDialogAsync(string title)
        {
            if (GetStorageProvider() is not { } provider)
                return null;

            var results = await provider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = title,
                AllowMultiple = false
            });

            return results.FirstOrDefault()?.TryGetLocalPath();
        }

        public async Task<string?> ShowOpenFileDialogAsync(string title)
        {
            if (GetStorageProvider() is not { } provider)
                return null;

            var results = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
                
            });

            return results.FirstOrDefault()?.TryGetLocalPath();
        }

        private static IStorageProvider? GetStorageProvider()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var window = desktop.Windows.FirstOrDefault(x => x.IsActive) ?? desktop.MainWindow;
                return window?.StorageProvider;
            }

            return null;
        }
    }
}
