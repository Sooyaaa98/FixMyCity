namespace FixMyCity.DAL.Models;

public partial class ComplaintRating
{
    public int RatingId { get; set; }
    public int ComplaintId { get; set; }
    public int CitizenUserId { get; set; }
    public byte Stars { get; set; }   // 1–5
    public string Comment { get; set; }
    public DateTime RatedAt { get; set; }

    // Navigation
    public Complaint Complaint { get; set; }
    public User CitizenUser { get; set; }
}
