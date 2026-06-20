namespace FixMyCity.API.Models
{
    public class RequestPasswordResetRequest
    {
        public string Email { get; set; }
    }

    public class ResetPasswordRequest
    {
        public string Token       { get; set; }   // raw token; controller hashes before DAL call
        public string NewPassword { get; set; }
    }
}
