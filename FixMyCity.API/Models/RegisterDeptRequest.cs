namespace FixMyCity.API.Models
{
    public class RegisterDeptRequest
    {
        // User account fields
        public string FullName   { get; set; }
        public string Email      { get; set; }
        public string Password   { get; set; }
        public string Phone      { get; set; }
        public string Address    { get; set; }
        public int    LocalityId { get; set; }

        // Department-specific fields
        public string DeptName     { get; set; }
        public string Ministry     { get; set; }
        public short  CategoryId   { get; set; }
        public string ContactEmail { get; set; }
        public string ContactPhone { get; set; }
    }
}
