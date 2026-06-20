namespace FixMyCity.DAL.Models;

public partial class Organisation
{
    public Organisation()
    {
        PWGParticipationRequests = new HashSet<PwgparticipationRequest>();
        PwgReports = new HashSet<Pwgreport>();
    }

    public int OrgId { get; set; }
    public int UserId { get; set; }
    public string OrgName { get; set; }
    public string OrgType { get; set; }   // 'NGO' | 'Student Group' | 'CSR' | 'Other'
    public string RegistrationNo { get; set; }
    public string ContactEmail { get; set; }
    public string ContactPhone { get; set; }
    public string Address { get; set; }
    public string ApprovalStatus { get; set; }   // 'Pending' | 'Approved' | 'Rejected'
    public DateTime? ApprovedAt { get; set; }

    // FIX-04 (GAP-06) — ALTER TABLE in 00_Schema_Sprint2.sql:162 added SuspendedAt.
    // EF model was missing the property until Phase 2 (2026-05-19); cascade suspension
    // from PWGReports could not be persisted via EF.
    public DateTime? SuspendedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public User User { get; set; }

    public ICollection<PwgparticipationRequest> PWGParticipationRequests { get; set; }
    public ICollection<Pwgreport> PwgReports { get; set; }
}
