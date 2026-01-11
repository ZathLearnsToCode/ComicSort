namespace ComicSort.Data.Entities
{
    public class ComicInfoEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        // FK -> ComicBook (unique)
        public Guid ComicBookId { get; set; }
        public ComicBookEntity ComicBook { get; set; } = default!;

        public string? Series { get; set; }
        public string? Number { get; set; }

        public string? AlternateSeries { get; set; }

        public string? Summary { get; set; }

        public string? Writer { get; set; }
        public string? Penciller { get; set; }
        public string? Inker { get; set; }
        public string? Colorist { get; set; }
        public string? Letterer { get; set; }
        public string? CoverArtist { get; set; }
        public string? Editor { get; set; }

        public string? Publisher { get; set; }

        public int? PageCount { get; set; }

        public string? Characters { get; set; }
        public string? Teams { get; set; }
        public string? Locations { get; set; }

        public ICollection<ComicInfoPageEntity> Pages { get; set; } = new List<ComicInfoPageEntity>();
    }
}