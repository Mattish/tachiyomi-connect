using System;

namespace TachiyomiConnect.Dtos
{
    public class VersionResponseDto
    {
        public Guid Guid { get; set; }
        public int VersionNumber { get; set; }
    }
}
