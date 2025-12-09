using ComicSort.Data.Entities;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace ComicSort.Data.SQL
{
    public class ComicSortDBSQLiteContext : DbContext
    {
        public DbSet<ComicBookEntity> ComicBooks => Set<ComicBookEntity>();

        public ComicSortDBSQLiteContext(DbContextOptions<ComicSortDBSQLiteContext> options)
            : base(options)
        {
        }
    }
}
