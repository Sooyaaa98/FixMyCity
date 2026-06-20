using FixMyCity.DAL.Models;
using FixMyCity.DAL.Repositories.Interfaces;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Data;

namespace FixMyCity.DAL.Repositories.Implementations;

/// <summary>
/// Implements <see cref="IComplaintRepository"/>.
/// Sprint 2: LocalityId replaces Locality; attachments replace ImagePath/ResolutionImagePath (F1);
/// map view, duplicate linking, and lat/lng support added.
///
/// Phase-2 hardening (2026-05-19): every catch block now logs via ILogger. Return
/// contracts (0 / false on failure) are preserved so controllers continue to
/// surface "success: false" with the existing user-facing messages.
/// </summary>
public class ComplaintRepository : IComplaintRepository
{
    private readonly FixMyCityDbContext _context;
    private readonly ILogger<ComplaintRepository> _logger;

    public ComplaintRepository(FixMyCityDbContext context, ILogger<ComplaintRepository> logger)
    {
        _context = context;
        _logger  = logger ?? NullLogger<ComplaintRepository>.Instance;
    }

    // Backward-compatible overload — existing `new ComplaintRepository(_context)` callers
    // get a NullLogger. Controllers can migrate to DI-injected ILogger in a future phase.
    public ComplaintRepository(FixMyCityDbContext context)
        : this(context, NullLogger<ComplaintRepository>.Instance) { }

    // ── SubmitComplaint ───────────────────────────────────────────────────────

