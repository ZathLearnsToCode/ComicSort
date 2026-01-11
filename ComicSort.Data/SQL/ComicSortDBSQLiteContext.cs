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
        public DbSet<ComicInfoEntity> ComicInfo => Set<ComicInfoEntity>();
        public DbSet<ComicInfoPageEntity> ComicInfoPages => Set<ComicInfoPageEntity>();

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

                // 1:0..1 ComicInfo
                entity.HasOne(e => e.ComicInfo)
                      .WithOne(ci => ci.ComicBook)
                      .HasForeignKey<ComicInfoEntity>(ci => ci.ComicBookId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ComicInfos
            modelBuilder.Entity<ComicInfoEntity>(entity =>
            {
                entity.ToTable("ComicInfo");
                entity.HasKey(e => e.Id);

                entity.HasIndex(e => e.ComicBookId).IsUnique();

                // Optional: store big text as TEXT (SQLite does this anyway)
                entity.Property(e => e.Summary);
                entity.Property(e => e.Characters);
                entity.Property(e => e.Teams);
                entity.Property(e => e.Locations);

                entity.HasMany(e => e.Pages)
                      .WithOne(p => p.ComicInfo)
                      .HasForeignKey(p => p.ComicInfoId)
                      .OnDelete(DeleteBehavior.Cascade);
            });

            // ComicInfoPages
            modelBuilder.Entity<ComicInfoPageEntity>(entity =>
            {
                entity.ToTable("ComicInfoPages");
                entity.HasKey(e => e.Id);

                // Prevent duplicate page index rows per ComicInfo
                entity.HasIndex(e => new { e.ComicInfoId, e.Image }).IsUnique();
            });
        }
    }
}
