using System;

namespace TachiyomiConnect.Dtos
{
    public class StatesResponseDto
    {
        public int FromVersionNumber { get; set; }
        public StateResponseDto[] States { get; set; }
    }
}