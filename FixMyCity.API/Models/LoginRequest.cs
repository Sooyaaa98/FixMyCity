namespace FixMyCity.API.Models
{
    public class LoginRequest
    {
        public string Email    { get; set; }
        public string Password { get; set; }   // plain-text; controller hashes before DAL call
    }
}
