using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ComicSort.UI.Services
{
    public interface IDialogService
    {
        Task<string?> ShowOpenFileDialogAsync(string title);
        Task<string?> ShowOpenFolderDialogAsync(string title);
        Task<bool> ShowSettingsDialogAsync();
    }
}
