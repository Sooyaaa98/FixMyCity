using FixMyCity.DAL.DTOs;
using FixMyCity.DAL.Models;

namespace FixMyCity.DAL.Repositories.Interfaces;

/// <summary>
/// Data-access contract for the Phase-8 feature wave (see 06_FeatureSuggestions.sql).
///
/// Groups together the new social, transparency and workflow features rather
/// than bloating the existing ComplaintRepository / AdminRepository — they all
/// share the same execution pattern (raw SP + read projection) but cover
/// concerns (up-vote, comments, appeals, internal notes, bulk ops, public
/// portal, activity feed) that warrant their own owner.
/// </summary>
public interface IFeatureRepository
{
    // ── §1 — Upvotes ──────────────────────────────────────────────────────────

    /// <summary>
    /// Toggles an up-vote: insert if absent, delete if present. Returns
    /// (NewCount, HasUpvoted). Calls usp_ToggleComplaintUpvote.
    /// </summary>
    UpvoteResult ToggleUpvote(int complaintId, int citizenUserId);

    /// <summary>Returns the up-vote count for a complaint.</summary>
    int GetUpvoteCount(int complaintId);

    /// <summary>True if this citizen has already up-voted the complaint.</summary>
    bool HasUpvoted(int complaintId, int citizenUserId);

    // ── §7 — Comments ─────────────────────────────────────────────────────────

    /// <summary>Adds a new comment. Calls usp_AddComplaintComment.</summary>
    int AddComment(int complaintId, int userId, string commentText);

    /// <summary>
    /// Returns all non-deleted comments for a complaint, oldest first so the
    /// conversation reads top-down. Includes the author for badge rendering.
    /// </summary>
    List<ComplaintComment> GetComments(int complaintId);

    /// <summary>Soft-deletes a comment (sets IsDeleted = 1). Admin / author only.</summary>
    bool SoftDeleteComment(int commentId, int actingUserId, bool isAdmin);

    // ── §6 — Appeals ──────────────────────────────────────────────────────────

    /// <summary>
    /// File an appeal on a Rejected complaint. The SP validates the citizen
    /// owns the complaint and the complaint is currently Rejected.
    /// Returns the new AppealId, or 0 on failure.
    /// </summary>
    int SubmitAppeal(int complaintId, int citizenUserId, string reason);

    /// <summary>
    /// Resolve an appeal (Approved | Rejected). On approval the SP also
    /// flips the underlying complaint back to 'Submitted'.
    /// </summary>
    bool ResolveAppeal(int appealId, int adminUserId, string decision, string adminNote = null);

    /// <summary>All appeals, optionally filtered by status. SuperAdmin only.</summary>
    List<ComplaintAppeal> GetAppeals(string status = null);

    /// <summary>Appeals filed by a single citizen.</summary>
    List<ComplaintAppeal> GetAppealsByCitizen(int citizenUserId);

    // ── §15 — Internal Notes ──────────────────────────────────────────────────

    /// <summary>Adds an internal note. Calls usp_AddInternalNote.</summary>
    int AddInternalNote(int complaintId, int createdByUserId, string noteText);

    /// <summary>All internal notes for a complaint, newest first.</summary>
    List<ComplaintInternalNote> GetInternalNotes(int complaintId);

    // ── §11 / §16 — Bulk operations ───────────────────────────────────────────

    /// <summary>
    /// Bulk status update. Only complaints whose current status has a valid
    /// transition into <paramref name="newStatus"/> (per ComplaintStatusTransitions)
    /// are updated. Returns the number of rows successfully changed.
    /// </summary>
    int BulkUpdateStatus(IEnumerable<int> complaintIds, string newStatus,
                          int actorUserId, string remark = null);

    // ── §12 — Manual dept reassignment ────────────────────────────────────────

    /// <summary>
    /// Manually reassign a complaint to a different department, overriding
    /// the AI routing. Writes EscalationLog + AuditLog rows.
    /// </summary>
    bool ReassignDept(int complaintId, int newDeptId, int adminUserId, string reason);

    // ── §9 — Trend ────────────────────────────────────────────────────────────

    /// <summary>Day-by-day complaint volume + resolved count for the last <paramref name="days"/>.</summary>
    List<ComplaintTrendRow> GetTrend(int days = 30);

    // ── §20 — Activity Feed ───────────────────────────────────────────────────

    /// <summary>Unified activity feed for a user. Mixes complaint timeline,
    /// points, certificates and comments.</summary>
    List<ActivityFeedRow> GetActivityFeed(int userId, int pageSize = 20, int pageNum = 1);

    // ── §17 — Public transparency portal ──────────────────────────────────────

    /// <summary>Read-only paginated complaint feed for anonymous visitors.</summary>
    List<PublicFeedRow> GetPublicFeed(int? localityId = null, short? categoryId = null,
                                      string status = null, string keyword = null,
                                      int pageNum = 1, int pageSize = 20);

    // ── §5 — Near-me ──────────────────────────────────────────────────────────

    /// <summary>
    /// Active complaints inside <paramref name="radiusKm"/> of (lat,lng),
    /// computed via Haversine inside the SP. Excludes Resolved/Rejected/Linked.
    /// </summary>
    List<NearbyComplaintRow> GetNearbyComplaints(decimal latitude, decimal longitude,
                                                  decimal radiusKm = 2.0m, int pageSize = 50);
}
