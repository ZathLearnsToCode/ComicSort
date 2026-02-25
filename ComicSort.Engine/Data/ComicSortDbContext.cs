using ComicSort.Engine.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace ComicSort.Engine.Data;

public sealed class ComicSortDbContext : DbContext
{
    public ComicSortDbContext(DbContextOptions<ComicSortDbContext> options) : base(options)
    {
    }

    public DbSet<ComicFileEntity> ComicFiles => Set<ComicFileEntity>();

    public DbSet<ComicInfoEntity> ComicInfos => Set<ComicInfoEntity>();

    public DbSet<ComicPageEntity> ComicPages => Set<ComicPageEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ComicFileEntity>(entity =>
        {
            entity.ToTable("ComicFiles");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.NormalizedPath).IsRequired();
            entity.Property(x => x.FileName).IsRequired();
            entity.Property(x => x.Extension).IsRequired();
            entity.Property(x => x.Fingerprint).IsRequired();
            entity.HasIndex(x => x.NormalizedPath).IsUnique();
            entity.HasIndex(x => x.ModifiedUtc);
            entity.HasIndex(x => x.LastScannedUtc);
            entity.HasIndex(x => x.ScanState);

            entity.HasOne(x => x.ComicInfo)
                .WithOne(x => x.ComicFile)
                .HasForeignKey<ComicInfoEntity>(x => x.ComicFileId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasMany(x => x.Pages)
                .WithOne(x => x.ComicFile)
                .HasForeignKey(x => x.ComicFileId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ComicInfoEntity>(entity =>
        {
            entity.ToTable("ComicInfo");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.ComicFileId).IsUnique();
        });

        modelBuilder.Entity<ComicPageEntity>(entity =>
        {
            entity.ToTable("ComicPages");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.ComicFileId, x.ImageIndex }).IsUnique();
        });
    }
}
