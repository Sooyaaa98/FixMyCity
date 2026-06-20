-- ═══════════════════════════════════════════════════════════════════════════
-- FixMyCity — Phase 8 Feature Suggestions schema (2026-05-20)
-- ═══════════════════════════════════════════════════════════════════════════
-- Run order:
--   00 → 01 → 02 → 03 → 04 → 05 → 06 (THIS FILE)
--
-- Adds tables + procedures for the new feature wave from
-- fixmycity-feature-suggestions.md:
--   • ComplaintUpvotes              (§1)
--   • ComplaintComments             (§7)
--   • ComplaintAppeals              (§6)
--   • ComplaintInternalNotes        (§15)
--   • usp_BulkUpdateComplaintStatus (§11, §16)
--   • usp_ReassignComplaintDept     (§12)
--   • usp_GetComplaintTrend         (§9)
--   • usp_GetActivityFeed           (§20)
--   • usp_GetPublicFeed             (§17)
--
-- All CREATE TABLE statements wrapped in IF NOT EXISTS guards so the script
-- is idempotent and safe to re-run.
-- ═══════════════════════════════════════════════════════════════════════════

USE FixMyCityDB;
GO
SET NOCOUNT ON;

-- ─────────────────────────────────────────────────────────────────────────────
-- 1. ComplaintUpvotes (§1) — citizens up-vote complaints in their locality.
-- Unique(ComplaintId, CitizenUserId) prevents double voting.
-- ─────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ComplaintUpvotes' AND SCHEMA_NAME(schema_id) = 'dbo')
BEGIN
    CREATE TABLE dbo.ComplaintUpvotes (
        UpvoteId        INT          NOT NULL IDENTITY(1,1),
        ComplaintId     INT          NOT NULL,
        CitizenUserId   INT          NOT NULL,
        CreatedAt       DATETIME2(7) NOT NULL CONSTRAINT df_CU_CreatedAt DEFAULT SYSDATETIME(),

        CONSTRAINT pk_ComplaintUpvotes        PRIMARY KEY CLUSTERED (UpvoteId),
        CONSTRAINT uq_Upvote_ComplaintCitizen UNIQUE (ComplaintId, CitizenUserId),
        CONSTRAINT fk_Upvote_Complaint        FOREIGN KEY (ComplaintId)   REFERENCES dbo.Complaints(ComplaintId),
        CONSTRAINT fk_Upvote_Citizen          FOREIGN KEY (CitizenUserId) REFERENCES dbo.Users(UserId)
    );
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_Upvote_Complaint' AND object_id = OBJECT_ID('dbo.ComplaintUpvotes'))
    CREATE NONCLUSTERED INDEX ix_Upvote_Complaint ON dbo.ComplaintUpvotes (ComplaintId);
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 2. ComplaintComments (§7) — public discussion thread per complaint.
-- IsOfficialReply distinguishes Solver/Admin replies from citizen comments.
-- ─────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ComplaintComments' AND SCHEMA_NAME(schema_id) = 'dbo')
BEGIN
    CREATE TABLE dbo.ComplaintComments (
        CommentId        INT             NOT NULL IDENTITY(1,1),
        ComplaintId      INT             NOT NULL,
        UserId           INT             NOT NULL,
        CommentText      NVARCHAR(1500)  NOT NULL,
        IsOfficialReply  BIT             NOT NULL CONSTRAINT df_CC_Official DEFAULT 0,
        IsDeleted        BIT             NOT NULL CONSTRAINT df_CC_Deleted DEFAULT 0,
        CreatedAt        DATETIME2(7)    NOT NULL CONSTRAINT df_CC_CreatedAt DEFAULT SYSDATETIME(),

        CONSTRAINT pk_ComplaintComments PRIMARY KEY CLUSTERED (CommentId),
        CONSTRAINT fk_Comment_Complaint FOREIGN KEY (ComplaintId) REFERENCES dbo.Complaints(ComplaintId),
        CONSTRAINT fk_Comment_User      FOREIGN KEY (UserId)      REFERENCES dbo.Users(UserId)
    );
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_Comment_Complaint' AND object_id = OBJECT_ID('dbo.ComplaintComments'))
    CREATE NONCLUSTERED INDEX ix_Comment_Complaint ON dbo.ComplaintComments (ComplaintId, CreatedAt DESC);
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 3. ComplaintAppeals (§6) — citizen appeals a Rejected complaint to admin.
-- One appeal per complaint per citizen (filtered unique handles re-appeal
-- after a rejection cycle).
-- ─────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ComplaintAppeals' AND SCHEMA_NAME(schema_id) = 'dbo')
BEGIN
    CREATE TABLE dbo.ComplaintAppeals (
        AppealId        INT             NOT NULL IDENTITY(1,1),
        ComplaintId     INT             NOT NULL,
        CitizenUserId   INT             NOT NULL,
        Reason          NVARCHAR(1000)  NOT NULL,
        Status          VARCHAR(20)     NOT NULL CONSTRAINT df_CA_Status DEFAULT 'Pending',
        AdminUserId     INT             NULL,
        AdminNote       NVARCHAR(500)   NULL,
        Decision        VARCHAR(20)     NULL,   -- 'Approved' | 'Rejected'
        CreatedAt       DATETIME2(7)    NOT NULL CONSTRAINT df_CA_CreatedAt DEFAULT SYSDATETIME(),
        ResolvedAt      DATETIME2(7)    NULL,

        CONSTRAINT pk_ComplaintAppeals  PRIMARY KEY CLUSTERED (AppealId),
        CONSTRAINT fk_Appeal_Complaint  FOREIGN KEY (ComplaintId)   REFERENCES dbo.Complaints(ComplaintId),
        CONSTRAINT fk_Appeal_Citizen    FOREIGN KEY (CitizenUserId) REFERENCES dbo.Users(UserId),
        CONSTRAINT fk_Appeal_Admin      FOREIGN KEY (AdminUserId)   REFERENCES dbo.Users(UserId),
        CONSTRAINT chk_Appeal_Status    CHECK (Status   IN ('Pending','Resolved')),
        CONSTRAINT chk_Appeal_Decision  CHECK (Decision IS NULL OR Decision IN ('Approved','Rejected'))
    );