    public int SubmitComplaint(int citizenUserId, short categoryId, string title,
                               string description, int localityId, string address,
                               string criticality, decimal? latitude = null,
                               decimal? longitude = null, string filePath = null,
                               string fileName = null, int? fileSizeKb = null)
    {
        try
        {
            var outId = new SqlParameter
            {
                ParameterName = "@ComplaintId",
                SqlDbType = SqlDbType.Int,
                Direction = ParameterDirection.Output
            };

            // F1: @FilePath routes to ComplaintAttachments inside the SP.
            // F48: SP auto-routes to dept matching category + locality.
            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_SubmitComplaint @CitizenUserId, @CategoryId, @Title, @Description, @LocalityId, @Address, @Criticality, @ComplaintId OUTPUT, @Latitude, @Longitude, @FilePath, @FileName, @FileSizeKB",
                new SqlParameter("@CitizenUserId", citizenUserId),
                new SqlParameter("@CategoryId", categoryId),
                new SqlParameter("@Title", title),
                new SqlParameter("@Description", description),
                new SqlParameter("@LocalityId", localityId),
                new SqlParameter("@Address", address),
                new SqlParameter("@Criticality", criticality),
                outId,
                new SqlParameter("@Latitude", (object)latitude ?? DBNull.Value),
                new SqlParameter("@Longitude", (object)longitude ?? DBNull.Value),
                new SqlParameter("@FilePath", (object)filePath ?? DBNull.Value),
                new SqlParameter("@FileName", (object)fileName ?? DBNull.Value),
                new SqlParameter("@FileSizeKB", (object)fileSizeKb ?? DBNull.Value));

            return outId.Value != DBNull.Value ? (int)outId.Value : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SubmitComplaint failed for citizen {CitizenUserId} category {CategoryId} locality {LocalityId}",
                citizenUserId, categoryId, localityId);
            return 0;
        }
    }

    // ── GetComplaintById ──────────────────────────────────────────────────────

    public Complaint GetComplaintById(int complaintId)
    {
        return _context.Complaints
                       .Include(c => c.Citizen)
                       .Include(c => c.Department)
                           .ThenInclude(d => d.User)
                       .Include(c => c.Category)
                       .Include(c => c.Locality)
                       .Include(c => c.ComplaintRatings)
                       .Include(c => c.Attachments)
                           .ThenInclude(a => a.UploadedBy)
                       .Include(c => c.MlScore)
                       .FirstOrDefault(c => c.ComplaintId == complaintId);
    }

    // ── GetComplaintsByCitizen ────────────────────────────────────────────────

    public List<Complaint> GetComplaintsByCitizen(int citizenUserId)
    {
        return _context.Complaints
                       .Include(c => c.Category)
                       .Include(c => c.Department)
                       .Include(c => c.Locality)
                       .Where(c => c.CitizenUserId == citizenUserId)
                       .OrderByDescending(c => c.SubmittedAt)
                       .ToList();
    }

    // ── FilterComplaintsByCitizen ─────────────────────────────────────────────

    public List<Complaint> FilterComplaintsByCitizen(int citizenUserId,
                                                      string status, int? localityId)
    {
        var query = _context.Complaints
                            .Include(c => c.Category)
                            .Include(c => c.Department)
                            .Include(c => c.Locality)
                            .Where(c => c.CitizenUserId == citizenUserId);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(c => c.Status == status);

        if (localityId.HasValue && localityId > 0)
            query = query.Where(c => c.LocalityId == localityId.Value);

        return query.OrderByDescending(c => c.SubmittedAt).ToList();
    }

    // ── GetComplaintsByDept ───────────────────────────────────────────────────

    public List<Complaint> GetComplaintsByDept(int deptId, string status,
                                               int? localityId, string criticality)
    {
        var query = _context.Complaints
                            .Include(c => c.Citizen)
                            .Include(c => c.Category)
                            .Include(c => c.Locality)
                            .Include(c => c.MlScore)
                            .Where(c => c.DeptId == deptId);

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(c => c.Status == status);

        if (localityId.HasValue && localityId > 0)
            query = query.Where(c => c.LocalityId == localityId.Value);

        if (!string.IsNullOrWhiteSpace(criticality))
            query = query.Where(c => c.Criticality == criticality);

        // ML priority score first; fall back to newest submitted.
        return query.OrderByDescending(c => c.MlScore != null ? c.MlScore.PriorityScore : 0)
                    .ThenByDescending(c => c.SubmittedAt)
                    .ToList();
    }

    // ── GetLocalityFeed ───────────────────────────────────────────────────────

    public List<Complaint> GetLocalityFeed(int localityId)
    {
        return _context.Complaints
                       .Include(c => c.Category)
                       .Include(c => c.Department)
                       .Include(c => c.Citizen)
                       .Where(c => c.LocalityId == localityId
                                && c.Status != "Resolved"
                                && c.Status != "Rejected"
                                && c.Status != "Linked")
                       .OrderByDescending(c => c.SubmittedAt)
                       .ToList();
    }

    // ── GetMapComplaints ──────────────────────────────────────────────────────

    public List<Complaint> GetMapComplaints(int? localityId)
    {
        var query = _context.Complaints
                            .Include(c => c.Category)
                            .Where(c => c.Latitude != null
                                     && c.Longitude != null);

        if (localityId.HasValue && localityId > 0)
            query = query.Where(c => c.LocalityId == localityId.Value);

        return query.OrderByDescending(c => c.SubmittedAt).ToList();
    }

    // ── SearchComplaints ──────────────────────────────────────────────────────

    public List<Complaint> SearchComplaints(string keyword, short? categoryId, int? localityId)
    {
        // ISSUE 11 FIX: Require at least one filter — prevents full-table scans.
        bool hasKeyword = !string.IsNullOrWhiteSpace(keyword);
        bool hasCategory = categoryId.HasValue;
        bool hasLocality = localityId.HasValue && localityId > 0;

        if (!hasKeyword && !hasCategory && !hasLocality)
            return new List<Complaint>();

        var query = _context.Complaints
                            .Include(c => c.Category)
                            .Include(c => c.Locality)
                            .Include(c => c.Department)
                            .AsQueryable();

        if (hasKeyword)
            query = query.Where(c => c.Title.Contains(keyword)
                                  || c.Description.Contains(keyword));

        if (hasCategory)
            query = query.Where(c => c.CategoryId == categoryId.Value);

        if (hasLocality)
            query = query.Where(c => c.LocalityId == localityId.Value);

        // Hard cap: add pagination parameters to the endpoint when full paging is needed.
        return query.OrderByDescending(c => c.SubmittedAt)
                    .Take(100)
                    .ToList();
    }
    // ── UpdateComplaintStatus ─────────────────────────────────────────────────

    public bool UpdateComplaintStatus(int complaintId, int solverUserId, string newStatus,
                                      string remark, string resolutionFilePath = null,
                                      string resolutionFileName = null,
                                      int? resolutionFileSizeKb = null)
    {
        try
        {
            // F21: Transition guard enforced inside SP.
            // F1: Resolution photo stored in ComplaintAttachments inside SP.
            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_UpdateComplaintStatus @ComplaintId, @SolverUserId, @NewStatus, @Remark, @ResolutionFilePath, @ResolutionFileName, @ResolutionFileSizeKB",
                new SqlParameter("@ComplaintId", complaintId),
                new SqlParameter("@SolverUserId", solverUserId),
                new SqlParameter("@NewStatus", newStatus),
                new SqlParameter("@Remark", (object)remark ?? DBNull.Value),
                new SqlParameter("@ResolutionFilePath", (object)resolutionFilePath ?? DBNull.Value),
                new SqlParameter("@ResolutionFileName", (object)resolutionFileName ?? DBNull.Value),
                new SqlParameter("@ResolutionFileSizeKB", (object)resolutionFileSizeKb ?? DBNull.Value));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "UpdateComplaintStatus failed: complaint {ComplaintId} -> {NewStatus} by solver {SolverUserId}",
                complaintId, newStatus, solverUserId);
            return false;
        }
    }

    // ── SetEstimatedResolutionDate ────────────────────────────────────────────

    public bool SetEstimatedResolutionDate(int complaintId, int solverUserId, DateTime estDate)
    {
        try
        {
            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_SetEstimatedResolutionDate @ComplaintId, @SolverUserId, @EstDate",
                new SqlParameter("@ComplaintId", complaintId),
                new SqlParameter("@SolverUserId", solverUserId),
                new SqlParameter("@EstDate", DateOnly.FromDateTime(estDate)));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "SetEstimatedResolutionDate failed: complaint {ComplaintId} -> {Date}",
                complaintId, estDate);
            return false;
        }
    }

    // ── GetTimeline ───────────────────────────────────────────────────────────

    public List<ComplaintTimeline> GetTimeline(int complaintId)
    {
        // ThenInclude(Actor.Role) so the timeline template can show the actor's
        // role name ("Solver", "Citizen", …) next to their full name. Without
        // it the template renders an empty "By: Name ()" string.
        return _context.ComplaintTimelines
                       .Include(t => t.Actor)
                           .ThenInclude(a => a.Role)
                       .Where(t => t.ComplaintId == complaintId)
                       .OrderByDescending(t => t.CreatedAt)
                       .ToList();
    }

    // ── RateComplaint ─────────────────────────────────────────────────────────

    public bool RateComplaint(int complaintId, int citizenUserId, byte stars, string comment)
    {
        try
        {
            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_RateComplaint @ComplaintId, @CitizenUserId, @Stars, @Comment",
                new SqlParameter("@ComplaintId", complaintId),
                new SqlParameter("@CitizenUserId", citizenUserId),
                new SqlParameter("@Stars", stars),
                new SqlParameter("@Comment", (object)comment ?? DBNull.Value));
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "RateComplaint failed: complaint {ComplaintId} by citizen {CitizenUserId}",
                complaintId, citizenUserId);
            return false;
        }
    }

    // ── GetRating ─────────────────────────────────────────────────────────────

    public ComplaintRating GetRating(int complaintId, int citizenUserId)
    {
        return _context.ComplaintRatings
                       .FirstOrDefault(r => r.ComplaintId == complaintId
                                         && r.CitizenUserId == citizenUserId);
    }

    // ── ReopenComplaint ───────────────────────────────────────────────────────

    public bool ReopenComplaint(int complaintId, int citizenUserId, string reason)
    {
        try
        {
            var outSuccess = new SqlParameter
            {
                ParameterName = "@IsSuccess",
                SqlDbType = SqlDbType.Bit,
                Direction = ParameterDirection.Output
            };

            // F17: SP enforces Stars < 3 guard before reopening.
            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_ReopenComplaint @ComplaintId, @CitizenUserId, @Reason, @IsSuccess OUTPUT",
                new SqlParameter("@ComplaintId", complaintId),
                new SqlParameter("@CitizenUserId", citizenUserId),
                new SqlParameter("@Reason", reason),
                outSuccess);

            return outSuccess.Value != DBNull.Value && (bool)outSuccess.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ReopenComplaint failed: complaint {ComplaintId} by citizen {CitizenUserId}",
                complaintId, citizenUserId);
            return false;
        }
    }

    // ── GetAttachments ────────────────────────────────────────────────────────

    public List<ComplaintAttachment> GetAttachments(int complaintId,
                                                    string attachmentType = null)
    {
        var query = _context.ComplaintAttachments
                            .Include(a => a.UploadedBy)
                            .Where(a => a.ComplaintId == complaintId);

        if (!string.IsNullOrWhiteSpace(attachmentType))
            query = query.Where(a => a.AttachmentType == attachmentType);

        return query.OrderByDescending(a => a.UploadedAt).ToList();
    }

    // ── AddAttachment ─────────────────────────────────────────────────────────

    public int AddAttachment(int complaintId, int uploadedByUserId,
                             string attachmentType, string filePath,
                             string fileName = null, int? fileSizeKb = null,
                             int? timelineId = null)
    {
        try
        {
            var outId = new SqlParameter
            {
                ParameterName = "@NewAttachmentId",
                SqlDbType = SqlDbType.Int,
                Direction = ParameterDirection.Output
            };

            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_AddComplaintAttachment @ComplaintId, @UploadedByUserId, @AttachmentType, @FilePath, @NewAttachmentId OUTPUT, @TimelineId, @FileName, @FileSizeKB",
                new SqlParameter("@ComplaintId", complaintId),
                new SqlParameter("@UploadedByUserId", uploadedByUserId),
                new SqlParameter("@AttachmentType", attachmentType),
                new SqlParameter("@FilePath", filePath),
                outId,
                new SqlParameter("@TimelineId", (object)timelineId ?? DBNull.Value),
                new SqlParameter("@FileName", (object)fileName ?? DBNull.Value),
                new SqlParameter("@FileSizeKB", (object)fileSizeKb ?? DBNull.Value));

            return outId.Value != DBNull.Value ? (int)outId.Value : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "AddAttachment failed: complaint {ComplaintId} type {AttachmentType} path {FilePath}",
                complaintId, attachmentType, filePath);
            return 0;
        }
    }

    // ── LinkDuplicateComplaint ────────────────────────────────────────────────

    public bool LinkDuplicateComplaint(int originalComplaintId, int linkedComplaintId,
                                       int linkedByUserId)
    {
        try
        {
            var outSuccess = new SqlParameter
            {
                ParameterName = "@IsSuccess",
                SqlDbType = SqlDbType.Bit,
                Direction = ParameterDirection.Output
            };

            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_LinkDuplicateComplaint @OriginalComplaintId, @LinkedComplaintId, @LinkedByUserId, @IsSuccess OUTPUT",
                new SqlParameter("@OriginalComplaintId", originalComplaintId),
                new SqlParameter("@LinkedComplaintId", linkedComplaintId),
                new SqlParameter("@LinkedByUserId", linkedByUserId),
                outSuccess);

            return outSuccess.Value != DBNull.Value && (bool)outSuccess.Value;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "LinkDuplicateComplaint failed: original {OriginalId} -> linked {LinkedId} by user {ActorUserId}",
                originalComplaintId, linkedComplaintId, linkedByUserId);
            return false;
        }
    }

    // ── GetCandidateDuplicates ────────────────────────────────────────────────

    public List<Complaint> GetCandidateDuplicates(int localityId, short categoryId,
                                                   int excludeComplaintId = 0)
    {
        return _context.Complaints
                       .Include(c => c.Citizen)
                       .Include(c => c.Category)
                       .Where(c => c.LocalityId == localityId
                                && c.CategoryId == categoryId
                                && c.Status != "Resolved"
                                && c.Status != "Rejected"
                                && c.Status != "Linked"
                                && c.ComplaintId != excludeComplaintId)
                       .OrderByDescending(c => c.SubmittedAt)
                       .Take(5)
                       .ToList();
    }

    // ── GetAllCategories ──────────────────────────────────────────────────────

    public List<IssueCategory> GetAllCategories()
        => _context.IssueCategories.OrderBy(c => c.CategoryName).ToList();
}
