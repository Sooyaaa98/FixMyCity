-- ═══════════════════════════════════════════════════════════════════════════
-- FixMyCity — Comprehensive Seed Data  [FIXED — schema-aligned]
-- ═══════════════════════════════════════════════════════════════════════════
-- Run this AFTER:
--   1. FixMyCityDB_Sprint2_FIXED.sql
--   2. 01_AI_Tables_Addition.sql
--   3. 02_UserRefreshTokens.sql
--
-- FIX SUMMARY (all errors from error log resolved):
--   • Table names corrected: UserRecommendationCache, ScoreboardSnapshot,
--     PlatformStatsSnapshot, EscalationLog, ComplaintTimeline (no plural 's')
--   • Localities: removed non-existent IsActive column
--   • NotificationPreferences: EnableInApp→InAppEnabled, EnableEmail→EmailDigestEnabled,
--     removed EnableSMS (not in schema), EnableWeeklyDigest→EmailDigestEnabled
--   • Complaints: removed AssignedToUserId, IsEscalated, EscalatedAt,
--     ReopenedAt, ReopenReason (not in schema); 'Closed'→'Resolved' (not in CHECK)
--   • Departments: added missing required columns Address, LocalityId,
--     CategoryId, ApprovalStatus; removed non-existent IsActive
--   • Organisations: removed IsApproved/IsActive; use ApprovalStatus
--   • MilestoneDefinitions: PointsRequired→PointsThreshold
--   • ComplaintRatings: Rating→Stars
--   • Contributions: Status→PaymentStatus; removed non-existent CreatedAt
--   • PWGParticipationRequests: RequestedByUserId→OrgId+SolverUserId,
--     ReviewedAt→DecidedAt, ReviewedByUserId removed (not in schema)
--   • PWGReports: corrected to actual schema (ReportedOrgId, ReportedByUserId,
--     ReportReason, Status); removed WorkDescription, StartDate, EndDate,
--     FundUtilized, CreatedAt (not in schema)
--   • PointsLedger: Points→PointsDelta, RelatedComplaintId→RefComplaintId,
--     CreatedAt→EarnedAt
--   • UserPoints: TotalPoints→Points, LastUpdated→UpdatedAt
--   • Certificates: IssuedToUserId→UserId; added required Milestone text column
--   • DuplicateComplaintLinks: corrected to actual schema
--     (OriginalComplaintId, LinkedComplaintId, LinkedByUserId)
--   • EscalationLog: corrected to actual schema (EscalationTrigger, ActorUserId,
--     OriginalDeptId); removed EscalationReason, NotifiedAdminUserId
--   • ScoreboardSnapshot: use stored proc usp_RefreshScoreboard instead of
--     direct insert (schema mismatch); direct insert columns corrected
--   • ComplaintMLScores: removed non-existent CreatedAt (use ScoredAt default)
-- ═══════════════════════════════════════════════════════════════════════════

USE FixMyCityDB;
GO

SET NOCOUNT ON;
PRINT 'Starting seed data...';

-- ═══════════════════════════════════════════════════════════════════════════
-- Wipe & re-seed (idempotent — safe to re-run)
-- ═══════════════════════════════════════════════════════════════════════════

-- Reset RLS so we can write all rows
EXEC sp_set_session_context N'UserRole', N'SuperAdmin', @read_only = 0;

-- Delete in dependency order
DELETE FROM dbo.UserRefreshTokens;
DELETE FROM dbo.ComplaintTags;
DELETE FROM dbo.ComplaintEmbeddings;
DELETE FROM dbo.AIDecisionLog;
DELETE FROM dbo.UserRecommendationCache;       -- FIX: was "RecommendationCache"
DELETE FROM dbo.AIPendingScoreQueue;
DELETE FROM dbo.ComplaintMLScores;
DELETE FROM dbo.UserInterests;
DELETE FROM dbo.Certificates;
DELETE FROM dbo.PointsLedger;
DELETE FROM dbo.UserPoints;
DELETE FROM dbo.ScoreboardSnapshot;            -- FIX: was "ScoreboardSnapshots"
DELETE FROM dbo.PlatformStatsSnapshot;         -- FIX: was "PlatformStarSnapshots"
DELETE FROM dbo.MilestoneDefinitions;
DELETE FROM dbo.NotificationPreferences;
DELETE FROM dbo.Notifications;
DELETE FROM dbo.PWGReports;
DELETE FROM dbo.PWGParticipationRequests;
DELETE FROM dbo.EscalationLog;                 -- FIX: was "EscalationLogs"
DELETE FROM dbo.DuplicateComplaintLinks;
DELETE FROM dbo.ComplaintRatings;
DELETE FROM dbo.Contributions;
DELETE FROM dbo.ComplaintAttachments;
DELETE FROM dbo.ComplaintTimeline;             -- FIX: was "ComplaintTimelines"
DELETE FROM dbo.Complaints;
DELETE FROM dbo.DepartmentCategories;
DELETE FROM dbo.Departments;
DELETE FROM dbo.Organisations;
DELETE FROM dbo.PasswordResetTokens;
DELETE FROM dbo.AuditLog;
DELETE FROM dbo.Users;
DELETE FROM dbo.IssueCategories;
DELETE FROM dbo.Localities;
DELETE FROM dbo.Roles;

DBCC CHECKIDENT ('dbo.Users',         RESEED, 0);
DBCC CHECKIDENT ('dbo.Complaints',    RESEED, 0);
DBCC CHECKIDENT ('dbo.Departments',   RESEED, 0);
DBCC CHECKIDENT ('dbo.Organisations', RESEED, 0);

-- ═══════════════════════════════════════════════════════════════════════════
-- 1. ROLES
-- ═══════════════════════════════════════════════════════════════════════════

SET IDENTITY_INSERT dbo.Roles ON;
INSERT INTO dbo.Roles (RoleId, RoleName) VALUES
    (1, 'SuperAdmin'),
    (2, 'Citizen'),
    (3, 'Solver'),
    (4, 'PWG');
SET IDENTITY_INSERT dbo.Roles OFF;

PRINT '✓ Roles seeded';

-- ═══════════════════════════════════════════════════════════════════════════
-- 2. LOCALITIES (8 Bengaluru areas)
-- FIX: removed IsActive column — not in schema
-- ═══════════════════════════════════════════════════════════════════════════

SET IDENTITY_INSERT dbo.Localities ON;
INSERT INTO dbo.Localities (LocalityId, LocalityName, City, State) VALUES
    (1, 'Indiranagar',     'Bengaluru', 'Karnataka'),
    (2, 'Koramangala',     'Bengaluru', 'Karnataka'),
    (3, 'Whitefield',      'Bengaluru', 'Karnataka'),
    (4, 'HSR Layout',      'Bengaluru', 'Karnataka'),
    (5, 'Jayanagar',       'Bengaluru', 'Karnataka'),
    (6, 'Marathahalli',    'Bengaluru', 'Karnataka'),
    (7, 'BTM Layout',      'Bengaluru', 'Karnataka'),
    (8, 'Electronic City', 'Bengaluru', 'Karnataka');