END;
GO
-- A citizen may only have ONE pending appeal per complaint at a time.
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'uix_Appeal_Pending' AND object_id = OBJECT_ID('dbo.ComplaintAppeals'))
    CREATE UNIQUE NONCLUSTERED INDEX uix_Appeal_Pending
        ON dbo.ComplaintAppeals (ComplaintId, CitizenUserId)
        WHERE Status = 'Pending';
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_Appeal_Status' AND object_id = OBJECT_ID('dbo.ComplaintAppeals'))
    CREATE NONCLUSTERED INDEX ix_Appeal_Status ON dbo.ComplaintAppeals (Status, CreatedAt DESC);
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 4. ComplaintInternalNotes (§15) — private notes visible only to Solver/Admin.
-- ─────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ComplaintInternalNotes' AND SCHEMA_NAME(schema_id) = 'dbo')
BEGIN
    CREATE TABLE dbo.ComplaintInternalNotes (
        NoteId            INT            NOT NULL IDENTITY(1,1),
        ComplaintId       INT            NOT NULL,
        CreatedByUserId   INT            NOT NULL,
        NoteText          NVARCHAR(1500) NOT NULL,
        CreatedAt         DATETIME2(7)   NOT NULL CONSTRAINT df_CIN_CreatedAt DEFAULT SYSDATETIME(),

        CONSTRAINT pk_ComplaintInternalNotes PRIMARY KEY CLUSTERED (NoteId),
        CONSTRAINT fk_IntNote_Complaint      FOREIGN KEY (ComplaintId)     REFERENCES dbo.Complaints(ComplaintId),
        CONSTRAINT fk_IntNote_User           FOREIGN KEY (CreatedByUserId) REFERENCES dbo.Users(UserId)
    );
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_IntNote_Complaint' AND object_id = OBJECT_ID('dbo.ComplaintInternalNotes'))
    CREATE NONCLUSTERED INDEX ix_IntNote_Complaint ON dbo.ComplaintInternalNotes (ComplaintId, CreatedAt DESC);
