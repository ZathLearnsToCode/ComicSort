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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<ComicBookEntity>(entity =>
            {
                entity.ToTable("ComicBooks");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.FilePath).IsRequired();
                entity.HasIndex(e => e.FilePath).IsUnique();
                
            });
        }
    }
}
