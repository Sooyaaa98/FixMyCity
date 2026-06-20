namespace FixMyCity.API.Models
{
    public class ChangePasswordRequest
    {
        public int    UserId          { get; set; }
        public string CurrentPassword { get; set; }
        public string NewPassword     { get; set; }
    }
}
