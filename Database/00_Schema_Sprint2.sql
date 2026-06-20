USE master;
GO
IF EXISTS (SELECT name FROM sys.databases WHERE name = N'FixMyCityDB')
BEGIN
   ALTER DATABASE FixMyCityDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
   DROP DATABASE FixMyCityDB;
END
GO
CREATE DATABASE FixMyCityDB;
GO
USE FixMyCityDB;
GO
IF DB_NAME() <> 'FixMyCityDB'
BEGIN
   RAISERROR('Wrong database context. Aborting.', 16, 1);
   RETURN;
END
GO
-- ============================================================
-- SECTION 1: LOOKUP TABLES
-- ============================================================

-- F14: Normalised locality store replaces all VARCHAR locality fields.
CREATE TABLE Localities (
   LocalityId   INT          NOT NULL IDENTITY(1,1),
   LocalityName VARCHAR(100) NOT NULL,
   City         VARCHAR(100) NOT NULL,
   State        VARCHAR(100) NOT NULL DEFAULT 'Karnataka',
   IsActive     BIT          NOT NULL DEFAULT 1,
   CONSTRAINT pk_Localities      PRIMARY KEY (LocalityId),
   CONSTRAINT uq_Localities_Name UNIQUE (LocalityName)
);
GO

CREATE TABLE Roles (
   RoleId   TINYINT     NOT NULL IDENTITY(1,1),
   RoleName VARCHAR(30) NOT NULL,
   CONSTRAINT pk_Roles      PRIMARY KEY (RoleId),
   CONSTRAINT uq_Roles_Name UNIQUE (RoleName),
   CONSTRAINT chk_Roles_Name CHECK (RoleName IN ('SuperAdmin','Citizen','Solver','PWG'))
);
GO

CREATE TABLE IssueCategories (
   CategoryId   SMALLINT     NOT NULL IDENTITY(1,1),
   CategoryName VARCHAR(80)  NOT NULL,
   Description  VARCHAR(300) NULL,
   CONSTRAINT pk_IssueCategories      PRIMARY KEY (CategoryId),
   CONSTRAINT uq_IssueCategories_Name UNIQUE (CategoryName)
);
GO

CREATE TABLE MilestoneDefinitions (
   MilestoneId     INT          NOT NULL IDENTITY(1,1),
   MilestoneName   VARCHAR(100) NOT NULL,
   PointsThreshold INT          NOT NULL,
   Description     VARCHAR(300) NULL,
   IsActive        BIT          NOT NULL DEFAULT 1,
   CreatedAt       DATETIME2(7) NOT NULL DEFAULT SYSDATETIME(),
   CONSTRAINT pk_MilestoneDefinitions PRIMARY KEY (MilestoneId),
   CONSTRAINT uq_Milestone_Name       UNIQUE (MilestoneName),
   CONSTRAINT chk_Milestone_Points    CHECK (PointsThreshold > 0)
);
GO

-- F21: Valid complaint status transitions enforced at DB layer.
CREATE TABLE ComplaintStatusTransitions (
   TransitionId TINYINT      NOT NULL IDENTITY(1,1),
   FromStatus   VARCHAR(20)  NOT NULL,
   ToStatus     VARCHAR(20)  NOT NULL,
   AllowedRoles VARCHAR(100) NOT NULL,
   CONSTRAINT pk_CST      PRIMARY KEY (TransitionId),
   CONSTRAINT uq_CST_Pair UNIQUE (FromStatus, ToStatus)
);
GO

-- ============================================================
-- SECTION 2: CORE TABLES
-- Identity seeds: Users=1001, Orgs=2001, Depts=3001, Complaints=5001
-- ============================================================

CREATE TABLE Users (
   UserId              INT           NOT NULL IDENTITY(1001,1),
   RoleId              TINYINT       NOT NULL,
   FullName            VARCHAR(100)  NOT NULL,
   Email               VARCHAR(150)  NOT NULL,
   PasswordHash        VARCHAR(256)  NULL,
   Phone               VARCHAR(15)   NOT NULL,
   Address             VARCHAR(300)  NOT NULL,
   LocalityId          INT           NULL,       
   AadhaarNo           VARCHAR(12)   NULL,
   SSOProvider         VARCHAR(30)   NULL,
   SSOExternalId       VARCHAR(200)  NULL,
   FailedLoginAttempts TINYINT       NOT NULL DEFAULT 0,
   LockoutUntil        DATETIME2(7)  NULL,
   IsActive            BIT           NOT NULL DEFAULT 1,
   IsApproved          BIT           NOT NULL DEFAULT 0,
   IsBanned            BIT           NOT NULL DEFAULT 0,      
   BanReason           NVARCHAR(300) NULL,                   
   BannedAt            DATETIME2(7)  NULL,                    
   IsSuspended         BIT           NOT NULL DEFAULT 0,      
   CreatedAt           DATETIME2(7)  NOT NULL DEFAULT SYSDATETIME(),
   UpdatedAt           DATETIME2(7)  NOT NULL DEFAULT SYSDATETIME(),
   CONSTRAINT pk_Users              PRIMARY KEY (UserId),
   CONSTRAINT uq_Users_Email        UNIQUE (Email),
   CONSTRAINT fk_Users_RoleId       FOREIGN KEY (RoleId)     REFERENCES Roles(RoleId),
   CONSTRAINT fk_Users_Locality     FOREIGN KEY (LocalityId) REFERENCES Localities(LocalityId),
   CONSTRAINT chk_Users_Phone       CHECK (LEN(Phone) >= 10),
   CONSTRAINT chk_Users_Aadhaar     CHECK (AadhaarNo IS NULL OR LEN(AadhaarNo) = 12),
   CONSTRAINT chk_Users_AuthMethod  CHECK (PasswordHash IS NOT NULL OR SSOExternalId IS NOT NULL),
   CONSTRAINT chk_Users_SSOProvider CHECK (SSOProvider IS NULL OR SSOProvider IN ('Google','GovID')),
   CONSTRAINT chk_Users_SSOPair     CHECK (SSOProvider IS NULL OR SSOExternalId IS NOT NULL),
   CONSTRAINT chk_Users_BanConsist  CHECK (IsBanned = 0 OR IsActive = 0)  -- F7: banned must be inactive
);
GO

-- Filtered unique index: allows unlimited password-only users (NULL SSO cols).
CREATE UNIQUE NONCLUSTERED INDEX uix_Users_SSO
   ON dbo.Users(SSOProvider, SSOExternalId)
   WHERE SSOProvider IS NOT NULL AND SSOExternalId IS NOT NULL;
GO

-- F16: Password reset tokens table.
CREATE TABLE PasswordResetTokens (
   TokenId   INT          NOT NULL IDENTITY(1,1),
   UserId    INT          NOT NULL,
   TokenHash VARCHAR(256) NOT NULL,        -- hash of raw token; app holds raw token only
   ExpiresAt DATETIME2(7) NOT NULL,
   IsUsed    BIT          NOT NULL DEFAULT 0,
   CreatedAt DATETIME2(7) NOT NULL DEFAULT SYSDATETIME(),
   UsedAt    DATETIME2(7) NULL,
   CONSTRAINT pk_PRT       PRIMARY KEY (TokenId),
   CONSTRAINT uq_PRT_Token UNIQUE (TokenHash),
   CONSTRAINT fk_PRT_User  FOREIGN KEY (UserId) REFERENCES Users(UserId)
);
GO

CREATE TABLE Organisations (
   OrgId          INT          NOT NULL IDENTITY(2001,1),
   UserId         INT          NOT NULL,
   OrgName        VARCHAR(150) NOT NULL,
   OrgType        VARCHAR(50)  NOT NULL,
   RegistrationNo VARCHAR(50)  NOT NULL,
   ContactEmail   VARCHAR(150) NOT NULL,
   ContactPhone   VARCHAR(15)  NOT NULL,
   Address        VARCHAR(300) NOT NULL,
   ApprovalStatus VARCHAR(20)  NOT NULL DEFAULT 'Pending',
   ApprovedAt     DATETIME2(7) NULL,
   CreatedAt      DATETIME2(7) NOT NULL DEFAULT SYSDATETIME(),
   UpdatedAt      DATETIME2(7) NOT NULL DEFAULT SYSDATETIME(),
   CONSTRAINT pk_Organisations          PRIMARY KEY (OrgId),
   CONSTRAINT uq_Organisations_RegNo    UNIQUE (RegistrationNo),
   CONSTRAINT uq_Organisations_UserId   UNIQUE (UserId),
   CONSTRAINT fk_Organisations_UserId   FOREIGN KEY (UserId) REFERENCES Users(UserId),
   CONSTRAINT chk_Organisations_Status  CHECK (ApprovalStatus IN ('Pending','Approved','Rejected')),
   CONSTRAINT chk_Organisations_OrgType CHECK (OrgType IN ('NGO','Student Group','CSR','Other'))
);
GO

-- FIX-04 (GAP-06 / US63): Suspension state for organisations.
-- IsSuspended on Users and SuspendedAt on Organisations provide cascade visibility.
ALTER TABLE Organisations ADD SuspendedAt DATETIME2(7) NULL;
GO

-- F14: LocalityId FK replaces Locality VARCHAR.
CREATE TABLE Departments (
   DeptId         INT          NOT NULL IDENTITY(3001,1),
   UserId         INT          NOT NULL,
   DeptName       VARCHAR(150) NOT NULL,
   Ministry       VARCHAR(100) NOT NULL,
   CategoryId     SMALLINT     NOT NULL,
   ContactEmail   VARCHAR(150) NOT NULL,
   ContactPhone   VARCHAR(15)  NOT NULL,
   Address        VARCHAR(300) NOT NULL,
   LocalityId     INT          NOT NULL,
   ApprovalStatus VARCHAR(20)  NOT NULL DEFAULT 'Pending',
   ApprovedAt     DATETIME2(7) NULL,
   CreatedAt      DATETIME2(7) NOT NULL DEFAULT SYSDATETIME(),
   UpdatedAt      DATETIME2(7) NOT NULL DEFAULT SYSDATETIME(),
   CONSTRAINT pk_Departments          PRIMARY KEY (DeptId),
   CONSTRAINT uq_Departments_UserId   UNIQUE (UserId),
   CONSTRAINT fk_Departments_UserId   FOREIGN KEY (UserId)     REFERENCES Users(UserId),
   CONSTRAINT fk_Departments_Category FOREIGN KEY (CategoryId) REFERENCES IssueCategories(CategoryId),
   CONSTRAINT fk_Departments_Locality FOREIGN KEY (LocalityId) REFERENCES Localities(LocalityId),
   CONSTRAINT chk_Departments_Status  CHECK (ApprovalStatus IN ('Pending','Approved','Rejected'))
);
GO

-- F1:  ImagePath and ResolutionImagePath removed. Use ComplaintAttachments.
-- F14: LocalityId FK replaces Locality VARCHAR.
CREATE TABLE Complaints (
   ComplaintId         INT            NOT NULL IDENTITY(5001,1),
   CitizenUserId       INT            NOT NULL,
   DeptId              INT            NULL,
   CategoryId          SMALLINT       NOT NULL,
   Title               VARCHAR(200)   NOT NULL,
   Description         NVARCHAR(2000) NOT NULL,
   LocalityId          INT            NOT NULL,
   Address             VARCHAR(300)   NOT NULL,
   Criticality         VARCHAR(10)    NOT NULL DEFAULT 'Medium',
   Status              VARCHAR(20)    NOT NULL DEFAULT 'Submitted',
   EstimatedResDate    DATE           NULL,
   Latitude            DECIMAL(10,7)  NULL,
   Longitude           DECIMAL(10,7)  NULL,
   LinkedToComplaintId INT            NULL,
   SubmittedAt         DATETIME2(7)   NOT NULL DEFAULT SYSDATETIME(),
   UpdatedAt           DATETIME2(7)   NOT NULL DEFAULT SYSDATETIME(),
   ResolvedAt          DATETIME2(7)   NULL,
   CONSTRAINT pk_Complaints              PRIMARY KEY (ComplaintId),
   CONSTRAINT fk_Complaints_Citizen      FOREIGN KEY (CitizenUserId) REFERENCES Users(UserId),
   CONSTRAINT fk_Complaints_Dept         FOREIGN KEY (DeptId)        REFERENCES Departments(DeptId),
   CONSTRAINT fk_Complaints_Category     FOREIGN KEY (CategoryId)    REFERENCES IssueCategories(CategoryId),
   CONSTRAINT fk_Complaints_Locality     FOREIGN KEY (LocalityId)    REFERENCES Localities(LocalityId),
   CONSTRAINT chk_Complaints_Criticality CHECK (Criticality IN ('Low','Medium','High','Critical')),
   CONSTRAINT chk_Complaints_Status      CHECK (Status IN (
                                             'Submitted','In Progress','Resolved',
                                             'Rejected','Re-opened','Escalated','Linked')),
   CONSTRAINT chk_Complaints_Latitude    CHECK (Latitude  IS NULL OR Latitude  BETWEEN -90  AND  90),
   CONSTRAINT chk_Complaints_Longitude   CHECK (Longitude IS NULL OR Longitude BETWEEN -180 AND 180)
);
GO
ALTER TABLE Complaints ADD CONSTRAINT fk_Complaints_LinkedTo
   FOREIGN KEY (LinkedToComplaintId) REFERENCES Complaints(ComplaintId);
GO

-- Add to Complaints indexes section
CREATE NONCLUSTERED INDEX ix_Complaints_Dept_Criticality
   ON Complaints(DeptId, Criticality) 
   INCLUDE (Title, Status, SubmittedAt, LocalityId);
GO

-- F1: PhotoPath removed. ComplaintAttachments is sole media store.
-- ActorUserId NULL = system-triggered event (auto-escalation).
CREATE TABLE ComplaintTimeline (
   TimelineId  INT            NOT NULL IDENTITY(1,1),
   ComplaintId INT            NOT NULL,
   ActorUserId INT            NULL,
   OldStatus   VARCHAR(20)    NULL,
   NewStatus   VARCHAR(20)    NOT NULL,
   Remark      NVARCHAR(1000) NULL,
   CreatedAt   DATETIME2(7)   NOT NULL DEFAULT SYSDATETIME(),
   CONSTRAINT pk_ComplaintTimeline   PRIMARY KEY (TimelineId),
   CONSTRAINT fk_Timeline_Complaint  FOREIGN KEY (ComplaintId) REFERENCES Complaints(ComplaintId),
   CONSTRAINT fk_Timeline_Actor      FOREIGN KEY (ActorUserId) REFERENCES Users(UserId),
   CONSTRAINT chk_Timeline_NewStatus CHECK (NewStatus IN (
                                         'Submitted','In Progress','Resolved',
                                         'Rejected','Re-opened','Escalated','Linked'))
);
GO

-- ============================================================
-- SECTION 3: ACTIVITY TABLES
-- ============================================================

-- F6: UNIQUE removed; replaced with filtered unique index in INDEXES section.
--     Allows re-application by same org after prior rejection.
CREATE TABLE PWGParticipationRequests (
   RequestId    INT           NOT NULL IDENTITY(1,1),
   ComplaintId  INT           NOT NULL,
   OrgId        INT           NOT NULL,
   SolverUserId INT           NOT NULL,
   Status       VARCHAR(20)   NOT NULL DEFAULT 'Pending',
   RequestNote  NVARCHAR(500) NULL,
   DecisionNote NVARCHAR(500) NULL,
   RequestedAt  DATETIME2(7)  NOT NULL DEFAULT SYSDATETIME(),
   DecidedAt    DATETIME2(7)  NULL,
   CONSTRAINT pk_PWGRequests      PRIMARY KEY (RequestId),
   CONSTRAINT fk_PWGReq_Complaint FOREIGN KEY (ComplaintId)  REFERENCES Complaints(ComplaintId),
   CONSTRAINT fk_PWGReq_Org       FOREIGN KEY (OrgId)        REFERENCES Organisations(OrgId),
   CONSTRAINT fk_PWGReq_Solver    FOREIGN KEY (SolverUserId) REFERENCES Users(UserId),
   CONSTRAINT chk_PWGReq_Status   CHECK (Status IN ('Pending','Approved','Rejected'))
);
GO

CREATE TABLE ComplaintRatings (
   RatingId      INT            NOT NULL IDENTITY(1,1),
   ComplaintId   INT            NOT NULL,
   CitizenUserId INT            NOT NULL,
   Stars         TINYINT        NOT NULL,
   Comment       NVARCHAR(1000) NULL,
   RatedAt       DATETIME2(7)   NOT NULL DEFAULT SYSDATETIME(),
   CONSTRAINT pk_ComplaintRatings        PRIMARY KEY (RatingId),
   CONSTRAINT uq_Rating_ComplaintCitizen UNIQUE (ComplaintId, CitizenUserId),
   CONSTRAINT fk_Rating_Complaint        FOREIGN KEY (ComplaintId)   REFERENCES Complaints(ComplaintId),
   CONSTRAINT fk_Rating_Citizen          FOREIGN KEY (CitizenUserId) REFERENCES Users(UserId),
   CONSTRAINT chk_Rating_Stars           CHECK (Stars BETWEEN 1 AND 5)
);
GO

-- F5: TransactionRef is NOT NULL — gateway callbacks require a reference to reconcile.
CREATE TABLE Contributions (
   ContributionId INT           NOT NULL IDENTITY(1,1),
   ComplaintId    INT           NOT NULL,
   CitizenUserId  INT           NOT NULL,
   Amount         DECIMAL(10,2) NOT NULL,
   TransactionRef VARCHAR(100)  NOT NULL,          -- F5: NOT NULL
   PaymentStatus  VARCHAR(20)   NOT NULL DEFAULT 'Pending',
   FailureReason  NVARCHAR(200) NULL,
   CompletedAt    DATETIME2(7)  NULL,
   ContributedAt  DATETIME2(7)  NOT NULL DEFAULT SYSDATETIME(),
   CONSTRAINT pk_Contributions          PRIMARY KEY (ContributionId),
   CONSTRAINT uq_Contrib_TxRef          UNIQUE (TransactionRef),
   CONSTRAINT fk_Contrib_Complaint      FOREIGN KEY (ComplaintId)   REFERENCES Complaints(ComplaintId),
   CONSTRAINT fk_Contrib_Citizen        FOREIGN KEY (CitizenUserId) REFERENCES Users(UserId),
   CONSTRAINT chk_Contrib_Amount        CHECK (Amount > 0),
   CONSTRAINT chk_Contrib_PaymentStatus CHECK (PaymentStatus IN ('Pending','Success','Failed','Refunded'))
);
GO

-- F10: NotificationType CHECK constraint added.
CREATE TABLE Notifications (
   NotificationId   INT           NOT NULL IDENTITY(1,1),
   UserId           INT           NOT NULL,
   ComplaintId      INT           NULL,
   Message          NVARCHAR(500) NOT NULL,
   IsRead           BIT           NOT NULL DEFAULT 0,
   NotificationType VARCHAR(30)   NULL,
   Channel          VARCHAR(20)   NOT NULL DEFAULT 'InApp',
   SentAt           DATETIME2(7)  NULL,
   ReadAt           DATETIME2(7)  NULL,
   IsArchived       BIT           NOT NULL DEFAULT 0,
   CreatedAt        DATETIME2(7)  NOT NULL DEFAULT SYSDATETIME(),
   CONSTRAINT pk_Notifications  PRIMARY KEY (NotificationId),
   CONSTRAINT fk_Notif_User     FOREIGN KEY (UserId)      REFERENCES Users(UserId),
   CONSTRAINT fk_Notif_Complaint FOREIGN KEY (ComplaintId) REFERENCES Complaints(ComplaintId),
   CONSTRAINT chk_Notif_Channel CHECK (Channel IN ('InApp','Push','Email')),
   CONSTRAINT chk_Notif_Type    CHECK (NotificationType IS NULL OR NotificationType IN (
                                    'StatusChange','NewAssignment','Registration',
                                    'PWGDecision','WeeklyDigest'))  -- F10
);
GO