GO

-- ═══════════════════════════════════════════════════════════════════════════
--  STORED PROCEDURES
-- ═══════════════════════════════════════════════════════════════════════════

-- §1 — toggle an upvote (insert if absent, delete if present). Returns new count.
CREATE OR ALTER PROCEDURE dbo.usp_ToggleComplaintUpvote
    @ComplaintId   INT,
    @CitizenUserId INT,
    @NewCount      INT OUTPUT,
    @HasUpvoted    BIT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
    BEGIN TRY
        IF EXISTS (SELECT 1 FROM dbo.ComplaintUpvotes
                   WHERE ComplaintId = @ComplaintId AND CitizenUserId = @CitizenUserId)
        BEGIN
            DELETE FROM dbo.ComplaintUpvotes
              WHERE ComplaintId = @ComplaintId AND CitizenUserId = @CitizenUserId;
            SET @HasUpvoted = 0;
        END
        ELSE
        BEGIN
            INSERT INTO dbo.ComplaintUpvotes (ComplaintId, CitizenUserId)
            VALUES (@ComplaintId, @CitizenUserId);
            SET @HasUpvoted = 1;
        END

        SELECT @NewCount = COUNT(*) FROM dbo.ComplaintUpvotes WHERE ComplaintId = @ComplaintId;
        COMMIT;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END;
GO

-- §7 — post a comment. Solver / Admin auto-marks IsOfficialReply.
CREATE OR ALTER PROCEDURE dbo.usp_AddComplaintComment
    @ComplaintId  INT,
    @UserId       INT,
    @CommentText  NVARCHAR(1500),
    @NewCommentId INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @RoleName VARCHAR(30);
    SELECT @RoleName = r.RoleName
    FROM dbo.Users u JOIN dbo.Roles r ON r.RoleId = u.RoleId
    WHERE u.UserId = @UserId;

    INSERT INTO dbo.ComplaintComments (ComplaintId, UserId, CommentText, IsOfficialReply)
    VALUES (@ComplaintId, @UserId, @CommentText,
            CASE WHEN @RoleName IN ('Solver','SuperAdmin') THEN 1 ELSE 0 END);

    SET @NewCommentId = SCOPE_IDENTITY();
END;
GO

-- §6 — file an appeal on a rejected complaint.
CREATE OR ALTER PROCEDURE dbo.usp_SubmitComplaintAppeal
    @ComplaintId   INT,
    @CitizenUserId INT,
    @Reason        NVARCHAR(1000),
    @NewAppealId   INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @NewAppealId = 0;

    -- Citizens may only appeal their OWN rejected complaint.
    IF NOT EXISTS (
        SELECT 1 FROM dbo.Complaints
         WHERE ComplaintId = @ComplaintId
           AND CitizenUserId = @CitizenUserId
           AND Status = 'Rejected')
    BEGIN
        RAISERROR('Appeal allowed only on your own rejected complaint.', 16, 1);
        RETURN;
    END

    INSERT INTO dbo.ComplaintAppeals (ComplaintId, CitizenUserId, Reason)
    VALUES (@ComplaintId, @CitizenUserId, @Reason);
    SET @NewAppealId = SCOPE_IDENTITY();

    -- Notify all super admins.
    INSERT INTO dbo.Notifications (UserId, ComplaintId, Message, NotificationType, Channel)
    SELECT u.UserId, @ComplaintId,
           'Citizen filed an appeal on rejected complaint #' + CAST(@ComplaintId AS VARCHAR) + '.',
           'StatusChange', 'InApp'
    FROM dbo.Users u JOIN dbo.Roles r ON r.RoleId = u.RoleId
    WHERE r.RoleName = 'SuperAdmin' AND u.IsActive = 1;
END;
GO

