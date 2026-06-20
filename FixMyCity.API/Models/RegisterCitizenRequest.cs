namespace FixMyCity.API.Models
{
    public class RegisterCitizenRequest
    {
        public string FullName   { get; set; }
        public string Email      { get; set; }
        public string Password   { get; set; }   // plain-text; controller hashes before DAL call
        public string Phone      { get; set; }
        public string Address    { get; set; }
        public int    LocalityId { get; set; }
        public string AadhaarNo  { get; set; }
    }
}
