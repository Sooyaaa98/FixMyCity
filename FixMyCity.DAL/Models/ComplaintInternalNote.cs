namespace FixMyCity.DAL.Models;

// Phase 8 (§15) — internal notes visible only to Solver / SuperAdmin.
// The controller enforces the role check before exposing these rows; the
// table itself is just a private journal so departments can coordinate
// without polluting the public comment thread.
public partial class ComplaintInternalNote
{
    public int      NoteId          { get; set; }
    public int      ComplaintId     { get; set; }
    public int      CreatedByUserId { get; set; }
    public string   NoteText        { get; set; }
    public DateTime CreatedAt       { get; set; }

    // Navigation
    public Complaint Complaint { get; set; }
    public User      Author    { get; set; }
}
