using FixMyCity.DAL.DTOs;
using FixMyCity.DAL.Models;
using FixMyCity.DAL.Repositories.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Data;

namespace FixMyCity.DAL.Repositories.Implementations;

/// <summary>
/// Implements <see cref="IFeatureRepository"/>.
///
/// Phase 8 (2026-05-20) — every method maps to one of the new stored
/// procedures in 06_FeatureSuggestions.sql. Reads use plain EF projections;
/// writes go through ExecuteSqlRaw so the SP's RAISERROR + transaction logic
/// is preserved. Exceptions are logged and the public contract returns a
/// neutral sentinel (0 / false / empty list) so the upstream controllers
/// keep their existing `{ success: false }` JSON shape without bubbling raw
/// SQL exception details to clients.
/// </summary>
public class FeatureRepository : IFeatureRepository
{
    private readonly FixMyCityDbContext _context;
    private readonly ILogger<FeatureRepository> _logger;

    public FeatureRepository(FixMyCityDbContext context, ILogger<FeatureRepository> logger)
    {
        _context = context;
        _logger  = logger ?? NullLogger<FeatureRepository>.Instance;
    }

    public FeatureRepository(FixMyCityDbContext context)
        : this(context, NullLogger<FeatureRepository>.Instance) { }

    // ── §1 Upvotes ────────────────────────────────────────────────────────────

