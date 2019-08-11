namespace TachiyomiConnect.Dtos
{
    public class MangaDto
    {
        public ChapterDto[] Chapters { get; set; }
        public string Url { get; set; }
        public string Title { get; set; }
        public string ThumbnailUrl { get; set; }
        public long LastUpdated { get; set; }
        public string Artist { get; set; }
        public string Author { get; set; }
        public long Source { get; set; }
    }
}