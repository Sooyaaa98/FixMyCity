using System.ComponentModel.DataAnnotations;

namespace FixMyCity.API.Models
{
    // ═══════════════════════════════════════════════════════════════════════════
    // Phase 8 — request DTOs for the new feature wave.
    // Validation annotations follow the conventions used by the existing
    // ComplaintRequests / AdminRequests files.
    // ═══════════════════════════════════════════════════════════════════════════

    // ── §1 Upvote ────────────────────────────────────────────────────────────
    public class ToggleUpvoteRequest
    {
        [Required, Range(1, int.MaxValue)] public int ComplaintId   { get; set; }
        [Required, Range(1, int.MaxValue)] public int CitizenUserId { get; set; }
    }

    // ── §7 Comments ──────────────────────────────────────────────────────────
    public class AddCommentRequest
    {
        [Required, Range(1, int.MaxValue)] public int ComplaintId { get; set; }
        [Required, Range(1, int.MaxValue)] public int UserId      { get; set; }

        [Required(AllowEmptyStrings = false)]
        [StringLength(1500, MinimumLength = 2)]
        public string CommentText { get; set; }
    }

    public class DeleteCommentRequest
    {
        [Required, Range(1, int.MaxValue)] public int CommentId     { get; set; }
        [Required, Range(1, int.MaxValue)] public int ActingUserId  { get; set; }
    }

    // ── §6 Appeals ───────────────────────────────────────────────────────────
    public class SubmitAppealRequest
    {
        [Required, Range(1, int.MaxValue)] public int ComplaintId   { get; set; }
        [Required, Range(1, int.MaxValue)] public int CitizenUserId { get; set; }

        [Required(AllowEmptyStrings = false)]
        [StringLength(1000, MinimumLength = 10)]
        public string Reason { get; set; }
    }

    public class ResolveAppealRequest
    {
        [Required, Range(1, int.MaxValue)] public int AppealId    { get; set; }
        [Required, Range(1, int.MaxValue)] public int AdminUserId { get; set; }

        [Required]
        [RegularExpression("^(Approved|Rejected)$",
            ErrorMessage = "Decision must be Approved or Rejected.")]
        public string Decision { get; set; }

        [StringLength(500)] public string AdminNote { get; set; }
    }

    // ── §15 Internal notes ───────────────────────────────────────────────────
    public class AddInternalNoteRequest
    {
        [Required, Range(1, int.MaxValue)] public int ComplaintId     { get; set; }
        [Required, Range(1, int.MaxValue)] public int CreatedByUserId { get; set; }

        [Required(AllowEmptyStrings = false)]
        [StringLength(1500, MinimumLength = 2)]
        public string NoteText { get; set; }
    }

    // ── §11/§16 Bulk update ──────────────────────────────────────────────────
    public class BulkUpdateStatusRequest
    {
        [Required, MinLength(1, ErrorMessage = "At least one ComplaintId is required.")]
        public List<int> ComplaintIds { get; set; }

        [Required]
        [RegularExpression("^(Submitted|In Progress|Resolved|Rejected|Re-opened|Escalated|Linked)$",
            ErrorMessage = "Invalid target status.")]
        public string NewStatus { get; set; }

        [Required, Range(1, int.MaxValue)] public int ActorUserId { get; set; }
        [StringLength(500)] public string Remark { get; set; }
    }

    // ── §12 Reassign ─────────────────────────────────────────────────────────
    public class ReassignDeptRequest
    {
        [Required, Range(1, int.MaxValue)] public int ComplaintId { get; set; }
        [Required, Range(1, int.MaxValue)] public int NewDeptId   { get; set; }
        [Required, Range(1, int.MaxValue)] public int AdminUserId { get; set; }

        [Required(AllowEmptyStrings = false)]
        [StringLength(500, MinimumLength = 5)]
        public string Reason { get; set; }
    }

    // ── §9 Trend ─────────────────────────────────────────────────────────────
    // No body — uses ?days=N query parameter.

    // ── §20 Activity Feed ────────────────────────────────────────────────────
    // No body — uses ?userId=N&pageSize=N&pageNum=N query parameters.

    // ── §17 Public feed ──────────────────────────────────────────────────────
    // Anonymous — query-string filters only. Documented in PublicController.

    // ── §5 Nearby ────────────────────────────────────────────────────────────
    // Query parameters: ?lat=...&lng=...&radiusKm=...&pageSize=...
}
