using System;

namespace TachiyomiConnect.Dtos
{
    public class StateResponseDto
    {
        public Guid Guid { get; set; }
        public int VersionNumber { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public MangaDto[] AddedOrUpdatedMangas { get; set; }
        public string[] RemovedMangas { get; set; }
    }
}