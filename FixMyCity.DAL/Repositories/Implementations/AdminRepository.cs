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
/// Implements <see cref="IAdminRepository"/>.
/// Sprint 2: Ban support (F7), typed escalation (F2/F13), PWG report review (US63),
/// audit log, daily stats snapshot, and multi-result-set GetPlatformStats added.
///
/// Phase-2 hardening (2026-05-19): every previously-silent catch now logs via ILogger.
/// </summary>
public class AdminRepository : IAdminRepository
{
    private readonly FixMyCityDbContext _context;
    private readonly ILogger<AdminRepository> _logger;

    public AdminRepository(FixMyCityDbContext context, ILogger<AdminRepository> logger)
    {
        _context = context;
        _logger  = logger ?? NullLogger<AdminRepository>.Instance;
    }

    // Backward-compatible overload for existing `new AdminRepository(_context)` callers.
    public AdminRepository(FixMyCityDbContext context)
        : this(context, NullLogger<AdminRepository>.Instance) { }

    // ── GetDepartmentsByStatus ────────────────────────────────────────────────

    public List<Department> GetDepartmentsByStatus(string approvalStatus)
    {
        return _context.Departments
                       .Include(d => d.User)
                       .Include(d => d.Category)
                       .Include(d => d.Locality)
                       .Where(d => d.ApprovalStatus == approvalStatus)
                       .OrderBy(d => d.CreatedAt)
                       .ToList();
    }

    // ── GetOrganisationsByStatus ──────────────────────────────────────────────

    public List<Organisation> GetOrganisationsByStatus(string approvalStatus)
    {
        return _context.Organisations
                       .Include(o => o.User)
                       .Where(o => o.ApprovalStatus == approvalStatus)
                       .OrderBy(o => o.CreatedAt)
                       .ToList();
    }

    // ── DecideRegistration ────────────────────────────────────────────────────