SET IDENTITY_INSERT dbo.Localities OFF;

PRINT '✓ Localities seeded';

-- ═══════════════════════════════════════════════════════════════════════════
-- 3. ISSUE CATEGORIES
-- ═══════════════════════════════════════════════════════════════════════════

SET IDENTITY_INSERT dbo.IssueCategories ON;
INSERT INTO dbo.IssueCategories (CategoryId, CategoryName, Description) VALUES
    (1, 'Road & Infrastructure', 'Potholes, broken roads, footpaths, bridges'),
    (2, 'Water Supply',          'Water leaks, supply issues, drainage, sewage'),
    (3, 'Electricity',           'Power outages, faulty wiring, streetlights'),
    (4, 'Garbage & Sanitation',  'Waste collection, illegal dumping, public toilets'),
    (5, 'Public Safety',         'Crime, accidents, hazards, dangerous areas'),
    (6, 'Parks & Trees',         'Public park maintenance, tree trimming, landscaping'),
    (7, 'Noise Pollution',       'Construction noise, traffic, loud establishments'),
    (8, 'Other',                 'General civic complaints not in above categories');
SET IDENTITY_INSERT dbo.IssueCategories OFF;

PRINT '✓ IssueCategories seeded';

-- ═══════════════════════════════════════════════════════════════════════════
-- 4. USERS
--   Password = "Password123!" → SHA256 hex (computed via SQL HASHBYTES)
--
--   CRITICAL: hash must match the API's HashPassword in AuthController.cs,
--   which uses Encoding.UTF8.GetBytes(password). HASHBYTES on a VARCHAR
--   literal hashes UTF-8 bytes for ASCII content (single-byte codepoints in
--   the default 1252 codepage are byte-identical to UTF-8). Using N'...'
--   (NVARCHAR / UTF-16 LE) here would produce a DIFFERENT hash and break
--   every login. Do NOT add an N prefix.
-- ═══════════════════════════════════════════════════════════════════════════

