namespace FixMyCity.DAL.Models;

public partial class IssueCategory
{
    public IssueCategory()
    {
        Complaints = new HashSet<Complaint>();
        Departments = new HashSet<Department>();
        DepartmentCategories = new HashSet<DepartmentCategory>();
        UserInterests = new HashSet<UserInterest>();
    }

    public short CategoryId { get; set; }
    public string CategoryName { get; set; }
    public string Description { get; set; }

    // Navigation
    public ICollection<Complaint> Complaints { get; set; }
    public ICollection<Department> Departments { get; set; }
    public ICollection<DepartmentCategory> DepartmentCategories { get; set; }
    public ICollection<UserInterest> UserInterests { get; set; }
}
