namespace FixMyCity.DAL.Models;

public partial class ComplaintTimeline
{
    public ComplaintTimeline()
    {
        Attachments = new HashSet<ComplaintAttachment>();
    }

    public int TimelineId { get; set; }
    public int ComplaintId { get; set; }
    public int? ActorUserId { get; set; }   // NULL = system-triggered event (auto-escalation)
    public string OldStatus { get; set; }
    public string NewStatus { get; set; }
    public string Remark { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigation
    public Complaint Complaint { get; set; }
    public User Actor { get; set; }

    public ICollection<ComplaintAttachment> Attachments { get; set; }
}