DECLARE @PwdHash VARCHAR(256) =
    LOWER(CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', 'Password123!'), 2));

SET IDENTITY_INSERT dbo.Users ON;

-- 4a. SuperAdmin (UserId = 1)
INSERT INTO dbo.Users (UserId, FullName, Email, PasswordHash, Phone, Address,
                       LocalityId, RoleId, IsActive, IsApproved, IsBanned)
VALUES (1, 'Super Admin', 'admin@fixmycity.in', @PwdHash, '+919811000001',
        'BBMP HQ, NR Square', 1, 1, 1, 1, 0);

-- 4b. Solvers (Government Department reps) — UserId 2..4
INSERT INTO dbo.Users (UserId, FullName, Email, PasswordHash, Phone, Address,
                       LocalityId, RoleId, IsActive, IsApproved, IsBanned) VALUES
    (2, 'Rakesh Iyer',  'rakesh.bbmp@fixmycity.in',  @PwdHash, '+919811000002', 'BBMP Road Engineering Division', 1, 3, 1, 1, 0),
    (3, 'Priya Menon',  'priya.bwssb@fixmycity.in',  @PwdHash, '+919811000003', 'BWSSB Cauvery Bhavan',          2, 3, 1, 1, 0),
    (4, 'Suresh Naidu', 'suresh.bescom@fixmycity.in', @PwdHash, '+919811000004', 'BESCOM K.R. Circle',            3, 3, 1, 1, 0);

-- 4c. PWG reps — UserId 5..7
INSERT INTO dbo.Users (UserId, FullName, Email, PasswordHash, Phone, Address,
                       LocalityId, RoleId, IsActive, IsApproved, IsBanned) VALUES
    (5, 'Anjali Rao',    'anjali@cleanbengaluru.org', @PwdHash, '+919811000005', 'Clean Bengaluru NGO Office',    4, 4, 1, 1, 0),
    (6, 'Vikram Bhatia', 'vikram@iiscbangalore.org',  @PwdHash, '+919811000006', 'IISc Student Volunteer Group',  5, 4, 1, 1, 0),
    (7, 'Meera Joshi',   'meera@infosys-csr.org',     @PwdHash, '+919811000007', 'Infosys CSR Bangalore',         8, 4, 1, 1, 0);

-- 4d. Citizens — UserId 8..19 (12 users across all localities)
INSERT INTO dbo.Users (UserId, FullName, Email, PasswordHash, Phone, Address,
                       LocalityId, RoleId, IsActive, IsApproved, IsBanned, AadhaarNo) VALUES
    (8,  'Arjun Reddy',     'arjun.r@example.com',   @PwdHash, '+919811000008', '12 Magrath Rd',  1, 2, 1, 1, 0, '123456789012'),
    (9,  'Kavya Sharma',    'kavya.s@example.com',   @PwdHash, '+919811000009', '45 5th Block',   2, 2, 1, 1, 0, '123456789013'),
    (10, 'Rohan Kumar',     'rohan.k@example.com',   @PwdHash, '+919811000010', '78 ITPL Rd',     3, 2, 1, 1, 0, '123456789014'),
    (11, 'Sneha Patel',     'sneha.p@example.com',   @PwdHash, '+919811000011', '23 27th Main',   4, 2, 1, 1, 0, '123456789015'),
    (12, 'Karthik Iyer',    'karthik.i@example.com', @PwdHash, '+919811000012', '56 11th Cross',  5, 2, 1, 1, 0, '123456789016'),
    (13, 'Aishwarya Nair',  'aish.n@example.com',    @PwdHash, '+919811000013', '90 EPIP Zone',   6, 2, 1, 1, 0, '123456789017'),
    (14, 'Deepak Singh',    'deepak.s@example.com',  @PwdHash, '+919811000014', '102 16th Main',  7, 2, 1, 1, 0, '123456789018'),
    (15, 'Pooja Verma',     'pooja.v@example.com',   @PwdHash, '+919811000015', '300 Phase 1',    8, 2, 1, 1, 0, '123456789019'),
    (16, 'Sanjay Mishra',   'sanjay.m@example.com',  @PwdHash, '+919811000016', '76 100ft Rd',    1, 2, 1, 1, 0, '123456789020'),
    (17, 'Ananya Gupta',    'ananya.g@example.com',  @PwdHash, '+919811000017', '34 80ft Rd',     2, 2, 1, 1, 0, '123456789021'),
    (18, 'Vikrant Chauhan', 'vikrant.c@example.com', @PwdHash, '+919811000018', '67 Brookefield', 3, 2, 1, 1, 0, '123456789022'),
    (19, 'Lakshmi Pillai',  'lakshmi.p@example.com', @PwdHash, '+919811000019', '12 Sector 1',    4, 2, 1, 1, 0, '123456789023');

SET IDENTITY_INSERT dbo.Users OFF;
PRINT '✓ Users seeded (1 admin + 3 solvers + 3 PWG reps + 12 citizens = 19 users)';

-- ═══════════════════════════════════════════════════════════════════════════
-- 5. DEPARTMENTS (Solver organizations)
-- FIX: added required columns Address, LocalityId, CategoryId, ApprovalStatus
--      removed non-existent IsActive column
-- ═══════════════════════════════════════════════════════════════════════════

SET IDENTITY_INSERT dbo.Departments ON;
INSERT INTO dbo.Departments (DeptId, UserId, DeptName, Ministry, CategoryId,
                              ContactEmail, ContactPhone, Address, LocalityId,
                              ApprovalStatus, ApprovedAt) VALUES
    (1, 2, 'BBMP Road Engineering', 'Urban Development', 1,
     'roads@bbmp.gov.in',    '+918022001001', 'BBMP HQ, Hudson Circle, Bengaluru', 1, 'Approved', SYSDATETIME()),
    (2, 3, 'BWSSB Water Supply',    'Public Works',      2,
     'support@bwssb.gov.in', '+918022002002', 'Cauvery Bhavan, Bengaluru',          2, 'Approved', SYSDATETIME()),
    (3, 4, 'BESCOM Electricity',    'Energy',            3,
     'help@bescom.co.in',    '+918022003003', 'BESCOM K.R. Circle, Bengaluru',      3, 'Approved', SYSDATETIME());
SET IDENTITY_INSERT dbo.Departments OFF;

-- Map departments to categories (US48: routing by category)
INSERT INTO dbo.DepartmentCategories (DeptId, CategoryId, IsPrimary, Priority) VALUES
    (1, 1, 1, 1),  -- BBMP → Road & Infrastructure (primary)
    (1, 4, 0, 2),  -- BBMP also handles Garbage & Sanitation
    (1, 6, 0, 3),  -- BBMP also handles Parks & Trees
    (2, 2, 1, 1),  -- BWSSB → Water Supply (primary)
    (3, 3, 1, 1);  -- BESCOM → Electricity (primary)

PRINT '✓ Departments seeded (BBMP, BWSSB, BESCOM)';

-- ═══════════════════════════════════════════════════════════════════════════
-- 6. ORGANISATIONS (PWG)
-- FIX: removed non-existent IsApproved, IsActive columns;
--      use ApprovalStatus per schema
-- ═══════════════════════════════════════════════════════════════════════════

SET IDENTITY_INSERT dbo.Organisations ON;
INSERT INTO dbo.Organisations (OrgId, UserId, OrgName, OrgType, RegistrationNo,
                                ContactEmail, ContactPhone, Address,
                                ApprovalStatus, ApprovedAt) VALUES
    (1, 5, 'Clean Bengaluru NGO',        'NGO',           'NGO-KA-2019-1234',
     'contact@cleanbengaluru.org', '+918022004004', 'NGO Office, Bengaluru', 'Approved', SYSDATETIME()),
    (2, 6, 'IISc Civic Volunteer Group', 'Student Group', 'STU-KA-2021-5678',
     'civic@iisc.ac.in',           '+918022005005', 'IISc Campus, Bengaluru', 'Approved', SYSDATETIME()),
    (3, 7, 'Infosys CSR Foundation',     'CSR',           'CSR-KA-2018-9012',
     'csr@infosys.com',            '+918022006006', 'Infosys Campus, Bengaluru', 'Approved', SYSDATETIME());
SET IDENTITY_INSERT dbo.Organisations OFF;

PRINT '✓ Organisations seeded';

-- ═══════════════════════════════════════════════════════════════════════════
-- 7. NOTIFICATION PREFERENCES (default for all users)
-- FIX: EnableInApp→InAppEnabled, EnableEmail→EmailDigestEnabled,
--      removed EnableSMS (not in schema), EnableWeeklyDigest→EmailDigestEnabled
-- ═══════════════════════════════════════════════════════════════════════════

INSERT INTO dbo.NotificationPreferences (UserId, InAppEnabled, PushEnabled, EmailDigestEnabled, DigestFrequencyDays)
SELECT UserId, 1, 1, 1, 7 FROM dbo.Users;

PRINT '✓ Notification preferences set';

-- ═══════════════════════════════════════════════════════════════════════════
-- 8. USER INTERESTS (for AI recommendations US26 + US51)
-- ═══════════════════════════════════════════════════════════════════════════

INSERT INTO dbo.UserInterests (UserId, CategoryId, PreferredLocalityId) VALUES
    -- Arjun Reddy: roads + electricity in Indiranagar + HSR
    (8,  1, NULL), (8,  3, NULL), (8,  NULL, 1), (8,  NULL, 4),
    -- Kavya Sharma: water + sanitation in Koramangala
    (9,  2, NULL), (9,  4, NULL), (9,  NULL, 2),
    -- Rohan Kumar: roads + parks in Whitefield
    (10, 1, NULL), (10, 6, NULL), (10, NULL, 3),
    -- Sneha Patel: garbage + safety in HSR
    (11, 4, NULL), (11, 5, NULL), (11, NULL, 4),
    -- Karthik Iyer: noise + water in Jayanagar
    (12, 7, NULL), (12, 2, NULL), (12, NULL, 5);

PRINT '✓ User interests seeded';

-- ═══════════════════════════════════════════════════════════════════════════
-- 9. MILESTONE DEFINITIONS (gamification — US27)
-- FIX: PointsRequired→PointsThreshold (actual column name in schema)
-- ═══════════════════════════════════════════════════════════════════════════

SET IDENTITY_INSERT dbo.MilestoneDefinitions ON;
INSERT INTO dbo.MilestoneDefinitions (MilestoneId, MilestoneName, Description, PointsThreshold, IsActive) VALUES
    (1, 'Bronze Citizen',  'Submit your first civic complaint',           10,  1),
    (2, 'Silver Citizen',  'Active contributor — 5 verified complaints',  50,  1),
    (3, 'Gold Citizen',    'Civic champion — 15 complaints + ratings',   150,  1),
    (4, 'Platinum Citizen','Top contributor — 30 complaints + funding',  300,  1),
    (5, 'Diamond Citizen', 'Hall of Fame — 50+ complaints',              500,  1);
SET IDENTITY_INSERT dbo.MilestoneDefinitions OFF;

PRINT '✓ Milestone definitions seeded';

-- ═══════════════════════════════════════════════════════════════════════════
-- 10. COMPLAINTS — 30 complaints
-- FIX: removed AssignedToUserId, IsEscalated, EscalatedAt, ReopenedAt,
--      ReopenReason — none of these exist in the Complaints schema.
--      'Closed' status is not in CHECK constraint; changed to 'Resolved'.
-- ═══════════════════════════════════════════════════════════════════════════

-- Seed the ComplaintStatusTransitions table (required for usp_UpdateComplaintStatus)
-- Guard: only insert rows that don't already exist (Sprint 2 schema seeds these too)
INSERT INTO dbo.ComplaintStatusTransitions (FromStatus, ToStatus, AllowedRoles)
SELECT v.FromStatus, v.ToStatus, v.AllowedRoles
FROM (VALUES
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
    ('Escalated',   'Resolved',    'Solver')
) AS v(FromStatus, ToStatus, AllowedRoles)
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.ComplaintStatusTransitions cst
    WHERE cst.FromStatus = v.FromStatus AND cst.ToStatus = v.ToStatus
);

