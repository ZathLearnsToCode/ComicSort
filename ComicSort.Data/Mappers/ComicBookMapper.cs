using ComicSort.Core.DTO;
using ComicSort.Data.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace ComicSort.Data.Mappers
{
    public static class ComicBookMapper
    {
        public static ComicBookDTO ToDTO(this ComicBookEntity entity)
        {
            return new ComicBookDTO
            {
                Id = entity.Id,
                FilePath =  entity.FilePath,
                DateAdded = entity.DateAdded,
                FileSize = entity.FileSize,
                CreationDate = entity.CreationDate,
                ModifiedDate = entity.ModifiedDate
            };
        }

        public static ComicBookEntity ToEntity(this ComicBookDTO dto)
        {
            return new ComicBookEntity
            {
                Id = dto.Id,
                FilePath = dto.FilePath,
                DateAdded = dto.DateAdded,
                FileSize = dto.FileSize,
                CreationDate = dto.CreationDate,
                ModifiedDate = dto.ModifiedDate
            };
        }
    }
}