CREATE TABLE UserPoints (
   PointsId  INT          NOT NULL IDENTITY(1,1),
   UserId    INT          NOT NULL,
   Points    INT          NOT NULL DEFAULT 0,
   UpdatedAt DATETIME2(7) NOT NULL DEFAULT SYSDATETIME(),  -- renamed from LastUpdated
   CONSTRAINT pk_UserPoints      PRIMARY KEY (PointsId),
   CONSTRAINT uq_UserPoints_User UNIQUE (UserId),
   CONSTRAINT fk_Points_User     FOREIGN KEY (UserId) REFERENCES Users(UserId),
   CONSTRAINT chk_Points_Value   CHECK (Points >= 0)
);
GO

CREATE TABLE Certificates (
   CertificateId    INT          NOT NULL IDENTITY(1,1),
   UserId           INT          NOT NULL,
   MilestoneId      INT          NULL,
   Milestone        VARCHAR(100) NOT NULL,
   VerificationCode VARCHAR(50)  NOT NULL,
   IssuedAt         DATETIME2(7) NOT NULL DEFAULT SYSDATETIME(),
   FilePath         VARCHAR(500) NULL,
   CONSTRAINT pk_Certificates         PRIMARY KEY (CertificateId),
   CONSTRAINT uq_Certificates_VerCode UNIQUE (VerificationCode),
   CONSTRAINT fk_Cert_User            FOREIGN KEY (UserId)      REFERENCES Users(UserId),
   CONSTRAINT fk_Cert_Milestone       FOREIGN KEY (MilestoneId) REFERENCES MilestoneDefinitions(MilestoneId)
   -- P7 fix: filtered unique indexes in INDEXES section handle nullable MilestoneId correctly.
);
GO

-- ============================================================
-- SECTION 4: JUNCTION / FEATURE TABLES
-- ============================================================

-- F14: PreferredLocalityId FK replaces PreferredLocality VARCHAR.
CREATE TABLE UserInterests (
   UserInterestId      INT          NOT NULL IDENTITY(1,1),
   UserId              INT          NOT NULL,
   CategoryId          SMALLINT     NULL,
   PreferredLocalityId INT          NULL,          -- F14
   CreatedAt           DATETIME2(7) NOT NULL DEFAULT SYSDATETIME(),
   CONSTRAINT pk_UserInterests  PRIMARY KEY (UserInterestId),
   CONSTRAINT fk_UI_User        FOREIGN KEY (UserId)              REFERENCES Users(UserId),
   CONSTRAINT fk_UI_Category    FOREIGN KEY (CategoryId)          REFERENCES IssueCategories(CategoryId),
   CONSTRAINT fk_UI_Locality    FOREIGN KEY (PreferredLocalityId) REFERENCES Localities(LocalityId),
   CONSTRAINT chk_UI_HasSignal  CHECK (CategoryId IS NOT NULL OR PreferredLocalityId IS NOT NULL)
   -- Filtered unique indexes for (UserId,CategoryId) and (UserId,PreferredLocalityId): see INDEXES.
);
GO

-- Priority: tie-breaker when multiple depts serve same category+locality.
CREATE TABLE DepartmentCategories (
   DeptCategoryId INT          NOT NULL IDENTITY(1,1),
   DeptId         INT          NOT NULL,
   CategoryId     SMALLINT     NOT NULL,
   IsPrimary      BIT          NOT NULL DEFAULT 0,
   Priority       TINYINT      NOT NULL DEFAULT 10,
   CreatedAt      DATETIME2(7) NOT NULL DEFAULT SYSDATETIME(),
   CONSTRAINT pk_DeptCategories PRIMARY KEY (DeptCategoryId),
   CONSTRAINT uq_DeptCat_Pair   UNIQUE (DeptId, CategoryId),
   CONSTRAINT fk_DC_Dept        FOREIGN KEY (DeptId)     REFERENCES Departments(DeptId),
   CONSTRAINT fk_DC_Category    FOREIGN KEY (CategoryId) REFERENCES IssueCategories(CategoryId)
);
GO

CREATE TABLE DuplicateComplaintLinks (
   LinkId              INT          NOT NULL IDENTITY(1,1),
   OriginalComplaintId INT          NOT NULL,
   LinkedComplaintId   INT          NOT NULL,
   LinkedByUserId      INT          NOT NULL,
   LinkedAt            DATETIME2(7) NOT NULL DEFAULT SYSDATETIME(),
   CONSTRAINT pk_DupLinks         PRIMARY KEY (LinkId),
   CONSTRAINT uq_DupLink_Pair     UNIQUE (OriginalComplaintId, LinkedComplaintId),
   CONSTRAINT fk_DupLink_Original FOREIGN KEY (OriginalComplaintId) REFERENCES Complaints(ComplaintId),
   CONSTRAINT fk_DupLink_Linked   FOREIGN KEY (LinkedComplaintId)   REFERENCES Complaints(ComplaintId),
   CONSTRAINT fk_DupLink_User     FOREIGN KEY (LinkedByUserId)      REFERENCES Users(UserId),
   CONSTRAINT chk_DupLink_NoSelf  CHECK (OriginalComplaintId <> LinkedComplaintId)
);
GO

-- ============================================================
-- SECTION 5: ML / ANALYTICS TABLES
-- ============================================================

CREATE TABLE ComplaintMLScores (
   ScoreId                 INT          NOT NULL IDENTITY(1,1),
   ComplaintId             INT          NOT NULL,
   PredictedResolutionDate DATE         NULL,
   ResolutionProbability   DECIMAL(5,4) NULL,
   PriorityScore           DECIMAL(8,2) NULL,
   PredictionModelVersion  VARCHAR(20)  NULL,
   ScoredAt                DATETIME2(7) NOT NULL DEFAULT SYSDATETIME(),
   UpdatedAt               DATETIME2(7) NOT NULL DEFAULT SYSDATETIME(),
   CONSTRAINT pk_ComplaintMLScores PRIMARY KEY (ScoreId),
   CONSTRAINT uq_ML_Complaint      UNIQUE (ComplaintId),
   CONSTRAINT fk_ML_Complaint      FOREIGN KEY (ComplaintId) REFERENCES Complaints(ComplaintId),
   CONSTRAINT chk_ML_Probability   CHECK (ResolutionProbability IS NULL OR
                                         ResolutionProbability BETWEEN 0.0000 AND 1.0000)
);
GO

-- F1: SOLE photo/file store for all complaint media types.
-- FilePath convention: complaints/{ComplaintId}/{AttachmentType}/{FileName}
CREATE TABLE ComplaintAttachments (
   AttachmentId     INT          NOT NULL IDENTITY(1,1),
   ComplaintId      INT          NOT NULL,
   TimelineId       INT          NULL,
   UploadedByUserId INT          NOT NULL,
   AttachmentType   VARCHAR(30)  NOT NULL,
   FilePath         VARCHAR(500) NOT NULL,
   FileName         VARCHAR(200) NULL,
   FileSizeKB       INT          NULL,
   UploadedAt       DATETIME2(7) NOT NULL DEFAULT SYSDATETIME(),
   CONSTRAINT pk_ComplaintAttachments PRIMARY KEY (AttachmentId),
   CONSTRAINT fk_CA_Complaint         FOREIGN KEY (ComplaintId)      REFERENCES Complaints(ComplaintId),
   CONSTRAINT fk_CA_Timeline          FOREIGN KEY (TimelineId)       REFERENCES ComplaintTimeline(TimelineId),
   CONSTRAINT fk_CA_Uploader          FOREIGN KEY (UploadedByUserId) REFERENCES Users(UserId),
   CONSTRAINT chk_CA_Type             CHECK (AttachmentType IN ('Complaint','Resolution','PWGProgress','Evidence'))
);
GO

-- ============================================================
-- SECTION 6: AUDIT & OPERATIONAL TABLES
-- ============================================================

-- F13: AdminUserId removed; single ActorUserId (NULL = system/auto).
-- F20: DATETIME2(7). OriginalDeptId NOT NULL — F2 guard ensures routed first.
CREATE TABLE EscalationLog (
   EscalationId       INT           NOT NULL IDENTITY(6001,1),
   ComplaintId        INT           NOT NULL,
   EscalationTrigger  VARCHAR(20)   NOT NULL,
   EscalatedAt        DATETIME2(7)  NOT NULL DEFAULT SYSDATETIME(),
   ActorUserId        INT           NULL,          -- F13: NULL=Auto; AdminUserId=Manual
   OriginalDeptId     INT           NOT NULL,      -- NOT NULL: F2 guard prevents escalating unrouted
   ReassignedToDeptId INT           NULL,
   Reason             NVARCHAR(500) NULL,
   ResolvedAt         DATETIME2(7)  NULL,
   CONSTRAINT pk_EscalationLog PRIMARY KEY (EscalationId),
   CONSTRAINT fk_Esc_Complaint FOREIGN KEY (ComplaintId)        REFERENCES Complaints(ComplaintId),
   CONSTRAINT fk_Esc_Actor     FOREIGN KEY (ActorUserId)        REFERENCES Users(UserId),
   CONSTRAINT fk_Esc_OrigDept  FOREIGN KEY (OriginalDeptId)     REFERENCES Departments(DeptId),
   CONSTRAINT fk_Esc_NewDept   FOREIGN KEY (ReassignedToDeptId) REFERENCES Departments(DeptId),
   CONSTRAINT chk_Esc_Trigger  CHECK (EscalationTrigger IN ('Auto','Manual'))
);
GO

CREATE TABLE PWGReports (
   ReportId              INT            NOT NULL IDENTITY(7001,1),
   ComplaintId           INT            NOT NULL,
   ReportedOrgId         INT            NOT NULL,
   ReportedByUserId      INT            NOT NULL,
   ReportReason          NVARCHAR(1000) NOT NULL,
   AdminReviewedByUserId INT            NULL,
   AdminAction           VARCHAR(30)    NULL,
   AdminNote             NVARCHAR(500)  NULL,
   ReportedAt            DATETIME2(7)   NOT NULL DEFAULT SYSDATETIME(),
   ReviewedAt            DATETIME2(7)   NULL,
   ClosedAt              DATETIME2(7)   NULL,
   Status                VARCHAR(20)    NOT NULL DEFAULT 'Pending',
   CONSTRAINT pk_PWGReports       PRIMARY KEY (ReportId),
   CONSTRAINT fk_PWGRep_Complaint FOREIGN KEY (ComplaintId)           REFERENCES Complaints(ComplaintId),
   CONSTRAINT fk_PWGRep_Org       FOREIGN KEY (ReportedOrgId)         REFERENCES Organisations(OrgId),
   CONSTRAINT fk_PWGRep_Reporter  FOREIGN KEY (ReportedByUserId)      REFERENCES Users(UserId),
   CONSTRAINT fk_PWGRep_Admin     FOREIGN KEY (AdminReviewedByUserId) REFERENCES Users(UserId),
   CONSTRAINT chk_PWGRep_Action   CHECK (AdminAction IS NULL OR
                                        AdminAction IN ('Warned','Suspended','Removed','Dismissed')),
   CONSTRAINT chk_PWGRep_Status   CHECK (Status IN ('Pending','Reviewed','Closed'))
);
GO

CREATE TABLE NotificationPreferences (
   PrefId              INT          NOT NULL IDENTITY(1,1),
   UserId              INT          NOT NULL,
   InAppEnabled        BIT          NOT NULL DEFAULT 1,
   PushEnabled         BIT          NOT NULL DEFAULT 1,
   EmailDigestEnabled  BIT          NOT NULL DEFAULT 1,
   DigestFrequencyDays TINYINT      NOT NULL DEFAULT 7,
   UpdatedAt           DATETIME2(7) NOT NULL DEFAULT SYSDATETIME(),
   CONSTRAINT pk_NotifPrefs       PRIMARY KEY (PrefId),
   CONSTRAINT uq_NotifPrefs_User  UNIQUE (UserId),
   CONSTRAINT fk_NotifPrefs_User  FOREIGN KEY (UserId) REFERENCES Users(UserId),
   CONSTRAINT chk_NotifPrefs_Freq CHECK (DigestFrequencyDays > 0)
);
GO

-- F22: ReferenceId (generic) replaced with typed FKs: RefComplaintId + RefMilestoneId.
CREATE TABLE PointsLedger (
   LedgerId       INT          NOT NULL IDENTITY(1,1),
   UserId         INT          NOT NULL,
   PointsDelta    INT          NOT NULL,
   Reason         VARCHAR(100) NOT NULL,
   RefComplaintId INT          NULL,         -- F22: typed FK
   RefMilestoneId INT          NULL,         -- F22: typed FK
   EarnedAt       DATETIME2(7) NOT NULL DEFAULT SYSDATETIME(),
   CONSTRAINT pk_PointsLedger  PRIMARY KEY (LedgerId),
   CONSTRAINT fk_PL_User       FOREIGN KEY (UserId)         REFERENCES Users(UserId),
   CONSTRAINT fk_PL_Complaint  FOREIGN KEY (RefComplaintId) REFERENCES Complaints(ComplaintId),
   CONSTRAINT fk_PL_Milestone  FOREIGN KEY (RefMilestoneId) REFERENCES MilestoneDefinitions(MilestoneId),
   CONSTRAINT chk_PL_Reason    CHECK (Reason IN (
                                   'ComplaintRated','PWGProgressUpdate','ManualAward',
                                   'CertificateMilestone','ComplaintSubmitted','Other'))
);
GO

-- F22: TargetEntityType/Id (generic) replaced with typed FK columns.
-- F7:  UserBanned added to ActionType CHECK.
CREATE TABLE AuditLog (
   AuditId           INT           NOT NULL IDENTITY(8001,1),
   ActorUserId       INT           NOT NULL,
   ActionType        VARCHAR(50)   NOT NULL,
   TargetUserId      INT           NULL,          -- F22
   TargetOrgId       INT           NULL,          -- F22
   TargetDeptId      INT           NULL,          -- F22
   TargetComplaintId INT           NULL,          -- F22
   Reason            NVARCHAR(500) NULL,
   CreatedAt         DATETIME2(7)  NOT NULL DEFAULT SYSDATETIME(),
   CONSTRAINT pk_AuditLog          PRIMARY KEY (AuditId),
   CONSTRAINT fk_Audit_Actor       FOREIGN KEY (ActorUserId)       REFERENCES Users(UserId),
   CONSTRAINT fk_Audit_TargetUser  FOREIGN KEY (TargetUserId)      REFERENCES Users(UserId),
   CONSTRAINT fk_Audit_TargetOrg   FOREIGN KEY (TargetOrgId)       REFERENCES Organisations(OrgId),
   CONSTRAINT fk_Audit_TargetDept  FOREIGN KEY (TargetDeptId)      REFERENCES Departments(DeptId),
   CONSTRAINT fk_Audit_TargetComp  FOREIGN KEY (TargetComplaintId) REFERENCES Complaints(ComplaintId),
   CONSTRAINT chk_Audit_ActionType CHECK (ActionType IN (
                                       'UserDeactivated','UserBanned','UserDeleted',
                                       'SolverApproved','SolverRejected',
                                       'PWGApproved','PWGRejected',
                                       'ComplaintReassigned','PWGReportActioned','AccountAnonymized'))
);
GO

-- F9:  FK to Users added. F14: LocalityId FK replaces Locality VARCHAR.
-- Materialized; refreshed by usp_RefreshScoreboard via MERGE (F12).
CREATE TABLE ScoreboardSnapshot (
   SnapshotId INT          NOT NULL IDENTITY(1,1),
   UserId     INT          NOT NULL,
   FullName   VARCHAR(100) NOT NULL,       -- denormalized for snapshot read performance
   LocalityId INT          NULL,           -- NULL for users with no locality yet
   Points     INT          NOT NULL,
   Rank       INT          NOT NULL,
   SnapshotAt DATETIME2(7) NOT NULL DEFAULT SYSDATETIME(),
   CONSTRAINT pk_ScoreboardSnapshot PRIMARY KEY (SnapshotId),
   CONSTRAINT uq_Snapshot_User      UNIQUE (UserId),
   CONSTRAINT fk_Snap_User          FOREIGN KEY (UserId)     REFERENCES Users(UserId),       -- F9
   CONSTRAINT fk_Snap_Locality      FOREIGN KEY (LocalityId) REFERENCES Localities(LocalityId) -- F14
);
GO
-- FIX-03 (GAP-03 / US12): Daily platform stats snapshot for trending graphs.
-- Populated by usp_SnapshotPlatformStats; schedule via Azure Function / SQL Agent.
CREATE TABLE PlatformStatsSnapshot (
   SnapshotId      INT          NOT NULL IDENTITY(1,1),
   TotalComplaints INT          NOT NULL,
   Submitted       INT          NOT NULL,
   InProgress      INT          NOT NULL,
   Resolved        INT          NOT NULL,
   Rejected        INT          NOT NULL,
   Reopened        INT          NOT NULL,
   Escalated       INT          NOT NULL,
   Linked          INT          NOT NULL,
   ActiveUsers     INT          NOT NULL,
   SnapshotDate    DATE         NOT NULL DEFAULT CAST(SYSDATETIME() AS DATE),
   CreatedAt       DATETIME2(7) NOT NULL DEFAULT SYSDATETIME(),
   CONSTRAINT pk_PSS      PRIMARY KEY (SnapshotId),
   CONSTRAINT uq_PSS_Date UNIQUE (SnapshotDate)
);
GO

-- ============================================================
-- SECTION 7: INDEXES
-- ============================================================

-- Users
CREATE NONCLUSTERED INDEX ix_Users_RoleId     ON Users(RoleId);
CREATE NONCLUSTERED INDEX ix_Users_LocalityId ON Users(LocalityId);
CREATE NONCLUSTERED INDEX ix_Users_IsActive   ON Users(IsActive) INCLUDE (RoleId, LocalityId);
GO

-- Complaints — core single-column
CREATE NONCLUSTERED INDEX ix_Complaints_Citizen     ON Complaints(CitizenUserId);
CREATE NONCLUSTERED INDEX ix_Complaints_Dept        ON Complaints(DeptId);
CREATE NONCLUSTERED INDEX ix_Complaints_Category    ON Complaints(CategoryId);
CREATE NONCLUSTERED INDEX ix_Complaints_Status      ON Complaints(Status);
CREATE NONCLUSTERED INDEX ix_Complaints_LocalityId  ON Complaints(LocalityId);
CREATE NONCLUSTERED INDEX ix_Complaints_SubmittedAt ON Complaints(SubmittedAt DESC);
CREATE NONCLUSTERED INDEX ix_Complaints_LinkedTo    ON Complaints(LinkedToComplaintId)
   WHERE LinkedToComplaintId IS NOT NULL;
GO

-- Complaints — composite covering indexes
CREATE NONCLUSTERED INDEX ix_Complaints_Dept_Status
   ON Complaints(DeptId, Status) INCLUDE (Title, Criticality, SubmittedAt, LocalityId);
CREATE NONCLUSTERED INDEX ix_Complaints_Citizen_Status
   ON Complaints(CitizenUserId, Status) INCLUDE (Title, Criticality, SubmittedAt);
CREATE NONCLUSTERED INDEX ix_Complaints_Locality_Status_Cat
   ON Complaints(LocalityId, Status, CategoryId);
CREATE NONCLUSTERED INDEX ix_Complaints_LatLng
   ON Complaints(Latitude, Longitude)
   WHERE Latitude IS NOT NULL AND Longitude IS NOT NULL;