SET IDENTITY_INSERT dbo.Complaints ON;

-- ── SUBMITTED (newly filed, awaiting solver pickup) ──
INSERT INTO dbo.Complaints (ComplaintId, CitizenUserId, CategoryId, Title, Description,
    LocalityId, Address, Criticality, Latitude, Longitude, Status, DeptId, SubmittedAt) VALUES
(1,  8,  1, 'Massive pothole on 100 Feet Road',
     'A deep pothole near Sony World junction causing two-wheeler accidents daily.',
     1, '100 Feet Rd near Sony World, Indiranagar', 'High', 12.9784, 77.6408, 'Submitted', 1, DATEADD(HOUR,-2, SYSDATETIME())),
(2,  9,  2, 'No water for 3 days in Block 4',
     'Entire Block 4 of Koramangala has had no water supply since Monday. Tankers are charging Rs 1000.',
     2, '5th Block, Koramangala', 'Critical', 12.9352, 77.6245, 'Submitted', 2, DATEADD(HOUR,-5, SYSDATETIME())),
(3,  10, 3, 'Street lights off in ITPL Main Road',
     'Stretch of 800m on ITPL Main Road has been dark for 4 days. Major safety risk for women at night.',
     3, 'ITPL Main Rd, Whitefield', 'High', 12.9698, 77.7500, 'Submitted', 3, DATEADD(HOUR,-8, SYSDATETIME())),
(4,  11, 4, 'Garbage pile near apartment gate',
     'Garbage collection truck has skipped our area for 5 days. Stench is unbearable.',
     4, 'Sector 6, HSR Layout', 'Medium', 12.9082, 77.6476, 'Submitted', 1, DATEADD(DAY,-1, SYSDATETIME())),
(5,  12, 7, 'Loud construction at 11 PM',
     'A nearby site is operating heavy machinery well past midnight every day.',
     5, '11th Main, 4th Block, Jayanagar', 'Medium', 12.9234, 77.5836, 'Submitted', 1, DATEADD(DAY,-1, SYSDATETIME()));

-- ── IN PROGRESS (solver acknowledged, working on it) ──
-- FIX: removed AssignedToUserId from INSERT (not in schema)
INSERT INTO dbo.Complaints (ComplaintId, CitizenUserId, CategoryId, Title, Description,
    LocalityId, Address, Criticality, Latitude, Longitude, Status, DeptId, SubmittedAt) VALUES
(6,  13, 1, 'Broken footpath near metro station',
     'Several broken slabs near Marathahalli metro entry — wheelchair users cannot pass.',
     6, 'Marathahalli Metro Stn', 'High', 12.9591, 77.6974, 'In Progress', 1, DATEADD(DAY,-3, SYSDATETIME())),
(7,  14, 2, 'Sewage overflow in BTM 2nd stage',
     'Manholes have been overflowing for 2 days. Likely a blockage further down the line.',
     7, '2nd Stage, BTM Layout', 'High', 12.9165, 77.6101, 'In Progress', 2, DATEADD(DAY,-2, SYSDATETIME())),
(8,  15, 3, 'Frequent power cuts in EC',
     'Electronic City Phase 1 has had 6+ outages this week — average 2 hours each.',
     8, 'Phase 1, Electronic City', 'Medium', 12.8456, 77.6601, 'In Progress', 3, DATEADD(DAY,-4, SYSDATETIME())),
(9,  16, 4, 'Illegal garbage dumping near temple',
     'Construction debris being dumped near the Hanuman temple every night.',
     1, 'Near Hanuman Temple, Indiranagar', 'Medium', 12.9762, 77.6395, 'In Progress', 1, DATEADD(DAY,-2, SYSDATETIME())),
(10, 17, 1, 'Crater-sized pothole on 80ft Rd',
     'Drivers swerving into the wrong lane to avoid it. An accident is imminent.',
     2, '80ft Rd, Koramangala', 'Critical', 12.9333, 77.6294, 'In Progress', 1, DATEADD(DAY,-5, SYSDATETIME())),
(11, 18, 6, 'Trees blocking road after storm',
     'Two large trees have fallen across the main road after yesterday''s storm.',
     3, 'Brookefield Main Rd, Whitefield', 'High', 12.9701, 77.7160, 'In Progress', 1, DATEADD(DAY,-1, SYSDATETIME())),
(12, 19, 4, 'Public toilet vandalism',
     'Public toilet near park has been vandalized — doors broken, lights smashed.',
     4, 'Park Rd, HSR Layout', 'Medium', 12.9143, 77.6411, 'In Progress', 1, DATEADD(DAY,-3, SYSDATETIME()));

-- ── RESOLVED (fixed by solver, awaiting citizen rating) ──
-- FIX: removed AssignedToUserId from INSERT
INSERT INTO dbo.Complaints (ComplaintId, CitizenUserId, CategoryId, Title, Description,
    LocalityId, Address, Criticality, Latitude, Longitude, Status, DeptId, SubmittedAt, ResolvedAt) VALUES
(13, 8,  3, 'Streetlight not working',
     'Streetlight outside house has been out for a week.',
     1, '12 Magrath Rd, Indiranagar', 'Low', 12.9778, 77.6411, 'Resolved', 3, DATEADD(DAY,-12, SYSDATETIME()), DATEADD(DAY,-2, SYSDATETIME())),
(14, 9,  2, 'Leaking water pipe near junction',
     'Continuous water leakage from a burst pipe causing road damage.',
     2, '5th Block junction, Koramangala', 'Medium', 12.9351, 77.6243, 'Resolved', 2, DATEADD(DAY,-15, SYSDATETIME()), DATEADD(DAY,-3, SYSDATETIME())),
