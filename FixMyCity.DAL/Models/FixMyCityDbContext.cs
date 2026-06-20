using FixMyCity.DAL.DTOs;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;
namespace FixMyCity.DAL.Models;

public partial class FixMyCityDbContext : DbContext
{
    public FixMyCityDbContext() { }

    public FixMyCityDbContext(DbContextOptions<FixMyCityDbContext> options)
        : base(options) { }

    public virtual DbSet<Role> Roles { get; set; }
    public virtual DbSet<Locality> Localities { get; set; }
    public virtual DbSet<User> Users { get; set; }
    public virtual DbSet<IssueCategory> IssueCategories { get; set; }
    public virtual DbSet<Organisation> Organisations { get; set; }
    public virtual DbSet<Department> Departments { get; set; }
    public virtual DbSet<Complaint> Complaints { get; set; }

    public virtual DbSet<ComplaintTimeline> ComplaintTimelines { get; set; }
    public virtual DbSet<PwgparticipationRequest> PWGParticipationRequests { get; set; }
    public virtual DbSet<ComplaintRating> ComplaintRatings { get; set; }
    public virtual DbSet<Contribution> Contributions { get; set; }
    public virtual DbSet<Notification> Notifications { get; set; }
    public virtual DbSet<UserPoint> UserPoints { get; set; }
    public virtual DbSet<Certificate> Certificates { get; set; }

    public virtual DbSet<PasswordResetToken> PasswordResetTokens { get; set; }
    public virtual DbSet<ComplaintStatusTransition> ComplaintStatusTransitions { get; set; }
    public virtual DbSet<MilestoneDefinition> MilestoneDefinitions { get; set; }
    public virtual DbSet<ComplaintMlscore> ComplaintMlScores { get; set; }
    public virtual DbSet<ComplaintAttachment> ComplaintAttachments { get; set; }
    public virtual DbSet<DuplicateComplaintLink> DuplicateComplaintLinks { get; set; }
    public virtual DbSet<DepartmentCategory> DepartmentCategories { get; set; }
    public virtual DbSet<EscalationLog> EscalationLogs { get; set; }
    public virtual DbSet<Pwgreport> PwgReports { get; set; }
    public virtual DbSet<NotificationPreference> NotificationPreferences { get; set; }
    public virtual DbSet<PointsLedger> PointsLedgers { get; set; }
    public virtual DbSet<AuditLog> AuditLogs { get; set; }
    public virtual DbSet<ScoreboardSnapshot> ScoreboardSnapshots { get; set; }
    public virtual DbSet<UserInterest> UserInterests { get; set; }

    // ── AI / auth tables added by 01_AI_Tables_Addition.sql + 02_UserRefreshTokens.sql ──
    // Read-only via EF — writes still go through the named stored procedures
    // (usp_SaveComplaintEmbedding, usp_SaveAIDecision, usp_SaveComplaintTags,
    // usp_UpsertRecommendationCache) and JwtService raw SQL respectively.
    public virtual DbSet<ComplaintEmbedding>              ComplaintEmbeddings              { get; set; }
    public virtual DbSet<UserRecommendationCache>         UserRecommendationCache          { get; set; }
    public virtual DbSet<AIDecisionLog>                   AIDecisionLogs                   { get; set; }
    public virtual DbSet<ComplaintTag>                    ComplaintTags                    { get; set; }
    public virtual DbSet<AIPendingScoreQueue>             AIPendingScoreQueue              { get; set; }
    public virtual DbSet<PlatformStatsCategorySnapshot>   PlatformStatsCategorySnapshots   { get; set; }
    public virtual DbSet<UserRefreshToken>                UserRefreshTokens                { get; set; }

    // ── Phase 8 feature-suggestion tables (06_FeatureSuggestions.sql) ─────────
    // EF is used for read queries only; writes still go through the new SPs
    // (usp_ToggleComplaintUpvote, usp_AddComplaintComment, usp_SubmitComplaintAppeal,
    //  usp_ResolveComplaintAppeal, usp_AddInternalNote, etc.)
    public virtual DbSet<ComplaintUpvote>        ComplaintUpvotes        { get; set; }
    public virtual DbSet<ComplaintComment>       ComplaintComments       { get; set; }
    public virtual DbSet<ComplaintAppeal>        ComplaintAppeals        { get; set; }
    public virtual DbSet<ComplaintInternalNote>  ComplaintInternalNotes  { get; set; }


