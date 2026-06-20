namespace FixMyCity.API.Models
{
    public class RegisterOrgRequest
    {
        // User account fields
        public string FullName   { get; set; }
        public string Email      { get; set; }
        public string Password   { get; set; }
        public string Phone      { get; set; }
        public string Address    { get; set; }
        public int    LocalityId { get; set; }

        // Organisation-specific fields
        public string OrgName        { get; set; }
        public string OrgType        { get; set; }   // NGO / Student Group / CSR / Other
        public string RegistrationNo { get; set; }
        public string ContactEmail   { get; set; }
        public string ContactPhone   { get; set; }
    }
}