(15, 10, 1, 'Speed breaker without warning paint',
     'Unmarked speed breaker causing vehicles to bottom out at night.',
     3, 'Phoenix Marketcity Rd', 'Medium', 12.9956, 77.6960, 'Resolved', 1, DATEADD(DAY,-20, SYSDATETIME()), DATEADD(DAY,-5, SYSDATETIME())),
(16, 11, 4, 'Overflowing public bin',
     'Bin near bus stop overflowing for days.',
     4, 'BDA Complex, HSR', 'Low', 12.9092, 77.6498, 'Resolved', 1, DATEADD(DAY,-10, SYSDATETIME()), DATEADD(DAY,-1, SYSDATETIME())),
(17, 13, 6, 'Park playground needs repair',
     'Swings broken, slide cracked — kids cannot use the playground.',
     6, 'Marathahalli Park', 'Medium', 12.9590, 77.6975, 'Resolved', 1, DATEADD(DAY,-25, SYSDATETIME()), DATEADD(DAY,-8, SYSDATETIME())),
(18, 14, 5, 'Stray dog menace near school',
     'Pack of aggressive stray dogs near the school gate scaring children.',
     7, 'BTM 1st Stage School Rd', 'High', 12.9162, 77.6107, 'Resolved', 1, DATEADD(DAY,-18, SYSDATETIME()), DATEADD(DAY,-4, SYSDATETIME())),
(19, 15, 1, 'Manhole cover missing',
     'Open manhole on busy footpath — pedestrians at risk.',
     8, 'Hosur Rd, EC', 'Critical', 12.8430, 77.6620, 'Resolved', 1, DATEADD(DAY,-22, SYSDATETIME()), DATEADD(DAY,-6, SYSDATETIME()));

-- ── RE-OPENED (citizen unsatisfied) ──
-- FIX: removed ReopenedAt, ReopenReason (not in schema)
INSERT INTO dbo.Complaints (ComplaintId, CitizenUserId, CategoryId, Title, Description,
    LocalityId, Address, Criticality, Latitude, Longitude, Status, DeptId, SubmittedAt, ResolvedAt) VALUES
(20, 16, 1, 'Pothole "fixed" but already broken again',
     'Patch job lasted 3 days — pothole bigger than before.',
     1, 'CMH Rd, Indiranagar', 'High', 12.9784, 77.6418, 'Re-opened', 1, DATEADD(DAY,-30, SYSDATETIME()), DATEADD(DAY,-10, SYSDATETIME())),
(21, 17, 4, 'Garbage pile back within a week',
     'They cleared it once but the dumping continues. Need a permanent solution.',
     2, 'Forum Mall area, Koramangala', 'Medium', 12.9347, 77.6131, 'Re-opened', 1, DATEADD(DAY,-28, SYSDATETIME()), DATEADD(DAY,-9, SYSDATETIME()));

-- ── ESCALATED (>30 days unresolved, auto-flagged) ──
-- FIX: removed IsEscalated, EscalatedAt (not in schema)
INSERT INTO dbo.Complaints (ComplaintId, CitizenUserId, CategoryId, Title, Description,
    LocalityId, Address, Criticality, Latitude, Longitude, Status, DeptId, SubmittedAt) VALUES
(22, 18, 2, 'Major water leak ignored for a month',
     'Massive leak wasting thousands of litres daily — multiple reports unanswered.',
     3, 'Whitefield Main Rd', 'Critical', 12.9712, 77.7497, 'Escalated', 2, DATEADD(DAY,-35, SYSDATETIME())),
(23, 19, 3, 'Transformer fire risk',
     'Sparking transformer on residential road — could cause a major fire.',
     4, 'HSR Sector 2', 'Critical', 12.9075, 77.6485, 'Escalated', 3, DATEADD(DAY,-32, SYSDATETIME()));

-- ── REJECTED (solver determined not in scope) ──
-- FIX: removed AssignedToUserId
INSERT INTO dbo.Complaints (ComplaintId, CitizenUserId, CategoryId, Title, Description,
    LocalityId, Address, Criticality, Latitude, Longitude, Status, DeptId, SubmittedAt) VALUES
(24, 8,  8, 'Loud party every weekend',
     'Neighbour hosts loud parties every weekend, complaint to police pending.',
     1, '76 100ft Rd, Indiranagar', 'Low', 12.9780, 77.6411, 'Rejected', 1, DATEADD(DAY,-15, SYSDATETIME())),
(25, 9,  8, 'Cab driver charged extra',
     'Cab driver overcharged at airport pickup.',
     2, '5th Block, Koramangala', 'Low', 12.9352, 77.6240, 'Rejected', 1, DATEADD(DAY,-20, SYSDATETIME()));

-- ── RESOLVED — older completed complaints (was "Closed"; FIX: 'Closed' not in CHECK) ──
-- FIX: removed AssignedToUserId; changed Status 'Closed' → 'Resolved'
INSERT INTO dbo.Complaints (ComplaintId, CitizenUserId, CategoryId, Title, Description,
    LocalityId, Address, Criticality, Latitude, Longitude, Status, DeptId, SubmittedAt, ResolvedAt) VALUES
(26, 10, 4, 'Public toilet cleaning request',
     'Public toilet near park needs urgent cleaning.',
     3, 'Whitefield Park', 'Low', 12.9700, 77.7501, 'Resolved', 1, DATEADD(DAY,-45, SYSDATETIME()), DATEADD(DAY,-40, SYSDATETIME())),
(27, 11, 6, 'Park gate broken',
     'Main gate of community park broken after storm.',
     4, 'HSR Park', 'Low', 12.9100, 77.6470, 'Resolved', 1, DATEADD(DAY,-50, SYSDATETIME()), DATEADD(DAY,-44, SYSDATETIME())),
(28, 12, 2, 'Old leak finally fixed',
     'Long-running leak in residential area.',
     5, 'Jayanagar 4th Block', 'Medium', 12.9230, 77.5837, 'Resolved', 2, DATEADD(DAY,-60, SYSDATETIME()), DATEADD(DAY,-53, SYSDATETIME())),
(29, 13, 3, 'Streetlight upgrade',
     'Old sodium-vapour bulbs replaced with LEDs.',
     6, 'Marathahalli Bridge Rd', 'Low', 12.9594, 77.6970, 'Resolved', 3, DATEADD(DAY,-55, SYSDATETIME()), DATEADD(DAY,-49, SYSDATETIME())),
(30, 14, 1, 'Road resurfacing completed',
     'Entire stretch of road resurfaced after multiple complaints.',
     7, 'BTM 16th Main', 'Medium', 12.9163, 77.6105, 'Resolved', 1, DATEADD(DAY,-65, SYSDATETIME()), DATEADD(DAY,-58, SYSDATETIME()));

