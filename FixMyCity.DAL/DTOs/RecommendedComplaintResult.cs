namespace FixMyCity.DAL.DTOs;

public class RecommendedComplaintResult
{
    public int ComplaintId { get; set; }
    public string Title { get; set; }
    public short CategoryId { get; set; }
    public int LocalityId { get; set; }
    public string Criticality { get; set; }
    public string Status { get; set; }
    public DateTime SubmittedAt { get; set; }
    public decimal? PriorityScore { get; set; }
}