-- §6 — admin resolves an appeal. If Approved → complaint goes back to 'Submitted'
-- so the routing pipeline kicks in again.
CREATE OR ALTER PROCEDURE dbo.usp_ResolveComplaintAppeal
    @AppealId    INT,
    @AdminUserId INT,
    @Decision    VARCHAR(20),    -- 'Approved' | 'Rejected'
    @AdminNote   NVARCHAR(500) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    IF @Decision NOT IN ('Approved','Rejected')
    BEGIN
        RAISERROR('Decision must be Approved or Rejected.', 16, 1);
        RETURN;
    END

    DECLARE @ComplaintId INT, @CitizenUserId INT;
    SELECT @ComplaintId = ComplaintId, @CitizenUserId = CitizenUserId
      FROM dbo.ComplaintAppeals
     WHERE AppealId = @AppealId AND Status = 'Pending';
    IF @ComplaintId IS NULL
    BEGIN
        RAISERROR('Appeal not found or already resolved.', 16, 1);
        RETURN;
    END

    BEGIN TRANSACTION;
    BEGIN TRY
        UPDATE dbo.ComplaintAppeals
           SET Status = 'Resolved',
               AdminUserId = @AdminUserId,
               AdminNote = @AdminNote,
               Decision = @Decision,
               ResolvedAt = SYSDATETIME()
         WHERE AppealId = @AppealId;

        IF @Decision = 'Approved'
        BEGIN
            -- Move the complaint back to 'Submitted' so it re-enters the workflow.
            UPDATE dbo.Complaints
               SET Status = 'Submitted', UpdatedAt = SYSDATETIME()
             WHERE ComplaintId = @ComplaintId;

            INSERT INTO dbo.ComplaintTimeline
                (ComplaintId, ActorUserId, OldStatus, NewStatus, Remark)
            VALUES (@ComplaintId, @AdminUserId, 'Rejected', 'Submitted',
                    'Appeal approved by admin: ' + ISNULL(@AdminNote, ''));
        END

        -- Notify citizen of the decision.
        INSERT INTO dbo.Notifications (UserId, ComplaintId, Message, NotificationType, Channel)
        VALUES (@CitizenUserId, @ComplaintId,
                'Your appeal on complaint #' + CAST(@ComplaintId AS VARCHAR)
                + ' was ' + @Decision + '.',
                'StatusChange', 'InApp');

        COMMIT;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END;
GO

-- §15 — add an internal note.
CREATE OR ALTER PROCEDURE dbo.usp_AddInternalNote
    @ComplaintId      INT,
    @CreatedByUserId  INT,
    @NoteText         NVARCHAR(1500),
    @NewNoteId        INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.ComplaintInternalNotes (ComplaintId, CreatedByUserId, NoteText)
    VALUES (@ComplaintId, @CreatedByUserId, @NoteText);
    SET @NewNoteId = SCOPE_IDENTITY();
END;
GO

-- §11 / §16 — bulk status update.
-- Returns the count of rows actually updated. ALL-or-nothing transaction.
CREATE OR ALTER PROCEDURE dbo.usp_BulkUpdateComplaintStatus
    @ComplaintIdsCsv NVARCHAR(MAX),
    @NewStatus       VARCHAR(20),
    @ActorUserId     INT,
    @Remark          NVARCHAR(500) = NULL,
    @UpdatedCount    INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @UpdatedCount = 0;

    -- Parse the CSV of complaint IDs into a TVP-like table.
    DECLARE @Ids TABLE (ComplaintId INT PRIMARY KEY);
    INSERT INTO @Ids (ComplaintId)
    SELECT TRY_CAST(value AS INT)
      FROM STRING_SPLIT(@ComplaintIdsCsv, ',')
     WHERE TRY_CAST(value AS INT) IS NOT NULL;

    IF NOT EXISTS (SELECT 1 FROM @Ids) RETURN;

    -- Filter to only complaints with a valid transition into @NewStatus.
    -- A simple "current status -> new status" check via ComplaintStatusTransitions.
    DECLARE @Eligible TABLE (ComplaintId INT PRIMARY KEY, OldStatus VARCHAR(20));
    INSERT INTO @Eligible (ComplaintId, OldStatus)
    SELECT c.ComplaintId, c.Status
      FROM dbo.Complaints c
      JOIN @Ids i ON i.ComplaintId = c.ComplaintId
     WHERE EXISTS (SELECT 1 FROM dbo.ComplaintStatusTransitions t
                    WHERE t.FromStatus = c.Status AND t.ToStatus = @NewStatus);

    IF NOT EXISTS (SELECT 1 FROM @Eligible) RETURN;

    BEGIN TRANSACTION;
    BEGIN TRY
        UPDATE c
           SET Status = @NewStatus,
               UpdatedAt = SYSDATETIME(),
               ResolvedAt = CASE WHEN @NewStatus = 'Resolved' THEN SYSDATETIME() ELSE c.ResolvedAt END
          FROM dbo.Complaints c
          JOIN @Eligible e ON e.ComplaintId = c.ComplaintId;

        SET @UpdatedCount = @@ROWCOUNT;

        INSERT INTO dbo.ComplaintTimeline
            (ComplaintId, ActorUserId, OldStatus, NewStatus, Remark)
        SELECT e.ComplaintId, @ActorUserId, e.OldStatus, @NewStatus,
               ISNULL(@Remark, 'Bulk update.')
          FROM @Eligible e;

        COMMIT;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END;