SET IDENTITY_INSERT dbo.Complaints OFF;
PRINT '✓ 30 complaints seeded across all statuses';

-- ═══════════════════════════════════════════════════════════════════════════
-- 11. COMPLAINT TIMELINES
-- FIX: table is ComplaintTimeline (no 's'); columns are ActorUserId+Remark
--      (not ChangedByUserId+Notes+ChangedAt in a direct insert).
--      Schema: ComplaintId, ActorUserId, OldStatus, NewStatus, Remark, CreatedAt
-- ═══════════════════════════════════════════════════════════════════════════

-- Submission timeline for all 30 complaints
INSERT INTO dbo.ComplaintTimeline (ComplaintId, ActorUserId, OldStatus, NewStatus, Remark)
SELECT ComplaintId, CitizenUserId, NULL, 'Submitted', 'Complaint submitted by citizen.'
FROM dbo.Complaints WHERE ComplaintId BETWEEN 1 AND 30;

-- IN PROGRESS transition (for complaints that moved past Submitted)
INSERT INTO dbo.ComplaintTimeline (ComplaintId, ActorUserId, OldStatus, NewStatus, Remark)
SELECT c.ComplaintId, d.UserId, 'Submitted', 'In Progress', 'Acknowledged. Team dispatched to inspect.'
FROM dbo.Complaints c
JOIN dbo.Departments d ON d.DeptId = c.DeptId
WHERE c.Status IN ('In Progress','Resolved','Re-opened','Escalated','Rejected');

-- RESOLVED transition
INSERT INTO dbo.ComplaintTimeline (ComplaintId, ActorUserId, OldStatus, NewStatus, Remark)
SELECT c.ComplaintId, d.UserId, 'In Progress', 'Resolved', 'Work completed. Please verify and rate.'
FROM dbo.Complaints c
JOIN dbo.Departments d ON d.DeptId = c.DeptId
WHERE c.Status IN ('Resolved','Re-opened') AND c.ResolvedAt IS NOT NULL;

-- RE-OPENED transition
INSERT INTO dbo.ComplaintTimeline (ComplaintId, ActorUserId, OldStatus, NewStatus, Remark)
SELECT ComplaintId, CitizenUserId, 'Resolved', 'Re-opened', 'Re-opened by citizen: issue recurred.'
FROM dbo.Complaints WHERE Status = 'Re-opened';

-- ESCALATED transition
INSERT INTO dbo.ComplaintTimeline (ComplaintId, ActorUserId, OldStatus, NewStatus, Remark)
SELECT ComplaintId, NULL, 'In Progress', 'Escalated', 'Auto-escalated: unresolved beyond 30 days.'
FROM dbo.Complaints WHERE Status = 'Escalated';

-- REJECTED transition
INSERT INTO dbo.ComplaintTimeline (ComplaintId, ActorUserId, OldStatus, NewStatus, Remark)
SELECT c.ComplaintId, d.UserId, 'In Progress', 'Rejected', 'Not within department jurisdiction.'
FROM dbo.Complaints c
JOIN dbo.Departments d ON d.DeptId = c.DeptId
WHERE c.Status = 'Rejected';

PRINT '✓ Complaint timelines seeded';

-- ═══════════════════════════════════════════════════════════════════════════
-- 12. COMPLAINT RATINGS
-- FIX: Rating→Stars (actual column name in schema)
-- ═══════════════════════════════════════════════════════════════════════════

INSERT INTO dbo.ComplaintRatings (ComplaintId, CitizenUserId, Stars, Comment, RatedAt) VALUES
    (13, 8,  5, 'Quick fix, very satisfied!',         DATEADD(DAY,-2, SYSDATETIME())),
    (14, 9,  4, 'Took time but properly fixed.',      DATEADD(DAY,-2, SYSDATETIME())),
    (15, 10, 5, 'Great job painting markings too.',   DATEADD(DAY,-4, SYSDATETIME())),
    (16, 11, 3, 'Took longer than expected.',          DATEADD(DAY,-1, SYSDATETIME())),
    (17, 13, 5, 'Kids are thrilled — new equipment!', DATEADD(DAY,-7, SYSDATETIME())),
    (18, 14, 4, 'Vaccinated and relocated. Thanks.',   DATEADD(DAY,-3, SYSDATETIME())),
    (19, 15, 5, 'Promptly covered, danger averted.',   DATEADD(DAY,-5, SYSDATETIME())),
    -- Re-opened ones got low ratings before re-open
    (20, 16, 1, 'Useless patch, will not last.',       DATEADD(DAY,-8, SYSDATETIME())),
    (21, 17, 2, 'Same problem every week.',            DATEADD(DAY,-7, SYSDATETIME())),
    -- Older resolved complaints
    (26, 10, 5, 'Clean now, thanks BBMP.',             DATEADD(DAY,-38, SYSDATETIME())),
    (27, 11, 4, 'Repaired quickly.',                   DATEADD(DAY,-42, SYSDATETIME())),
    (28, 12, 5, 'Finally fixed after years.',          DATEADD(DAY,-51, SYSDATETIME())),
    (29, 13, 5, 'LEDs are much brighter.',             DATEADD(DAY,-47, SYSDATETIME())),
    (30, 14, 5, 'Smooth driving now.',                 DATEADD(DAY,-55, SYSDATETIME()));

PRINT '✓ Ratings seeded';

-- ═══════════════════════════════════════════════════════════════════════════
-- 13. CONTRIBUTIONS
-- FIX: Status→PaymentStatus; removed non-existent CreatedAt column
-- ═══════════════════════════════════════════════════════════════════════════

INSERT INTO dbo.Contributions (ComplaintId, CitizenUserId, Amount, TransactionRef, PaymentStatus) VALUES
    (1,  8,  500.00,  'TXN-PAY-A001', 'Success'),
    (1,  16, 1000.00, 'TXN-PAY-A002', 'Success'),
    (3,  10, 2500.00, 'TXN-PAY-A003', 'Success'),
    (10, 17, 500.00,  'TXN-PAY-A004', 'Success'),
    (22, 18, 5000.00, 'TXN-PAY-A005', 'Success'),
    (23, 19, 3000.00, 'TXN-PAY-A006', 'Success');

PRINT '✓ Contributions seeded';

-- ═══════════════════════════════════════════════════════════════════════════
-- 14. PWG PARTICIPATION REQUESTS + REPORTS
-- FIX (PWGParticipationRequests): schema has OrgId+SolverUserId+RequestNote+
--      DecisionNote+DecidedAt; seed used RequestedByUserId, ReviewedAt,
--      ReviewedByUserId — corrected to match schema.
-- FIX (PWGReports): schema has ReportedOrgId, ReportedByUserId, ReportReason,
--      Status only (no WorkDescription, StartDate, EndDate, FundUtilized,
--      CreatedAt, OrgId). Corrected to actual schema columns.
-- ═══════════════════════════════════════════════════════════════════════════

