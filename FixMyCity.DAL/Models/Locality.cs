namespace FixMyCity.DAL.Models;

// F14 — Localities lookup table replaces all VARCHAR locality fields across the schema.
public partial class Locality
{
    public Locality()
    {
        Users = new HashSet<User>();
        Departments = new HashSet<Department>();
        Complaints = new HashSet<Complaint>();
        UserInterests = new HashSet<UserInterest>();
    }

    public int LocalityId { get; set; }
    public string LocalityName { get; set; }
    public string City { get; set; }
    public string State { get; set; }   // default 'Karnataka'
    public bool IsActive { get; set; }

    // Navigation
    public ICollection<User> Users { get; set; }
    public ICollection<Department> Departments { get; set; }
    public ICollection<Complaint> Complaints { get; set; }
    public ICollection<UserInterest> UserInterests { get; set; }
}
