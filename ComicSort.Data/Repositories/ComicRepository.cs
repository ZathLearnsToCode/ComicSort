using ComicSort.Core.DTO;
using ComicSort.Core.Services.Repositories;
using ComicSort.Data.Mappers;
using ComicSort.Data.SQL;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace ComicSort.Data.Repositories
{
    public class ComicRepository : IComicRepository
    {
        private readonly ComicSortDBSQLiteContext _db;
        public ComicRepository(ComicSortDBSQLiteContext db)
        {
            _db = db;
        }

        public async Task AddComicsAsync(IEnumerable<ComicBookDTO> comics)
        {
            var entities = comics.Select(c => c.ToEntity());
            await _db.ComicBooks.AddRangeAsync(entities);
            await _db.SaveChangesAsync();
        }

        public async Task<bool> ComicExistsAsync(string filePath)
        {
            return await _db.ComicBooks.AnyAsync(c => c.FilePath == filePath);
        }

        public async Task<List<ComicBookDTO>> GetAllComicsAsync()
        {
            return await _db.ComicBooks
                .Select(e => e.ToDTO())
                .ToListAsync();
        }
    }
}
