// FixMyCity.API/Models/AuthModels.cs
// New DTOs for JWT auth endpoints.

namespace FixMyCity.API.Models
{
    public class RefreshTokenRequest
    {
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class LogoutRequest
    {
        public string RefreshToken { get; set; } = string.Empty;
    }
}
