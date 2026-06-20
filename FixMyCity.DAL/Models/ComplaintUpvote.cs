namespace FixMyCity.DAL.Models;

// Phase 8 (§1) — citizen up-votes on a complaint (a "+1" signal).
// (ComplaintId, CitizenUserId) is unique so duplicate votes are rejected by
// the SQL constraint as well as by the toggle stored procedure.
public partial class ComplaintUpvote
{
    public int      UpvoteId      { get; set; }
    public int      ComplaintId   { get; set; }
    public int      CitizenUserId { get; set; }
    public DateTime CreatedAt     { get; set; }

    // Navigation
    public Complaint Complaint { get; set; }
    public User      Citizen   { get; set; }
}