GO

-- ComplaintTimeline
CREATE NONCLUSTERED INDEX ix_Timeline_Complaint_Date
   ON ComplaintTimeline(ComplaintId, CreatedAt DESC);
GO

-- PWGParticipationRequests
-- F6: Filtered unique index — allows re-application after rejection.
CREATE UNIQUE NONCLUSTERED INDEX uix_PWGReq_OrgComp_Pending
   ON PWGParticipationRequests(ComplaintId, OrgId)
   WHERE Status = 'Pending';
CREATE NONCLUSTERED INDEX ix_PWGReq_Complaint  ON PWGParticipationRequests(ComplaintId);
CREATE NONCLUSTERED INDEX ix_PWGReq_Org        ON PWGParticipationRequests(OrgId);
CREATE NONCLUSTERED INDEX ix_PWGReq_Org_Status ON PWGParticipationRequests(OrgId, Status);
GO

-- ComplaintRatings — F15
CREATE NONCLUSTERED INDEX ix_Rating_Complaint ON ComplaintRatings(ComplaintId);
GO

-- Contributions — F5 + F15
CREATE NONCLUSTERED INDEX ix_Contrib_Complaint_Status
   ON Contributions(ComplaintId, PaymentStatus) INCLUDE (Amount); -- covering for fn_GetComplaintFunding
GO

-- Notifications
CREATE NONCLUSTERED INDEX ix_Notif_User           ON Notifications(UserId, IsRead);
CREATE NONCLUSTERED INDEX ix_Notif_User_CreatedAt ON Notifications(UserId, CreatedAt DESC);
CREATE NONCLUSTERED INDEX ix_Notif_Unread
   ON Notifications(UserId, ReadAt) WHERE ReadAt IS NULL;
GO

-- UserPoints
CREATE NONCLUSTERED INDEX ix_Points_Points ON UserPoints(Points DESC);
GO

-- Departments
CREATE NONCLUSTERED INDEX ix_Dept_Category   ON Departments(CategoryId);
CREATE NONCLUSTERED INDEX ix_Dept_LocalityId ON Departments(LocalityId);
GO

-- UserInterests
-- F6-style fix: filtered unique indexes handle nullable CategoryId and PreferredLocalityId.
CREATE NONCLUSTERED INDEX ix_UI_UserId ON UserInterests(UserId);
CREATE UNIQUE NONCLUSTERED INDEX uix_UI_UserCategory
   ON UserInterests(UserId, CategoryId)
   WHERE CategoryId IS NOT NULL;
CREATE UNIQUE NONCLUSTERED INDEX uix_UI_UserLocality
   ON UserInterests(UserId, PreferredLocalityId)
   WHERE PreferredLocalityId IS NOT NULL;
GO

-- DepartmentCategories
CREATE NONCLUSTERED INDEX ix_DC_Category ON DepartmentCategories(CategoryId, DeptId);
CREATE NONCLUSTERED INDEX ix_DC_Priority ON DepartmentCategories(DeptId, IsPrimary DESC, Priority ASC);
GO

-- DuplicateComplaintLinks
CREATE NONCLUSTERED INDEX ix_DupLink_Original ON DuplicateComplaintLinks(OriginalComplaintId);
CREATE NONCLUSTERED INDEX ix_DupLink_Linked   ON DuplicateComplaintLinks(LinkedComplaintId);
GO

-- ComplaintMLScores
CREATE NONCLUSTERED INDEX ix_ML_ScoredAt ON ComplaintMLScores(ComplaintId, ScoredAt DESC);
GO

-- ComplaintAttachments
CREATE NONCLUSTERED INDEX ix_CA_Complaint ON ComplaintAttachments(ComplaintId, AttachmentType);
GO

-- EscalationLog
CREATE NONCLUSTERED INDEX ix_Esc_Complaint ON EscalationLog(ComplaintId, EscalatedAt DESC);
CREATE UNIQUE NONCLUSTERED INDEX uix_Esc_ComplaintActive          -- allows re-escalation after resolution
   ON EscalationLog(ComplaintId, EscalationTrigger)
   WHERE ResolvedAt IS NULL;
CREATE NONCLUSTERED INDEX ix_Esc_Open                              -- F15: open escalation lookup
   ON EscalationLog(ComplaintId) WHERE ResolvedAt IS NULL;
GO

-- PWGReports — F15
CREATE NONCLUSTERED INDEX ix_PWGRep_Status    ON PWGReports(Status, ReportedAt DESC);
CREATE NONCLUSTERED INDEX ix_PWGRep_Complaint ON PWGReports(ComplaintId);           -- F15
GO

-- MilestoneDefinitions
CREATE NONCLUSTERED INDEX ix_Milestone_Active
   ON MilestoneDefinitions(PointsThreshold) WHERE IsActive = 1;
GO

-- PointsLedger — F15 / F22
CREATE NONCLUSTERED INDEX ix_PL_User_EarnedAt  ON PointsLedger(UserId, EarnedAt DESC);
CREATE NONCLUSTERED INDEX ix_PL_RefComplaintId ON PointsLedger(RefComplaintId)
   WHERE RefComplaintId IS NOT NULL;
CREATE NONCLUSTERED INDEX ix_PL_RefMilestoneId ON PointsLedger(RefMilestoneId)
   WHERE RefMilestoneId IS NOT NULL;
GO

-- AuditLog — F15 / F22
CREATE NONCLUSTERED INDEX ix_Audit_ActionType_Date ON AuditLog(ActionType, CreatedAt DESC);
CREATE NONCLUSTERED INDEX ix_Audit_TargetUser      ON AuditLog(TargetUserId)
   WHERE TargetUserId IS NOT NULL;
CREATE NONCLUSTERED INDEX ix_Audit_TargetOrg       ON AuditLog(TargetOrgId)
   WHERE TargetOrgId IS NOT NULL;
CREATE NONCLUSTERED INDEX ix_Audit_TargetComp      ON AuditLog(TargetComplaintId)
   WHERE TargetComplaintId IS NOT NULL;
GO

-- NotificationPreferences
CREATE NONCLUSTERED INDEX ix_NotifPref_User ON NotificationPreferences(UserId);
GO

-- Certificates — F15 / P7 fix: filtered unique indexes for nullable MilestoneId.
CREATE NONCLUSTERED INDEX ix_Cert_User ON Certificates(UserId);
CREATE UNIQUE NONCLUSTERED INDEX uix_Cert_UserMilestone         -- prevents duplicate milestone certs
   ON Certificates(UserId, MilestoneId)
   WHERE MilestoneId IS NOT NULL;
CREATE UNIQUE NONCLUSTERED INDEX uix_Cert_UserMilestoneText     -- prevents duplicate free-text certs
   ON Certificates(UserId, Milestone)
   WHERE MilestoneId IS NULL;
GO

-- PasswordResetTokens — F16
CREATE NONCLUSTERED INDEX ix_PRT_Token
   ON PasswordResetTokens(TokenHash) WHERE IsUsed = 0;
CREATE NONCLUSTERED INDEX ix_PRT_User
   ON PasswordResetTokens(UserId, ExpiresAt DESC) WHERE IsUsed = 0;
GO

-- ============================================================
-- SECTION 8: FULL-TEXT SEARCH
-- ============================================================

IF FULLTEXTSERVICEPROPERTY('IsFulltextInstalled') = 1
BEGIN
   IF NOT EXISTS (SELECT 1 FROM sys.fulltext_catalogs WHERE name = 'FixMyCityFTCatalog')
       EXEC('CREATE FULLTEXT CATALOG FixMyCityFTCatalog AS DEFAULT');
END
ELSE
   PRINT 'INFO: Full-text search not installed — FT catalog skipped (non-fatal).';
GO

