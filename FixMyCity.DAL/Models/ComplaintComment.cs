namespace FixMyCity.DAL.Models;

// Phase 8 (§7) — public discussion thread per complaint.
// `IsOfficialReply` is set by the stored procedure when the author has the
// 'Solver' or 'SuperAdmin' role; the UI uses it to render an "Official" badge.
// `IsDeleted` is a soft-delete flag — rows are retained for moderation audit.
public partial class ComplaintComment
{
    public int      CommentId       { get; set; }
    public int      ComplaintId     { get; set; }
    public int      UserId          { get; set; }
    public string   CommentText     { get; set; }
    public bool     IsOfficialReply { get; set; }
    public bool     IsDeleted       { get; set; }
    public DateTime CreatedAt       { get; set; }

    // Navigation
    public Complaint Complaint { get; set; }
    public User      User      { get; set; }
}
