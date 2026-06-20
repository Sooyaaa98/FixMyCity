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
/// Implements <see cref="IGamificationRepository"/>.
/// Sprint 2: Milestone definitions, typed points ledger (F22), scoreboard snapshot (F12),
/// weekly digest (F11), notification prefs, archive, and RefreshScoreboard added.
///
/// Phase-2 hardening (2026-05-19): every previously-silent catch now logs via ILogger.
/// </summary>
public class GamificationRepository : IGamificationRepository
{
    private readonly FixMyCityDbContext _context;
    private readonly ILogger<GamificationRepository> _logger;

    public GamificationRepository(FixMyCityDbContext context, ILogger<GamificationRepository> logger)
    {
        _context = context;
        _logger  = logger ?? NullLogger<GamificationRepository>.Instance;
    }

    // Backward-compatible overload for existing `new GamificationRepository(_context)` callers.
    public GamificationRepository(FixMyCityDbContext context)
        : this(context, NullLogger<GamificationRepository>.Instance) { }

    // ── GetUnreadNotifications ────────────────────────────────────────────────

    public List<Notification> GetUnreadNotifications(int userId)
    {
        return _context.Notifications
                       .Where(n => n.UserId == userId
                                && n.IsRead == false
                                && n.IsArchived == false)
                       .OrderByDescending(n => n.CreatedAt)
                       .ToList();
    }

    // ── GetAllNotifications ───────────────────────────────────────────────────

    public List<Notification> GetAllNotifications(int userId)
    {
        return _context.Notifications
                       .Where(n => n.UserId == userId && n.IsArchived == false)
                       .OrderByDescending(n => n.CreatedAt)
                       .ToList();
    }

    // ── MarkOneRead ───────────────────────────────────────────────────────────

    public bool MarkOneRead(int notificationId)
    {
        try
        {
            // AsTracking required: the default QueryTrackingBehavior is NoTracking
            // (set in Program.cs to avoid serialization cycles on list endpoints),
            // and SaveChanges only persists modifications to tracked entities.
            var n = _context.Notifications
                            .AsTracking()
                            .FirstOrDefault(x => x.NotificationId == notificationId);
            if (n != null && !n.IsRead)
            {
                n.IsRead = true;
                n.ReadAt = DateTime.Now;
                _context.SaveChanges();
            }
            return true;   // idempotent — already-read is still success
        }
        catch (Exception ex) { _logger.LogError(ex, "GamificationRepository stored-procedure call failed"); return false; }
    }

    // ── MarkAllRead ───────────────────────────────────────────────────────────