INSERT INTO dbo.PWGParticipationRequests
    (ComplaintId, OrgId, SolverUserId, Status, RequestNote, DecisionNote, DecidedAt) VALUES
    (4,  1, 2, 'Pending',  'Clean Bengaluru available for garbage clearance.', NULL, NULL),
    (6,  2, 2, 'Approved', 'IISc volunteers ready for footpath repair.', 'Approved — begin site coordination.', DATEADD(DAY,-1, SYSDATETIME())),
    (9,  1, 2, 'Approved', 'Clean Bengaluru can handle illegal dumping site.', 'Approved.', DATEADD(HOUR,-12, SYSDATETIME())),
    (11, 2, 2, 'Approved', 'IISc team experienced in tree removal.', 'Approved.', DATEADD(HOUR,-10, SYSDATETIME())),
    (17, 3, 2, 'Approved', 'Infosys CSR can fund playground equipment.', 'Approved.', DATEADD(DAY,-8, SYSDATETIME()));

INSERT INTO dbo.PWGReports
    (ComplaintId, ReportedOrgId, ReportedByUserId, ReportReason, Status) VALUES
    (6,  2, 2, 'IISc Group stopped responding after initial deployment on complaint #6.', 'Pending'),
    (17, 3, 2, 'Infosys CSR completed playground work on complaint #17 — report for record.', 'Reviewed');

PRINT '✓ PWG requests + reports seeded';

-- ═══════════════════════════════════════════════════════════════════════════
-- 15. USER POINTS
-- FIX: TotalPoints→Points, LastUpdated→UpdatedAt (actual column names)
-- ═══════════════════════════════════════════════════════════════════════════

INSERT INTO dbo.UserPoints (UserId, Points) VALUES
    (8,  165),
    (9,  130),
    (10, 220),
    (11,  95),
    (12,  70),
    (13, 110),
    (14, 175),
    (15, 145),
    (16, 200),
    (17, 155),
    (18, 280),
    (19, 190);

-- ═══════════════════════════════════════════════════════════════════════════
-- 16. POINTS LEDGER
-- FIX: Points→PointsDelta, RelatedComplaintId→RefComplaintId, CreatedAt→EarnedAt
-- ═══════════════════════════════════════════════════════════════════════════

INSERT INTO dbo.PointsLedger (UserId, PointsDelta, Reason, RefComplaintId, EarnedAt) VALUES
    (8,  10, 'ComplaintSubmitted',   1,    DATEADD(HOUR,-2,  SYSDATETIME())),
    (8,  20, 'ComplaintSubmitted',   13,   DATEADD(DAY, -2,  SYSDATETIME())),
    (8,  15, 'ComplaintRated',       13,   DATEADD(DAY, -2,  SYSDATETIME())),
    (8,  50, 'ManualAward',          NULL, DATEADD(HOUR,-1,  SYSDATETIME())),
    (8,  70, 'ManualAward',          NULL, DATEADD(DAY, -1,  SYSDATETIME())),
    (10, 10, 'ComplaintSubmitted',   3,    DATEADD(HOUR,-8,  SYSDATETIME())),
    (10,100, 'ManualAward',          NULL, DATEADD(DAY, -1,  SYSDATETIME())),
    (10, 30, 'ComplaintSubmitted',   15,   DATEADD(DAY,-20,  SYSDATETIME())),
    (18,200, 'ManualAward',          NULL, DATEADD(DAY,-20,  SYSDATETIME())),
    (18, 30, 'ManualAward',          NULL, DATEADD(DAY, -2,  SYSDATETIME())),
    (18, 50, 'ManualAward',          NULL, DATEADD(DAY,-15,  SYSDATETIME()));

PRINT '✓ Points ledger seeded';

-- ═══════════════════════════════════════════════════════════════════════════
-- 17. CERTIFICATES
-- FIX: IssuedToUserId→UserId; added required Milestone text column
-- ═══════════════════════════════════════════════════════════════════════════

INSERT INTO dbo.Certificates (UserId, MilestoneId, Milestone, VerificationCode, IssuedAt) VALUES
    (8,  2, 'Silver Citizen', 'FMC-SLVR-' + LEFT(REPLACE(CAST(NEWID() AS VARCHAR(36)),'-',''), 8), DATEADD(DAY,-1, SYSDATETIME())),
    (10, 3, 'Gold Citizen',   'FMC-GOLD-' + LEFT(REPLACE(CAST(NEWID() AS VARCHAR(36)),'-',''), 8), DATEADD(DAY,-3, SYSDATETIME())),
    (18, 3, 'Gold Citizen',   'FMC-GOLD-' + LEFT(REPLACE(CAST(NEWID() AS VARCHAR(36)),'-',''), 8), DATEADD(DAY,-10, SYSDATETIME()));

PRINT '✓ Certificates seeded';

-- ═══════════════════════════════════════════════════════════════════════════
-- 18. NOTIFICATIONS
-- FIX: removed Title column (not in schema); NotificationType values must be
--      in ('StatusChange','NewAssignment','Registration','PWGDecision','WeeklyDigest')
-- ═══════════════════════════════════════════════════════════════════════════

INSERT INTO dbo.Notifications (UserId, NotificationType, Message, ComplaintId, IsRead) VALUES
    (8,  'StatusChange',  'Complaint #13 (Streetlight) has been resolved. Please rate it.', 13, 0),
    (2,  'NewAssignment', 'You have been assigned complaint #6 (Broken footpath).',          6,  1),
    (5,  'PWGDecision',   'Your request for complaint #9 has been approved by BBMP.',        9,  0),
    (1,  'StatusChange',  'Complaint #22 (Water leak) has exceeded SLA — auto-escalated.',  22, 0),
    (1,  'StatusChange',  'Complaint #23 (Transformer fire) has exceeded SLA — auto-escalated.', 23, 0),
    (16, 'StatusChange',  'Complaint #20 (Pothole) was re-opened — back to In Progress.',   20, 0);

PRINT '✓ Notifications seeded';

-- ═══════════════════════════════════════════════════════════════════════════
-- 19. ML SCORES
-- FIX: removed non-existent CreatedAt column (ScoredAt has DEFAULT)
-- ═══════════════════════════════════════════════════════════════════════════