    public bool DecideRegistration(int userId, string decision, int adminUserId)
    {
        try
        {
            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_DecideRegistration @UserId, @Decision, @AdminUserId",
                new SqlParameter("@UserId", userId),
                new SqlParameter("@Decision", decision),
                new SqlParameter("@AdminUserId", adminUserId));
            return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "AdminRepository stored-procedure call failed"); return false; }
    }

    // ── GetUsersByRole ────────────────────────────────────────────────────────

    public List<User> GetUsersByRole(string roleName)
    {
        return _context.Users
                       .Include(u => u.Role)
                       .Include(u => u.Locality)
                       .Where(u => u.Role.RoleName == roleName)
                       .OrderBy(u => u.UserId)
                       .ToList();
    }

    // ── DeactivateUser ────────────────────────────────────────────────────────

    public bool DeactivateUser(int targetUserId, string reason, int adminUserId)
    {
        try
        {
            // F7: IsBan=0 — deactivation only; IsBanned remains false.
            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_DeactivateUser @TargetUserId, @Reason, @AdminUserId, @IsBan",
                new SqlParameter("@TargetUserId", targetUserId),
                new SqlParameter("@Reason", reason),
                new SqlParameter("@AdminUserId", adminUserId),
                new SqlParameter("@IsBan", false));
            return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "AdminRepository stored-procedure call failed"); return false; }
    }

    // ── BanUser ───────────────────────────────────────────────────────────────

    public bool BanUser(int targetUserId, string reason, int adminUserId)
    {
        try
        {
            // F7: IsBan=1 — sets IsBanned=1, BanReason, BannedAt; blocks all future logins.
            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_DeactivateUser @TargetUserId, @Reason, @AdminUserId, @IsBan",
                new SqlParameter("@TargetUserId", targetUserId),
                new SqlParameter("@Reason", reason),
                new SqlParameter("@AdminUserId", adminUserId),
                new SqlParameter("@IsBan", true));
            return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "AdminRepository stored-procedure call failed"); return false; }
    }

    // ── GetEscalatedComplaints ────────────────────────────────────────────────

    public List<Complaint> GetEscalatedComplaints()
    {
        return _context.Complaints
                       .Include(c => c.Citizen)
                       .Include(c => c.Department)
                       .Include(c => c.Category)
                       .Include(c => c.Locality)
                       .Where(c => c.Status == "Escalated")
                       .OrderBy(c => c.SubmittedAt)   // oldest (longest-waiting) first
                       .ToList();
    }

    // ── ManualEscalation ──────────────────────────────────────────────────────

    public bool ManualEscalation(int complaintId, int adminUserId,
                                 int? reassignedToDeptId, string reason)
    {
        try
        {
            // F2 guard: SP raises error if DeptId is NULL (complaint not yet routed).
            // F13: ActorUserId = adminUserId for Manual; NULL only for Auto trigger.
            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_FileEscalation @ComplaintId, @EscalationTrigger, @AdminUserId, @ReassignedToDeptId, @Reason",
                new SqlParameter("@ComplaintId", complaintId),
                new SqlParameter("@EscalationTrigger", "Manual"),
                new SqlParameter("@AdminUserId", adminUserId),
                new SqlParameter("@ReassignedToDeptId", (object)reassignedToDeptId ?? DBNull.Value),
                new SqlParameter("@Reason", (object)reason ?? DBNull.Value));
            return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "AdminRepository stored-procedure call failed"); return false; }
    }

    // ── GetEscalationLog ──────────────────────────────────────────────────────

    public List<EscalationLog> GetEscalationLog(int complaintId)
    {
        return _context.EscalationLogs
                       .Include(e => e.Actor)
                       .Include(e => e.OriginalDept)
                       .Include(e => e.ReassignedToDept)
                       .Where(e => e.ComplaintId == complaintId)
                       .OrderByDescending(e => e.EscalatedAt)
                       .ToList();
    }

    // ── GetAllPWGReports ──────────────────────────────────────────────────────

    public List<Pwgreport> GetAllPWGReports(string status = null)
    {
        var query = _context.PwgReports
                            .Include(r => r.ReportedOrg)
                            .Include(r => r.ReportedByUser)
                            .Include(r => r.Complaint)
                                .ThenInclude(c => c.Category)
                            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(r => r.Status == status);

        return query.OrderByDescending(r => r.ReportedAt).ToList();
    }

    // ── ReviewPWGReport ───────────────────────────────────────────────────────

    public bool ReviewPWGReport(int reportId, int adminUserId, string adminAction,
                                string adminNote = null, bool finalClose = false)
    {
        try
        {
            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_ReviewPWGReport @ReportId, @AdminUserId, @AdminAction, @AdminNote, @FinalClose",
                new SqlParameter("@ReportId", reportId),
                new SqlParameter("@AdminUserId", adminUserId),
                new SqlParameter("@AdminAction", adminAction),
                new SqlParameter("@AdminNote", (object)adminNote ?? DBNull.Value),
                new SqlParameter("@FinalClose", finalClose));
            return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "AdminRepository stored-procedure call failed"); return false; }
    }

    // ── ClosePWGReport ────────────────────────────────────────────────────────

    public bool ClosePWGReport(int reportId, int adminUserId, string closeNote = null)
    {
        try
        {
            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_CloseReport @ReportId, @AdminUserId, @CloseNote",
                new SqlParameter("@ReportId", reportId),
                new SqlParameter("@AdminUserId", adminUserId),
                new SqlParameter("@CloseNote", (object)closeNote ?? DBNull.Value));
            return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "AdminRepository stored-procedure call failed"); return false; }
    }

    // ── GetAllComplaints ──────────────────────────────────────────────────────

    public List<Complaint> GetAllComplaints(string status = null)
    {
        var query = _context.Complaints
                            .Include(c => c.Citizen)
                            .Include(c => c.Department)
                            .Include(c => c.Category)
                            .Include(c => c.Locality)
                            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(c => c.Status == status);

        return query.OrderByDescending(c => c.SubmittedAt).ToList();
    }

    // ── GetPlatformStats ──────────────────────────────────────────────────
    // usp_GetPlatformStats emits 3 SELECT result sets.
    // Raw ADO.NET is used because EF Core cannot handle multiple result sets.

    public PlatformStats GetPlatformStats()
    {
        var stats = new PlatformStats();
        try
        {
            var conn = _context.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open)
                conn.Open();

            // ISSUE 10 FIX: Inject SESSION_CONTEXT manually.
            // Raw ADO.NET bypasses the EF Core SessionContextInterceptor.
            using (var sessionCmd = conn.CreateCommand())
            {
                sessionCmd.CommandText = @"
                EXEC sp_set_session_context N'UserRole', N'SuperAdmin', @read_only = 0;
                EXEC sp_set_session_context N'UserId',   NULL,          @read_only = 0;
                EXEC sp_set_session_context N'DeptId',   NULL,          @read_only = 0;";
                sessionCmd.ExecuteNonQuery();
            }

            using var cmd = conn.CreateCommand();
            cmd.CommandText = "EXEC dbo.usp_GetPlatformStats";
            using var reader = cmd.ExecuteReader();

            // Result set 1 — complaint status counts
            if (reader.Read())
            {
                stats.TotalComplaints = reader.GetInt32(reader.GetOrdinal("TotalComplaints"));
                stats.Submitted = reader.GetInt32(reader.GetOrdinal("Submitted"));
                stats.InProgress = reader.GetInt32(reader.GetOrdinal("InProgress"));
                stats.Resolved = reader.GetInt32(reader.GetOrdinal("Resolved"));
                stats.Rejected = reader.GetInt32(reader.GetOrdinal("Rejected"));
                stats.Reopened = reader.GetInt32(reader.GetOrdinal("Reopened"));
                stats.Escalated = reader.GetInt32(reader.GetOrdinal("Escalated"));
                stats.Linked = reader.GetInt32(reader.GetOrdinal("Linked"));
            }

            // Result set 2 — active user count
            if (reader.NextResult() && reader.Read())
                stats.ActiveUsers = reader.GetInt32(reader.GetOrdinal("ActiveUsers"));

            // Result set 3 — role totals
            if (reader.NextResult() && reader.Read())
            {
                stats.TotalCitizens = reader.GetInt32(reader.GetOrdinal("TotalCitizens"));
                stats.TotalSolvers = reader.GetInt32(reader.GetOrdinal("TotalSolvers"));
                stats.TotalPWG = reader.GetInt32(reader.GetOrdinal("TotalPWG"));
                stats.TotalAdmins = reader.GetInt32(reader.GetOrdinal("TotalAdmins"));
            }
        }
        catch (Exception ex) { _logger.LogWarning(ex, "GetPlatformStats partial failure — dashboard returns degraded view"); }
        return stats;
    }
    // ── SnapshotPlatformStats ─────────────────────────────────────────────────

    public bool SnapshotPlatformStats()
    {
        try
        {
            _context.Database.ExecuteSqlRaw("EXEC dbo.usp_SnapshotPlatformStats");
            return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "AdminRepository stored-procedure call failed"); return false; }
    }

    // ── GetAllCategories ──────────────────────────────────────────────────────

    public List<IssueCategory> GetAllCategories()
        => _context.IssueCategories.OrderBy(c => c.CategoryName).ToList();

    // ── GetAllDepartments ─────────────────────────────────────────────────────

    public List<Department> GetAllDepartments()
    {
        return _context.Departments
                       .Include(d => d.Category)
                       .Include(d => d.Locality)
                       .Where(d => d.ApprovalStatus == "Approved")
                       .OrderBy(d => d.DeptName)
                       .ToList();
    }

    // ── GetAuditLog ───────────────────────────────────────────────────────────

    public List<AuditLog> GetAuditLog(string actionType = null, int top = 100)
    {
        var query = _context.AuditLogs
                            .Include(a => a.Actor)
                            .Include(a => a.TargetUser)
                            .Include(a => a.TargetOrg)
                            .Include(a => a.TargetDept)
                            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(actionType))
            query = query.Where(a => a.ActionType == actionType);

        return query.OrderByDescending(a => a.CreatedAt)
                    .Take(top)
                    .ToList();
    }
}

