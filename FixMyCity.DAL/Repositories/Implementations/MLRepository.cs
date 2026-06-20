using FixMyCity.DAL.Models;
using FixMyCity.DAL.DTOs;
using FixMyCity.DAL.Repositories.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Data;

namespace FixMyCity.DAL.Repositories.Implementations;

/// <summary>
/// Implements <see cref="IMLRepository"/>.
/// Sprint 2 new repository. Handles ML prediction scores, complaint recommendations,
/// and user interest (category and locality preference) management.
/// US24, US26, US51–US54.
///
/// Phase-2 hardening (2026-05-19): every previously-silent catch now logs via ILogger.
/// </summary>
public class MLRepository : IMLRepository
{
    private readonly FixMyCityDbContext _context;
    private readonly ILogger<MLRepository> _logger;

    public MLRepository(FixMyCityDbContext context, ILogger<MLRepository> logger)
    {
        _context = context;
        _logger  = logger ?? NullLogger<MLRepository>.Instance;
    }

    // Backward-compatible overload for existing `new MLRepository(_context)` callers.
    public MLRepository(FixMyCityDbContext context)
        : this(context, NullLogger<MLRepository>.Instance) { }

    // ── SaveMLScores ──────────────────────────────────────────────────────────

    public bool SaveMLScores(int complaintId, DateTime? predictedResolutionDate,
                             decimal? resolutionProbability, decimal? priorityScore,
                             string modelVersion = null)
    {
        try
        {
            // usp_SaveMLScores is an UPSERT — INSERT or UPDATE existing record.
            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_SaveMLScores @ComplaintId, @PredictedResolutionDate, @ResolutionProbability, @PriorityScore, @ModelVersion",
                new SqlParameter("@ComplaintId", complaintId),
                new SqlParameter("@PredictedResolutionDate",
                    predictedResolutionDate.HasValue
                        ? (object)DateOnly.FromDateTime(predictedResolutionDate.Value)
                        : DBNull.Value),
                new SqlParameter("@ResolutionProbability", (object)resolutionProbability ?? DBNull.Value),
                new SqlParameter("@PriorityScore", (object)priorityScore ?? DBNull.Value),
                new SqlParameter("@ModelVersion", (object)modelVersion ?? DBNull.Value));
            return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "MLRepository stored-procedure call failed"); return false; }
    }

    // ── GetMLScores ───────────────────────────────────────────────────────────

    public ComplaintMlscore GetMLScores(int complaintId)
    {
        return _context.ComplaintMlScores
                       .FirstOrDefault(s => s.ComplaintId == complaintId);
    }

    // ── GetRecommendedComplaints ──────────────────────────────────────────────

    public List<RecommendedComplaintResult> GetRecommendedComplaints(int userId, int topN = 10)
    {
        // Database.SqlQueryRaw<T> streams the SP result set directly —
        // no subquery wrapping. Correct API for keyless/DTO projections.
        return _context.Database
                       .SqlQueryRaw<RecommendedComplaintResult>(
                           "EXEC dbo.usp_GetRecommendedComplaints @UserId, @TopN",
                           new SqlParameter("@UserId", userId),
                           new SqlParameter("@TopN", topN))
                       .ToList();
    }

    // ── GetUserInterests ──────────────────────────────────────────────────────

    public List<UserInterest> GetUserInterests(int userId)
    {
        return _context.UserInterests
                       .Include(i => i.Category)
                       .Include(i => i.PreferredLocality)
                       .Where(i => i.UserId == userId)
                       .OrderBy(i => i.CreatedAt)
                       .ToList();
    }

    // ── AddUserInterest ───────────────────────────────────────────────────────

    public bool AddUserInterest(int userId, short? categoryId = null,
                                int? preferredLocalityId = null)
    {
        // DB CHECK: at least one must be provided.
        if (!categoryId.HasValue && !preferredLocalityId.HasValue)
            return false;

        try
        {
            // SP is idempotent — ignores duplicates via filtered unique index (F6-style).
            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_AddUserInterest @UserId, @CategoryId, @PreferredLocalityId",
                new SqlParameter("@UserId", userId),
                new SqlParameter("@CategoryId", (object)categoryId ?? DBNull.Value),
                new SqlParameter("@PreferredLocalityId", (object)preferredLocalityId ?? DBNull.Value));
            return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "MLRepository stored-procedure call failed"); return false; }
    }

    // ── RemoveUserInterest ────────────────────────────────────────────────────

    public bool RemoveUserInterest(int userId, short? categoryId = null,
                                   int? preferredLocalityId = null)
    {
        if (!categoryId.HasValue && !preferredLocalityId.HasValue)
            return false;

        try
        {
            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_RemoveUserInterest @UserId, @CategoryId, @PreferredLocalityId",
                new SqlParameter("@UserId", userId),
                new SqlParameter("@CategoryId", (object)categoryId ?? DBNull.Value),
                new SqlParameter("@PreferredLocalityId", (object)preferredLocalityId ?? DBNull.Value));
            return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "MLRepository stored-procedure call failed"); return false; }
    }
}
