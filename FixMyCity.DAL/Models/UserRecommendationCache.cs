using System;

namespace FixMyCity.DAL.Models;

/// <summary>
/// Nightly pre-computed top-N complaint IDs per user. Consumed by
/// <c>GET /api/ML/GetRecommendedComplaints</c> via <c>usp_GetRecommendationsFromCache</c>.
/// Writes go through <c>usp_UpsertRecommendationCache</c> (atomic DELETE+INSERT
/// inside a transaction). Reads can use EF directly.
/// </summary>
public partial class UserRecommendationCache
{
    public int      CacheId     { get; set; }
    public int      UserId      { get; set; }
    public int      ComplaintId { get; set; }
    public decimal  Score       { get; set; }
    public DateTime GeneratedAt { get; set; }

    public User      User      { get; set; }
    public Complaint Complaint { get; set; }
}
