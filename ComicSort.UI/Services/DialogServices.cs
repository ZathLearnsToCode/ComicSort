using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ComicSort.UI.Services
{
    public class DialogServices : IDialogServices
    {
        private static Window? GetActiveWindow()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                return desktop.Windows.FirstOrDefault(x => x.IsActive) ?? desktop.MainWindow;
            }
            return null;
        }

        public async Task<string?> ShowOpenFileDialogAsync(string title)
        {
            var window = GetActiveWindow();
            if (window?.StorageProvider is not { } provider)
                return null;

            var options = new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
            };

            var file = await provider.OpenFilePickerAsync(options);
            return file?.FirstOrDefault()?.TryGetLocalPath();
        }

        public async Task<string?> ShowOpenFolderDialogAsync(string Title)
        {
            var window = GetActiveWindow();
            if (window?.StorageProvider is not { } provider)
                return null;

            var folder = await provider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = Title,
                AllowMultiple = false,

            });

            return folder?.FirstOrDefault()?.TryGetLocalPath();
        }
    }
}
