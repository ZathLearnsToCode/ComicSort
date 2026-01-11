namespace ComicSort.Data.Entities
{
    public class ComicInfoPageEntity
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid ComicInfoId { get; set; }
        public ComicInfoEntity ComicInfo { get; set; } = default!;

        public int Image { get; set; }

        public int? ImageWidth { get; set; }
        public int? ImageHeight { get; set; }

        public string? Type { get; set; } // FrontCover, Story, etc (optional)
    }
}