    public bool MarkAllRead(int userId)
    {
        try
        {
            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_MarkNotificationsRead @UserId",
                new SqlParameter("@UserId", userId));
            return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "GamificationRepository stored-procedure call failed"); return false; }
    }

    // ── ArchiveNotification ───────────────────────────────────────────────────

    public bool ArchiveNotification(int userId, int? notificationId = null)
    {
        try
        {
            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_ArchiveNotification @UserId, @NotificationId",
                new SqlParameter("@UserId", userId),
                new SqlParameter("@NotificationId", (object)notificationId ?? DBNull.Value));
            return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "GamificationRepository stored-procedure call failed"); return false; }
    }

    // ── SendNotification ──────────────────────────────────────────────────────

    public int SendNotification(int userId, int? complaintId, string message,
                                string notificationType = null, string channel = "InApp")
    {
        try
        {
            var outId = new SqlParameter
            {
                ParameterName = "@NewNotificationId",
                SqlDbType = SqlDbType.Int,
                Direction = ParameterDirection.Output
            };

            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_SendNotification @UserId, @Message, @NewNotificationId OUTPUT, @ComplaintId, @NotificationType, @Channel",
                new SqlParameter("@UserId", userId),
                new SqlParameter("@Message", message),
                outId,
                new SqlParameter("@ComplaintId", (object)complaintId ?? DBNull.Value),
                new SqlParameter("@NotificationType", (object)notificationType ?? DBNull.Value),
                new SqlParameter("@Channel", channel));

            return outId.Value != DBNull.Value ? (int)outId.Value : 0;
        }
        catch (Exception ex) { _logger.LogError(ex, "GamificationRepository stored-procedure call failed"); return 0; }
    }

    // ── GetNotificationPreferences ────────────────────────────────────────────

    public NotificationPreference GetNotificationPreferences(int userId)
    {
        return _context.NotificationPreferences
                       .FirstOrDefault(p => p.UserId == userId);
    }

    // ── UpdateNotificationPreferences ─────────────────────────────────────────

    public bool UpdateNotificationPreferences(int userId, bool inAppEnabled,
                                              bool pushEnabled, bool emailDigestEnabled,
                                              byte digestFrequencyDays = 7)
    {
        try
        {
            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_UpdateNotificationPreferences @UserId, @InAppEnabled, @PushEnabled, @EmailDigestEnabled, @DigestFrequencyDays",
                new SqlParameter("@UserId", userId),
                new SqlParameter("@InAppEnabled", inAppEnabled),
                new SqlParameter("@PushEnabled", pushEnabled),
                new SqlParameter("@EmailDigestEnabled", emailDigestEnabled),
                new SqlParameter("@DigestFrequencyDays", digestFrequencyDays));
            return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "GamificationRepository stored-procedure call failed"); return false; }
    }

    // ── GenerateWeeklyDigest ──────────────────────────────────────────────────

    public bool GenerateWeeklyDigest()
    {
        try
        {
            // F11: SP is set-based (no cursor). One INSERT for all eligible users.
            _context.Database.ExecuteSqlRaw("EXEC dbo.usp_GenerateWeeklyDigest");
            return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "GamificationRepository stored-procedure call failed"); return false; }
    }

    // ── GetUserPoints ─────────────────────────────────────────────────────────

    public UserPoint GetUserPoints(int userId)
    {
        return _context.UserPoints
                       .Include(up => up.User)
                       .FirstOrDefault(up => up.UserId == userId);
    }

    // ── AwardPoints ───────────────────────────────────────────────────────────

    public int AwardPoints(int userId, int points, string reason = "ManualAward",
                           int? refComplaintId = null, int? refMilestoneId = null)
    {
        try
        {
            var outUpdated = new SqlParameter
            {
                ParameterName = "@UpdatedPoints",
                SqlDbType = SqlDbType.Int,
                Direction = ParameterDirection.Output
            };

            // F22: Typed FKs — @RefComplaintId and @RefMilestoneId replace generic @ReferenceId.
            // SP auto-issues milestone certificates for newly crossed thresholds.
            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_AwardPoints @UserId, @Points, @UpdatedPoints OUTPUT, @Reason, @RefComplaintId, @RefMilestoneId",
                new SqlParameter("@UserId", userId),
                new SqlParameter("@Points", points),
                outUpdated,
                new SqlParameter("@Reason", reason),
                new SqlParameter("@RefComplaintId", (object)refComplaintId ?? DBNull.Value),
                new SqlParameter("@RefMilestoneId", (object)refMilestoneId ?? DBNull.Value));

            return outUpdated.Value != DBNull.Value ? (int)outUpdated.Value : 0;
        }
        catch (Exception ex) { _logger.LogError(ex, "GamificationRepository stored-procedure call failed"); return 0; }
    }

    // ── GetPointsLedger ───────────────────────────────────────────────────────

    public List<PointsLedger> GetPointsLedger(int userId)
    {
        return _context.PointsLedgers
                       .Include(p => p.Complaint)
                       .Include(p => p.Milestone)
                       .Where(p => p.UserId == userId)
                       .OrderByDescending(p => p.EarnedAt)
                       .ToList();
    }

    // ── GetActiveMilestones ───────────────────────────────────────────────────

    public List<MilestoneDefinition> GetActiveMilestones()
    {
        return _context.MilestoneDefinitions
                       .Where(m => m.IsActive)
                       .OrderBy(m => m.PointsThreshold)
                       .ToList();
    }

    // ── IssueCertificate ──────────────────────────────────────────────────────

    public CertificateIssuedResult IssueCertificate(int userId, string milestone,
                                                     string filePath = null,
                                                     int? milestoneId = null)
    {
        var result = new CertificateIssuedResult();
        try
        {
            var outCertId = new SqlParameter
            {
                ParameterName = "@NewCertId",
                SqlDbType = SqlDbType.Int,
                Direction = ParameterDirection.Output
            };
            var outCode = new SqlParameter
            {
                ParameterName = "@VerificationCode",
                SqlDbType = SqlDbType.VarChar,
                Size = 50,
                Direction = ParameterDirection.Output
            };

            // P7 fix: SP guards against duplicate milestone certificates.
            // Returns NewCertId = -1 if already issued for this MilestoneId.
            _context.Database.ExecuteSqlRaw(
                "EXEC dbo.usp_IssueCertificate @UserId, @Milestone, @FilePath, @NewCertId OUTPUT, @VerificationCode OUTPUT, @MilestoneId",
                new SqlParameter("@UserId", userId),
                new SqlParameter("@Milestone", milestone),
                new SqlParameter("@FilePath", (object)filePath ?? DBNull.Value),
                outCertId,
                outCode,
                new SqlParameter("@MilestoneId", (object)milestoneId ?? DBNull.Value));

            result.NewCertId = outCertId.Value != DBNull.Value ? (int)outCertId.Value : -1;
            result.VerificationCode = outCode.Value != DBNull.Value ? (string)outCode.Value : null;
        }
        catch (Exception ex) { _logger.LogError(ex, "IssueCertificate SP failed"); result.NewCertId = 0; }
        return result;
    }

    // ── GetCertificatesByUser ─────────────────────────────────────────────────

    public List<Certificate> GetCertificatesByUser(int userId)
    {
        return _context.Certificates
                       .Include(c => c.Milestone_Nav)
                       .Where(c => c.UserId == userId)
                       .OrderByDescending(c => c.IssuedAt)
                       .ToList();
    }

    // ── GetScoreboard ─────────────────────────────────────────────────────────

    public List<ScoreboardEntry> GetScoreboard(int? localityId = null)
    {
        // Sprint 2: read from ScoreboardSnapshot table (MERGE-based F12).
        // No JSON function call — direct LINQ projection.
        var query = _context.ScoreboardSnapshots
                            .Include(s => s.Locality)
                            .AsQueryable();

        if (localityId.HasValue && localityId > 0)
            query = query.Where(s => s.LocalityId == localityId.Value);

        return query.OrderBy(s => s.Rank)
                    .Select(s => new ScoreboardEntry
                    {
                        UserId = s.UserId,
                        FullName = s.FullName,
                        LocalityName = s.Locality != null ? s.Locality.LocalityName : string.Empty,
                        Points = s.Points,
                        Rank = s.Rank,
                        SnapshotAt = s.SnapshotAt
                    })
                    .ToList();
    }

    // ── RefreshScoreboard ─────────────────────────────────────────────────────

    public bool RefreshScoreboard()
    {
        try
        {
            // F12: MERGE replaces TRUNCATE + INSERT — no empty-table race window.
            _context.Database.ExecuteSqlRaw("EXEC dbo.usp_RefreshScoreboard");
            return true;
        }
        catch (Exception ex) { _logger.LogError(ex, "GamificationRepository stored-procedure call failed"); return false; }
    }
}
