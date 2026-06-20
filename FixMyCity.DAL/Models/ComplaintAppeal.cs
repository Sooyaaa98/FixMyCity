namespace FixMyCity.DAL.Models;

// Phase 8 (§6) — citizen appeals against a 'Rejected' complaint.
// A filtered unique index on (ComplaintId, CitizenUserId) WHERE Status = 'Pending'
// allows a citizen to re-appeal after a previous appeal was Resolved.
// When the admin approves the appeal the SP flips the complaint back to
// 'Submitted' so the routing pipeline runs again.
public partial class ComplaintAppeal
{
    public int       AppealId      { get; set; }
    public int       ComplaintId   { get; set; }
    public int       CitizenUserId { get; set; }
    public string    Reason        { get; set; }
    public string    Status        { get; set; }   // 'Pending' | 'Resolved'
    public int?      AdminUserId   { get; set; }
    public string    AdminNote     { get; set; }
    public string    Decision      { get; set; }   // 'Approved' | 'Rejected' (NULL while Pending)
    public DateTime  CreatedAt     { get; set; }
    public DateTime? ResolvedAt    { get; set; }

    // Navigation
    public Complaint Complaint     { get; set; }
    public User      CitizenUser   { get; set; }
    public User      AdminUser     { get; set; }
}
