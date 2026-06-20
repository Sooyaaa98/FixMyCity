using System.Text.Json.Serialization;

namespace FixMyCity.DAL.Models;

public partial class User
{
    public User()
    {
        ComplaintsAsAuthor = new HashSet<Complaint>();
        ComplaintTimelines = new HashSet<ComplaintTimeline>();
        ComplaintRatings = new HashSet<ComplaintRating>();
        Contributions = new HashSet<Contribution>();
        Notifications = new HashSet<Notification>();
        PWGParticipationRequests = new HashSet<PwgparticipationRequest>();
        Certificates = new HashSet<Certificate>();
        PointsLedgers = new HashSet<PointsLedger>();
        UserInterests = new HashSet<UserInterest>();
        PasswordResetTokens = new HashSet<PasswordResetToken>();
    }

    public int UserId { get; set; }
    public byte RoleId { get; set; }
    public string FullName { get; set; }
    public string Email { get; set; }

    [JsonIgnore]
    public string PasswordHash { get; set; }

    public string Phone { get; set; }
    public string Address { get; set; }

    // F14 — FK to Localities; NULL allowed for SSO users before profile completion.
    public int? LocalityId { get; set; }

    [JsonIgnore]
    public string AadhaarNo { get; set; }

    // SSO support (US05)
    public string SSOProvider { get; set; }
    public string SSOExternalId { get; set; }

    // Login lockout (US04)
    public byte FailedLoginAttempts { get; set; }
    public DateTime? LockoutUntil { get; set; }

    public bool IsActive { get; set; }
    public bool IsApproved { get; set; }

    // F7 — Ban support
    public bool IsBanned { get; set; }
    public string BanReason { get; set; }
    public DateTime? BannedAt { get; set; }

    // FIX-04 (GAP-06) — Suspension state. Schema column dbo.Users.IsSuspended;
    // fn_ValidateLogin blocks login when IsSuspended = 1. Was missing from the EF
    // entity, meaning a suspended-user lookup returned IsSuspended=false even if
    // the row actually had it set. Added in Phase 2 (2026-05-19).
    public bool IsSuspended { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    // Navigation
    public Role Role { get; set; }
    public Locality Locality { get; set; }
    public Organisation Organisation { get; set; }
    public Department Department { get; set; }
    public UserPoint UserPoint { get; set; }
    public NotificationPreference NotificationPreference { get; set; }

    public ICollection<Complaint> ComplaintsAsAuthor { get; set; }
    public ICollection<ComplaintTimeline> ComplaintTimelines { get; set; }
    public ICollection<ComplaintRating> ComplaintRatings { get; set; }
    public ICollection<Contribution> Contributions { get; set; }
    public ICollection<Notification> Notifications { get; set; }
    public ICollection<PwgparticipationRequest> PWGParticipationRequests { get; set; }
    public ICollection<Certificate> Certificates { get; set; }
    public ICollection<PointsLedger> PointsLedgers { get; set; }
    public ICollection<UserInterest> UserInterests { get; set; }
    public ICollection<PasswordResetToken> PasswordResetTokens { get; set; }
}