GO

-- §12 — manual department reassignment by admin (override AI routing).
CREATE OR ALTER PROCEDURE dbo.usp_ReassignComplaintDept
    @ComplaintId  INT,
    @NewDeptId    INT,
    @AdminUserId  INT,
    @Reason       NVARCHAR(500)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @OldDeptId INT, @CitizenId INT;
    SELECT @OldDeptId = DeptId, @CitizenId = CitizenUserId
      FROM dbo.Complaints WHERE ComplaintId = @ComplaintId;

    IF @OldDeptId IS NULL OR @CitizenId IS NULL
    BEGIN
        RAISERROR('Complaint not found.', 16, 1);
        RETURN;
    END

    BEGIN TRANSACTION;
    BEGIN TRY
        UPDATE dbo.Complaints
           SET DeptId = @NewDeptId, UpdatedAt = SYSDATETIME()
         WHERE ComplaintId = @ComplaintId;

        -- Log to escalation history as a 'Manual' rerouting.
        INSERT INTO dbo.EscalationLog
            (ComplaintId, EscalationTrigger, ActorUserId,
             OriginalDeptId, ReassignedToDeptId, Reason)
        VALUES
            (@ComplaintId, 'Manual', @AdminUserId,
             @OldDeptId, @NewDeptId, @Reason);

        -- Audit
        INSERT INTO dbo.AuditLog (ActorUserId, ActionType, TargetComplaintId, Reason)
        VALUES (@AdminUserId, 'ComplaintReassigned', @ComplaintId, @Reason);

        -- Notify the new solver
        DECLARE @SolverId INT;
        SELECT @SolverId = UserId FROM dbo.Departments WHERE DeptId = @NewDeptId;
        IF @SolverId IS NOT NULL
            INSERT INTO dbo.Notifications (UserId, ComplaintId, Message, NotificationType, Channel)
            VALUES (@SolverId, @ComplaintId,
                    'Complaint #' + CAST(@ComplaintId AS VARCHAR)
                    + ' has been reassigned to your department.',
                    'NewAssignment', 'InApp');

        -- Notify the citizen
        INSERT INTO dbo.Notifications (UserId, ComplaintId, Message, NotificationType, Channel)
        VALUES (@CitizenId, @ComplaintId,
                'Complaint #' + CAST(@ComplaintId AS VARCHAR)
                + ' was reassigned by admin.',
                'StatusChange', 'InApp');

        COMMIT;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END;
GO

-- §9 — day-by-day complaint count trend for the last N days.
CREATE OR ALTER PROCEDURE dbo.usp_GetComplaintTrend
    @Days INT = 30
