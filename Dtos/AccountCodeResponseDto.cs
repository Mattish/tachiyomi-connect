using System;

namespace TachiyomiConnect.Dtos
{
    public class AccountCodeResponseDto
    {
        public string Code { get; set; }
        public DateTimeOffset ValidUntil { get; set; }
    }
}