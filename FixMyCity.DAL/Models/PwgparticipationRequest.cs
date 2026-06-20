namespace FixMyCity.DAL.Models;

// F6 — UNIQUE constraint replaced with filtered unique index on (ComplaintId, OrgId)
//       WHERE Status = 'Pending', allowing re-application after rejection.
public partial class PwgparticipationRequest
{
    public int RequestId { get; set; }
    public int ComplaintId { get; set; }
    public int OrgId { get; set; }
    public int SolverUserId { get; set; }
    public string Status { get; set; }   // 'Pending' | 'Approved' | 'Rejected'
    public string RequestNote { get; set; }
    public string DecisionNote { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? DecidedAt { get; set; }

    // Navigation
    public Complaint Complaint { get; set; }
    public Organisation Organisation { get; set; }
    public User SolverUser { get; set; }
}
