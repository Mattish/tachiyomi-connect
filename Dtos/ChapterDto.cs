namespace TachiyomiConnect.Dtos
{
    public class ChapterDto
    {
        public string Url { get; set; }
        public string Name { get; set; }
        public long DateUpload { get; set; }
        public long DateFetch { get; set; }
        public decimal ChapterNumber { get; set; }
        public bool Read { get; set; }
        public bool Bookmark { get; set; }
        public int LastPageRead { get; set; }
        public int SourceOrder { get; set; }
    }
}