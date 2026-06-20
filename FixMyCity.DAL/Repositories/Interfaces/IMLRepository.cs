using FixMyCity.DAL.Models;
using FixMyCity.DAL.DTOs;

namespace FixMyCity.DAL.Repositories.Interfaces;

/// <summary>
/// Data-access contract for ML prediction scores, complaint recommendations,
/// and user interest management (category and locality preferences).
/// Sprint 2 new repository. US24, US26, US51–US54.
/// </summary>
public interface IMLRepository
{
    // ── ML Scores ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Upserts ML prediction scores for a complaint.
    /// Calls usp_SaveMLScores. Returns true on success. US52, US53, US54.
    /// </summary>
    bool SaveMLScores(int complaintId, DateTime? predictedResolutionDate,
                      decimal? resolutionProbability, decimal? priorityScore,
                      string modelVersion = null);

    /// <summary>
    /// Returns the ML score record for a complaint. Null if not yet scored.
    /// US52, US53, US54.
    /// </summary>
    ComplaintMlscore GetMLScores(int complaintId);

    // ── Recommendations ───────────────────────────────────────────────────────

    /// <summary>
    /// Returns personalised open complaint recommendations based on the user's
    /// category and locality interests, ranked by ML priority score desc.
    /// Calls usp_GetRecommendedComplaints via FromSqlRaw. US24, US51.
    /// </summary>
    List<RecommendedComplaintResult> GetRecommendedComplaints(int userId, int topN = 10);

    // ── User Interests ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all interest records for a user. Includes Category, PreferredLocality. US26.
    /// </summary>
    List<UserInterest> GetUserInterests(int userId);

    /// <summary>
    /// Adds a category or locality interest (idempotent — ignores duplicates).
    /// At least one of categoryId or preferredLocalityId must be provided.
    /// Calls usp_AddUserInterest. Returns true on success. US26.
    /// </summary>
    bool AddUserInterest(int userId, short? categoryId = null, int? preferredLocalityId = null);

    /// <summary>
    /// Removes a category or locality interest.
    /// Calls usp_RemoveUserInterest. Returns true on success. US26.
    /// </summary>
    bool RemoveUserInterest(int userId, short? categoryId = null, int? preferredLocalityId = null);
}