    public UpvoteResult ToggleUpvote(int complaintId, int citizenUserId)
    {
        try
        {
            var outCount = new SqlParameter
            {
                ParameterName = "@NewCount",
                SqlDbType = SqlDbType.Int,
                Direction = ParameterDirection.Output
            };
            var outHas = new SqlParameter
            {
                ParameterName = "@HasUpvoted",
                SqlDbType = SqlDbType.Bit,
                Direction = ParameterDirection.Output
            };

            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_ToggleComplaintUpvote @ComplaintId, @CitizenUserId, @NewCount OUTPUT, @HasUpvoted OUTPUT",
                new SqlParameter("@ComplaintId",   complaintId),
                new SqlParameter("@CitizenUserId", citizenUserId),
                outCount, outHas);

            return new UpvoteResult
            {
                NewCount   = outCount.Value != DBNull.Value ? (int)outCount.Value : 0,
                HasUpvoted = outHas.Value   != DBNull.Value && (bool)outHas.Value,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ToggleUpvote failed: complaint {ComplaintId} citizen {CitizenUserId}",
                complaintId, citizenUserId);
            return new UpvoteResult { NewCount = 0, HasUpvoted = false };
        }
    }

    public int GetUpvoteCount(int complaintId)
        => _context.ComplaintUpvotes.Count(u => u.ComplaintId == complaintId);

    public bool HasUpvoted(int complaintId, int citizenUserId)
        => _context.ComplaintUpvotes
                   .Any(u => u.ComplaintId == complaintId && u.CitizenUserId == citizenUserId);

    // ── §7 Comments ───────────────────────────────────────────────────────────

    public int AddComment(int complaintId, int userId, string commentText)
    {
        try
        {
            var outId = new SqlParameter
            {
                ParameterName = "@NewCommentId",
                SqlDbType = SqlDbType.Int,
                Direction = ParameterDirection.Output
            };

            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_AddComplaintComment @ComplaintId, @UserId, @CommentText, @NewCommentId OUTPUT",
                new SqlParameter("@ComplaintId", complaintId),
                new SqlParameter("@UserId",      userId),
                new SqlParameter("@CommentText", commentText),
                outId);

            return outId.Value != DBNull.Value ? (int)outId.Value : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "AddComment failed: complaint {ComplaintId} user {UserId}",
                complaintId, userId);
            return 0;
        }
    }

    public List<ComplaintComment> GetComments(int complaintId)
        => _context.ComplaintComments
                   .Include(c => c.User)
                       .ThenInclude(u => u.Role)
                   .Where(c => c.ComplaintId == complaintId && !c.IsDeleted)
                   .OrderBy(c => c.CreatedAt)
                   .ToList();

    public bool SoftDeleteComment(int commentId, int actingUserId, bool isAdmin)
    {
        try
        {
            var c = _context.ComplaintComments.FirstOrDefault(x => x.CommentId == commentId);
            if (c == null) return false;
            // Author can delete own; admin can delete any.
            if (!isAdmin && c.UserId != actingUserId) return false;
            c.IsDeleted = true;
            _context.SaveChanges();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SoftDeleteComment failed: comment {CommentId} by user {ActingUserId}",
                commentId, actingUserId);
            return false;
        }
    }

    // ── §6 Appeals ────────────────────────────────────────────────────────────

    public int SubmitAppeal(int complaintId, int citizenUserId, string reason)
    {
        try
        {
            var outId = new SqlParameter
            {
                ParameterName = "@NewAppealId",
                SqlDbType = SqlDbType.Int,
                Direction = ParameterDirection.Output
            };

            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_SubmitComplaintAppeal @ComplaintId, @CitizenUserId, @Reason, @NewAppealId OUTPUT",
                new SqlParameter("@ComplaintId",   complaintId),
                new SqlParameter("@CitizenUserId", citizenUserId),
                new SqlParameter("@Reason",        reason),
                outId);

            return outId.Value != DBNull.Value ? (int)outId.Value : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SubmitAppeal failed: complaint {ComplaintId} citizen {CitizenUserId}",
                complaintId, citizenUserId);
            return 0;
        }
    }

    public bool ResolveAppeal(int appealId, int adminUserId, string decision, string adminNote = null)
    {
        try
        {
            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_ResolveComplaintAppeal @AppealId, @AdminUserId, @Decision, @AdminNote",
                new SqlParameter("@AppealId",    appealId),
                new SqlParameter("@AdminUserId", adminUserId),
                new SqlParameter("@Decision",    decision),
                new SqlParameter("@AdminNote",   (object)adminNote ?? DBNull.Value));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ResolveAppeal failed: appeal {AppealId} decision {Decision}",
                appealId, decision);
            return false;
        }
    }

    public List<ComplaintAppeal> GetAppeals(string status = null)
    {
        var q = _context.ComplaintAppeals
                        .Include(a => a.Complaint)
                            .ThenInclude(c => c.Category)
                        .Include(a => a.Complaint)
                            .ThenInclude(c => c.Locality)
                        .Include(a => a.CitizenUser)
                        .Include(a => a.AdminUser)
                        .AsQueryable();
        if (!string.IsNullOrWhiteSpace(status))
            q = q.Where(a => a.Status == status);
        return q.OrderByDescending(a => a.CreatedAt).ToList();
    }

    public List<ComplaintAppeal> GetAppealsByCitizen(int citizenUserId)
        => _context.ComplaintAppeals
                   .Include(a => a.Complaint)
                   .Include(a => a.AdminUser)
                   .Where(a => a.CitizenUserId == citizenUserId)
                   .OrderByDescending(a => a.CreatedAt)
                   .ToList();

    // ── §15 Internal Notes ────────────────────────────────────────────────────

    public int AddInternalNote(int complaintId, int createdByUserId, string noteText)
    {
        try
        {
            var outId = new SqlParameter
            {
                ParameterName = "@NewNoteId",
                SqlDbType = SqlDbType.Int,
                Direction = ParameterDirection.Output
            };

            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_AddInternalNote @ComplaintId, @CreatedByUserId, @NoteText, @NewNoteId OUTPUT",
                new SqlParameter("@ComplaintId",     complaintId),
                new SqlParameter("@CreatedByUserId", createdByUserId),
                new SqlParameter("@NoteText",        noteText),
                outId);

            return outId.Value != DBNull.Value ? (int)outId.Value : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "AddInternalNote failed: complaint {ComplaintId} user {UserId}",
                complaintId, createdByUserId);
            return 0;
        }
    }

    public List<ComplaintInternalNote> GetInternalNotes(int complaintId)
        => _context.ComplaintInternalNotes
                   .Include(n => n.Author)
                       .ThenInclude(u => u.Role)
                   .Where(n => n.ComplaintId == complaintId)
                   .OrderByDescending(n => n.CreatedAt)
                   .ToList();

    // ── §11/§16 Bulk update ───────────────────────────────────────────────────

    public int BulkUpdateStatus(IEnumerable<int> complaintIds, string newStatus,
                                int actorUserId, string remark = null)
    {
        try
        {
            var ids = complaintIds?.Where(i => i > 0).Distinct().ToList()
                                  ?? new List<int>();
            if (!ids.Any()) return 0;
            var csv = string.Join(",", ids);

            var outCount = new SqlParameter
            {
                ParameterName = "@UpdatedCount",
                SqlDbType = SqlDbType.Int,
                Direction = ParameterDirection.Output
            };

            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_BulkUpdateComplaintStatus @ComplaintIdsCsv, @NewStatus, @ActorUserId, @Remark, @UpdatedCount OUTPUT",
                new SqlParameter("@ComplaintIdsCsv", csv),
                new SqlParameter("@NewStatus",       newStatus),
                new SqlParameter("@ActorUserId",     actorUserId),
                new SqlParameter("@Remark",          (object)remark ?? DBNull.Value),
                outCount);

            return outCount.Value != DBNull.Value ? (int)outCount.Value : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "BulkUpdateStatus failed: newStatus {NewStatus} actor {ActorUserId}",
                newStatus, actorUserId);
            return 0;
        }
    }

    // ── §12 Reassign ──────────────────────────────────────────────────────────

    public bool ReassignDept(int complaintId, int newDeptId, int adminUserId, string reason)
    {
        try
        {
            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_ReassignComplaintDept @ComplaintId, @NewDeptId, @AdminUserId, @Reason",
                new SqlParameter("@ComplaintId", complaintId),
                new SqlParameter("@NewDeptId",   newDeptId),
                new SqlParameter("@AdminUserId", adminUserId),
                new SqlParameter("@Reason",      (object)reason ?? DBNull.Value));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ReassignDept failed: complaint {ComplaintId} -> dept {NewDeptId}",
                complaintId, newDeptId);
            return false;
        }
    }

    // ── §9 Trend ──────────────────────────────────────────────────────────────

    public List<ComplaintTrendRow> GetTrend(int days = 30)
    {
        try
        {
            return _context.Set<ComplaintTrendRow>()
                           .FromSqlRaw("EXEC dbo.usp_GetComplaintTrend @Days = {0}", days)
                           .AsNoTracking()
                           .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetTrend failed: days {Days}", days);
            return new List<ComplaintTrendRow>();
        }
    }

    // ── §20 Activity Feed ─────────────────────────────────────────────────────

    public List<ActivityFeedRow> GetActivityFeed(int userId, int pageSize = 20, int pageNum = 1)
    {
        try
        {
            return _context.Set<ActivityFeedRow>()
                           .FromSqlRaw(
                               "EXEC dbo.usp_GetActivityFeed @UserId = {0}, @PageSize = {1}, @PageNum = {2}",
                               userId, pageSize, pageNum)
                           .AsNoTracking()
                           .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetActivityFeed failed: user {UserId}", userId);
            return new List<ActivityFeedRow>();
        }
    }

    // ── §17 Public feed ───────────────────────────────────────────────────────

    public List<PublicFeedRow> GetPublicFeed(int? localityId = null, short? categoryId = null,
                                              string status = null, string keyword = null,
                                              int pageNum = 1, int pageSize = 20)
    {
        try
        {
            return _context.Set<PublicFeedRow>()
                           .FromSqlRaw(
                               "EXEC dbo.usp_GetPublicFeed @LocalityId = {0}, @CategoryId = {1}, @Status = {2}, @Keyword = {3}, @PageNum = {4}, @PageSize = {5}",
                               (object)localityId ?? DBNull.Value,
                               (object)categoryId ?? DBNull.Value,
                               (object)status     ?? DBNull.Value,
                               (object)keyword    ?? DBNull.Value,
                               pageNum, pageSize)
                           .AsNoTracking()
                           .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetPublicFeed failed");
            return new List<PublicFeedRow>();
        }
    }

    // ── §5 Nearby ─────────────────────────────────────────────────────────────

    public List<NearbyComplaintRow> GetNearbyComplaints(decimal latitude, decimal longitude,
                                                         decimal radiusKm = 2.0m, int pageSize = 50)
    {
        try
        {
            return _context.Set<NearbyComplaintRow>()
                           .FromSqlRaw(
                               "EXEC dbo.usp_GetNearbyComplaints @Lat = {0}, @Lng = {1}, @RadiusKm = {2}, @PageSize = {3}",
                               latitude, longitude, radiusKm, pageSize)
                           .AsNoTracking()
                           .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GetNearbyComplaints failed: lat {Lat} lng {Lng}", latitude, longitude);
            return new List<NearbyComplaintRow>();
        }
    }
}