IF FULLTEXTSERVICEPROPERTY('IsFulltextInstalled') = 1
BEGIN
   IF NOT EXISTS (
       SELECT 1 FROM sys.fulltext_indexes
       WHERE  object_id = OBJECT_ID('dbo.Complaints'))
   BEGIN
       EXEC('CREATE FULLTEXT INDEX ON dbo.Complaints
                 (Title       LANGUAGE 1033,
                  Description LANGUAGE 1033)
             KEY INDEX pk_Complaints
             ON FixMyCityFTCatalog
             WITH CHANGE_TRACKING AUTO');
   END
END
ELSE
   PRINT 'INFO: Full-text search not installed — FT index skipped (non-fatal).';
GO

-- ============================================================
-- SECTION 9: ROW-LEVEL SECURITY
-- F19: Replaced IS_MEMBER('db_owner') with SESSION_CONTEXT(N'UserRole').
-- F3:  Secure default — connections with no UserRole set see ZERO rows.
-- ============================================================

CREATE FUNCTION dbo.fn_RLS_ComplaintFilter(@DeptId INT)
RETURNS TABLE WITH SCHEMABINDING
AS
RETURN
   SELECT 1 AS fn_result
   WHERE
       -- SuperAdmin sees all complaints
       CAST(SESSION_CONTEXT(N'UserRole') AS NVARCHAR(30)) = N'SuperAdmin'
       -- Citizens and PWGs see all complaints (public feed)
       OR CAST(SESSION_CONTEXT(N'UserRole') AS NVARCHAR(30)) IN (N'Citizen', N'PWG')
       -- Solvers see only their department's complaints; unrouted (NULL DeptId) are not visible to them
       OR (
           CAST(SESSION_CONTEXT(N'UserRole') AS NVARCHAR(30)) = N'Solver'
           AND CAST(SESSION_CONTEXT(N'DeptId') AS INT) = @DeptId
       );
GO

CREATE SECURITY POLICY ComplaintRLS
   ADD FILTER PREDICATE dbo.fn_RLS_ComplaintFilter(DeptId) ON dbo.Complaints
   WITH (STATE = ON);
GO

-- ============================================================
-- SECTION 10: DATABASE ROLES
-- ============================================================

CREATE ROLE app_exec;
CREATE ROLE app_read;
GO
GRANT EXECUTE ON SCHEMA::dbo TO app_exec;
GRANT SELECT ON SCHEMA::dbo TO app_read;
GO

-- ============================================================
-- SECTION 11: FUNCTIONS
-- ============================================================

-- US04: Validates password-auth login. Returns RoleId (>0) or 0.
-- F7:   IsBanned = 0 check added.
-- SECURITY NOTE: Password comparison happens in SQL (dev only).
-- Production MUST hash+salt at app layer (bcrypt/Argon2/PBKDF2).
CREATE OR ALTER FUNCTION fn_ValidateLogin
(
   @Email        VARCHAR(150),
   @PasswordHash VARCHAR(256)
)
RETURNS TINYINT
AS
BEGIN
   DECLARE @RoleId TINYINT = 0;
   SELECT @RoleId = r.RoleId
   FROM   Users u
   JOIN   Roles r ON r.RoleId = u.RoleId
   WHERE  u.Email        = @Email
     AND  u.PasswordHash IS NOT NULL
     AND  u.PasswordHash = @PasswordHash
     AND  u.IsActive     = 1
     AND  u.IsBanned     = 0                                            -- F7
     AND  u.IsSuspended  = 0                                            -- FIX-04: suspended users cannot login
     AND  (u.LockoutUntil IS NULL OR u.LockoutUntil < SYSDATETIME())
     AND  (r.RoleName IN ('Citizen','SuperAdmin') OR u.IsApproved = 1);
   RETURN ISNULL(@RoleId, 0);
END
GO

-- US56: Total successful contributions for a complaint.
CREATE OR ALTER FUNCTION fn_GetComplaintFunding(@ComplaintId INT)
RETURNS DECIMAL(10,2)
AS
BEGIN
   DECLARE @Total DECIMAL(10,2) = 0;
   SELECT @Total = ISNULL(SUM(Amount), 0)
   FROM   Contributions
   WHERE  ComplaintId = @ComplaintId AND PaymentStatus = 'Success';
   RETURN @Total;
END
GO

-- US19 guard: has a citizen already rated this complaint?
CREATE OR ALTER FUNCTION fn_HasCitizenRated
(
   @ComplaintId   INT,
   @CitizenUserId INT
)
RETURNS BIT
AS
BEGIN
   DECLARE @Exists BIT = 0;
   IF EXISTS (SELECT 1 FROM ComplaintRatings
              WHERE ComplaintId = @ComplaintId AND CitizenUserId = @CitizenUserId)
       SET @Exists = 1;
   RETURN @Exists;
END
GO
-- ============================================================
-- SECTION 12: STORED PROCEDURES
-- ============================================================

-- ─── REGISTRATION ───────────────────────────────────────────

-- US01: Citizen registration. F14: @LocalityId replaces @Locality.
CREATE OR ALTER PROCEDURE usp_RegisterCitizen
   @FullName     VARCHAR(100),
   @Email        VARCHAR(150),
   @PasswordHash VARCHAR(256),
   @Phone        VARCHAR(15),
   @Address      VARCHAR(300),
   @LocalityId   INT,
   @AadhaarNo    VARCHAR(12),
   @NewUserId    INT OUTPUT
AS
BEGIN
   SET NOCOUNT ON;
   BEGIN TRANSACTION;
   BEGIN TRY
       DECLARE @RoleId TINYINT;
       SELECT @RoleId = RoleId FROM Roles WHERE RoleName = 'Citizen';
       INSERT INTO Users
           (RoleId, FullName, Email, PasswordHash, Phone, Address, LocalityId, AadhaarNo, IsActive, IsApproved)
       VALUES
           (@RoleId, @FullName, @Email, @PasswordHash, @Phone, @Address, @LocalityId, @AadhaarNo, 1, 1);
       SET @NewUserId = SCOPE_IDENTITY();
       INSERT INTO NotificationPreferences (UserId) VALUES (@NewUserId);
       COMMIT TRANSACTION;
   END TRY
   BEGIN CATCH ROLLBACK TRANSACTION; THROW; END CATCH
END
GO

-- US05: SSO login or create citizen account. LocalityId NULL until profile completion.
CREATE OR ALTER PROCEDURE usp_SSOLoginOrCreate
   @SSOProvider   VARCHAR(30),
   @SSOExternalId VARCHAR(200),
   @Email         VARCHAR(150),
   @FullName      VARCHAR(100),
   @Phone         VARCHAR(15) = '0000000000',
   @UserId        INT OUTPUT,
   @RoleId        TINYINT OUTPUT
AS
BEGIN
   SET NOCOUNT ON;
   SET @UserId = NULL; SET @RoleId = NULL;

   -- 1. Existing SSO user
   SELECT @UserId = UserId, @RoleId = RoleId
   FROM   Users
   WHERE  SSOProvider = @SSOProvider AND SSOExternalId = @SSOExternalId AND IsActive = 1;
   IF @UserId IS NOT NULL RETURN;

   -- 2. Link SSO to existing email account
   SELECT @UserId = UserId, @RoleId = RoleId
   FROM   Users WHERE Email = @Email AND IsActive = 1;
   IF @UserId IS NOT NULL
   BEGIN
       UPDATE Users
       SET SSOProvider = @SSOProvider, SSOExternalId = @SSOExternalId, UpdatedAt = SYSDATETIME()
       WHERE UserId = @UserId;
       RETURN;
   END

   -- 3. Create new citizen via SSO
   BEGIN TRANSACTION;
   BEGIN TRY
       DECLARE @CitizenRoleId TINYINT;
       SELECT @CitizenRoleId = RoleId FROM Roles WHERE RoleName = 'Citizen';
       INSERT INTO Users
           (RoleId, FullName, Email, Phone, Address, LocalityId,
            SSOProvider, SSOExternalId, IsActive, IsApproved)
       VALUES
           (@CitizenRoleId, @FullName, @Email, @Phone, 'Not Provided', NULL,
            @SSOProvider, @SSOExternalId, 1, 1);
       SET @UserId = SCOPE_IDENTITY();
       SET @RoleId = @CitizenRoleId;
       INSERT INTO NotificationPreferences (UserId) VALUES (@UserId);
       COMMIT TRANSACTION;
   END TRY
   BEGIN CATCH ROLLBACK TRANSACTION; THROW; END CATCH
END
GO

-- US02: Organisation (PWG) registration. F14: @LocalityId.
CREATE OR ALTER PROCEDURE usp_RegisterOrganisation
   @FullName       VARCHAR(100),
   @Email          VARCHAR(150),
   @PasswordHash   VARCHAR(256),
   @Phone          VARCHAR(15),
   @Address        VARCHAR(300),
   @LocalityId     INT,
   @OrgName        VARCHAR(150),
   @OrgType        VARCHAR(50),
   @RegistrationNo VARCHAR(50),
   @ContactEmail   VARCHAR(150),
   @ContactPhone   VARCHAR(15),
   @UserId         INT OUTPUT
AS
BEGIN
   SET NOCOUNT ON;
   BEGIN TRANSACTION;
   BEGIN TRY
       DECLARE @RoleId TINYINT;
       SELECT @RoleId = RoleId FROM Roles WHERE RoleName = 'PWG';
       INSERT INTO Users
           (RoleId, FullName, Email, PasswordHash, Phone, Address, LocalityId, IsActive, IsApproved)
       VALUES (@RoleId, @FullName, @Email, @PasswordHash, @Phone, @Address, @LocalityId, 1, 0);
       SET @UserId = SCOPE_IDENTITY();
       INSERT INTO Organisations
           (UserId, OrgName, OrgType, RegistrationNo, ContactEmail, ContactPhone, Address, ApprovalStatus)
       VALUES (@UserId, @OrgName, @OrgType, @RegistrationNo, @ContactEmail, @ContactPhone, @Address, 'Pending');
       INSERT INTO NotificationPreferences (UserId) VALUES (@UserId);
       -- Notify all SuperAdmins
       INSERT INTO Notifications (UserId, Message, NotificationType, Channel)
       SELECT u.UserId,
              'New PWG registration pending approval: ' + @OrgName + '.',
              'Registration', 'InApp'
       FROM   Users u JOIN Roles r ON r.RoleId = u.RoleId
       WHERE  r.RoleName = 'SuperAdmin' AND u.IsActive = 1;
       COMMIT TRANSACTION;
   END TRY
   BEGIN CATCH ROLLBACK TRANSACTION; THROW; END CATCH
END
GO

-- US03: Department (Solver) registration. F14: @LocalityId.
CREATE OR ALTER PROCEDURE usp_RegisterDepartment
   @FullName     VARCHAR(100),
   @Email        VARCHAR(150),
   @PasswordHash VARCHAR(256),
   @Phone        VARCHAR(15),
   @Address      VARCHAR(300),
   @LocalityId   INT,
   @DeptName     VARCHAR(150),
   @Ministry     VARCHAR(100),
   @CategoryId   SMALLINT,
   @ContactEmail VARCHAR(150),
   @ContactPhone VARCHAR(15),
   @UserId       INT OUTPUT
AS
BEGIN
   SET NOCOUNT ON;
   BEGIN TRANSACTION;
   BEGIN TRY
       DECLARE @RoleId TINYINT;
       SELECT @RoleId = RoleId FROM Roles WHERE RoleName = 'Solver';
       INSERT INTO Users
           (RoleId, FullName, Email, PasswordHash, Phone, Address, LocalityId, IsActive, IsApproved)
       VALUES (@RoleId, @FullName, @Email, @PasswordHash, @Phone, @Address, @LocalityId, 1, 0);
       SET @UserId = SCOPE_IDENTITY();
       INSERT INTO Departments
           (UserId, DeptName, Ministry, CategoryId, ContactEmail, ContactPhone, Address, LocalityId, ApprovalStatus)
       VALUES (@UserId, @DeptName, @Ministry, @CategoryId, @ContactEmail, @ContactPhone, @Address, @LocalityId, 'Pending');
       INSERT INTO NotificationPreferences (UserId) VALUES (@UserId);
       -- Notify all SuperAdmins
       INSERT INTO Notifications (UserId, Message, NotificationType, Channel)
       SELECT u.UserId,
              'New department registration pending approval: ' + @DeptName + '.',
              'Registration', 'InApp'
       FROM   Users u JOIN Roles r ON r.RoleId = u.RoleId
       WHERE  r.RoleName = 'SuperAdmin' AND u.IsActive = 1;
       COMMIT TRANSACTION;
   END TRY
   BEGIN CATCH ROLLBACK TRANSACTION; THROW; END CATCH
END
GO

-- US10 / US11: Admin approve or reject Solver / PWG. F22: typed AuditLog FKs.
CREATE OR ALTER PROCEDURE usp_DecideRegistration
   @UserId      INT,
   @Decision    VARCHAR(10),
   @AdminUserId INT
AS
BEGIN
   SET NOCOUNT ON;
   BEGIN TRANSACTION;
   BEGIN TRY
       UPDATE Users
       SET IsApproved = CASE WHEN @Decision = 'Approved' THEN 1 ELSE 0 END, UpdatedAt = SYSDATETIME()
       WHERE UserId = @UserId;

       UPDATE Organisations
       SET ApprovalStatus = @Decision,
           ApprovedAt = CASE WHEN @Decision = 'Approved' THEN SYSDATETIME() ELSE NULL END,
           UpdatedAt  = SYSDATETIME()
       WHERE UserId = @UserId;

       UPDATE Departments
       SET ApprovalStatus = @Decision,
           ApprovedAt = CASE WHEN @Decision = 'Approved' THEN SYSDATETIME() ELSE NULL END,
           UpdatedAt  = SYSDATETIME()
       WHERE UserId = @UserId;

       INSERT INTO Notifications (UserId, Message, NotificationType, Channel)
       VALUES (@UserId,
               'Your registration has been ' + @Decision + ' by the Super Admin.',
               'Registration', 'InApp');

       DECLARE @IsOrg BIT = 0;
       IF EXISTS (SELECT 1 FROM Organisations WHERE UserId = @UserId) SET @IsOrg = 1;

       DECLARE @ActionType VARCHAR(50) = CASE
           WHEN @IsOrg = 1 AND @Decision = 'Approved' THEN 'PWGApproved'
           WHEN @IsOrg = 1                            THEN 'PWGRejected'
           WHEN @Decision = 'Approved'                THEN 'SolverApproved'
           ELSE                                            'SolverRejected' END;

       IF @IsOrg = 1
           INSERT INTO AuditLog (ActorUserId, ActionType, TargetUserId, TargetOrgId, Reason)
           SELECT @AdminUserId, @ActionType, @UserId, OrgId,
                  'Registration ' + @Decision + ' by admin.'
           FROM   Organisations WHERE UserId = @UserId;
       ELSE
           INSERT INTO AuditLog (ActorUserId, ActionType, TargetUserId, TargetDeptId, Reason)
           SELECT @AdminUserId, @ActionType, @UserId, DeptId,
                  'Registration ' + @Decision + ' by admin.'
           FROM   Departments WHERE UserId = @UserId;

       COMMIT TRANSACTION;
   END TRY
   BEGIN CATCH ROLLBACK TRANSACTION; THROW; END CATCH
END
GO

-- ─── USER ACCOUNT ───────────────────────────────────────────

-- US04: Record failed login; lock account after 5 consecutive failures.
CREATE OR ALTER PROCEDURE usp_RecordFailedLogin
   @Email VARCHAR(150)
AS
BEGIN
   SET NOCOUNT ON;
   DECLARE @UserId INT, @Attempts TINYINT;
   SELECT @UserId = UserId, @Attempts = FailedLoginAttempts
   FROM   Users WHERE Email = @Email AND IsActive = 1;
   IF @UserId IS NULL RETURN;
   SET @Attempts = ISNULL(@Attempts, 0) + 1;
   IF @Attempts >= 5
       UPDATE Users
       SET FailedLoginAttempts = @Attempts,
           LockoutUntil = DATEADD(MINUTE, 30, SYSDATETIME()), UpdatedAt = SYSDATETIME()
       WHERE UserId = @UserId;
   ELSE
       UPDATE Users
       SET FailedLoginAttempts = @Attempts, UpdatedAt = SYSDATETIME()
       WHERE UserId = @UserId;
END
GO

-- US04: Reset failed login counter after successful authentication.
CREATE OR ALTER PROCEDURE usp_ResetLoginAttempts
   @UserId INT
AS
BEGIN
   SET NOCOUNT ON;
   UPDATE Users SET FailedLoginAttempts = 0, LockoutUntil = NULL, UpdatedAt = SYSDATETIME()
   WHERE UserId = @UserId;
END
GO

-- US08: Update profile. F14: @LocalityId replaces @Locality.
CREATE OR ALTER PROCEDURE usp_UpdateProfile
   @UserId     INT,
   @FullName   VARCHAR(100),
   @Phone      VARCHAR(15),
   @Address    VARCHAR(300),
   @LocalityId INT
AS
BEGIN
   SET NOCOUNT ON;
   UPDATE Users
   SET FullName = @FullName, Phone = @Phone, Address = @Address,
       LocalityId = @LocalityId, UpdatedAt = SYSDATETIME()
   WHERE UserId = @UserId;
END
GO

-- US07: Change password. F8: validates old password before update.
CREATE OR ALTER PROCEDURE usp_ChangePassword
   @UserId          INT,
   @OldPasswordHash VARCHAR(256),
   @NewPasswordHash VARCHAR(256),
   @IsSuccess       BIT OUTPUT
AS
BEGIN
   SET NOCOUNT ON;
   SET @IsSuccess = 0;
   -- F8: Verify current password matches before allowing update.
   IF NOT EXISTS (
       SELECT 1 FROM Users
       WHERE UserId = @UserId AND PasswordHash = @OldPasswordHash AND IsActive = 1)
       RETURN;   -- old password incorrect; @IsSuccess remains 0

   UPDATE Users
   SET PasswordHash = @NewPasswordHash, UpdatedAt = SYSDATETIME()
   WHERE UserId = @UserId;
   SET @IsSuccess = 1;
END
GO

-- US09: Anonymize / GDPR-delete user account in-place. F22: typed AuditLog FKs.
CREATE OR ALTER PROCEDURE usp_AnonymizeUser
   @TargetUserId INT,
   @AdminUserId  INT = NULL    -- NULL = self-requested deletion
AS
BEGIN
   SET NOCOUNT ON;
   DECLARE @ActorId INT = ISNULL(@AdminUserId, @TargetUserId);
   BEGIN TRANSACTION;
   BEGIN TRY
       INSERT INTO AuditLog (ActorUserId, ActionType, TargetUserId, Reason)
       VALUES (@ActorId, 'AccountAnonymized', @TargetUserId,
               'Account deletion requested. PII fields anonymized per US09.');

       UPDATE Users
       SET FullName      = 'Deleted User #' + CAST(@TargetUserId AS VARCHAR),
           Email         = 'deleted_' + CAST(@TargetUserId AS VARCHAR) + '@anonymized.invalid',
           PasswordHash  = NULL, SSOProvider = NULL, SSOExternalId = NULL,
           Phone         = '0000000000', Address = 'Anonymized',
           LocalityId    = NULL, AadhaarNo = NULL,
           IsActive      = 0, BanReason = NULL, BannedAt = NULL,
           UpdatedAt     = SYSDATETIME()
       WHERE UserId = @TargetUserId;
       COMMIT TRANSACTION;
   END TRY
   BEGIN CATCH ROLLBACK TRANSACTION; THROW; END CATCH
END
GO

-- US13: Deactivate OR ban a user. F7: @IsBan parameter; F22: typed AuditLog FKs.
CREATE OR ALTER PROCEDURE usp_DeactivateUser
   @TargetUserId INT,
   @Reason       NVARCHAR(300),
   @AdminUserId  INT = NULL,
   @IsBan        BIT = 0          -- F7: 1 = ban (permanent), 0 = deactivate
AS
BEGIN
   SET NOCOUNT ON;
   BEGIN TRANSACTION;
   BEGIN TRY
       UPDATE Users
       SET IsActive  = 0,
           IsBanned  = @IsBan,
           BanReason = CASE WHEN @IsBan = 1 THEN @Reason ELSE NULL END,
           BannedAt  = CASE WHEN @IsBan = 1 THEN SYSDATETIME() ELSE NULL END,
           UpdatedAt = SYSDATETIME()
       WHERE UserId = @TargetUserId;

       INSERT INTO Notifications (UserId, Message, NotificationType, Channel)
       VALUES (@TargetUserId,
               CASE WHEN @IsBan = 1
                    THEN 'Your account has been banned. Reason: ' + ISNULL(@Reason, 'Not specified') + '.'
                    ELSE 'Your account has been deactivated. Reason: ' + ISNULL(@Reason, 'Not specified') + '.'
               END, 'Registration', 'InApp');

       IF @AdminUserId IS NOT NULL
           INSERT INTO AuditLog (ActorUserId, ActionType, TargetUserId, Reason)
           VALUES (@AdminUserId,
                   CASE WHEN @IsBan = 1 THEN 'UserBanned' ELSE 'UserDeactivated' END,
                   @TargetUserId, @Reason);
       COMMIT TRANSACTION;
   END TRY
   BEGIN CATCH ROLLBACK TRANSACTION; THROW; END CATCH
END
GO

-- F16: Initiate password reset — app generates token, passes its SHA-256 hash.
CREATE OR ALTER PROCEDURE usp_RequestPasswordReset
   @Email     VARCHAR(150),
   @TokenHash VARCHAR(256),
   @ExpiresAt DATETIME2(7),
   @IsSuccess BIT OUTPUT
AS
BEGIN
   SET NOCOUNT ON;
   SET @IsSuccess = 0;
   DECLARE @UserId INT;
   -- SSO-only users (no PasswordHash) cannot use password reset. Silent fail prevents user enumeration.
   SELECT @UserId = UserId
   FROM   Users WHERE Email = @Email AND IsActive = 1 AND PasswordHash IS NOT NULL;
   IF @UserId IS NULL RETURN;

   BEGIN TRANSACTION;
   BEGIN TRY
       -- Invalidate any existing live tokens for this user.
       UPDATE PasswordResetTokens SET IsUsed = 1 WHERE UserId = @UserId AND IsUsed = 0;
       INSERT INTO PasswordResetTokens (UserId, TokenHash, ExpiresAt)
       VALUES (@UserId, @TokenHash, @ExpiresAt);
       COMMIT TRANSACTION;
       SET @IsSuccess = 1;
   END TRY
   BEGIN CATCH ROLLBACK TRANSACTION; SET @IsSuccess = 0; THROW; END CATCH
END
GO

-- F16: Consume reset token and set new password.
CREATE OR ALTER PROCEDURE usp_ResetPassword
   @TokenHash       VARCHAR(256),
   @NewPasswordHash VARCHAR(256),
   @IsSuccess       BIT OUTPUT
AS
BEGIN
   SET NOCOUNT ON;
   SET @IsSuccess = 0;
   DECLARE @UserId INT;
   SELECT @UserId = UserId
   FROM   PasswordResetTokens
   WHERE  TokenHash = @TokenHash AND IsUsed = 0 AND ExpiresAt > SYSDATETIME();
   IF @UserId IS NULL RETURN;

   BEGIN TRANSACTION;
   BEGIN TRY
       UPDATE PasswordResetTokens
       SET IsUsed = 1, UsedAt = SYSDATETIME()
       WHERE TokenHash = @TokenHash;

       UPDATE Users
       SET PasswordHash = @NewPasswordHash, FailedLoginAttempts = 0,
           LockoutUntil = NULL, UpdatedAt = SYSDATETIME()
       WHERE UserId = @UserId;
       COMMIT TRANSACTION;
       SET @IsSuccess = 1;
   END TRY
   BEGIN CATCH ROLLBACK TRANSACTION; SET @IsSuccess = 0; THROW; END CATCH
END
GO

-- ─── COMPLAINT LIFECYCLE ────────────────────────────────────

-- US14 / US48: Submit complaint with primary + fallback auto-routing.
-- F1:  Photo written to ComplaintAttachments (not Complaints.ImagePath).
-- F14: @LocalityId replaces @Locality.
CREATE OR ALTER PROCEDURE usp_SubmitComplaint
   @CitizenUserId INT,
   @CategoryId    SMALLINT,
   @Title         VARCHAR(200),
   @Description   NVARCHAR(2000),
   @LocalityId    INT,
   @Address       VARCHAR(300),
   @Criticality   VARCHAR(10),
   @ComplaintId   INT OUTPUT,
   @Latitude      DECIMAL(10,7) = NULL,
   @Longitude     DECIMAL(10,7) = NULL,
   @FilePath      VARCHAR(500)  = NULL,   -- F1: optional submission photo
   @FileName      VARCHAR(200)  = NULL,
   @FileSizeKB    INT           = NULL
AS
BEGIN
   SET NOCOUNT ON;
   DECLARE @DeptId INT;

   -- Primary routing: match category + locality on Departments directly
   SELECT TOP 1 @DeptId = d.DeptId
   FROM   Departments d
   WHERE  d.CategoryId = @CategoryId AND d.LocalityId = @LocalityId AND d.ApprovalStatus = 'Approved'
   ORDER  BY d.DeptId;

   -- Fallback routing: DepartmentCategories junction table
   IF @DeptId IS NULL
   BEGIN
       SELECT TOP 1 @DeptId = dc.DeptId
       FROM   DepartmentCategories dc
       JOIN   Departments d ON d.DeptId = dc.DeptId
       WHERE  dc.CategoryId = @CategoryId AND d.LocalityId = @LocalityId AND d.ApprovalStatus = 'Approved'
       ORDER  BY dc.IsPrimary DESC, dc.Priority ASC, dc.DeptId;
   END

   BEGIN TRANSACTION;
   BEGIN TRY
       INSERT INTO Complaints
           (CitizenUserId, DeptId, CategoryId, Title, Description,
            LocalityId, Address, Criticality, Status, Latitude, Longitude)
       VALUES
           (@CitizenUserId, @DeptId, @CategoryId, @Title, @Description,
            @LocalityId, @Address, @Criticality, 'Submitted', @Latitude, @Longitude);
       SET @ComplaintId = SCOPE_IDENTITY();

       INSERT INTO ComplaintTimeline (ComplaintId, ActorUserId, OldStatus, NewStatus, Remark)
       VALUES (@ComplaintId, @CitizenUserId, NULL, 'Submitted', 'Complaint submitted by citizen.');
       DECLARE @TlId INT = SCOPE_IDENTITY();

       -- F1: Attachment goes to ComplaintAttachments only.
       IF @FilePath IS NOT NULL
           INSERT INTO ComplaintAttachments
               (ComplaintId, TimelineId, UploadedByUserId, AttachmentType, FilePath, FileName, FileSizeKB)
           VALUES
               (@ComplaintId, @TlId, @CitizenUserId, 'Complaint', @FilePath, @FileName, @FileSizeKB);

       IF @DeptId IS NOT NULL
       BEGIN
           DECLARE @SolverUserId INT;
           SELECT @SolverUserId = UserId FROM Departments WHERE DeptId = @DeptId;
           INSERT INTO Notifications (UserId, ComplaintId, Message, NotificationType, Channel)
           VALUES (@SolverUserId, @ComplaintId,
                   'New complaint #' + CAST(@ComplaintId AS VARCHAR) + ' routed to your department.',
                   'NewAssignment', 'InApp');
       END

       INSERT INTO Notifications (UserId, ComplaintId, Message, NotificationType, Channel)
       VALUES (@CitizenUserId, @ComplaintId,
               'Your complaint #' + CAST(@ComplaintId AS VARCHAR) + ' has been submitted successfully.',
               'StatusChange', 'InApp');

       COMMIT TRANSACTION;
   END TRY
   BEGIN CATCH ROLLBACK TRANSACTION; THROW; END CATCH
END
GO

-- US40: Solver updates complaint status.
-- F1:  Resolution photo written to ComplaintAttachments only.
-- F21: Transition validated against ComplaintStatusTransitions reference table.
CREATE OR ALTER PROCEDURE usp_UpdateComplaintStatus
   @ComplaintId          INT,
   @SolverUserId         INT,
   @NewStatus            VARCHAR(20),
   @Remark               NVARCHAR(1000),
   @ResolutionFilePath   VARCHAR(500) = NULL,  -- F1
   @ResolutionFileName   VARCHAR(200) = NULL,
   @ResolutionFileSizeKB INT          = NULL
AS
BEGIN
   SET NOCOUNT ON;
   DECLARE @OldStatus VARCHAR(20);
   SELECT @OldStatus = Status FROM Complaints WHERE ComplaintId = @ComplaintId;

   IF @OldStatus IS NULL
   BEGIN
       RAISERROR('Complaint %d not found.', 16, 1, @ComplaintId);
       RETURN;
   END

   -- F21: Reject disallowed transitions at DB layer.
   IF NOT EXISTS (
       SELECT 1 FROM ComplaintStatusTransitions
       WHERE FromStatus = @OldStatus AND ToStatus = @NewStatus)
   BEGIN
       RAISERROR('Invalid status transition: %s to %s is not permitted.',
                 16, 1, @OldStatus, @NewStatus);
       RETURN;
   END

   -- FIX-02 (GAP-02 / US40): Remark is mandatory when rejecting a complaint.
   IF @NewStatus = 'Rejected' AND (@Remark IS NULL OR LTRIM(RTRIM(@Remark)) = '')
   BEGIN
       RAISERROR('Remark is mandatory when rejecting a complaint.', 16, 1);
       RETURN;
   END

   BEGIN TRANSACTION;
   BEGIN TRY
       UPDATE Complaints
       SET Status    = @NewStatus,
           UpdatedAt = SYSDATETIME(),
           ResolvedAt = CASE WHEN @NewStatus = 'Resolved' THEN SYSDATETIME() ELSE NULL END
       WHERE ComplaintId = @ComplaintId;

       INSERT INTO ComplaintTimeline (ComplaintId, ActorUserId, OldStatus, NewStatus, Remark)
       VALUES (@ComplaintId, @SolverUserId, @OldStatus, @NewStatus, @Remark);
       DECLARE @TlId INT = SCOPE_IDENTITY();

       -- F1: Resolution photo to ComplaintAttachments — no deprecated column.
       IF @ResolutionFilePath IS NOT NULL
           INSERT INTO ComplaintAttachments
               (ComplaintId, TimelineId, UploadedByUserId, AttachmentType,
                FilePath, FileName, FileSizeKB)
           VALUES
               (@ComplaintId, @TlId, @SolverUserId, 'Resolution',
                @ResolutionFilePath, @ResolutionFileName, @ResolutionFileSizeKB);

       DECLARE @CitizenId INT;
       SELECT @CitizenId = CitizenUserId FROM Complaints WHERE ComplaintId = @ComplaintId;
       INSERT INTO Notifications (UserId, ComplaintId, Message, NotificationType, Channel)
       VALUES (@CitizenId, @ComplaintId,
               'Your complaint #' + CAST(@ComplaintId AS VARCHAR) + ' status changed to: ' + @NewStatus + '.',
               'StatusChange', 'InApp');

       COMMIT TRANSACTION;
   END TRY
   BEGIN CATCH ROLLBACK TRANSACTION; THROW; END CATCH
END
GO

-- US41: Solver sets estimated resolution date.
CREATE OR ALTER PROCEDURE usp_SetEstimatedResolutionDate
   @ComplaintId  INT,
   @SolverUserId INT,
   @EstDate      DATE
AS
BEGIN
   SET NOCOUNT ON;
   UPDATE Complaints SET EstimatedResDate = @EstDate, UpdatedAt = SYSDATETIME()
   WHERE ComplaintId = @ComplaintId;
   INSERT INTO ComplaintTimeline (ComplaintId, ActorUserId, OldStatus, NewStatus, Remark)
   SELECT ComplaintId, @SolverUserId, Status, Status,
          'Estimated resolution date set to ' + CONVERT(VARCHAR, @EstDate, 23)
   FROM Complaints WHERE ComplaintId = @ComplaintId;
END
GO

-- US19: Citizen rates a resolved complaint. F22: typed PointsLedger FK.
CREATE OR ALTER PROCEDURE usp_RateComplaint
   @ComplaintId   INT,
   @CitizenUserId INT,
   @Stars         TINYINT,
   @Comment       NVARCHAR(1000)
AS
BEGIN
   SET NOCOUNT ON;
   BEGIN TRANSACTION;
   BEGIN TRY
       INSERT INTO ComplaintRatings (ComplaintId, CitizenUserId, Stars, Comment)
       VALUES (@ComplaintId, @CitizenUserId, @Stars, @Comment);

       IF EXISTS (SELECT 1 FROM UserPoints WHERE UserId = @CitizenUserId)
           UPDATE UserPoints SET Points = Points + 1, UpdatedAt = SYSDATETIME()
           WHERE UserId = @CitizenUserId;
       ELSE
           INSERT INTO UserPoints (UserId, Points) VALUES (@CitizenUserId, 1);

       INSERT INTO PointsLedger (UserId, PointsDelta, Reason, RefComplaintId)  -- F22
       VALUES (@CitizenUserId, 1, 'ComplaintRated', @ComplaintId);
       COMMIT TRANSACTION;
   END TRY
   BEGIN CATCH ROLLBACK TRANSACTION; THROW; END CATCH
END
GO

-- US20: Citizen re-opens resolved complaint.
-- F17: Stars < 3 guard enforced at DB layer.
-- F21: Transition 'Resolved' -> 'Re-opened' exists in ComplaintStatusTransitions.
CREATE OR ALTER PROCEDURE usp_ReopenComplaint
   @ComplaintId   INT,
   @CitizenUserId INT,
   @Reason        NVARCHAR(500),
   @IsSuccess     BIT OUTPUT
AS
BEGIN
   SET NOCOUNT ON;
   SET @IsSuccess = 0;
   DECLARE @OldStatus VARCHAR(20);
   SELECT @OldStatus = Status FROM Complaints WHERE ComplaintId = @ComplaintId;
   IF @OldStatus IS NULL OR @OldStatus <> 'Resolved' RETURN;

   -- F17: Citizen must have rated this complaint below 3 stars.
   IF NOT EXISTS (
       SELECT 1 FROM ComplaintRatings
       WHERE ComplaintId = @ComplaintId AND CitizenUserId = @CitizenUserId AND Stars < 3)
   BEGIN
       RAISERROR('Re-open requires a prior rating of less than 3 stars for this complaint.', 16, 1);
       RETURN;
   END

   BEGIN TRANSACTION;
   BEGIN TRY
       UPDATE Complaints SET Status = 'Re-opened', UpdatedAt = SYSDATETIME()
       WHERE ComplaintId = @ComplaintId;

       INSERT INTO ComplaintTimeline (ComplaintId, ActorUserId, OldStatus, NewStatus, Remark)
       VALUES (@ComplaintId, @CitizenUserId, @OldStatus, 'Re-opened', @Reason);

       DECLARE @SolverUserId INT;
       SELECT @SolverUserId = u.UserId
       FROM   Complaints c
       JOIN   Departments d ON d.DeptId = c.DeptId
       JOIN   Users u       ON u.UserId = d.UserId
       WHERE  c.ComplaintId = @ComplaintId;

       IF @SolverUserId IS NOT NULL
           INSERT INTO Notifications (UserId, ComplaintId, Message, NotificationType, Channel)
           VALUES (@SolverUserId, @ComplaintId,
                   'Complaint #' + CAST(@ComplaintId AS VARCHAR) +
                   ' has been re-opened. Reason: ' + @Reason,
                   'StatusChange', 'InApp');

       COMMIT TRANSACTION;
       SET @IsSuccess = 1;
   END TRY
   BEGIN CATCH ROLLBACK TRANSACTION; SET @IsSuccess = 0; THROW; END CATCH
END
GO

-- ─── DUPLICATE COMPLAINTS ────────────────────────────────────

-- US49: Link a detected duplicate complaint to its original.
CREATE OR ALTER PROCEDURE usp_LinkDuplicateComplaint
   @OriginalComplaintId INT,
   @LinkedComplaintId   INT,
   @LinkedByUserId      INT,
   @IsSuccess           BIT OUTPUT
AS
BEGIN
   SET NOCOUNT ON;
   SET @IsSuccess = 0;
   IF @OriginalComplaintId = @LinkedComplaintId RETURN;

   BEGIN TRANSACTION;
   BEGIN TRY
       DECLARE @OldStatus VARCHAR(20);
       SELECT @OldStatus = Status FROM Complaints WHERE ComplaintId = @LinkedComplaintId;
       IF @OldStatus IS NULL BEGIN ROLLBACK TRANSACTION; RETURN; END

       INSERT INTO DuplicateComplaintLinks (OriginalComplaintId, LinkedComplaintId, LinkedByUserId)
       VALUES (@OriginalComplaintId, @LinkedComplaintId, @LinkedByUserId);

       UPDATE Complaints
       SET Status = 'Linked', LinkedToComplaintId = @OriginalComplaintId, UpdatedAt = SYSDATETIME()
       WHERE ComplaintId = @LinkedComplaintId;

       INSERT INTO ComplaintTimeline (ComplaintId, ActorUserId, OldStatus, NewStatus, Remark)
       VALUES (@LinkedComplaintId, @LinkedByUserId, @OldStatus, 'Linked',
               'Linked to complaint #' + CAST(@OriginalComplaintId AS VARCHAR) + ' as a duplicate.');

       DECLARE @CitizenId INT;
       SELECT @CitizenId = CitizenUserId FROM Complaints WHERE ComplaintId = @LinkedComplaintId;
       IF @CitizenId IS NOT NULL
           INSERT INTO Notifications (UserId, ComplaintId, Message, NotificationType, Channel)
           VALUES (@CitizenId, @LinkedComplaintId,
                   'Your complaint #' + CAST(@LinkedComplaintId AS VARCHAR) +
                   ' has been linked to existing complaint #' + CAST(@OriginalComplaintId AS VARCHAR) + '.',
                   'StatusChange', 'InApp');

       COMMIT TRANSACTION;
       SET @IsSuccess = 1;
   END TRY
   BEGIN CATCH ROLLBACK TRANSACTION; SET @IsSuccess = 0; THROW; END CATCH
END
GO

-- ─── ESCALATION ─────────────────────────────────────────────

-- US50 / US55: File escalation (Auto or Manual).
-- F2:  Guard against NULL OriginalDeptId.
-- F13: Single ActorUserId — no redundant AdminUserId column.
-- F22: Typed AuditLog FKs.
CREATE OR ALTER PROCEDURE usp_FileEscalation
   @ComplaintId        INT,
   @EscalationTrigger  VARCHAR(20),
   @AdminUserId        INT = NULL,
   @ReassignedToDeptId INT = NULL,
   @Reason             NVARCHAR(500) = NULL
AS
BEGIN
   SET NOCOUNT ON;
   DECLARE @OriginalDeptId INT, @CitizenId INT, @OldStatus VARCHAR(20);
   SELECT @OriginalDeptId = DeptId, @CitizenId = CitizenUserId, @OldStatus = Status
   FROM   Complaints WHERE ComplaintId = @ComplaintId;

   -- F2: Unrouted complaints cannot be escalated — EscalationLog.OriginalDeptId is NOT NULL.
   IF @OriginalDeptId IS NULL
   BEGIN
       RAISERROR('Cannot escalate complaint %d: no department assigned. Route the complaint first.',
                 16, 1, @ComplaintId);
       RETURN;
   END

   BEGIN TRANSACTION;
   BEGIN TRY
       UPDATE Complaints
       SET Status    = 'Escalated',
           DeptId    = ISNULL(@ReassignedToDeptId, DeptId),
           UpdatedAt = SYSDATETIME()
       WHERE ComplaintId = @ComplaintId;

       INSERT INTO ComplaintTimeline (ComplaintId, ActorUserId, OldStatus, NewStatus, Remark)
       VALUES (@ComplaintId, @AdminUserId, @OldStatus, 'Escalated',
               ISNULL(@Reason, 'Auto-escalated after 30 days without resolution.'));

       -- F13: ActorUserId is NULL for Auto, AdminUserId for Manual.
       INSERT INTO EscalationLog
           (ComplaintId, EscalationTrigger, ActorUserId, OriginalDeptId, ReassignedToDeptId, Reason)
       VALUES (@ComplaintId, @EscalationTrigger,
               CASE WHEN @EscalationTrigger = 'Auto' THEN NULL ELSE @AdminUserId END,
               @OriginalDeptId, @ReassignedToDeptId, @Reason);

       IF @CitizenId IS NOT NULL
           INSERT INTO Notifications (UserId, ComplaintId, Message, NotificationType, Channel)
           VALUES (@CitizenId, @ComplaintId,
                   'Your complaint #' + CAST(@ComplaintId AS VARCHAR) + ' has been escalated for priority review.',
                   'StatusChange', 'InApp');

       IF @ReassignedToDeptId IS NOT NULL
       BEGIN
           DECLARE @NewSolverUserId INT;
           SELECT @NewSolverUserId = UserId FROM Departments WHERE DeptId = @ReassignedToDeptId;
           IF @NewSolverUserId IS NOT NULL
               INSERT INTO Notifications (UserId, ComplaintId, Message, NotificationType, Channel)
               VALUES (@NewSolverUserId, @ComplaintId,
                       'Escalated complaint #' + CAST(@ComplaintId AS VARCHAR) +
                       ' has been reassigned to your department.',
                       'NewAssignment', 'InApp');
       END

       IF @EscalationTrigger = 'Manual' AND @AdminUserId IS NOT NULL
           INSERT INTO AuditLog (ActorUserId, ActionType, TargetComplaintId, Reason)  -- F22
           VALUES (@AdminUserId, 'ComplaintReassigned', @ComplaintId, @Reason);

       COMMIT TRANSACTION;
   END TRY
   BEGIN CATCH ROLLBACK TRANSACTION; THROW; END CATCH
END
GO

-- ─── PWG COLLABORATION ──────────────────────────────────────

-- US31: PWG submits participation request for a complaint.
CREATE OR ALTER PROCEDURE usp_SubmitPWGRequest
   @ComplaintId  INT,
   @OrgId        INT,
   @RequestNote  NVARCHAR(500),
   @NewRequestId INT OUTPUT
AS
BEGIN
   SET NOCOUNT ON;
   BEGIN TRANSACTION;
   BEGIN TRY
       DECLARE @SolverUserId INT;
       SELECT @SolverUserId = u.UserId
       FROM   Complaints c
       JOIN   Departments d ON d.DeptId = c.DeptId
       JOIN   Users u       ON u.UserId = d.UserId
       WHERE  c.ComplaintId = @ComplaintId;

       INSERT INTO PWGParticipationRequests
           (ComplaintId, OrgId, SolverUserId, Status, RequestNote)
       VALUES
           (@ComplaintId, @OrgId, @SolverUserId, 'Pending', @RequestNote);
       SET @NewRequestId = SCOPE_IDENTITY();

       IF @SolverUserId IS NOT NULL
           INSERT INTO Notifications (UserId, ComplaintId, Message, NotificationType, Channel)
           VALUES (@SolverUserId, @ComplaintId,
                   'A PWG has requested to assist on complaint #' + CAST(@ComplaintId AS VARCHAR) + '.',
                   'PWGDecision', 'InApp');
       COMMIT TRANSACTION;
   END TRY
   BEGIN CATCH ROLLBACK TRANSACTION; THROW; END CATCH
END
GO

-- US42: Solver approves or rejects a PWG participation request.
CREATE OR ALTER PROCEDURE usp_ResolvePWGRequest
   @RequestId    INT,
   @SolverUserId INT,
   @Decision     VARCHAR(10),
   @DecisionNote NVARCHAR(500)
AS
BEGIN
   SET NOCOUNT ON;
   BEGIN TRANSACTION;
   BEGIN TRY
       UPDATE PWGParticipationRequests
       SET Status = @Decision, DecisionNote = @DecisionNote, DecidedAt = SYSDATETIME()
       WHERE RequestId = @RequestId;

       DECLARE @PWGUserId INT, @CompId INT;
       SELECT @CompId = ComplaintId FROM PWGParticipationRequests WHERE RequestId = @RequestId;
       SELECT @PWGUserId = u.UserId
       FROM   PWGParticipationRequests pr
       JOIN   Organisations o ON o.OrgId = pr.OrgId
       JOIN   Users u         ON u.UserId = o.UserId
       WHERE  pr.RequestId = @RequestId;

       IF @PWGUserId IS NOT NULL
           INSERT INTO Notifications (UserId, ComplaintId, Message, NotificationType, Channel)
           VALUES (@PWGUserId, @CompId,
                   'Your participation request for complaint #' + CAST(@CompId AS VARCHAR) +
                   ' has been ' + @Decision + '.',
                   'PWGDecision', 'InApp');
       COMMIT TRANSACTION;
   END TRY
   BEGIN CATCH ROLLBACK TRANSACTION; THROW; END CATCH
END
GO

-- US32: PWG logs a progress update on an approved complaint.
-- F4:  Photo written to ComplaintAttachments (not deprecated PhotoPath).
-- F22: Typed PointsLedger FK.
CREATE OR ALTER PROCEDURE usp_PWGProgressUpdate
   @ComplaintId     INT,
   @PWGUserId       INT,
   @ProgressNote    NVARCHAR(1000),
   @IsSuccess       BIT OUTPUT,
   @PhotoPath       VARCHAR(500) = NULL,   -- F4: routes to ComplaintAttachments
   @PhotoFileName   VARCHAR(200) = NULL,
   @PhotoFileSizeKB INT          = NULL
AS
BEGIN
   SET NOCOUNT ON;
   SET @IsSuccess = 0;
   DECLARE @CurrStatus VARCHAR(20);
   SELECT @CurrStatus = Status FROM Complaints WHERE ComplaintId = @ComplaintId;
   IF @CurrStatus IS NULL RETURN;

   BEGIN TRANSACTION;
   BEGIN TRY
       INSERT INTO ComplaintTimeline (ComplaintId, ActorUserId, OldStatus, NewStatus, Remark)
       VALUES (@ComplaintId, @PWGUserId, @CurrStatus, @CurrStatus, @ProgressNote);
       DECLARE @TlId INT = SCOPE_IDENTITY();

       -- F4: Photo to ComplaintAttachments — authoritative file store.
       IF @PhotoPath IS NOT NULL
           INSERT INTO ComplaintAttachments
               (ComplaintId, TimelineId, UploadedByUserId, AttachmentType,
                FilePath, FileName, FileSizeKB)
           VALUES
               (@ComplaintId, @TlId, @PWGUserId, 'PWGProgress',
                @PhotoPath, @PhotoFileName, @PhotoFileSizeKB);

       IF EXISTS (SELECT 1 FROM UserPoints WHERE UserId = @PWGUserId)
           UPDATE UserPoints SET Points = Points + 2, UpdatedAt = SYSDATETIME()
           WHERE UserId = @PWGUserId;
       ELSE
           INSERT INTO UserPoints (UserId, Points) VALUES (@PWGUserId, 2);

       INSERT INTO PointsLedger (UserId, PointsDelta, Reason, RefComplaintId)  -- F22
       VALUES (@PWGUserId, 2, 'PWGProgressUpdate', @ComplaintId);

       DECLARE @CitizenId INT;
       SELECT @CitizenId = CitizenUserId FROM Complaints WHERE ComplaintId = @ComplaintId;
       IF @CitizenId IS NOT NULL
           INSERT INTO Notifications (UserId, ComplaintId, Message, NotificationType, Channel)
           VALUES (@CitizenId, @ComplaintId,
                   'PWG posted a progress update on complaint #' + CAST(@ComplaintId AS VARCHAR) + '.',
                   'StatusChange', 'InApp');

       COMMIT TRANSACTION;
       SET @IsSuccess = 1;
   END TRY
   BEGIN CATCH ROLLBACK TRANSACTION; SET @IsSuccess = 0; THROW; END CATCH
END
GO

-- ─── PWG REPORTS ────────────────────────────────────────────

-- US44: Solver files a report against a PWG organisation.
CREATE OR ALTER PROCEDURE usp_FilePWGReport
   @ComplaintId      INT,
   @ReportedOrgId    INT,
   @ReportedByUserId INT,
   @ReportReason     NVARCHAR(1000),
   @NewReportId      INT OUTPUT
AS
BEGIN
   SET NOCOUNT ON;
   BEGIN TRANSACTION;
   BEGIN TRY
       INSERT INTO PWGReports (ComplaintId, ReportedOrgId, ReportedByUserId, ReportReason)
       VALUES (@ComplaintId, @ReportedOrgId, @ReportedByUserId, @ReportReason);
       SET @NewReportId = SCOPE_IDENTITY();

       DECLARE @PWGUserId INT;
       SELECT @PWGUserId = UserId FROM Organisations WHERE OrgId = @ReportedOrgId;
       IF @PWGUserId IS NOT NULL
           INSERT INTO Notifications (UserId, ComplaintId, Message, NotificationType, Channel)
           VALUES (@PWGUserId, @ComplaintId,
                   'A report has been filed against your organisation for complaint #' +
                   CAST(@ComplaintId AS VARCHAR) + '.',
                   'PWGDecision', 'InApp');

       -- Notify all SuperAdmins
       INSERT INTO Notifications (UserId, ComplaintId, Message, NotificationType, Channel)
       SELECT u.UserId, @ComplaintId,
              'PWG report filed for complaint #' + CAST(@ComplaintId AS VARCHAR) + '. Review required.',
              'PWGDecision', 'InApp'
       FROM   Users u JOIN Roles r ON r.RoleId = u.RoleId
       WHERE  r.RoleName = 'SuperAdmin' AND u.IsActive = 1;

       COMMIT TRANSACTION;
   END TRY
   BEGIN CATCH ROLLBACK TRANSACTION; THROW; END CATCH
END
GO

-- US63: Admin reviews a PWG report and takes action. F22: typed AuditLog FKs.
CREATE OR ALTER PROCEDURE usp_ReviewPWGReport
   @ReportId    INT,
   @AdminUserId INT,
   @AdminAction VARCHAR(30),
   @AdminNote   NVARCHAR(500) = NULL,
   @FinalClose  BIT = 0
AS
BEGIN
   SET NOCOUNT ON;
   BEGIN TRANSACTION;
   BEGIN TRY
       DECLARE @ReportedOrgId INT, @ReportedUserId INT, @ComplaintId INT;
       DECLARE @NewStatus VARCHAR(20) = CASE WHEN @FinalClose = 1 THEN 'Closed' ELSE 'Reviewed' END;

       SELECT @ReportedOrgId = ReportedOrgId, @ComplaintId = ComplaintId
       FROM   PWGReports WHERE ReportId = @ReportId;
       SELECT @ReportedUserId = UserId FROM Organisations WHERE OrgId = @ReportedOrgId;

       UPDATE PWGReports
       SET AdminReviewedByUserId = @AdminUserId, AdminAction = @AdminAction,
           AdminNote  = @AdminNote, ReviewedAt = SYSDATETIME(),
           ClosedAt   = CASE WHEN @FinalClose = 1 THEN SYSDATETIME() ELSE NULL END,
           Status     = @NewStatus
       WHERE ReportId = @ReportId;

       IF @AdminAction = 'Removed' AND @ReportedUserId IS NOT NULL
           EXEC usp_DeactivateUser
               @TargetUserId = @ReportedUserId,
               @Reason       = 'PWG removed following admin review.',
               @AdminUserId  = @AdminUserId,
               @IsBan        = 0;
       -- FIX-04 (GAP-06 / US63): Suspended action now cascades to Users and Organisations.
       ELSE IF @AdminAction = 'Suspended' AND @ReportedUserId IS NOT NULL
       BEGIN
           UPDATE Users
           SET IsSuspended = 1, IsActive = 0, UpdatedAt = SYSDATETIME()
           WHERE UserId = @ReportedUserId;

           UPDATE Organisations
           SET SuspendedAt = SYSDATETIME(), UpdatedAt = SYSDATETIME()
           WHERE OrgId = @ReportedOrgId;

           INSERT INTO Notifications (UserId, ComplaintId, Message, NotificationType, Channel)
           VALUES (@ReportedUserId, @ComplaintId,
                   'Your organisation has been suspended following an admin review.',
                   'PWGDecision', 'InApp');
       END
       ELSE IF @ReportedUserId IS NOT NULL
           INSERT INTO Notifications (UserId, ComplaintId, Message, NotificationType, Channel)
           VALUES (@ReportedUserId, @ComplaintId,
                   'Admin reviewed the report against your organisation. Action: ' + @AdminAction + '.',
                   'PWGDecision', 'InApp');

       INSERT INTO AuditLog (ActorUserId, ActionType, TargetOrgId, Reason)  -- F22
       VALUES (@AdminUserId, 'PWGReportActioned', @ReportedOrgId, @AdminNote);

       COMMIT TRANSACTION;
   END TRY
   BEGIN CATCH ROLLBACK TRANSACTION; THROW; END CATCH
END
GO

-- US63: Admin closes a PWG report. F22: typed AuditLog FKs.
CREATE OR ALTER PROCEDURE usp_CloseReport
   @ReportId    INT,
   @AdminUserId INT,
   @CloseNote   NVARCHAR(500) = NULL
AS
BEGIN
   SET NOCOUNT ON;
   DECLARE @ReportedOrgId INT;
   SELECT @ReportedOrgId = ReportedOrgId FROM PWGReports WHERE ReportId = @ReportId;

   UPDATE PWGReports
   SET Status = 'Closed', ClosedAt = SYSDATETIME(),
       AdminNote = ISNULL(@CloseNote, AdminNote)
   WHERE ReportId = @ReportId AND Status IN ('Pending','Reviewed');

   IF @@ROWCOUNT > 0
       INSERT INTO AuditLog (ActorUserId, ActionType, TargetOrgId, Reason)  -- F22
       VALUES (@AdminUserId, 'PWGReportActioned', @ReportedOrgId,
               'Report closed: ' + ISNULL(@CloseNote, 'No note provided.'));
END
GO

-- ─── NOTIFICATIONS ──────────────────────────────────────────

CREATE OR ALTER PROCEDURE usp_SendNotification
   @UserId            INT,
   @Message           NVARCHAR(500),
   @NewNotificationId INT OUTPUT,
   @ComplaintId       INT    = NULL,
   @NotificationType  VARCHAR(30) = NULL,
   @Channel           VARCHAR(20) = 'InApp'
AS
BEGIN
   SET NOCOUNT ON;
   INSERT INTO Notifications (UserId, ComplaintId, Message, NotificationType, Channel)
   VALUES (@UserId, @ComplaintId, @Message, @NotificationType, @Channel);
   SET @NewNotificationId = SCOPE_IDENTITY();
END
GO

CREATE OR ALTER PROCEDURE usp_MarkNotificationsRead
   @UserId         INT,
   @NotificationId INT = NULL
AS
BEGIN
   SET NOCOUNT ON;
   IF @NotificationId IS NULL
       UPDATE Notifications SET IsRead = 1, ReadAt = SYSDATETIME()
       WHERE UserId = @UserId AND IsRead = 0;
   ELSE
       UPDATE Notifications SET IsRead = 1, ReadAt = SYSDATETIME()
       WHERE NotificationId = @NotificationId AND UserId = @UserId AND IsRead = 0;
END
GO

CREATE OR ALTER PROCEDURE usp_ArchiveNotification
   @UserId         INT,
   @NotificationId INT = NULL
AS
BEGIN
   SET NOCOUNT ON;
   IF @NotificationId IS NULL
       UPDATE Notifications SET IsArchived = 1 WHERE UserId = @UserId AND IsRead = 1;
   ELSE
       UPDATE Notifications SET IsArchived = 1
       WHERE NotificationId = @NotificationId AND UserId = @UserId;
END
GO

CREATE OR ALTER PROCEDURE usp_UpdateNotificationPreferences
   @UserId              INT,
   @InAppEnabled        BIT,
   @PushEnabled         BIT,
   @EmailDigestEnabled  BIT,
   @DigestFrequencyDays TINYINT = 7
AS
BEGIN
   SET NOCOUNT ON;
   UPDATE NotificationPreferences
   SET InAppEnabled       = @InAppEnabled,
       PushEnabled        = @PushEnabled,
       EmailDigestEnabled = @EmailDigestEnabled,
       DigestFrequencyDays = @DigestFrequencyDays,
       UpdatedAt          = SYSDATETIME()
   WHERE UserId = @UserId;
END
GO

-- US65: Weekly locality digest. F11: Set-based — no cursor.
CREATE OR ALTER PROCEDURE usp_GenerateWeeklyDigest
AS
BEGIN
   SET NOCOUNT ON;
   -- F11: Single set-based INSERT replaces the original row-by-row cursor.
   INSERT INTO Notifications (UserId, Message, NotificationType, Channel)
   SELECT
       np.UserId,
       CAST(COUNT(c.ComplaintId) AS VARCHAR) +
       ' new complaint(s) in your locality in the last ' +
       CAST(np.DigestFrequencyDays AS VARCHAR) + ' day(s).',
       'WeeklyDigest',
       'Email'
   FROM NotificationPreferences np
   JOIN Users u
       ON  u.UserId    = np.UserId
       AND u.IsActive  = 1
       AND u.LocalityId IS NOT NULL
   JOIN Complaints c
       ON  c.LocalityId  = u.LocalityId
       AND c.SubmittedAt >= DATEADD(DAY, -np.DigestFrequencyDays, SYSDATETIME())
       AND c.Status NOT IN ('Resolved','Rejected')
   WHERE np.EmailDigestEnabled = 1
   GROUP BY np.UserId, np.DigestFrequencyDays
   HAVING COUNT(c.ComplaintId) > 0;
END
GO

-- ─── GAMIFICATION ───────────────────────────────────────────

-- US27: Issue a certificate for a milestone. P7: duplicate guard handles nullable MilestoneId.
CREATE OR ALTER PROCEDURE usp_IssueCertificate
   @UserId           INT,
   @Milestone        VARCHAR(100),
   @FilePath         VARCHAR(500),
   @NewCertId        INT OUTPUT,
   @VerificationCode VARCHAR(50) OUTPUT,
   @MilestoneId      INT = NULL
AS
BEGIN
   SET NOCOUNT ON;
   -- Guard against duplicate milestone certificates (non-null MilestoneId path).
   IF @MilestoneId IS NOT NULL AND EXISTS (
       SELECT 1 FROM Certificates WHERE UserId = @UserId AND MilestoneId = @MilestoneId)
   BEGIN
       SET @NewCertId = -1; SET @VerificationCode = NULL; RETURN;
   END
   DECLARE @Code VARCHAR(50) =
       LEFT(UPPER(REPLACE(CAST(NEWID() AS VARCHAR(36)),'-','')) + '_' + CAST(@UserId AS VARCHAR), 50);
   INSERT INTO Certificates (UserId, Milestone, VerificationCode, FilePath, MilestoneId)
   VALUES (@UserId, @Milestone, @Code, @FilePath, @MilestoneId);
   SET @NewCertId = SCOPE_IDENTITY();
   SET @VerificationCode = @Code;
END
GO

-- US25 / US27: Award points and auto-issue milestone certificates.
-- F22: @RefComplaintId + @RefMilestoneId replace the old generic @ReferenceId.
CREATE OR ALTER PROCEDURE usp_AwardPoints
   @UserId         INT,
   @Points         INT,
   @UpdatedPoints  INT OUTPUT,
   @Reason         VARCHAR(100) = 'ManualAward',
   @RefComplaintId INT = NULL,   -- F22
   @RefMilestoneId INT = NULL    -- F22
AS
BEGIN
   SET NOCOUNT ON;
   BEGIN TRANSACTION;
   BEGIN TRY
       IF EXISTS (SELECT 1 FROM UserPoints WHERE UserId = @UserId)
       BEGIN
           UPDATE UserPoints SET Points = Points + @Points, UpdatedAt = SYSDATETIME()
           WHERE UserId = @UserId;
           SELECT @UpdatedPoints = Points FROM UserPoints WHERE UserId = @UserId;
       END
       ELSE
       BEGIN
           INSERT INTO UserPoints (UserId, Points) VALUES (@UserId, @Points);
           SET @UpdatedPoints = @Points;
       END
       INSERT INTO PointsLedger (UserId, PointsDelta, Reason, RefComplaintId, RefMilestoneId)  -- F22
       VALUES (@UserId, @Points, @Reason, @RefComplaintId, @RefMilestoneId);
       COMMIT TRANSACTION;
   END TRY
   BEGIN CATCH ROLLBACK TRANSACTION; THROW; END CATCH

   -- Auto-issue certificates for newly crossed milestones (runs outside transaction).
   DECLARE @MId INT, @MName VARCHAR(100), @NewCertId INT, @VerCode VARCHAR(50);
   DECLARE milestone_cursor CURSOR LOCAL FAST_FORWARD FOR
       SELECT md.MilestoneId, md.MilestoneName
       FROM   MilestoneDefinitions md
       WHERE  md.IsActive = 1
         AND  md.PointsThreshold <= @UpdatedPoints
         AND  NOT EXISTS (
             SELECT 1 FROM Certificates c
             WHERE c.UserId = @UserId AND c.MilestoneId = md.MilestoneId)
       ORDER BY md.PointsThreshold;
   OPEN milestone_cursor;
   FETCH NEXT FROM milestone_cursor INTO @MId, @MName;
   WHILE @@FETCH_STATUS = 0
   BEGIN
       EXEC usp_IssueCertificate
           @UserId = @UserId, @Milestone = @MName, @FilePath = NULL,
           @NewCertId = @NewCertId OUTPUT, @VerificationCode = @VerCode OUTPUT,
           @MilestoneId = @MId;
       IF @NewCertId > 0
       BEGIN
           INSERT INTO PointsLedger (UserId, PointsDelta, Reason, RefMilestoneId)  -- F22
           VALUES (@UserId, 0, 'CertificateMilestone', @MId);
           INSERT INTO Notifications (UserId, Message, NotificationType, Channel)
           VALUES (@UserId,
                   'Congratulations! You earned the "' + @MName + '" certificate!',
                   'StatusChange', 'InApp');
       END
       FETCH NEXT FROM milestone_cursor INTO @MId, @MName;
   END
   CLOSE milestone_cursor; DEALLOCATE milestone_cursor;
END
GO

-- US25: Refresh scoreboard. F12: MERGE replaces TRUNCATE + INSERT — no empty-table race.
CREATE OR ALTER PROCEDURE usp_RefreshScoreboard
AS
BEGIN
   SET NOCOUNT ON;
   MERGE ScoreboardSnapshot AS tgt
   USING (
       SELECT u.UserId, u.FullName, u.LocalityId, up.Points,
              RANK() OVER (ORDER BY up.Points DESC) AS Rank
       FROM   UserPoints up
       JOIN   Users u ON u.UserId = up.UserId
       WHERE  u.IsActive = 1
   ) AS src ON tgt.UserId = src.UserId
   WHEN MATCHED THEN
       UPDATE SET tgt.FullName   = src.FullName,
                  tgt.LocalityId = src.LocalityId,
                  tgt.Points     = src.Points,
                  tgt.Rank       = src.Rank,
                  tgt.SnapshotAt = SYSDATETIME()
   WHEN NOT MATCHED BY TARGET THEN
       INSERT (UserId, FullName, LocalityId, Points, Rank, SnapshotAt)
       VALUES (src.UserId, src.FullName, src.LocalityId, src.Points, src.Rank, SYSDATETIME())
   WHEN NOT MATCHED BY SOURCE THEN
       DELETE;
END
GO

-- ─── PROFILE UPDATES ────────────────────────────────────────

CREATE OR ALTER PROCEDURE usp_UpdateOrgProfile
   @OrgId        INT,
   @OrgName      VARCHAR(150),
   @ContactEmail VARCHAR(150),
   @ContactPhone VARCHAR(15),
   @Address      VARCHAR(300)
AS
BEGIN
   SET NOCOUNT ON;
   UPDATE Organisations
   SET OrgName = @OrgName, ContactEmail = @ContactEmail,
       ContactPhone = @ContactPhone, Address = @Address, UpdatedAt = SYSDATETIME()
   WHERE OrgId = @OrgId;
END
GO

-- US46: F14: @LocalityId replaces @Locality.
CREATE OR ALTER PROCEDURE usp_UpdateDeptProfile
   @DeptId       INT,
   @DeptName     VARCHAR(150),
   @Ministry     VARCHAR(100),
   @ContactEmail VARCHAR(150),
   @ContactPhone VARCHAR(15),
   @Address      VARCHAR(300),
   @LocalityId   INT
AS
BEGIN
   SET NOCOUNT ON;
   UPDATE Departments
   SET DeptName = @DeptName, Ministry = @Ministry, ContactEmail = @ContactEmail,
       ContactPhone = @ContactPhone, Address = @Address, LocalityId = @LocalityId,
       UpdatedAt = SYSDATETIME()
   WHERE DeptId = @DeptId;
END
GO

-- ─── CONTRIBUTIONS ──────────────────────────────────────────

-- US22: Update payment status on gateway callback. Uses UPDLOCK to prevent race on same TxRef.
CREATE OR ALTER PROCEDURE usp_UpdatePaymentStatus
   @TransactionRef VARCHAR(100),
   @NewStatus      VARCHAR(20),
   @FailureReason  NVARCHAR(200) = NULL
AS
BEGIN
   SET NOCOUNT ON;
   BEGIN TRANSACTION;
   BEGIN TRY
       UPDATE Contributions WITH (UPDLOCK, ROWLOCK)
       SET PaymentStatus = @NewStatus,
           FailureReason = @FailureReason,
           CompletedAt   = CASE WHEN @NewStatus = 'Success' THEN SYSDATETIME() ELSE NULL END
       WHERE TransactionRef = @TransactionRef AND PaymentStatus = 'Pending';
       COMMIT TRANSACTION;
   END TRY
   BEGIN CATCH ROLLBACK TRANSACTION; THROW; END CATCH
END
GO

-- ─── ATTACHMENTS ────────────────────────────────────────────

-- US60 / US62: Add an attachment to a complaint. F1: sole entry point for complaint media.
CREATE OR ALTER PROCEDURE usp_AddComplaintAttachment
   @ComplaintId      INT,
   @UploadedByUserId INT,
   @AttachmentType   VARCHAR(30),
   @FilePath         VARCHAR(500),
   @NewAttachmentId  INT OUTPUT,
   @TimelineId       INT = NULL,
   @FileName         VARCHAR(200) = NULL,
   @FileSizeKB       INT = NULL
AS
BEGIN
   SET NOCOUNT ON;
   INSERT INTO ComplaintAttachments
       (ComplaintId, TimelineId, UploadedByUserId, AttachmentType, FilePath, FileName, FileSizeKB)
   VALUES
       (@ComplaintId, @TimelineId, @UploadedByUserId, @AttachmentType, @FilePath, @FileName, @FileSizeKB);
   SET @NewAttachmentId = SCOPE_IDENTITY();
END
GO

-- ─── USER INTERESTS ─────────────────────────────────────────

-- US26: Save a user interest (category or locality). F14: @PreferredLocalityId replaces @PreferredLocality.
CREATE OR ALTER PROCEDURE usp_AddUserInterest
   @UserId              INT,
   @CategoryId          SMALLINT = NULL,
   @PreferredLocalityId INT      = NULL   -- F14
AS
BEGIN
   SET NOCOUNT ON;
   IF @CategoryId IS NULL AND @PreferredLocalityId IS NULL RETURN;
   IF @CategoryId IS NOT NULL
   BEGIN
       IF NOT EXISTS (SELECT 1 FROM UserInterests WHERE UserId = @UserId AND CategoryId = @CategoryId)
           INSERT INTO UserInterests (UserId, CategoryId, PreferredLocalityId)
           VALUES (@UserId, @CategoryId, @PreferredLocalityId);
   END
   ELSE
   BEGIN
       IF NOT EXISTS (SELECT 1 FROM UserInterests
                      WHERE UserId = @UserId AND PreferredLocalityId = @PreferredLocalityId
                        AND CategoryId IS NULL)
           INSERT INTO UserInterests (UserId, CategoryId, PreferredLocalityId)
           VALUES (@UserId, NULL, @PreferredLocalityId);
   END
END
GO

-- US26: Remove a user interest. F14: @PreferredLocalityId.
CREATE OR ALTER PROCEDURE usp_RemoveUserInterest
   @UserId              INT,
   @CategoryId          SMALLINT = NULL,
   @PreferredLocalityId INT      = NULL   -- F14
AS
BEGIN
   SET NOCOUNT ON;
   IF @CategoryId IS NOT NULL
       DELETE FROM UserInterests WHERE UserId = @UserId AND CategoryId = @CategoryId;
   ELSE IF @PreferredLocalityId IS NOT NULL
       DELETE FROM UserInterests
       WHERE UserId = @UserId AND PreferredLocalityId = @PreferredLocalityId AND CategoryId IS NULL;
END
GO

-- ─── ML SCORES ──────────────────────────────────────────────

-- US52–US54: Upsert ML prediction scores for a complaint.
CREATE OR ALTER PROCEDURE usp_SaveMLScores
   @ComplaintId             INT,
   @PredictedResolutionDate DATE         = NULL,
   @ResolutionProbability   DECIMAL(5,4) = NULL,
   @PriorityScore           DECIMAL(8,2) = NULL,
   @ModelVersion            VARCHAR(20)  = NULL
AS
BEGIN
   SET NOCOUNT ON;
   IF EXISTS (SELECT 1 FROM ComplaintMLScores WHERE ComplaintId = @ComplaintId)
       UPDATE ComplaintMLScores
       SET PredictedResolutionDate = ISNULL(@PredictedResolutionDate, PredictedResolutionDate),
           ResolutionProbability   = ISNULL(@ResolutionProbability,   ResolutionProbability),
           PriorityScore           = ISNULL(@PriorityScore,           PriorityScore),
           PredictionModelVersion  = ISNULL(@ModelVersion,            PredictionModelVersion),
           UpdatedAt               = SYSDATETIME()
       WHERE ComplaintId = @ComplaintId;
   ELSE
       INSERT INTO ComplaintMLScores
           (ComplaintId, PredictedResolutionDate, ResolutionProbability,
            PriorityScore, PredictionModelVersion)
       VALUES (@ComplaintId, @PredictedResolutionDate, @ResolutionProbability,
               @PriorityScore, @ModelVersion);
END
GO

-- US24 / US51: Recommend open complaints matching user interests. F14: joins on LocalityId.
CREATE OR ALTER PROCEDURE usp_GetRecommendedComplaints
   @UserId INT,
   @TopN   INT = 10
AS
BEGIN
   SET NOCOUNT ON;
   SELECT TOP (@TopN)
       c.ComplaintId, c.Title, c.CategoryId, c.LocalityId, c.Criticality,
       c.Status, c.SubmittedAt, ml.PriorityScore
   FROM Complaints c
   JOIN UserInterests ui ON ui.UserId = @UserId
       AND (ui.CategoryId = c.CategoryId OR ui.PreferredLocalityId = c.LocalityId)
   LEFT JOIN ComplaintMLScores ml ON ml.ComplaintId = c.ComplaintId
   WHERE c.Status NOT IN ('Resolved','Rejected','Linked')
     AND c.CitizenUserId <> @UserId
   GROUP BY c.ComplaintId, c.Title, c.CategoryId, c.LocalityId, c.Criticality,
            c.Status, c.SubmittedAt, ml.PriorityScore
   ORDER BY ISNULL(ml.PriorityScore, 0) DESC, c.SubmittedAt DESC;
END
GO

-- US12 / FIX-03 (GAP-03): Capture a daily platform stats snapshot for trending graphs.
-- Schedule via Azure Function (Timer Trigger, daily) or SQL Agent Job.
CREATE OR ALTER PROCEDURE usp_SnapshotPlatformStats
AS
BEGIN
   SET NOCOUNT ON;
   -- MERGE prevents duplicate-date errors if re-run on the same day.
   MERGE PlatformStatsSnapshot AS tgt
   USING (
       SELECT
           COUNT(*)                                                   AS TotalComplaints,
           SUM(CASE WHEN Status = 'Submitted'   THEN 1 ELSE 0 END)  AS Submitted,
           SUM(CASE WHEN Status = 'In Progress' THEN 1 ELSE 0 END)  AS InProgress,
           SUM(CASE WHEN Status = 'Resolved'    THEN 1 ELSE 0 END)  AS Resolved,
           SUM(CASE WHEN Status = 'Rejected'    THEN 1 ELSE 0 END)  AS Rejected,
           SUM(CASE WHEN Status = 'Re-opened'   THEN 1 ELSE 0 END)  AS Reopened,
           SUM(CASE WHEN Status = 'Escalated'   THEN 1 ELSE 0 END)  AS Escalated,
           SUM(CASE WHEN Status = 'Linked'      THEN 1 ELSE 0 END)  AS Linked,
           (SELECT COUNT(*) FROM Users WHERE IsActive = 1)          AS ActiveUsers,
           CAST(SYSDATETIME() AS DATE)                               AS SnapshotDate
       FROM Complaints
   ) AS src ON tgt.SnapshotDate = src.SnapshotDate
   WHEN MATCHED THEN
       UPDATE SET
           TotalComplaints = src.TotalComplaints,
           Submitted       = src.Submitted,
           InProgress      = src.InProgress,
           Resolved        = src.Resolved,
           Rejected        = src.Rejected,
           Reopened        = src.Reopened,
           Escalated       = src.Escalated,
           Linked          = src.Linked,
           ActiveUsers     = src.ActiveUsers
   WHEN NOT MATCHED BY TARGET THEN
       INSERT (TotalComplaints, Submitted, InProgress, Resolved, Rejected,
               Reopened, Escalated, Linked, ActiveUsers, SnapshotDate)
       VALUES (src.TotalComplaints, src.Submitted, src.InProgress, src.Resolved,
               src.Rejected, src.Reopened, src.Escalated, src.Linked,
               src.ActiveUsers, src.SnapshotDate);
END
GO

-- US12: Platform-wide stats for Admin dashboard.
CREATE OR ALTER PROCEDURE usp_GetPlatformStats
AS
BEGIN
   SET NOCOUNT ON;
   SELECT
       COUNT(*)                                                   AS TotalComplaints,
       SUM(CASE WHEN Status = 'Submitted'   THEN 1 ELSE 0 END)  AS Submitted,
       SUM(CASE WHEN Status = 'In Progress' THEN 1 ELSE 0 END)  AS InProgress,
       SUM(CASE WHEN Status = 'Resolved'    THEN 1 ELSE 0 END)  AS Resolved,
       SUM(CASE WHEN Status = 'Rejected'    THEN 1 ELSE 0 END)  AS Rejected,
       SUM(CASE WHEN Status = 'Re-opened'   THEN 1 ELSE 0 END)  AS Reopened,
       SUM(CASE WHEN Status = 'Escalated'   THEN 1 ELSE 0 END)  AS Escalated,
       SUM(CASE WHEN Status = 'Linked'      THEN 1 ELSE 0 END)  AS Linked
   FROM Complaints;

   SELECT COUNT(*) AS ActiveUsers FROM Users WHERE IsActive = 1;

   SELECT
       SUM(CASE WHEN r.RoleName = 'Citizen'    THEN 1 ELSE 0 END) AS TotalCitizens,
       SUM(CASE WHEN r.RoleName = 'Solver'     THEN 1 ELSE 0 END) AS TotalSolvers,
       SUM(CASE WHEN r.RoleName = 'PWG'        THEN 1 ELSE 0 END) AS TotalPWG,
       SUM(CASE WHEN r.RoleName = 'SuperAdmin' THEN 1 ELSE 0 END) AS TotalAdmins
   FROM Users u JOIN Roles r ON r.RoleId = u.RoleId;
END
GO
-- ============================================================
-- SECTION 13: SEED DATA
-- Password hash = SHA-256("Password@123") — dev/test ONLY.
-- Production MUST use app-layer hashing (bcrypt/Argon2/PBKDF2).
-- ============================================================

-- PATCH_001: Disable RLS until DbConnectionInterceptor is implemented.
-- Safe for dev/test. Re-enable with STATE = ON once interceptor is wired.
ALTER SECURITY POLICY dbo.ComplaintRLS WITH (STATE = OFF);
GO

INSERT INTO Roles (RoleName) VALUES ('SuperAdmin'),('Citizen'),('Solver'),('PWG');
GO

INSERT INTO IssueCategories (CategoryName, Description) VALUES
   ('Roads & Potholes',      'Road damage, potholes, and pavement issues'),
   ('Water & Sanitation',    'Water supply, drainage, and sewage issues'),
   ('Electricity',           'Street lights, power cuts, and electrical hazards'),
   ('Garbage & Waste',       'Garbage collection, illegal dumping, and waste management'),
   ('Parks & Public Spaces', 'Maintenance of parks, gardens, and public areas'),
   ('Noise Pollution',       'Excessive noise, illegal construction, and disturbances'),
   ('Public Safety',         'Safety hazards, broken rails, and unsafe structures'),
   ('Animal Control',        'Stray animals and animal welfare concerns');
GO

INSERT INTO MilestoneDefinitions (MilestoneName, PointsThreshold, Description) VALUES
   ('First Report',       10,  'Awarded for reaching 10 civic points.'),
   ('Active Citizen',     50,  'Awarded for reaching 50 civic points.'),
   ('Community Champion', 100, 'Awarded for reaching 100 civic points.'),
   ('City Hero',          250, 'Awarded for reaching 250 civic points.'),
   ('Civic Legend',       500, 'Awarded for reaching 500 civic points.');
GO

-- F21: Seed valid status transitions.
INSERT INTO ComplaintStatusTransitions (FromStatus, ToStatus, AllowedRoles) VALUES
   ('Submitted',   'In Progress', 'Solver'),
   ('Submitted',   'Rejected',    'Solver'),
   ('Submitted',   'Linked',      'SuperAdmin,System'),
   ('Submitted',   'Escalated',   'SuperAdmin,System'),
   ('In Progress', 'Resolved',    'Solver'),
   ('In Progress', 'Rejected',    'Solver'),
   ('In Progress', 'Escalated',   'SuperAdmin,System'),
   ('Resolved',    'Re-opened',   'Citizen'),
   ('Re-opened',   'In Progress', 'Solver'),
   ('Re-opened',   'Rejected',    'Solver'),
   ('Re-opened',   'Escalated',   'SuperAdmin,System'),
   ('Escalated',   'In Progress', 'Solver'),
   ('Escalated',   'Resolved',    'Solver');
GO

-- F14: Seed localities before any entity that references them.
INSERT INTO Localities (LocalityName, City, State) VALUES
   ('Bengaluru', 'Bengaluru', 'Karnataka'),    -- LocalityId 1
   ('New Delhi', 'New Delhi', 'Delhi');         -- LocalityId 2
GO

-- ─── USERS ───────────────────────────────────────────────────
-- SHA-256 hash of "Password@123"
DECLARE @H VARCHAR(256) = 'ff7bd97b1a7789ddd2775122fd6817f3173672da9f802ceec57f284325bf589f';
DECLARE @SA TINYINT, @CI TINYINT, @SO TINYINT, @PW TINYINT;
SELECT @SA = RoleId FROM Roles WHERE RoleName = 'SuperAdmin';
SELECT @CI = RoleId FROM Roles WHERE RoleName = 'Citizen';
SELECT @SO = RoleId FROM Roles WHERE RoleName = 'Solver';
SELECT @PW = RoleId FROM Roles WHERE RoleName = 'PWG';

DECLARE @BLR INT, @NDL INT;
SELECT @BLR = LocalityId FROM Localities WHERE LocalityName = 'Bengaluru';
SELECT @NDL = LocalityId FROM Localities WHERE LocalityName = 'New Delhi';

-- UserId 1001: SuperAdmin
INSERT INTO Users (RoleId,FullName,Email,PasswordHash,Phone,Address,LocalityId,IsActive,IsApproved)
VALUES (@SA,'Super Admin','admin@fixmycity.gov',@H,'9000000000','1 Admin Block, New Delhi',@NDL,1,1);

-- UserId 1002, 1003: Citizens (Bengaluru)
INSERT INTO Users (RoleId,FullName,Email,PasswordHash,Phone,Address,LocalityId,AadhaarNo,IsActive,IsApproved)
VALUES
   (@CI,'Amit Sharma','amit.sharma@gmail.com',@H,'9811111111','42 MG Road, Bengaluru',@BLR,'123456789012',1,1),
   (@CI,'Priya Nair','priya.nair@gmail.com',@H,'9822222222','8 Koramangala, Bengaluru',@BLR,'234567890123',1,1);

-- UserId 1004, 1005: Solvers (Bengaluru)
INSERT INTO Users (RoleId,FullName,Email,PasswordHash,Phone,Address,LocalityId,IsActive,IsApproved)
VALUES
   (@SO,'BBMP Roads Dept','bbmp.roads@gov.in',@H,'9833333333','BBMP HQ, Hudson Circle, Bengaluru',@BLR,1,1),
   (@SO,'BWSSB Water Board','bwssb@gov.in',@H,'9844444444','Cauvery Bhavan, Bengaluru',@BLR,1,1);

-- UserId 1006: PWG (Bengaluru)
INSERT INTO Users (RoleId,FullName,Email,PasswordHash,Phone,Address,LocalityId,IsActive,IsApproved)
VALUES (@PW,'GreenBengaluru NGO','contact@greenbengaluru.org',@H,'9855555555','23 NGO Colony, Bengaluru',@BLR,1,1);

-- UserId 1007: Citizen — will earn "First Report" certificate via seed points (11 pts)
INSERT INTO Users (RoleId,FullName,Email,PasswordHash,Phone,Address,LocalityId,AadhaarNo,IsActive,IsApproved)
VALUES (@CI,'Rahul Kumar','rahul.kumar@gmail.com',@H,'9866666666','77 Jayanagar, Bengaluru',@BLR,'135792468012',1,1);

-- UserId 1008: Citizen — locked account (5 failed attempts)
INSERT INTO Users (RoleId,FullName,Email,PasswordHash,Phone,Address,LocalityId,AadhaarNo,
                  IsActive,IsApproved,FailedLoginAttempts,LockoutUntil)
VALUES (@CI,'Sneha Patel','sneha.patel@gmail.com',@H,'9877777777',
       '15 Whitefield Road, Bengaluru',@BLR,'998877665544',1,1,5,
       DATEADD(HOUR,24,SYSDATETIME()));
GO

-- OrgId 2001
INSERT INTO Organisations
   (UserId,OrgName,OrgType,RegistrationNo,ContactEmail,ContactPhone,Address,ApprovalStatus,ApprovedAt)
VALUES (1006,'GreenBengaluru NGO','NGO','KA-NGO-2018-0047',
       'contact@greenbengaluru.org','9855555555','23 NGO Colony, Bengaluru','Approved',SYSDATETIME());
GO

-- DeptId 3001, 3002
DECLARE @RC SMALLINT, @WC SMALLINT, @BLR2 INT;
SELECT @RC   = CategoryId FROM IssueCategories WHERE CategoryName = 'Roads & Potholes';
SELECT @WC   = CategoryId FROM IssueCategories WHERE CategoryName = 'Water & Sanitation';
SELECT @BLR2 = LocalityId FROM Localities WHERE LocalityName = 'Bengaluru';

INSERT INTO Departments
   (UserId,DeptName,Ministry,CategoryId,ContactEmail,ContactPhone,Address,LocalityId,ApprovalStatus,ApprovedAt)
VALUES
   (1004,'BBMP Roads & Infrastructure','Urban Development',@RC,
    'bbmp.roads@gov.in','9833333333','BBMP HQ, Hudson Circle, Bengaluru',@BLR2,'Approved',SYSDATETIME()),
   (1005,'BWSSB Water & Sanitation','Water Resources',@WC,
    'bwssb@gov.in','9844444444','Cauvery Bhavan, Bengaluru',@BLR2,'Approved',SYSDATETIME());
GO

-- DepartmentCategories: primary category entries auto-seeded from Departments.
INSERT INTO DepartmentCategories (DeptId, CategoryId, IsPrimary, Priority)
SELECT d.DeptId, d.CategoryId, 1, 1
FROM   Departments d
WHERE  NOT EXISTS (
   SELECT 1 FROM DepartmentCategories dc
   WHERE dc.DeptId = d.DeptId AND dc.CategoryId = d.CategoryId);
GO

-- ─── COMPLAINTS (9; covers all 7 lifecycle statuses) ─────────

DECLARE @RD INT, @WD INT, @RC2 SMALLINT, @WC2 SMALLINT, @BLR3 INT;
SELECT @RD   = DeptId     FROM Departments     WHERE UserId = 1004;
SELECT @WD   = DeptId     FROM Departments     WHERE UserId = 1005;
SELECT @RC2  = CategoryId FROM IssueCategories WHERE CategoryName = 'Roads & Potholes';
SELECT @WC2  = CategoryId FROM IssueCategories WHERE CategoryName = 'Water & Sanitation';
SELECT @BLR3 = LocalityId FROM Localities      WHERE LocalityName = 'Bengaluru';

-- 5001: Submitted
INSERT INTO Complaints
   (CitizenUserId,DeptId,CategoryId,Title,Description,LocalityId,Address,Criticality,Status,Latitude,Longitude)
VALUES (1002,@RD,@RC2,
       'Large pothole on MG Road near Brigade Road signal',
       'A large pothole approximately 2 feet wide is causing accidents near Brigade Road signal on MG Road.',
       @BLR3,'MG Road, near Brigade Road Signal, Bengaluru 560001',
       'High','Submitted',12.9716000,77.5946000);

-- 5002: In Progress
INSERT INTO Complaints
   (CitizenUserId,DeptId,CategoryId,Title,Description,LocalityId,Address,Criticality,Status,EstimatedResDate)
VALUES (1003,@WD,@WC2,
       'No water supply in Koramangala 4th Block for 3 days',
       'Residents of Koramangala 4th Block have had no water supply since Monday morning.',
       @BLR3,'Koramangala 4th Block, Bengaluru 560034',
       'Critical','In Progress',DATEADD(DAY,7,SYSDATETIME()));

-- 5003: Resolved
INSERT INTO Complaints
   (CitizenUserId,DeptId,CategoryId,Title,Description,LocalityId,Address,Criticality,Status,ResolvedAt)
VALUES (1002,@RD,@RC2,
       'Broken pavement tiles on Residency Road footpath',
       'Multiple tiles on the footpath along Residency Road are broken, posing a trip hazard.',
       @BLR3,'Residency Road, Bengaluru 560025',
       'Medium','Resolved',SYSDATETIME());

-- 5004: Rejected
INSERT INTO Complaints
   (CitizenUserId,DeptId,CategoryId,Title,Description,LocalityId,Address,Criticality,Status)
VALUES (1003,@RD,@RC2,
       'Street light out near BBMP office Jayanagar',
       'The street light in front of BBMP office Jayanagar 4th Block has been non-functional for a week.',
       @BLR3,'Jayanagar 4th Block, Bengaluru 560041',
       'Low','Rejected');

-- 5005: Re-opened
INSERT INTO Complaints
   (CitizenUserId,DeptId,CategoryId,Title,Description,LocalityId,Address,Criticality,Status)
VALUES (1007,@WD,@WC2,
       'Sewage overflow near Jayanagar bus stand',
       'Sewage is overflowing onto the road near Jayanagar bus stand, creating a health hazard.',
       @BLR3,'Jayanagar Bus Stand, Bengaluru 560041',
       'High','Re-opened');

-- 5006: Escalated (with contributions)
INSERT INTO Complaints
   (CitizenUserId,DeptId,CategoryId,Title,Description,LocalityId,Address,Criticality,Status)
VALUES (1007,@RD,@RC2,
       'Road collapse on Airport Road near Hebbal flyover',
       'A significant road collapse has occurred on Airport Road near Hebbal flyover. Urgent repair needed.',
       @BLR3,'Airport Road, Hebbal, Bengaluru 560024',
       'Critical','Escalated');

-- 5007: Submitted (original for duplicate pair)
INSERT INTO Complaints
   (CitizenUserId,DeptId,CategoryId,Title,Description,LocalityId,Address,Criticality,Status)
VALUES (1007,@RD,@RC2,
       'Illegal garbage dump near Jayanagar 3rd Block park',
       'An illegal garbage dump has appeared near the entrance to Jayanagar 3rd Block park.',
       @BLR3,'Jayanagar 3rd Block, Bengaluru 560011',
       'Medium','Submitted');

-- 5008: Linked (duplicate of 5007)
INSERT INTO Complaints
   (CitizenUserId,DeptId,CategoryId,Title,Description,LocalityId,Address,Criticality,Status,LinkedToComplaintId)
VALUES (1002,@RD,@RC2,
       'Garbage pile-up near Jayanagar park gate',
       'A large pile of uncollected garbage has formed near the main gate of Jayanagar park.',
       @BLR3,'Jayanagar 3rd Block Park Gate, Bengaluru 560011',
       'Medium','Linked',5007);

-- 5009: In Progress with PWG collaboration
INSERT INTO Complaints
   (CitizenUserId,DeptId,CategoryId,Title,Description,LocalityId,Address,Criticality,Status,EstimatedResDate)
VALUES (1003,@RD,@RC2,
       'Major road collapse BTM 2nd Stage near water tank',
       'A major road collapse has occurred in BTM 2nd Stage. Emergency repair required immediately.',
       @BLR3,'BTM 2nd Stage, near BWSSB Water Tank, Bengaluru 560076',
       'Critical','In Progress',DATEADD(DAY,14,SYSDATETIME()));
GO

-- ─── COMPLAINT TIMELINE ──────────────────────────────────────

INSERT INTO ComplaintTimeline (ComplaintId,ActorUserId,OldStatus,NewStatus,Remark) VALUES
   (5001,1002,NULL,'Submitted','Complaint submitted by citizen.'),
   (5002,1003,NULL,'Submitted','Complaint submitted by citizen.'),
   (5002,1005,'Submitted','In Progress','Team dispatched. Estimated resolution in 7 days.'),
   (5003,1002,NULL,'Submitted','Complaint submitted by citizen.'),
   (5003,1004,'Submitted','In Progress','Repair crew assigned. Work begins tomorrow.'),
   (5003,1004,'In Progress','Resolved','All broken tiles replaced with anti-slip tiles. Work complete.'),
   (5004,1003,NULL,'Submitted','Complaint submitted by citizen.'),
   (5004,1004,'Submitted','Rejected','Falls under BESCOM jurisdiction. Please re-file under Electricity.'),
   (5005,1007,NULL,'Submitted','Complaint submitted by citizen.'),
   (5005,1005,'Submitted','In Progress','Sewage team dispatched. Clearance in 3 days.'),
   (5005,1005,'In Progress','Resolved','Sewage blockage cleared and area sanitized.'),
   (5005,1007,'Resolved','Re-opened','Issue recurred within 2 days. Sewage backs up each evening.'),
   (5006,1007,NULL,'Submitted','Complaint submitted by citizen.'),
   (5006,1004,'Submitted','In Progress','Damage survey assigned. Heavy machinery required.'),
   (5006,NULL,'In Progress','Escalated','Auto-escalated after 30 days without resolution.'),
   (5007,1007,NULL,'Submitted','Complaint submitted by citizen.'),
   (5008,1002,NULL,'Submitted','Complaint submitted by citizen.'),
   (5008,1001,'Submitted','Linked','Linked to complaint #5007 as a duplicate issue.'),
   (5009,1003,NULL,'Submitted','Complaint submitted by citizen.'),
   (5009,1004,'Submitted','In Progress','Major collapse confirmed. Heavy machinery deployed.'),
   (5009,1006,'In Progress','In Progress','PWG update: Temporary barricades placed. Materials procured.');
GO

-- ─── COMPLAINT RATINGS ───────────────────────────────────────

INSERT INTO ComplaintRatings (ComplaintId,CitizenUserId,Stars,Comment) VALUES
   (5003,1002,4,'Good work by the BBMP team. Tiles look sturdy. Minor delay but acceptable.'),
   (5005,1007,2,'The issue recurred within 2 days. Fix was superficial. Very disappointed.');
GO

-- ─── DUPLICATE COMPLAINT LINK ────────────────────────────────

INSERT INTO DuplicateComplaintLinks (OriginalComplaintId,LinkedComplaintId,LinkedByUserId)
VALUES (5007,5008,1001);
GO

-- ─── ESCALATION LOG ─────────────────────────────────────────
-- F13: Single ActorUserId (NULL = Auto). F2: OriginalDeptId NOT NULL ensured by seed.

INSERT INTO EscalationLog
   (ComplaintId,EscalationTrigger,ActorUserId,OriginalDeptId,ReassignedToDeptId,Reason)
VALUES (5006,'Auto',NULL,3001,NULL,'Auto-escalated after 30 days without resolution.');
GO

-- ─── PWG PARTICIPATION REQUESTS ──────────────────────────────
-- F6: Filtered unique index on (ComplaintId, OrgId) WHERE Status = 'Pending'
--     allows the rejected entry for 5005 to coexist with an Approved entry.

INSERT INTO PWGParticipationRequests
   (ComplaintId,OrgId,SolverUserId,Status,RequestNote,DecisionNote,DecidedAt)
VALUES
   (5009,2001,1004,'Approved',
    'GreenBengaluru has heavy equipment and volunteers ready for road repair assistance.',
    'Approved. PWG may begin site coordination immediately.',SYSDATETIME()),
   (5006,2001,1004,'Pending',
    'GreenBengaluru can provide temporary road barricades and safety crew.',
    NULL,NULL),
   (5005,2001,1005,'Rejected',
    'Our team can assist with clearing and sanitization after the sewage is fixed.',
    'Rejected. Sewage repair requires specialized BWSSB equipment.',SYSDATETIME());
GO

-- ─── CONTRIBUTIONS ───────────────────────────────────────────
-- F5: TransactionRef is NOT NULL throughout.

INSERT INTO Contributions (ComplaintId,CitizenUserId,Amount,TransactionRef,PaymentStatus,CompletedAt)
VALUES
   (5006,1002,500.00,'TXN2024060001','Success',SYSDATETIME()),
   (5009,1007,250.00,'TXN2024060002','Pending',NULL);

INSERT INTO Contributions
   (ComplaintId,CitizenUserId,Amount,TransactionRef,PaymentStatus,FailureReason)
VALUES
   (5006,1007,100.00,'TXN2024060003','Failed','Card declined by payment gateway.');
GO

-- ─── COMPLAINT ATTACHMENTS (sole file store — F1) ────────────

INSERT INTO ComplaintAttachments
   (ComplaintId,TimelineId,UploadedByUserId,AttachmentType,FilePath,FileName,FileSizeKB)
VALUES
   -- Resolution photo for complaint 5003 (solver uploaded)
   (5003,NULL,1004,'Resolution',
    'complaints/5003/Resolution/tile_repair_complete.jpg','tile_repair_complete.jpg',420),
   -- Initial complaint photo for 5009 (citizen uploaded)
   (5009,NULL,1003,'Complaint',
    'complaints/5009/Complaint/road_collapse_photo.jpg','road_collapse_photo.jpg',890),
   -- PWG progress photo for 5009 (F4: correctly stored in ComplaintAttachments, not PhotoPath)
   (5009,NULL,1006,'PWGProgress',
    'complaints/5009/PWGProgress/barricades_placed.jpg','barricades_placed.jpg',610),
   -- Evidence for escalated complaint 5006 (citizen uploaded)
   (5006,NULL,1007,'Evidence',
    'complaints/5006/Evidence/road_damage_aerial.jpg','road_damage_aerial.jpg',1240);
GO

-- ─── PWG REPORTS ─────────────────────────────────────────────

INSERT INTO PWGReports
   (ComplaintId,ReportedOrgId,ReportedByUserId,ReportReason,
    AdminReviewedByUserId,AdminAction,AdminNote,ReportedAt,ReviewedAt,Status)
VALUES (5009,2001,1004,
       'GreenBengaluru was approved for complaint #5009 but stopped responding after initial deployment.',
       1001,'Warned',
       'First warning issued. Organisation notified to resume work or face suspension.',
       DATEADD(DAY,-5,SYSDATETIME()),DATEADD(DAY,-3,SYSDATETIME()),'Reviewed');

INSERT INTO PWGReports
   (ComplaintId,ReportedOrgId,ReportedByUserId,ReportReason,Status)
VALUES (5002,2001,1005,
       'GreenBengaluru submitted a participation request for a water supply complaint '
     + 'despite having no relevant expertise. Wasted solver review time.',
       'Pending');
GO

-- ─── USER INTERESTS ──────────────────────────────────────────

DECLARE @BLR4 INT;
SELECT @BLR4 = LocalityId FROM Localities WHERE LocalityName = 'Bengaluru';

DECLARE @RdC SMALLINT, @WsC SMALLINT, @GbC SMALLINT;
SELECT @RdC = CategoryId FROM IssueCategories WHERE CategoryName = 'Roads & Potholes';
SELECT @WsC = CategoryId FROM IssueCategories WHERE CategoryName = 'Water & Sanitation';
SELECT @GbC = CategoryId FROM IssueCategories WHERE CategoryName = 'Garbage & Waste';

INSERT INTO UserInterests (UserId,CategoryId,PreferredLocalityId) VALUES
   (1002,@RdC,@BLR4),   -- Amit: Roads & Potholes, Bengaluru
   (1003,@WsC,@BLR4),   -- Priya: Water & Sanitation, Bengaluru
   (1007,@RdC,@BLR4),   -- Rahul: Roads & Potholes, Bengaluru
   (1007,@GbC,NULL);    -- Rahul: also Garbage & Waste (any locality)
GO

-- ─── ML SCORES ───────────────────────────────────────────────

INSERT INTO ComplaintMLScores
   (ComplaintId,PredictedResolutionDate,ResolutionProbability,PriorityScore,PredictionModelVersion)
VALUES
   (5001,DATEADD(DAY,14,SYSDATETIME()),0.7800,72.50,'v1.0'),
   (5002,DATEADD(DAY, 7,SYSDATETIME()),0.8500,68.00,'v1.0'),
   (5006,DATEADD(DAY, 3,SYSDATETIME()),0.3500,97.00,'v1.0'),
   (5009,DATEADD(DAY,14,SYSDATETIME()),0.6200,95.50,'v1.0');
GO

-- ─── USER POINTS ─────────────────────────────────────────────
-- Amit (1002):         10 manual + 1 rating     = 11 pts (earns First Report cert)
-- Priya (1003):         5 manual                =  5 pts
-- GreenBengaluru(1006):25 manual + 2 PWG update = 27 pts (earns First Report cert)
-- Rahul (1007):        10 manual + 1 rating     = 11 pts (earns First Report cert)

INSERT INTO UserPoints (UserId,Points) VALUES
   (1002,11),(1003,5),(1006,27),(1007,11);
GO

-- ─── POINTS LEDGER ───────────────────────────────────────────
-- F22: RefComplaintId + RefMilestoneId typed FKs replace the generic ReferenceId.

INSERT INTO PointsLedger (UserId,PointsDelta,Reason,RefComplaintId,RefMilestoneId) VALUES
   (1002,10,'ManualAward',     NULL,NULL),
   (1002, 1,'ComplaintRated',  5003,NULL),
   (1003, 5,'ManualAward',     NULL,NULL),
   (1006,25,'ManualAward',     NULL,NULL),
   (1006, 2,'PWGProgressUpdate',5009,NULL),
   (1007,10,'ManualAward',     NULL,NULL),
   (1007, 1,'ComplaintRated',  5005,NULL);
GO

-- ─── CERTIFICATES ────────────────────────────────────────────

DECLARE @M1 INT;
SELECT @M1 = MilestoneId FROM MilestoneDefinitions WHERE MilestoneName = 'First Report';

INSERT INTO Certificates (UserId,MilestoneId,Milestone,VerificationCode,FilePath) VALUES
   (1002,@M1,'First Report','FMC2024CERT001AMIT1002',  NULL),
   (1006,@M1,'First Report','FMC2024CERT002GREEN1006', NULL),
   (1007,@M1,'First Report','FMC2024CERT003RAHUL1007', NULL);

-- F22: Certificate milestone ledger entries use RefMilestoneId typed FK.
INSERT INTO PointsLedger (UserId,PointsDelta,Reason,RefComplaintId,RefMilestoneId) VALUES
   (1002,0,'CertificateMilestone',NULL,@M1),
   (1006,0,'CertificateMilestone',NULL,@M1),
   (1007,0,'CertificateMilestone',NULL,@M1);
GO

-- ─── NOTIFICATION PREFERENCES ────────────────────────────────

INSERT INTO NotificationPreferences (UserId)
SELECT UserId FROM Users
WHERE NOT EXISTS (
   SELECT 1 FROM NotificationPreferences np WHERE np.UserId = Users.UserId);
GO

-- ─── NOTIFICATIONS ───────────────────────────────────────────

INSERT INTO Notifications (UserId,ComplaintId,Message,NotificationType,Channel) VALUES
   (1002,5001,'Your complaint #5001 has been submitted successfully.','StatusChange','InApp'),
   (1004,5001,'New complaint #5001 has been routed to your department.','NewAssignment','InApp'),
   (1003,5002,'Your complaint #5002 has been submitted successfully.','StatusChange','InApp'),
   (1005,5002,'New complaint #5002 has been routed to your department.','NewAssignment','InApp'),
   (1003,5002,'Your complaint #5002 status changed to: In Progress.','StatusChange','InApp'),
   (1004,5003,'New complaint #5003 has been routed to your department.','NewAssignment','InApp'),
   (1002,5003,'Your complaint #5003 status changed to: Resolved.','StatusChange','InApp'),
   (1003,5004,'Your complaint #5004 has been submitted successfully.','StatusChange','InApp'),
   (1003,5004,'Your complaint #5004 status changed to: Rejected.','StatusChange','InApp'),
   (1007,5005,'Your complaint #5005 has been submitted successfully.','StatusChange','InApp'),
   (1007,5005,'Your complaint #5005 status changed to: Resolved.','StatusChange','InApp'),
   (1005,5005,'Complaint #5005 has been re-opened. Reason: Issue recurred within 2 days.','StatusChange','InApp'),
   (1007,5006,'Your complaint #5006 has been escalated for priority review.','StatusChange','InApp'),
   (1001,5006,'Complaint #5006 has been auto-escalated. Admin review required.','StatusChange','InApp'),
   (1002,5008,'Your complaint #5008 has been linked to existing complaint #5007.','StatusChange','InApp'),
   (1006,5009,'Your participation request for complaint #5009 has been Approved.','PWGDecision','InApp'),
   (1006,5005,'Your participation request for complaint #5005 has been Rejected.','PWGDecision','InApp'),
   (1006,5009,'A report has been filed against your organisation for complaint #5009.','PWGDecision','InApp'),
   (1001,5009,'PWG report filed for complaint #5009. Review required.','PWGDecision','InApp'),
   (1006,5009,'Admin reviewed the report against your organisation. Action: Warned.','PWGDecision','InApp'),
   (1002,NULL,'Congratulations! You earned the "First Report" certificate!','StatusChange','InApp'),
   (1006,NULL,'Congratulations! You earned the "First Report" certificate!','StatusChange','InApp'),
   (1007,NULL,'Congratulations! You earned the "First Report" certificate!','StatusChange','InApp');
GO

-- Populate initial scoreboard snapshot via MERGE (F12).
EXEC usp_RefreshScoreboard;
GO

-- ============================================================
-- SECTION 14: POST-DEPLOYMENT VALIDATION QUERIES
-- Run these after deployment to confirm a clean schema and seed.
-- ============================================================

PRINT '=== TABLE COUNT (expected: 29) ===';
SELECT COUNT(*) AS TableCount
FROM sys.tables WHERE type_desc = 'USER_TABLE';

PRINT '=== STORED PROCEDURE COUNT (expected: 37) ===';
SELECT COUNT(*) AS SPCount
FROM sys.objects WHERE type = 'P';

PRINT '=== FUNCTION COUNT (expected: 3) ===';
SELECT COUNT(*) AS FuncCount
FROM sys.objects WHERE type IN ('FN','IF','TF');

PRINT '=== NONCLUSTERED INDEX COUNT ===';
SELECT COUNT(*) AS NCICount
FROM sys.indexes
WHERE type_desc = 'NONCLUSTERED'
 AND is_primary_key = 0
 AND is_unique_constraint = 0
 AND object_id IN (SELECT object_id FROM sys.tables);

PRINT '=== ALL TABLES ===';
SELECT name AS TableName
FROM sys.tables WHERE type_desc = 'USER_TABLE' ORDER BY name;

PRINT '=== USER ROSTER ===';
SELECT u.UserId, u.FullName, r.RoleName, l.LocalityName,
      u.IsActive, u.IsApproved, u.IsBanned,
      u.FailedLoginAttempts,
      CASE WHEN u.LockoutUntil IS NOT NULL THEN 'LOCKED' ELSE 'OK' END AS LoginStatus
FROM Users u
JOIN Roles r ON r.RoleId = u.RoleId
LEFT JOIN Localities l ON l.LocalityId = u.LocalityId
ORDER BY u.UserId;

PRINT '=== COMPLAINTS ===';
SELECT c.ComplaintId, c.Status, c.CitizenUserId,
      c.DeptId, l.LocalityName, c.Criticality
FROM Complaints c
LEFT JOIN Localities l ON l.LocalityId = c.LocalityId
ORDER BY c.ComplaintId;

PRINT '=== STATUS TRANSITIONS ===';
SELECT FromStatus, ToStatus, AllowedRoles
FROM ComplaintStatusTransitions ORDER BY TransitionId;

PRINT '=== ROW COUNTS ===';
SELECT 'ComplaintTimeline'        AS [Table], COUNT(*) AS Rows FROM ComplaintTimeline   UNION ALL
SELECT 'ComplaintRatings',                    COUNT(*)         FROM ComplaintRatings     UNION ALL
SELECT 'PWGParticipationRequests',            COUNT(*)         FROM PWGParticipationRequests UNION ALL
SELECT 'Contributions',                       COUNT(*)         FROM Contributions        UNION ALL
SELECT 'ComplaintAttachments',                COUNT(*)         FROM ComplaintAttachments UNION ALL
SELECT 'EscalationLog',                       COUNT(*)         FROM EscalationLog        UNION ALL
SELECT 'DuplicateComplaintLinks',             COUNT(*)         FROM DuplicateComplaintLinks UNION ALL
SELECT 'PWGReports',                          COUNT(*)         FROM PWGReports           UNION ALL
SELECT 'UserPoints',                          COUNT(*)         FROM UserPoints            UNION ALL
SELECT 'PointsLedger',                        COUNT(*)         FROM PointsLedger         UNION ALL
SELECT 'Certificates',                        COUNT(*)         FROM Certificates          UNION ALL
SELECT 'Notifications',                       COUNT(*)         FROM Notifications         UNION ALL
SELECT 'UserInterests',                       COUNT(*)         FROM UserInterests         UNION ALL
SELECT 'ComplaintMLScores',                   COUNT(*)         FROM ComplaintMLScores     UNION ALL
SELECT 'ScoreboardSnapshot',                  COUNT(*)         FROM ScoreboardSnapshot    UNION ALL
SELECT 'PasswordResetTokens',                 COUNT(*)         FROM PasswordResetTokens   UNION ALL
SELECT 'Localities',                          COUNT(*)         FROM Localities            UNION ALL
SELECT 'AuditLog',                            COUNT(*)         FROM AuditLog
UNION ALL
SELECT 'PlatformStatsSnapshot',               COUNT(*)         FROM PlatformStatsSnapshot;

PRINT '=== SCOREBOARD SNAPSHOT ===';
SELECT ss.Rank, ss.FullName, l.LocalityName, ss.Points, ss.SnapshotAt
FROM ScoreboardSnapshot ss
LEFT JOIN Localities l ON l.LocalityId = ss.LocalityId
ORDER BY ss.Rank;

PRINT '=== FK INTEGRITY CHECK (0 rows = clean) ===';
-- Users referencing non-existent localities
SELECT 'Orphaned Users.LocalityId' AS Issue, COUNT(*) AS Count
FROM Users u
WHERE u.LocalityId IS NOT NULL
 AND NOT EXISTS (SELECT 1 FROM Localities l WHERE l.LocalityId = u.LocalityId)
UNION ALL
-- Complaints referencing non-existent localities
SELECT 'Orphaned Complaints.LocalityId', COUNT(*)
FROM Complaints c
WHERE NOT EXISTS (SELECT 1 FROM Localities l WHERE l.LocalityId = c.LocalityId)
UNION ALL
-- Contributions with no matching Complaint
SELECT 'Orphaned Contributions.ComplaintId', COUNT(*)
FROM Contributions ct
WHERE NOT EXISTS (SELECT 1 FROM Complaints c WHERE c.ComplaintId = ct.ComplaintId)
UNION ALL
-- PointsLedger typed FK check
SELECT 'Orphaned PointsLedger.RefComplaintId', COUNT(*)
FROM PointsLedger pl
WHERE pl.RefComplaintId IS NOT NULL
 AND NOT EXISTS (SELECT 1 FROM Complaints c WHERE c.ComplaintId = pl.RefComplaintId);

PRINT '';
PRINT '============================================================';
PRINT 'FixMyCityDB Sprint 2 FIXED — deployment complete.';
PRINT 'All F1-F22 audit fixes applied.';
PRINT 'Sprint 2 Plan fixes applied: FIX-01 (confirmed), FIX-02, FIX-03, FIX-04.';
PRINT 'REMINDER: Configure DbConnectionInterceptor for RLS context.';
PRINT '============================================================';
GO