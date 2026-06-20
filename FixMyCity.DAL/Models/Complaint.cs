namespace FixMyCity.DAL.Models;

// F1 — ImagePath and ResolutionImagePath removed. ComplaintAttachments is the sole file store.
// F14 — LocalityId FK replaces VARCHAR Locality.
public partial class Complaint
{
    public Complaint()
    {
        ComplaintTimelines = new HashSet<ComplaintTimeline>();
        ComplaintRatings = new HashSet<ComplaintRating>();
        Contributions = new HashSet<Contribution>();
        Notifications = new HashSet<Notification>();
        PWGParticipationRequests = new HashSet<PwgparticipationRequest>();
        Attachments = new HashSet<ComplaintAttachment>();
        DuplicateLinks = new HashSet<DuplicateComplaintLink>();
        EscalationLogs = new HashSet<EscalationLog>();
        PwgReports = new HashSet<Pwgreport>();
        PointsLedgers = new HashSet<PointsLedger>();
    }

    public int ComplaintId { get; set; }
    public int CitizenUserId { get; set; }
    public int? DeptId { get; set; }
    public short CategoryId { get; set; }
    public string Title { get; set; }
    public string Description { get; set; }

    // F14 — FK to Localities
    public int LocalityId { get; set; }

    public string Address { get; set; }
    public string Criticality { get; set; }   // 'Low' | 'Medium' | 'High' | 'Critical'
    public string Status { get; set; }   // 'Submitted' | 'In Progress' | 'Resolved' | 'Rejected' | 'Re-opened' | 'Escalated' | 'Linked'

    public DateTime? EstimatedResDate { get; set; }

    // Geo-coordinates (US14, US59)
    public decimal? Latitude { get; set; }
    public decimal? Longitude { get; set; }

    // Duplicate linking (US49)
    public int? LinkedToComplaintId { get; set; }

    public DateTime SubmittedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? ResolvedAt { get; set; }

    // Navigation
    public User Citizen { get; set; }
    public Department Department { get; set; }
    public IssueCategory Category { get; set; }
    public Locality Locality { get; set; }
    public Complaint LinkedToComplaint { get; set; }
    public ComplaintMlscore MlScore { get; set; }

    public ICollection<ComplaintTimeline> ComplaintTimelines { get; set; }
    public ICollection<ComplaintRating> ComplaintRatings { get; set; }
    public ICollection<Contribution> Contributions { get; set; }
    public ICollection<Notification> Notifications { get; set; }
    public ICollection<PwgparticipationRequest> PWGParticipationRequests { get; set; }
    public ICollection<ComplaintAttachment> Attachments { get; set; }
    public ICollection<DuplicateComplaintLink> DuplicateLinks { get; set; }
    public ICollection<EscalationLog> EscalationLogs { get; set; }
    public ICollection<Pwgreport> PwgReports { get; set; }
    public ICollection<PointsLedger> PointsLedgers { get; set; }
}
