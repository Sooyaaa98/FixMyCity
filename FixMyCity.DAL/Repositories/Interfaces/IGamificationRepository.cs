using FixMyCity.DAL.Models;
using FixMyCity.DAL.DTOs;

namespace FixMyCity.DAL.Repositories.Interfaces;

/// <summary>
/// Data-access contract for gamification: points, milestones, certificates, scoreboard,
/// notifications, notification preferences, and weekly digest.
/// Sprint 2: Milestone definitions, points ledger, scoreboard snapshot (MERGE-based),
/// weekly digest, notification prefs, and archive added.
/// </summary>
public interface IGamificationRepository
{
    // ── Notifications ─────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all unread, non-archived notifications for a user, newest first. US23.
    /// </summary>
    List<Notification> GetUnreadNotifications(int userId);

    /// <summary>
    /// Returns all non-archived notifications for a user, newest first. US23.
    /// </summary>
    List<Notification> GetAllNotifications(int userId);

    /// <summary>
    /// Marks a single notification as read by its PK. Direct EF update.
    /// Idempotent — already-read returns true. US23.
    /// </summary>
    bool MarkOneRead(int notificationId);

    /// <summary>
    /// Marks all unread notifications for a user as read.
    /// Calls usp_MarkNotificationsRead. Returns true on success.
    /// </summary>
    bool MarkAllRead(int userId);

    /// <summary>
    /// Archives a single notification or all read notifications for a user.
    /// Pass null notificationId to archive all read. Calls usp_ArchiveNotification.
    /// Returns true on success.
    /// </summary>
    bool ArchiveNotification(int userId, int? notificationId = null);

    /// <summary>
    /// Sends a notification with type and channel.
    /// Calls usp_SendNotification. Returns the new NotificationId; 0 on failure.
    /// </summary>
    int SendNotification(int userId, int? complaintId, string message,
                         string notificationType = null, string channel = "InApp");

    // ── Notification Preferences ──────────────────────────────────────────────

    /// <summary>
    /// Returns the notification preferences for a user. Null if not found. US65.
    /// </summary>
    NotificationPreference GetNotificationPreferences(int userId);

    /// <summary>
    /// Updates notification preference flags and digest frequency.
    /// Calls usp_UpdateNotificationPreferences. Returns true on success. US65.
    /// </summary>
    bool UpdateNotificationPreferences(int userId, bool inAppEnabled,
                                       bool pushEnabled, bool emailDigestEnabled,
                                       byte digestFrequencyDays = 7);

    // ── Weekly Digest ─────────────────────────────────────────────────────────

    /// <summary>
    /// Generates the weekly locality digest for all eligible users (F11: set-based).
    /// Calls usp_GenerateWeeklyDigest. Intended for Azure Function Timer Trigger.
    /// Returns true on success. US65.
    /// </summary>
    bool GenerateWeeklyDigest();

    // ── Points ────────────────────────────────────────────────────────────────

    /// <summary>Returns the UserPoints record for a user. Null if none yet.</summary>
    UserPoint GetUserPoints(int userId);

    /// <summary>
    /// Awards points and auto-issues milestone certificates for newly crossed thresholds.
    /// F22: typed RefComplaintId / RefMilestoneId replace the old generic ReferenceId.
    /// Calls usp_AwardPoints. Returns the updated total; 0 on failure. US25, US27.
    /// </summary>
    int AwardPoints(int userId, int points, string reason = "ManualAward",
                    int? refComplaintId = null, int? refMilestoneId = null);

    /// <summary>
    /// Returns the points ledger for a user, newest first. Includes Complaint and Milestone.
    /// Used for points history display. US25.
    /// </summary>
    List<PointsLedger> GetPointsLedger(int userId);

    // ── Milestones ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns all active milestone definitions ordered by threshold ascending.
    /// Used to render progress bars and next-milestone targets. US25, US27.
    /// </summary>
    List<MilestoneDefinition> GetActiveMilestones();

    // ── Certificates ──────────────────────────────────────────────────────────

    /// <summary>
    /// Issues a milestone certificate. SP guards against duplicates (P7 fix).
    /// Calls usp_IssueCertificate.
    /// Returns CertificateIssuedResult; NewCertId=-1 if already issued for this MilestoneId.
    /// US27.
    /// </summary>
    CertificateIssuedResult IssueCertificate(int userId, string milestone,
                                              string filePath = null,
                                              int? milestoneId = null);

    /// <summary>Returns all certificates for a user, newest first. Includes Milestone. US27.</summary>
    List<Certificate> GetCertificatesByUser(int userId);

    // ── Scoreboard ────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the scoreboard from ScoreboardSnapshot, optionally filtered by localityId.
    /// Ordered by rank ascending. US25.
    /// </summary>
    List<ScoreboardEntry> GetScoreboard(int? localityId = null);

    /// <summary>
    /// Rebuilds ScoreboardSnapshot via MERGE (F12 — no TRUNCATE race window).
    /// Calls usp_RefreshScoreboard. Returns true on success.
    /// Called after points are awarded or by daily Azure Function. US25.
    /// </summary>
    bool RefreshScoreboard();
}
