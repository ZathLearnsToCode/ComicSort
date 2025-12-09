using ComicSort.Data.Entities;
using System;
using System.Collections.Generic;
using System.Text;

namespace ComicSort.Core.Mappers
{
    public static class ComicBookMapper
    {
        public static DTO.ComicBookDTO ToDTO(this ComicBookEntity entity)
        {
            return new DTO.ComicBookDTO
            {
                Id = entity.Id,
                FilePath =  entity.FilePath,
                DateAdded = entity.DateAdded,
                FileSize = entity.FileSize,
                CreationDate = entity.CreationDate,
                ModifiedDate = entity.ModifiedDate
            };
        }

        public static ComicBookEntity ToEntity(this DTO.ComicBookDTO dto)
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
