namespace FixMyCity.DAL.Models;

public partial class Department
{
    public Department()
    {
        Complaints = new HashSet<Complaint>();
        DepartmentCategories = new HashSet<DepartmentCategory>();
    }

    public int DeptId { get; set; }
    public int UserId { get; set; }
    public string DeptName { get; set; }
    public string Ministry { get; set; }
    public short CategoryId { get; set; }
    public string ContactEmail { get; set; }
    public string ContactPhone { get; set; }
    public string Address { get; set; }

    // F14 — FK to Localities (replaces VARCHAR Locality)
    public int LocalityId { get; set; }

    public string ApprovalStatus { get; set; }   // 'Pending' | 'Approved' | 'Rejected'
    public DateTime? ApprovedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public User User { get; set; }
    public IssueCategory Category { get; set; }
    public Locality Locality { get; set; }

    public ICollection<Complaint> Complaints { get; set; }
    public ICollection<DepartmentCategory> DepartmentCategories { get; set; }
}
