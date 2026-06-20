namespace FixMyCity.API.Models
{
    public class UpdateProfileRequest
    {
        public int    UserId     { get; set; }
        public string FullName   { get; set; }
        public string Phone      { get; set; }
        public string Address    { get; set; }
        public int    LocalityId { get; set; }
    }
}