AS
BEGIN
    SET NOCOUNT ON;
    IF @Days IS NULL OR @Days <= 0 SET @Days = 30;
    IF @Days > 365 SET @Days = 365;

    DECLARE @StartDate DATE = DATEADD(DAY, -@Days + 1, CAST(SYSDATETIME() AS DATE));

    ;WITH Days AS (
        SELECT @StartDate AS d
        UNION ALL
        SELECT DATEADD(DAY, 1, d) FROM Days WHERE d < CAST(SYSDATETIME() AS DATE)
    )
    SELECT
        d.d                                                AS [Date],
        ISNULL(SUM(CASE WHEN c.ComplaintId IS NOT NULL THEN 1 ELSE 0 END), 0) AS [Count],
        ISNULL(SUM(CASE WHEN c.Status = 'Resolved' THEN 1 ELSE 0 END), 0)     AS [Resolved]
      FROM Days d
      LEFT JOIN dbo.Complaints c ON CAST(c.SubmittedAt AS DATE) = d.d
     GROUP BY d.d
     ORDER BY d.d
     OPTION (MAXRECURSION 366);
END;
GO

-- §20 — unified activity feed for any user.
-- Mixes complaint timeline, points ledger entries, certificates, comments.
CREATE OR ALTER PROCEDURE dbo.usp_GetActivityFeed
    @UserId    INT,
    @PageSize  INT = 20,
    @PageNum   INT = 1
AS
BEGIN
    SET NOCOUNT ON;
    IF @PageSize IS NULL OR @PageSize <= 0  SET @PageSize = 20;
    IF @PageSize > 100                       SET @PageSize = 100;
    IF @PageNum  IS NULL OR @PageNum  <= 0  SET @PageNum  = 1;

    ;WITH Events AS (
        -- complaint submitted / status changes by the user
        SELECT 'ComplaintSubmitted' AS EventType,
               'Submitted complaint #' + CAST(c.ComplaintId AS VARCHAR) + ' — ' + c.Title AS Description,
               c.ComplaintId AS RelatedId,
               c.SubmittedAt AS CreatedAt
          FROM dbo.Complaints c
         WHERE c.CitizenUserId = @UserId

        UNION ALL

        SELECT 'StatusChange',
               'Complaint #' + CAST(t.ComplaintId AS VARCHAR)
                 + ' changed to ' + t.NewStatus,
               t.ComplaintId,
               t.CreatedAt
          FROM dbo.ComplaintTimeline t
          JOIN dbo.Complaints c ON c.ComplaintId = t.ComplaintId
         WHERE c.CitizenUserId = @UserId
           AND t.ActorUserId IS NOT NULL
           AND t.ActorUserId <> @UserId

        UNION ALL

        -- Points
        SELECT 'PointsAwarded',
               CAST(pl.PointsDelta AS VARCHAR) + ' points — ' + pl.Reason,
               pl.RefComplaintId,
               pl.EarnedAt
          FROM dbo.PointsLedger pl
         WHERE pl.UserId = @UserId

        UNION ALL

        -- Certificates
        SELECT 'CertificateIssued',
               'Earned the "' + cert.Milestone + '" certificate.',
               cert.CertificateId,
               cert.IssuedAt
          FROM dbo.Certificates cert
         WHERE cert.UserId = @UserId

        UNION ALL

        -- Comments authored
        SELECT 'CommentPosted',
               'Commented on complaint #' + CAST(cc.ComplaintId AS VARCHAR),
               cc.ComplaintId,
               cc.CreatedAt
          FROM dbo.ComplaintComments cc
         WHERE cc.UserId = @UserId AND cc.IsDeleted = 0
    )
    SELECT * FROM Events
     ORDER BY CreatedAt DESC
    OFFSET ((@PageNum - 1) * @PageSize) ROWS
     FETCH NEXT @PageSize ROWS ONLY;
END;
GO

-- §17 — public read-only transparency feed.
CREATE OR ALTER PROCEDURE dbo.usp_GetPublicFeed
    @LocalityId  INT = NULL,
    @CategoryId  SMALLINT = NULL,
    @Status      VARCHAR(20) = NULL,
    @Keyword     NVARCHAR(200) = NULL,
    @PageNum     INT = 1,
    @PageSize    INT = 20
