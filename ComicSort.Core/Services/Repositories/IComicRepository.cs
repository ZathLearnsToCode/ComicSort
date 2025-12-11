using ComicSort.Core.DTO;
using System;
using System.Collections.Generic;
using System.Text;

namespace ComicSort.Core.Services.Repositories
{
    public interface IComicRepository
    {
        Task AddComicsAsync(IEnumerable<ComicBookDTO> comics);
        Task<bool> ComicExistsAsync(string filePath);
        Task<List<ComicBookDTO>> GetAllComicsAsync();
    }
}
