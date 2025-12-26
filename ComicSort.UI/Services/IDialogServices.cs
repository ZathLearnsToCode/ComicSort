using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ComicSort.UI.Services
{
    public interface IDialogServices
    {
        Task<string?> ShowOpenFileDialogAsync(string title);

        Task<string?> ShowOpenFolderDialogAsync(string Title);
        void ShowProfileDialog();
    }
}