    // ── OnModelCreating ───────────────────────────────────────────────────────

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // ── HasNoKey registrations ────────────────────────────────────────────
        modelBuilder.Entity<RecommendedComplaintResult>().HasNoKey();
        // Phase 8 — keyless projections returned by the new stored procedures.
        modelBuilder.Entity<ComplaintTrendRow>().HasNoKey();
        modelBuilder.Entity<ActivityFeedRow>().HasNoKey();
        modelBuilder.Entity<PublicFeedRow>().HasNoKey();
        modelBuilder.Entity<NearbyComplaintRow>().HasNoKey();

        // ── Role ──────────────────────────────────────────────────────────────
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("Roles");
            entity.HasKey(e => e.RoleId);
            entity.Property(e => e.RoleName).HasMaxLength(30).IsRequired();
            entity.HasIndex(e => e.RoleName).IsUnique();
        });

        // ── Locality ──────────────────────────────────────────────────────────
        modelBuilder.Entity<Locality>(entity =>
        {
            entity.ToTable("Localities");
            entity.HasKey(e => e.LocalityId);
            entity.Property(e => e.LocalityName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.City).HasMaxLength(100).IsRequired();
            entity.Property(e => e.State).HasMaxLength(100).HasDefaultValue("Karnataka");
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.HasIndex(e => e.LocalityName).IsUnique();
        });

        // ── User ──────────────────────────────────────────────────────────────
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("Users");
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.FullName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Email).HasMaxLength(150).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(256);
            entity.Property(e => e.Phone).HasMaxLength(15).IsRequired();
            entity.Property(e => e.Address).HasMaxLength(300).IsRequired();
            entity.Property(e => e.AadhaarNo).HasMaxLength(12);
            entity.Property(e => e.SSOProvider).HasMaxLength(30);
            entity.Property(e => e.SSOExternalId).HasMaxLength(200);
            entity.Property(e => e.BanReason).HasMaxLength(300);
            entity.Property(e => e.IsBanned).HasDefaultValue(false);
            entity.Property(e => e.IsSuspended).HasDefaultValue(false);   // FIX-04 — Phase 2 added
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.IsApproved).HasDefaultValue(false);
            entity.Property(e => e.FailedLoginAttempts).HasDefaultValue((byte)0);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2(7)");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime2(7)");
            entity.Property(e => e.LockoutUntil).HasColumnType("datetime2(7)");
            entity.Property(e => e.BannedAt).HasColumnType("datetime2(7)");
            entity.HasIndex(e => e.Email).IsUnique();
            entity.HasOne(e => e.Role)
                  .WithMany(r => r.Users)
                  .HasForeignKey(e => e.RoleId);
            entity.HasOne(e => e.Locality)
                  .WithMany(l => l.Users)
                  .HasForeignKey(e => e.LocalityId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ── IssueCategory ─────────────────────────────────────────────────────
        modelBuilder.Entity<IssueCategory>(entity =>
        {
            entity.ToTable("IssueCategories");
            entity.HasKey(e => e.CategoryId);
            entity.Property(e => e.CategoryName).HasMaxLength(80).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(300);
            entity.HasIndex(e => e.CategoryName).IsUnique();
        });

        // ── Organisation ──────────────────────────────────────────────────────
        modelBuilder.Entity<Organisation>(entity =>
        {
            entity.ToTable("Organisations");
            entity.HasKey(e => e.OrgId);
            entity.Property(e => e.OrgName).HasMaxLength(150).IsRequired();
            entity.Property(e => e.OrgType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.RegistrationNo).HasMaxLength(50).IsRequired();
            entity.Property(e => e.ContactEmail).HasMaxLength(150).IsRequired();
            entity.Property(e => e.ContactPhone).HasMaxLength(15).IsRequired();
            entity.Property(e => e.Address).HasMaxLength(300).IsRequired();
            entity.Property(e => e.ApprovalStatus).HasMaxLength(20).HasDefaultValue("Pending");
            entity.Property(e => e.ApprovedAt).HasColumnType("datetime2(7)");
            entity.Property(e => e.SuspendedAt).HasColumnType("datetime2(7)");   // FIX-04 — Phase 2 added
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2(7)");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime2(7)");
            entity.HasIndex(e => e.RegistrationNo).IsUnique();
            entity.HasOne(e => e.User)
                  .WithOne(u => u.Organisation)
                  .HasForeignKey<Organisation>(e => e.UserId);
        });

        // ── Department ────────────────────────────────────────────────────────
        modelBuilder.Entity<Department>(entity =>
        {
            entity.ToTable("Departments");
            entity.HasKey(e => e.DeptId);
            entity.Property(e => e.DeptName).HasMaxLength(150).IsRequired();
            entity.Property(e => e.Ministry).HasMaxLength(100).IsRequired();
            entity.Property(e => e.ContactEmail).HasMaxLength(150).IsRequired();
            entity.Property(e => e.ContactPhone).HasMaxLength(15).IsRequired();
            entity.Property(e => e.Address).HasMaxLength(300).IsRequired();
            entity.Property(e => e.ApprovalStatus).HasMaxLength(20).HasDefaultValue("Pending");
            entity.Property(e => e.ApprovedAt).HasColumnType("datetime2(7)");
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2(7)");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime2(7)");
            entity.HasOne(e => e.User)
                  .WithOne(u => u.Department)
                  .HasForeignKey<Department>(e => e.UserId);
            entity.HasOne(e => e.Category)
                  .WithMany(c => c.Departments)
                  .HasForeignKey(e => e.CategoryId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Locality)
                  .WithMany(l => l.Departments)
                  .HasForeignKey(e => e.LocalityId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ── Complaint ─────────────────────────────────────────────────────────
        modelBuilder.Entity<Complaint>(entity =>
        {
            entity.ToTable("Complaints");
            entity.HasKey(e => e.ComplaintId);
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.Address).HasMaxLength(300).IsRequired();
            entity.Property(e => e.Criticality).HasMaxLength(10).HasDefaultValue("Medium");
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("Submitted");
            entity.Property(e => e.EstimatedResDate).HasColumnType("date");
            entity.Property(e => e.Latitude).HasColumnType("decimal(10,7)");
            entity.Property(e => e.Longitude).HasColumnType("decimal(10,7)");
            entity.Property(e => e.SubmittedAt).HasColumnType("datetime2(7)");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime2(7)");
            entity.Property(e => e.ResolvedAt).HasColumnType("datetime2(7)");
            entity.HasOne(e => e.Citizen)
                  .WithMany(u => u.ComplaintsAsAuthor)
                  .HasForeignKey(e => e.CitizenUserId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Department)
                  .WithMany(d => d.Complaints)
                  .HasForeignKey(e => e.DeptId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Category)
                  .WithMany(c => c.Complaints)
                  .HasForeignKey(e => e.CategoryId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Locality)
                  .WithMany(l => l.Complaints)
                  .HasForeignKey(e => e.LocalityId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.LinkedToComplaint)
                  .WithMany()
                  .HasForeignKey(e => e.LinkedToComplaintId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ── ComplaintTimeline ─────────────────────────────────────────────────
        modelBuilder.Entity<ComplaintTimeline>(entity =>
        {
            entity.ToTable("ComplaintTimeline");
            entity.HasKey(e => e.TimelineId);
            entity.Property(e => e.NewStatus).HasMaxLength(20).IsRequired();
            entity.Property(e => e.OldStatus).HasMaxLength(20);
            entity.Property(e => e.Remark).HasMaxLength(1000);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2(7)");
            entity.HasOne(e => e.Complaint)
                  .WithMany(c => c.ComplaintTimelines)
                  .HasForeignKey(e => e.ComplaintId);
            entity.HasOne(e => e.Actor)
                  .WithMany(u => u.ComplaintTimelines)
                  .HasForeignKey(e => e.ActorUserId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ── PwgParticipationRequest ───────────────────────────────────────────
        modelBuilder.Entity<PwgparticipationRequest>(entity =>
        {
            entity.ToTable("PWGParticipationRequests");
            entity.HasKey(e => e.RequestId);
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("Pending");
            entity.Property(e => e.RequestNote).HasMaxLength(500);
            entity.Property(e => e.DecisionNote).HasMaxLength(500);
            entity.Property(e => e.RequestedAt).HasColumnType("datetime2(7)");
            entity.Property(e => e.DecidedAt).HasColumnType("datetime2(7)");
            entity.HasOne(e => e.Complaint)
                  .WithMany(c => c.PWGParticipationRequests)
                  .HasForeignKey(e => e.ComplaintId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Organisation)
                  .WithMany(o => o.PWGParticipationRequests)
                  .HasForeignKey(e => e.OrgId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.SolverUser)
                  .WithMany(u => u.PWGParticipationRequests)
                  .HasForeignKey(e => e.SolverUserId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ── ComplaintRating ───────────────────────────────────────────────────
        modelBuilder.Entity<ComplaintRating>(entity =>
        {
            entity.ToTable("ComplaintRatings");
            entity.HasKey(e => e.RatingId);
            entity.Property(e => e.Comment).HasMaxLength(1000);
            entity.Property(e => e.RatedAt).HasColumnType("datetime2(7)");
            entity.HasIndex(e => new { e.ComplaintId, e.CitizenUserId }).IsUnique();
            entity.HasOne(e => e.Complaint)
                  .WithMany(c => c.ComplaintRatings)
                  .HasForeignKey(e => e.ComplaintId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.CitizenUser)
                  .WithMany(u => u.ComplaintRatings)
                  .HasForeignKey(e => e.CitizenUserId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ── Contribution ──────────────────────────────────────────────────────
        modelBuilder.Entity<Contribution>(entity =>
        {
            entity.ToTable("Contributions");
            entity.HasKey(e => e.ContributionId);
            entity.Property(e => e.Amount).HasColumnType("decimal(10,2)").IsRequired();
            entity.Property(e => e.TransactionRef).HasMaxLength(100).IsRequired();
            entity.Property(e => e.PaymentStatus).HasMaxLength(20).HasDefaultValue("Pending");
            entity.Property(e => e.FailureReason).HasMaxLength(200);
            entity.Property(e => e.CompletedAt).HasColumnType("datetime2(7)");
            entity.Property(e => e.ContributedAt).HasColumnType("datetime2(7)");
            entity.HasIndex(e => e.TransactionRef).IsUnique();
            entity.HasOne(e => e.Complaint)
                  .WithMany(c => c.Contributions)
                  .HasForeignKey(e => e.ComplaintId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.CitizenUser)
                  .WithMany(u => u.Contributions)
                  .HasForeignKey(e => e.CitizenUserId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ── Notification ──────────────────────────────────────────────────────
        modelBuilder.Entity<Notification>(entity =>
        {
            entity.ToTable("Notifications");
            entity.HasKey(e => e.NotificationId);
            entity.Property(e => e.Message).HasMaxLength(500).IsRequired();
            entity.Property(e => e.NotificationType).HasMaxLength(30);
            entity.Property(e => e.Channel).HasMaxLength(20).HasDefaultValue("InApp");
            entity.Property(e => e.IsRead).HasDefaultValue(false);
            entity.Property(e => e.IsArchived).HasDefaultValue(false);
            entity.Property(e => e.SentAt).HasColumnType("datetime2(7)");
            entity.Property(e => e.ReadAt).HasColumnType("datetime2(7)");
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2(7)");
            entity.HasOne(e => e.User)
                  .WithMany(u => u.Notifications)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Complaint)
                  .WithMany(c => c.Notifications)
                  .HasForeignKey(e => e.ComplaintId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ── UserPoint ─────────────────────────────────────────────────────────
        modelBuilder.Entity<UserPoint>(entity =>
        {
            entity.ToTable("UserPoints");
            entity.HasKey(e => e.PointsId);
            entity.Property(e => e.Points).HasDefaultValue(0);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime2(7)");
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.HasOne(e => e.User)
                  .WithOne(u => u.UserPoint)
                  .HasForeignKey<UserPoint>(e => e.UserId);
        });

        // ── Certificate ───────────────────────────────────────────────────────
        modelBuilder.Entity<Certificate>(entity =>
        {
            entity.ToTable("Certificates");
            entity.HasKey(e => e.CertificateId);
            entity.Property(e => e.Milestone).HasMaxLength(100).IsRequired();
            entity.Property(e => e.VerificationCode).HasMaxLength(50).IsRequired();
            entity.Property(e => e.FilePath).HasMaxLength(500);
            entity.Property(e => e.IssuedAt).HasColumnType("datetime2(7)");
            entity.HasIndex(e => e.VerificationCode).IsUnique();
            entity.HasOne(e => e.User)
                  .WithMany(u => u.Certificates)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Milestone_Nav)
                  .WithMany(m => m.Certificates)
                  .HasForeignKey(e => e.MilestoneId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ── PasswordResetToken ────────────────────────────────────────────────
        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.ToTable("PasswordResetTokens");
            entity.HasKey(e => e.TokenId);
            entity.Property(e => e.TokenHash).HasMaxLength(256).IsRequired();
            entity.Property(e => e.ExpiresAt).HasColumnType("datetime2(7)");
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2(7)");
            entity.Property(e => e.UsedAt).HasColumnType("datetime2(7)");
            entity.Property(e => e.IsUsed).HasDefaultValue(false);
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasOne(e => e.User)
                  .WithMany(u => u.PasswordResetTokens)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ── ComplaintStatusTransition ─────────────────────────────────────────
        modelBuilder.Entity<ComplaintStatusTransition>(entity =>
        {
            entity.ToTable("ComplaintStatusTransitions");
            entity.HasKey(e => e.TransitionId);
            entity.Property(e => e.FromStatus).HasMaxLength(20).IsRequired();
            entity.Property(e => e.ToStatus).HasMaxLength(20).IsRequired();
            entity.Property(e => e.AllowedRoles).HasMaxLength(100).IsRequired();
            entity.HasIndex(e => new { e.FromStatus, e.ToStatus }).IsUnique();
        });

        // ── MilestoneDefinition ───────────────────────────────────────────────
        modelBuilder.Entity<MilestoneDefinition>(entity =>
        {
            entity.ToTable("MilestoneDefinitions");
            entity.HasKey(e => e.MilestoneId);
            entity.Property(e => e.MilestoneName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(300);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2(7)");
            entity.HasIndex(e => e.MilestoneName).IsUnique();
        });

        // ── ComplaintMlScore ──────────────────────────────────────────────────
        modelBuilder.Entity<ComplaintMlscore>(entity =>
        {
            entity.ToTable("ComplaintMLScores");
            entity.HasKey(e => e.ScoreId);
            entity.Property(e => e.ResolutionProbability).HasColumnType("decimal(5,4)");
            entity.Property(e => e.PriorityScore).HasColumnType("decimal(8,2)");
            entity.Property(e => e.PredictionModelVersion).HasMaxLength(50);   // 01_AI_Tables_Addition.sql widened DB col to VARCHAR(50)
            entity.Property(e => e.ScoredAt).HasColumnType("datetime2(7)");
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime2(7)");
            entity.HasIndex(e => e.ComplaintId).IsUnique();
            entity.HasOne(e => e.Complaint)
                  .WithOne(c => c.MlScore)
                  .HasForeignKey<ComplaintMlscore>(e => e.ComplaintId);
        });

        // ── ComplaintAttachment ───────────────────────────────────────────────
        modelBuilder.Entity<ComplaintAttachment>(entity =>
        {
            entity.ToTable("ComplaintAttachments");
            entity.HasKey(e => e.AttachmentId);
            entity.Property(e => e.AttachmentType).HasMaxLength(30).IsRequired();
            entity.Property(e => e.FilePath).HasMaxLength(500).IsRequired();
            entity.Property(e => e.FileName).HasMaxLength(200);
            entity.Property(e => e.UploadedAt).HasColumnType("datetime2(7)");
            entity.HasOne(e => e.Complaint)
                  .WithMany(c => c.Attachments)
                  .HasForeignKey(e => e.ComplaintId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Timeline)
                  .WithMany(t => t.Attachments)
                  .HasForeignKey(e => e.TimelineId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.UploadedBy)
                  .WithMany()
                  .HasForeignKey(e => e.UploadedByUserId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ── DuplicateComplaintLink ────────────────────────────────────────────
        modelBuilder.Entity<DuplicateComplaintLink>(entity =>
        {
            entity.ToTable("DuplicateComplaintLinks");
            entity.HasKey(e => e.LinkId);
            entity.Property(e => e.LinkedAt).HasColumnType("datetime2(7)");
            entity.HasIndex(e => new { e.OriginalComplaintId, e.LinkedComplaintId }).IsUnique();
            entity.HasOne(e => e.OriginalComplaint)
                  .WithMany(c => c.DuplicateLinks)
                  .HasForeignKey(e => e.OriginalComplaintId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.LinkedComplaint)
                  .WithMany()
                  .HasForeignKey(e => e.LinkedComplaintId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.LinkedByUser)
                  .WithMany()
                  .HasForeignKey(e => e.LinkedByUserId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ── DepartmentCategory ────────────────────────────────────────────────
        modelBuilder.Entity<DepartmentCategory>(entity =>
        {
            entity.ToTable("DepartmentCategories");
            entity.HasKey(e => e.DeptCategoryId);
            entity.Property(e => e.IsPrimary).HasDefaultValue(false);
            entity.Property(e => e.Priority).HasDefaultValue((byte)10);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2(7)");
            entity.HasIndex(e => new { e.DeptId, e.CategoryId }).IsUnique();
            entity.HasOne(e => e.Department)
                  .WithMany(d => d.DepartmentCategories)
                  .HasForeignKey(e => e.DeptId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Category)
                  .WithMany(c => c.DepartmentCategories)
                  .HasForeignKey(e => e.CategoryId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ── EscalationLog ─────────────────────────────────────────────────────
        modelBuilder.Entity<EscalationLog>(entity =>
        {
            entity.ToTable("EscalationLog");
            entity.HasKey(e => e.EscalationId);
            entity.Property(e => e.EscalationTrigger).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.EscalatedAt).HasColumnType("datetime2(7)");
            entity.Property(e => e.ResolvedAt).HasColumnType("datetime2(7)");
            entity.HasOne(e => e.Complaint)
                  .WithMany(c => c.EscalationLogs)
                  .HasForeignKey(e => e.ComplaintId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Actor)
                  .WithMany()
                  .HasForeignKey(e => e.ActorUserId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.OriginalDept)
                  .WithMany()
                  .HasForeignKey(e => e.OriginalDeptId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.ReassignedToDept)
                  .WithMany()
                  .HasForeignKey(e => e.ReassignedToDeptId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ── PwgReport ─────────────────────────────────────────────────────────
        modelBuilder.Entity<Pwgreport>(entity =>
        {
            entity.ToTable("PWGReports");
            entity.HasKey(e => e.ReportId);
            entity.Property(e => e.ReportReason).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.AdminAction).HasMaxLength(30);
            entity.Property(e => e.AdminNote).HasMaxLength(500);
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("Pending");
            entity.Property(e => e.ReportedAt).HasColumnType("datetime2(7)");
            entity.Property(e => e.ReviewedAt).HasColumnType("datetime2(7)");
            entity.Property(e => e.ClosedAt).HasColumnType("datetime2(7)");
            entity.HasOne(e => e.Complaint)
                  .WithMany(c => c.PwgReports)
                  .HasForeignKey(e => e.ComplaintId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.ReportedOrg)
                  .WithMany(o => o.PwgReports)
                  .HasForeignKey(e => e.ReportedOrgId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.ReportedByUser)
                  .WithMany()
                  .HasForeignKey(e => e.ReportedByUserId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.AdminReviewedBy)
                  .WithMany()
                  .HasForeignKey(e => e.AdminReviewedByUserId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ── NotificationPreference ────────────────────────────────────────────
        modelBuilder.Entity<NotificationPreference>(entity =>
        {
            entity.ToTable("NotificationPreferences");
            entity.HasKey(e => e.PrefId);
            entity.Property(e => e.InAppEnabled).HasDefaultValue(true);
            entity.Property(e => e.PushEnabled).HasDefaultValue(true);
            entity.Property(e => e.EmailDigestEnabled).HasDefaultValue(true);
            entity.Property(e => e.DigestFrequencyDays).HasDefaultValue((byte)7);
            entity.Property(e => e.UpdatedAt).HasColumnType("datetime2(7)");
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.HasOne(e => e.User)
                  .WithOne(u => u.NotificationPreference)
                  .HasForeignKey<NotificationPreference>(e => e.UserId);
        });

        // ── PointsLedger ──────────────────────────────────────────────────────
        modelBuilder.Entity<PointsLedger>(entity =>
        {
            entity.ToTable("PointsLedger");
            entity.HasKey(e => e.LedgerId);
            entity.Property(e => e.Reason).HasMaxLength(100).IsRequired();
            entity.Property(e => e.EarnedAt).HasColumnType("datetime2(7)");
            entity.HasOne(e => e.User)
                  .WithMany(u => u.PointsLedgers)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Complaint)
                  .WithMany(c => c.PointsLedgers)
                  .HasForeignKey(e => e.RefComplaintId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Milestone)
                  .WithMany(m => m.PointsLedgers)
                  .HasForeignKey(e => e.RefMilestoneId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ── AuditLog ──────────────────────────────────────────────────────────
        modelBuilder.Entity<AuditLog>(entity =>
        {
            entity.ToTable("AuditLog");
            entity.HasKey(e => e.AuditId);
            entity.Property(e => e.ActionType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Reason).HasMaxLength(500);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2(7)");
            entity.HasOne(e => e.Actor)
                  .WithMany()
                  .HasForeignKey(e => e.ActorUserId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.TargetUser)
                  .WithMany()
                  .HasForeignKey(e => e.TargetUserId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.TargetOrg)
                  .WithMany()
                  .HasForeignKey(e => e.TargetOrgId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.TargetDept)
                  .WithMany()
                  .HasForeignKey(e => e.TargetDeptId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.TargetComplaint)
                  .WithMany()
                  .HasForeignKey(e => e.TargetComplaintId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ── ScoreboardSnapshot ────────────────────────────────────────────────
        modelBuilder.Entity<ScoreboardSnapshot>(entity =>
        {
            entity.ToTable("ScoreboardSnapshot");
            entity.HasKey(e => e.SnapshotId);
            entity.Property(e => e.FullName).HasMaxLength(100).IsRequired();
            entity.Property(e => e.SnapshotAt).HasColumnType("datetime2(7)");
            entity.HasIndex(e => e.UserId).IsUnique();
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Locality)
                  .WithMany()
                  .HasForeignKey(e => e.LocalityId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ── UserInterest ──────────────────────────────────────────────────────
        modelBuilder.Entity<UserInterest>(entity =>
        {
            entity.ToTable("UserInterests");
            entity.HasKey(e => e.UserInterestId);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2(7)");
            entity.HasOne(e => e.User)
                  .WithMany(u => u.UserInterests)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Category)
                  .WithMany(c => c.UserInterests)
                  .HasForeignKey(e => e.CategoryId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.PreferredLocality)
                  .WithMany(l => l.UserInterests)
                  .HasForeignKey(e => e.PreferredLocalityId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ── ComplaintEmbedding (01_AI_Tables_Addition.sql §2) ─────────────────
        modelBuilder.Entity<ComplaintEmbedding>(entity =>
        {
            entity.ToTable("ComplaintEmbeddings");
            entity.HasKey(e => e.EmbeddingId);
            entity.Property(e => e.EmbeddingJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(e => e.ModelVersion).HasMaxLength(50).IsRequired();
            entity.Property(e => e.GeneratedAt).HasColumnType("datetime2(7)");
            entity.HasIndex(e => e.ComplaintId).IsUnique();
            entity.HasOne(e => e.Complaint)
                  .WithOne()
                  .HasForeignKey<ComplaintEmbedding>(e => e.ComplaintId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ── UserRecommendationCache (01 §3) ───────────────────────────────────
        modelBuilder.Entity<UserRecommendationCache>(entity =>
        {
            entity.ToTable("UserRecommendationCache");
            entity.HasKey(e => e.CacheId);
            entity.Property(e => e.Score).HasColumnType("decimal(8,4)").IsRequired();
            entity.Property(e => e.GeneratedAt).HasColumnType("datetime2(7)");
            entity.HasIndex(e => new { e.UserId, e.ComplaintId }).IsUnique();
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Complaint)
                  .WithMany()
                  .HasForeignKey(e => e.ComplaintId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ── AIDecisionLog (01 §4) ─────────────────────────────────────────────
        modelBuilder.Entity<AIDecisionLog>(entity =>
        {
            entity.ToTable("AIDecisionLog");
            entity.HasKey(e => e.LogId);
            entity.Property(e => e.DecisionType).HasMaxLength(50).IsRequired();
            entity.Property(e => e.InputSummary).HasMaxLength(500);
            entity.Property(e => e.OutputSummary).HasMaxLength(500);
            entity.Property(e => e.Confidence).HasColumnType("decimal(5,4)");
            entity.Property(e => e.ModelVersion).HasMaxLength(50);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2(7)");
            entity.HasOne(e => e.Complaint)
                  .WithMany()
                  .HasForeignKey(e => e.ComplaintId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Overrider)
                  .WithMany()
                  .HasForeignKey(e => e.OverriddenBy)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ── ComplaintTag (01 §5) ──────────────────────────────────────────────
        modelBuilder.Entity<ComplaintTag>(entity =>
        {
            entity.ToTable("ComplaintTags");
            entity.HasKey(e => e.TagId);
            entity.Property(e => e.Tag).HasMaxLength(80).IsRequired();
            entity.Property(e => e.Score).HasColumnType("decimal(5,4)");
            entity.Property(e => e.Source).HasMaxLength(20).HasDefaultValue("AI");
            entity.HasOne(e => e.Complaint)
                  .WithMany()
                  .HasForeignKey(e => e.ComplaintId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ── AIPendingScoreQueue (01 §7) ───────────────────────────────────────
        modelBuilder.Entity<AIPendingScoreQueue>(entity =>
        {
            entity.ToTable("AIPendingScoreQueue");
            entity.HasKey(e => e.QueueId);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2(7)");
            entity.Property(e => e.LastAttempt).HasColumnType("datetime2(7)");
            entity.Property(e => e.ErrorMessage).HasMaxLength(500);
            entity.HasIndex(e => e.ComplaintId).IsUnique();
            entity.HasOne(e => e.Complaint)
                  .WithMany()
                  .HasForeignKey(e => e.ComplaintId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ── PlatformStatsCategorySnapshot (01 §6) ─────────────────────────────
        modelBuilder.Entity<PlatformStatsCategorySnapshot>(entity =>
        {
            entity.ToTable("PlatformStatsCategorySnapshot");
            entity.HasKey(e => new { e.SnapshotId, e.CategoryId });
            entity.HasOne(e => e.Category)
                  .WithMany()
                  .HasForeignKey(e => e.CategoryId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ── UserRefreshToken (02_UserRefreshTokens.sql) ──────────────────────
        modelBuilder.Entity<UserRefreshToken>(entity =>
        {
            entity.ToTable("UserRefreshTokens");
            entity.HasKey(e => e.TokenId);
            entity.Property(e => e.TokenHash).HasColumnType("char(64)").IsRequired();
            entity.Property(e => e.ExpiresAt).HasColumnType("datetime2(0)");
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2(0)");
            entity.Property(e => e.RevokedAt).HasColumnType("datetime2(0)");
            entity.HasIndex(e => e.TokenHash).IsUnique();
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // ═══════════════════════════════════════════════════════════════════
        //  Phase 8 — feature-suggestion tables (06_FeatureSuggestions.sql)
        // ═══════════════════════════════════════════════════════════════════

        // ── ComplaintUpvote (§1) ──────────────────────────────────────────────
        modelBuilder.Entity<ComplaintUpvote>(entity =>
        {
            entity.ToTable("ComplaintUpvotes");
            entity.HasKey(e => e.UpvoteId);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2(7)");
            // The SQL constraint uq_Upvote_ComplaintCitizen already enforces this,
            // but registering it in EF lets the model validation catch duplicates
            // before a round-trip.
            entity.HasIndex(e => new { e.ComplaintId, e.CitizenUserId }).IsUnique();
            entity.HasOne(e => e.Complaint)
                  .WithMany()
                  .HasForeignKey(e => e.ComplaintId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Citizen)
                  .WithMany()
                  .HasForeignKey(e => e.CitizenUserId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ── ComplaintComment (§7) ─────────────────────────────────────────────
        modelBuilder.Entity<ComplaintComment>(entity =>
        {
            entity.ToTable("ComplaintComments");
            entity.HasKey(e => e.CommentId);
            entity.Property(e => e.CommentText).HasMaxLength(1500).IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2(7)");
            entity.Property(e => e.IsOfficialReply).HasDefaultValue(false);
            entity.Property(e => e.IsDeleted).HasDefaultValue(false);
            entity.HasOne(e => e.Complaint)
                  .WithMany()
                  .HasForeignKey(e => e.ComplaintId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.User)
                  .WithMany()
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ── ComplaintAppeal (§6) ──────────────────────────────────────────────
        modelBuilder.Entity<ComplaintAppeal>(entity =>
        {
            entity.ToTable("ComplaintAppeals");
            entity.HasKey(e => e.AppealId);
            entity.Property(e => e.Reason).HasMaxLength(1000).IsRequired();
            entity.Property(e => e.Status).HasMaxLength(20).HasDefaultValue("Pending");
            entity.Property(e => e.AdminNote).HasMaxLength(500);
            entity.Property(e => e.Decision).HasMaxLength(20);
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2(7)");
            entity.Property(e => e.ResolvedAt).HasColumnType("datetime2(7)");
            entity.HasOne(e => e.Complaint)
                  .WithMany()
                  .HasForeignKey(e => e.ComplaintId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.CitizenUser)
                  .WithMany()
                  .HasForeignKey(e => e.CitizenUserId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.AdminUser)
                  .WithMany()
                  .HasForeignKey(e => e.AdminUserId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        // ── ComplaintInternalNote (§15) ───────────────────────────────────────
        modelBuilder.Entity<ComplaintInternalNote>(entity =>
        {
            entity.ToTable("ComplaintInternalNotes");
            entity.HasKey(e => e.NoteId);
            entity.Property(e => e.NoteText).HasMaxLength(1500).IsRequired();
            entity.Property(e => e.CreatedAt).HasColumnType("datetime2(7)");
            entity.HasOne(e => e.Complaint)
                  .WithMany()
                  .HasForeignKey(e => e.ComplaintId)
                  .OnDelete(DeleteBehavior.NoAction);
            entity.HasOne(e => e.Author)
                  .WithMany()
                  .HasForeignKey(e => e.CreatedByUserId)
                  .OnDelete(DeleteBehavior.NoAction);
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
