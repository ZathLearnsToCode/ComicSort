using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ComicSort.UI.UI_Services
{
    public interface IDialogServices
    {
        Task<string?> ShowOpenFileDialogAsync(string title);

        Task<string?> ShowOpenFolderDialogAsync(string Title);

    }
}