AS
BEGIN
    SET NOCOUNT ON;
    IF @PageSize IS NULL OR @PageSize <= 0  SET @PageSize = 20;
    IF @PageSize > 100                       SET @PageSize = 100;
    IF @PageNum  IS NULL OR @PageNum  <= 0  SET @PageNum  = 1;

    SELECT
        c.ComplaintId, c.Title,
        CAST(LEFT(c.Description, 200) AS NVARCHAR(200)) AS Description,
        c.Status, c.Criticality, c.SubmittedAt, c.ResolvedAt,
        c.Latitude, c.Longitude,
        cat.CategoryId,   cat.CategoryName,
        loc.LocalityId,   loc.LocalityName,
        d.DeptId,         d.DeptName,
        (SELECT COUNT(*) FROM dbo.ComplaintUpvotes WHERE ComplaintId = c.ComplaintId) AS UpvoteCount
      FROM dbo.Complaints c
      LEFT JOIN dbo.IssueCategories cat ON cat.CategoryId = c.CategoryId
      LEFT JOIN dbo.Localities      loc ON loc.LocalityId = c.LocalityId
      LEFT JOIN dbo.Departments     d   ON d.DeptId       = c.DeptId
     WHERE (@LocalityId IS NULL OR c.LocalityId = @LocalityId)
       AND (@CategoryId IS NULL OR c.CategoryId = @CategoryId)
       AND (@Status     IS NULL OR c.Status     = @Status)
       AND (@Keyword    IS NULL OR @Keyword = ''
            OR c.Title       LIKE '%' + @Keyword + '%'
            OR c.Description LIKE '%' + @Keyword + '%')
     ORDER BY c.SubmittedAt DESC
    OFFSET ((@PageNum - 1) * @PageSize) ROWS
     FETCH NEXT @PageSize ROWS ONLY;
END;
GO

-- §5 — server-side "near me" filter via Haversine. SQL has no native
-- geography type required for this dataset, so we compute distance inline.
CREATE OR ALTER PROCEDURE dbo.usp_GetNearbyComplaints
    @Lat       DECIMAL(10,7),
    @Lng       DECIMAL(10,7),
    @RadiusKm  DECIMAL(8,3) = 2.0,
    @PageSize  INT = 50
AS
BEGIN
    SET NOCOUNT ON;
    IF @RadiusKm IS NULL OR @RadiusKm <= 0  SET @RadiusKm = 2.0;
    IF @PageSize IS NULL OR @PageSize <= 0  SET @PageSize = 50;
    IF @PageSize > 200                       SET @PageSize = 200;

    DECLARE @LatRad FLOAT = @Lat * PI() / 180.0;

    SELECT TOP (@PageSize)
        c.ComplaintId, c.Title, c.Status, c.Criticality,
        c.Latitude, c.Longitude, c.SubmittedAt,
        cat.CategoryId, cat.CategoryName,
        loc.LocalityId, loc.LocalityName,
        -- Haversine in kilometres
        2 * 6371 * ASIN(SQRT(
            POWER(SIN(((CAST(c.Latitude AS FLOAT) - @Lat) * PI() / 180.0) / 2), 2)
          + COS(@LatRad) * COS(CAST(c.Latitude AS FLOAT) * PI() / 180.0)
          * POWER(SIN(((CAST(c.Longitude AS FLOAT) - @Lng) * PI() / 180.0) / 2), 2)
        )) AS DistanceKm
      FROM dbo.Complaints c
      LEFT JOIN dbo.IssueCategories cat ON cat.CategoryId = c.CategoryId
      LEFT JOIN dbo.Localities      loc ON loc.LocalityId = c.LocalityId
     WHERE c.Latitude IS NOT NULL AND c.Longitude IS NOT NULL
       AND c.Status NOT IN ('Resolved','Rejected','Linked')
       AND 2 * 6371 * ASIN(SQRT(
            POWER(SIN(((CAST(c.Latitude AS FLOAT) - @Lat) * PI() / 180.0) / 2), 2)
          + COS(@LatRad) * COS(CAST(c.Latitude AS FLOAT) * PI() / 180.0)
          * POWER(SIN(((CAST(c.Longitude AS FLOAT) - @Lng) * PI() / 180.0) / 2), 2)
        )) <= @RadiusKm
     ORDER BY DistanceKm ASC;
END;
GO

PRINT 'Phase-8 feature-suggestion schema applied.';
GO