INSERT INTO dbo.ComplaintMLScores
    (ComplaintId, PredictedResolutionDate, ResolutionProbability, PriorityScore, PredictionModelVersion) VALUES
    (1,  DATEADD(DAY,5,  SYSDATETIME()), 0.82, 78.5, 'v1.0.0-rules'),
    (2,  DATEADD(DAY,3,  SYSDATETIME()), 0.71, 92.0, 'v1.0.0-rules'),
    (3,  DATEADD(DAY,6,  SYSDATETIME()), 0.78, 75.0, 'v1.0.0-rules'),
    (4,  DATEADD(DAY,10, SYSDATETIME()), 0.85, 45.0, 'v1.0.0-rules'),
    (5,  DATEADD(DAY,12, SYSDATETIME()), 0.65, 40.0, 'v1.0.0-rules'),
    (6,  DATEADD(DAY,4,  SYSDATETIME()), 0.74, 72.5, 'v1.0.0-rules'),
    (7,  DATEADD(DAY,5,  SYSDATETIME()), 0.69, 80.0, 'v1.0.0-rules'),
    (10, DATEADD(DAY,2,  SYSDATETIME()), 0.55, 95.0, 'v1.0.0-rules'),
    (22, DATEADD(DAY,7,  SYSDATETIME()), 0.42, 99.0, 'v1.0.0-rules'),
    (23, DATEADD(DAY,4,  SYSDATETIME()), 0.48, 98.0, 'v1.0.0-rules');

PRINT '✓ ML scores seeded';

-- ═══════════════════════════════════════════════════════════════════════════
-- 20. COMPLAINT TAGS (AI-generated)
-- ═══════════════════════════════════════════════════════════════════════════

INSERT INTO dbo.ComplaintTags (ComplaintId, Tag, Score) VALUES
    (1,  'pothole',        0.95), (1,  'road damage',    0.91), (1,  'traffic hazard', 0.83),
    (2,  'water shortage', 0.97), (2,  'no supply',      0.92), (2,  'urgent',         0.88),
    (3,  'streetlight',    0.96), (3,  'dark area',      0.90), (3,  'safety',         0.85),
    (4,  'garbage',        0.94), (4,  'sanitation',     0.89), (4,  'collection',     0.81),
    (10, 'pothole',        0.97), (10, 'major',          0.94), (10, 'critical',       0.92),
    (22, 'water leak',     0.96), (22, 'wastage',        0.91), (22, 'long pending',   0.89);

PRINT '✓ Complaint tags seeded';

-- ═══════════════════════════════════════════════════════════════════════════
-- 21. DUPLICATE COMPLAINT LINKS
-- FIX: schema has OriginalComplaintId, LinkedComplaintId, LinkedByUserId, LinkedAt
--      seed used ComplaintId, DuplicateOfId, Similarity, DetectedAt — all wrong
-- ═══════════════════════════════════════════════════════════════════════════

INSERT INTO dbo.DuplicateComplaintLinks (OriginalComplaintId, LinkedComplaintId, LinkedByUserId) VALUES
    (4, 16, 1);  -- complaint 16 (overflowing bin) is a duplicate of complaint 4 (garbage pile)

PRINT '✓ Duplicate links seeded';

-- ═══════════════════════════════════════════════════════════════════════════
-- 22. ESCALATION LOG
-- FIX: schema has EscalationTrigger ('Auto'|'Manual'), ActorUserId (NULL=auto),
--      OriginalDeptId (NOT NULL), ReassignedToDeptId, Reason.
--      Seed used EscalationReason, NotifiedAdminUserId — wrong column names.
-- ═══════════════════════════════════════════════════════════════════════════

INSERT INTO dbo.EscalationLog
    (ComplaintId, EscalationTrigger, ActorUserId, OriginalDeptId, ReassignedToDeptId, Reason) VALUES
    (22, 'Auto', NULL, 2, NULL, 'In Progress > 30 days without resolution.'),
    (23, 'Auto', NULL, 3, NULL, 'In Progress > 30 days without resolution.');

PRINT '✓ Escalation logs seeded';

-- ═══════════════════════════════════════════════════════════════════════════
-- 23. AI DECISION LOG
-- ═══════════════════════════════════════════════════════════════════════════

INSERT INTO dbo.AIDecisionLog
    (ComplaintId, DecisionType, InputSummary, OutputSummary, Confidence, ModelVersion, WasOverridden) VALUES
    (1,  'Categorization', 'pothole 100 feet road indiranagar',  'Road & Infrastructure (0.94)',     0.94, 'v1.1.0-knn',       0),
    (1,  'PriorityScore',  'cat=1 crit=High days=0',             'priority=78.5 prob=0.82',          0.82, 'v1.0.0-rules',     0),
    (2,  'Categorization', 'no water 3 days koramangala',        'Water Supply (0.97)',               0.97, 'v1.1.0-knn',       0),
    (16, 'DuplicateFlag',  'overflowing bin BDA Complex HSR',    'top_sim=0.87 is_dup=True',         0.87, 'all-MiniLM-L6-v2', 0);

PRINT '✓ AI decision log seeded';

-- ═══════════════════════════════════════════════════════════════════════════
-- 24. SCOREBOARD SNAPSHOT
-- FIX: schema columns are UserId, FullName, LocalityId, Points, Rank, SnapshotAt
--      (and UNIQUE on UserId). Use the stored proc to populate correctly.
-- ═══════════════════════════════════════════════════════════════════════════

EXEC dbo.usp_RefreshScoreboard;

PRINT '✓ Scoreboard snapshot populated via usp_RefreshScoreboard';

-- ═══════════════════════════════════════════════════════════════════════════
-- DONE
-- ═══════════════════════════════════════════════════════════════════════════

PRINT '';
PRINT '═══════════════════════════════════════════════════════════════════════';
PRINT 'SEED DATA COMPLETE';
PRINT '═══════════════════════════════════════════════════════════════════════';
PRINT '';
PRINT 'Login credentials for testing (all passwords: Password123!):';
PRINT '  SuperAdmin: admin@fixmycity.in';
PRINT '  Solvers:    rakesh.bbmp@fixmycity.in / priya.bwssb@fixmycity.in / suresh.bescom@fixmycity.in';
PRINT '  PWG reps:   anjali@cleanbengaluru.org / vikram@iiscbangalore.org / meera@infosys-csr.org';
PRINT '  Citizens:   arjun.r@example.com .. lakshmi.p@example.com (12 citizens)';
PRINT '';
PRINT 'Coverage:';
PRINT '  30 complaints: Submitted=5, In Progress=7, Resolved=12, Re-opened=2,';
PRINT '  Escalated=2, Rejected=2';
PRINT '  14 ratings, 6 contributions, 5 PWG requests, 2 PWG reports';
PRINT '  10 ML scores, 18 AI tags, 1 duplicate link, 2 escalation logs';
PRINT '  12 user points records, 3 certificates, 5 milestone definitions';
PRINT '';
GO