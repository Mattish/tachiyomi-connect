using System;

namespace TachiyomiConnect.Dtos
{
    public class RegistrationResponseDto
    {
        public Guid DeviceId { get; set; }
        public Guid RecoveryCode { get; set; }
        public string SecretToken { get; set; }
        public StateResponseDto InitialState { get; set; }
    }
}