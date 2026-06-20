namespace FixMyCity.API.Models
{
    public class SSOLoginRequest
    {
        public string SSOProvider   { get; set; }   // "Google" | "Microsoft"
        public string SSOExternalId { get; set; }
        public string Email         { get; set; }
        public string FullName      { get; set; }
    }
}
