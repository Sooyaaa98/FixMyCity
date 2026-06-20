-- ═══════════════════════════════════════════════════════════════════════════
-- FixMyCity — Massive Realistic Seed (Phase 1)
-- ═══════════════════════════════════════════════════════════════════════════
-- Run order:
--   00_Schema_Sprint2.sql  → 01_AI_Tables_Addition.sql →
--   02_UserRefreshTokens.sql → 03_SeedData.sql → 04_DB_Patch.sql →
--   05_MassiveSeed.sql  (THIS FILE)
--
-- Behaviour:
--   • Additive — does not wipe the 19 canonical users from 03_SeedData.sql.
--     Re-running the file is a no-op (guarded by an idempotency probe).
--   • Adds 81 users (7 solvers + 9 PWGs + 65 citizens), 7 departments,
--     9 organisations, 8 localities, 2 categories, 170 complaints across
--     all status spectrums, plus AI scores, tags, decisions, embeddings,
--     timelines, attachments, ratings, contributions, escalations,
--     duplicate links, notifications, points ledger entries, certificates,
--     recommendation cache rows, audit log and 14 days of platform stats.
--   • Designed to support:
--       - end-to-end frontend testing across every role
--       - AI model training (KNN categorisation, ALS recs, KeyBERT)
--       - dashboard analytics (per-category, per-locality, time-series)
--       - Prophet trend forecasting (14-day stats history)
--
-- All passwords: Password123!  (same SHA-256 UTF-8 hex hash as 03).
-- ═══════════════════════════════════════════════════════════════════════════

USE FixMyCityDB;
GO

SET NOCOUNT ON;

-- Preconditions
IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE Email = 'admin@fixmycity.in')
BEGIN
    RAISERROR('03_SeedData.sql must be run before 05_MassiveSeed.sql. Aborting.', 16, 1);
    RETURN;
END;

-- Idempotency probe — this email is unique to the massive seed
IF EXISTS (SELECT 1 FROM dbo.Users WHERE Email = 'anita.bbmp2@fixmycity.in')
BEGIN
    PRINT '⚠ Massive seed already applied — exiting without changes.';
    RETURN;
END;

-- Reset RLS context so we can write any row (RLS is OFF per PATCH_001 but be defensive)
EXEC sp_set_session_context N'UserRole', N'SuperAdmin', @read_only = 0;

PRINT 'Starting massive seed...';

-- Common password hash (Password123! → SHA-256 UTF-8 hex, lowercase)
DECLARE @PwdHash VARCHAR(64) =
    LOWER(CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', 'Password123!'), 2));

-- ═══════════════════════════════════════════════════════════════════════════
-- SECTION 1 — Additional localities (8 more, total 16)
-- ═══════════════════════════════════════════════════════════════════════════

SET IDENTITY_INSERT dbo.Localities ON;
INSERT INTO dbo.Localities (LocalityId, LocalityName, City, State) VALUES
    (9,  'Yelahanka',         'Bengaluru', 'Karnataka'),
    (10, 'Hebbal',             'Bengaluru', 'Karnataka'),
    (11, 'RT Nagar',           'Bengaluru', 'Karnataka'),
    (12, 'Banashankari',       'Bengaluru', 'Karnataka'),
    (13, 'JP Nagar',           'Bengaluru', 'Karnataka'),
    (14, 'Bellandur',          'Bengaluru', 'Karnataka'),
    (15, 'Sarjapur Road',      'Bengaluru', 'Karnataka'),
    (16, 'CV Raman Nagar',     'Bengaluru', 'Karnataka');
SET IDENTITY_INSERT dbo.Localities OFF;

PRINT '✓ Localities: +8 (total 16)';

-- ═══════════════════════════════════════════════════════════════════════════
-- SECTION 2 — Additional categories (2 more, total 10)
-- ═══════════════════════════════════════════════════════════════════════════

SET IDENTITY_INSERT dbo.IssueCategories ON;
INSERT INTO dbo.IssueCategories (CategoryId, CategoryName, Description) VALUES
    (9,  'Public Transport',  'Bus services, BMTC routes, metro feeders, stops, ticketing'),
    (10, 'Animal Welfare',    'Stray animals, animal cruelty, rescue, sterilisation drives');
SET IDENTITY_INSERT dbo.IssueCategories OFF;

PRINT '✓ Categories: +2 (total 10)';

-- ═══════════════════════════════════════════════════════════════════════════
-- SECTION 3 — New solvers (users + departments)
-- 5 approved + 1 pending + 1 rejected = 7 new solver users
-- UserIds 20-26, DeptIds 4-10
-- ═══════════════════════════════════════════════════════════════════════════

SET IDENTITY_INSERT dbo.Users ON;
INSERT INTO dbo.Users (UserId, FullName, Email, PasswordHash, Phone, Address,
                       LocalityId, RoleId, IsActive, IsApproved, IsBanned) VALUES
    (20, 'Anita Kumari',   'anita.bbmp2@fixmycity.in',  @PwdHash, '+919811000020', 'BBMP SWM Cell, Jayanagar',           5, 3, 1, 1, 0),
    (21, 'Venkat Rao',     'venkat.bbmp3@fixmycity.in', @PwdHash, '+919811000021', 'BBMP Parks Division, Whitefield',    3, 3, 1, 1, 0),
    (22, 'Ranjit Hegde',   'ranjit.kspcb@fixmycity.in', @PwdHash, '+919811000022', 'KSPCB Office, Koramangala',          2, 3, 1, 1, 0),
    (23, 'Nikhil Shetty',  'nikhil.bmtc@fixmycity.in',  @PwdHash, '+919811000023', 'BMTC Depot, Indiranagar',            1, 3, 1, 1, 0),
    (24, 'Divya Pillai',   'divya.animal@fixmycity.in', @PwdHash, '+919811000024', 'Animal Welfare Centre, Marathahalli', 6, 3, 1, 1, 0),
    -- Pending solver (IsApproved=0)
    (25, 'Prakash Naik',   'pending.solver@fixmycity.in', @PwdHash, '+919811000025', 'Awaiting approval, Yelahanka',     9, 3, 1, 0, 0),
    -- Rejected solver (IsApproved=0; Departments.ApprovalStatus='Rejected')
    (26, 'Rejected Solver','rejected.solver@fixmycity.in', @PwdHash, '+919811000026', 'Banashankari',                    12, 3, 1, 0, 0);
SET IDENTITY_INSERT dbo.Users OFF;

SET IDENTITY_INSERT dbo.Departments ON;
INSERT INTO dbo.Departments (DeptId, UserId, DeptName, Ministry, CategoryId,
                              ContactEmail, ContactPhone, Address, LocalityId,
                              ApprovalStatus, ApprovedAt) VALUES
    (4,  20, 'BBMP Solid Waste Management', 'Urban Development', 4,
         'swm@bbmp.gov.in',    '+918022004004', 'BBMP SWM Cell, Jayanagar HQ',           5, 'Approved', SYSDATETIME()),
    (5,  21, 'BBMP Parks & Trees',          'Urban Development', 6,
         'parks@bbmp.gov.in',  '+918022005005', 'BBMP Parks Division, Whitefield Ops',   3, 'Approved', SYSDATETIME()),
    (6,  22, 'KSPCB Pollution Control',     'Environment',       7,
         'noise@kspcb.gov.in', '+918022006006', 'KSPCB Office, Koramangala',             2, 'Approved', SYSDATETIME()),
    (7,  23, 'BMTC Public Transport',       'Transport',         9,
         'support@bmtc.gov.in', '+918022007007', 'BMTC Depot, Indiranagar',               1, 'Approved', SYSDATETIME()),
    (8,  24, 'KA Animal Welfare Board',     'Animal Husbandry', 10,
         'help@kawb.gov.in',   '+918022008008', 'Animal Welfare Centre, Marathahalli',   6, 'Approved', SYSDATETIME()),
    -- Pending dept
    (9,  25, 'BBMP Yelahanka Sub-division', 'Urban Development', 1,
         'yelahanka@bbmp.gov.in','+918022009009','Yelahanka Civic Centre',                9, 'Pending', NULL),
    -- Rejected dept
    (10, 26, 'Disputed Civic Body',          'Other',             5,
         'rejected@fixmycity.in','+918022010010','Banashankari Office',                  12, 'Rejected', NULL);
SET IDENTITY_INSERT dbo.Departments OFF;

-- Department→Category mappings for routing fallback
INSERT INTO dbo.DepartmentCategories (DeptId, CategoryId, IsPrimary, Priority) VALUES
    (4, 4,  1, 1),   -- BBMP SWM → Garbage (primary)
    (5, 6,  1, 1),   -- BBMP Parks → Parks (primary)
    (5, 1,  0, 5),   -- BBMP Parks → also Roads (footpaths near parks)
    (6, 7,  1, 1),   -- KSPCB → Noise (primary)
    (6, 4,  0, 4),   -- KSPCB → also Waste (pollution overlap)
    (7, 9,  1, 1),   -- BMTC → Public Transport (primary)
    (8, 10, 1, 1);   -- Animal Welfare → Animal Welfare (primary)

PRINT '✓ Solvers: +7 users (5 approved + 1 pending + 1 rejected), +7 departments';

-- ═══════════════════════════════════════════════════════════════════════════
-- SECTION 4 — New PWG organisations + users
-- 7 approved + 1 pending + 1 rejected = 9 new PWG users
-- UserIds 27-35, OrgIds 4-12
-- ═══════════════════════════════════════════════════════════════════════════

SET IDENTITY_INSERT dbo.Users ON;
INSERT INTO dbo.Users (UserId, FullName, Email, PasswordHash, Phone, Address,
                       LocalityId, RoleId, IsActive, IsApproved, IsBanned) VALUES
    (27, 'Manish Tiwari',      'manish@bbb.org',           @PwdHash, '+919811000027', 'BBB Office, Indiranagar',        1, 4, 1, 1, 0),
    (28, 'Sushma Bhardwaj',    'sushma@welfareforall.org', @PwdHash, '+919811000028', 'Welfare Trust, Banashankari',   12, 4, 1, 1, 0),
    (29, 'Rajiv Bhat',         'rajiv@cca.org',            @PwdHash, '+919811000029', 'CCA HQ, HSR Layout',             4, 4, 1, 1, 0),
    (30, 'Pankaj Joshi',       'pankaj@aravindcsr.org',    @PwdHash, '+919811000030', 'Aravind CSR Cell, Sarjapur',    15, 4, 1, 1, 0),
    (31, 'Bhavna Acharya',     'bhavna@saahasi.org',       @PwdHash, '+919811000031', 'Saahasi Volunteers, Jayanagar',  5, 4, 1, 1, 0),
    (32, 'Ashwin Bhat',        'ashwin@techmcsr.org',      @PwdHash, '+919811000032', 'Tech Mahindra Foundation, BTM',  7, 4, 1, 1, 0),
    (33, 'Aditi Sinha',        'aditi@sankalp.org',        @PwdHash, '+919811000033', 'Sankalp Initiative, Hebbal',    10, 4, 1, 1, 0),
    -- Pending PWG
    (34, 'Pending PWG Rep',    'pending.pwg@fixmycity.in', @PwdHash, '+919811000034', 'Pending NGO Office',            14, 4, 1, 0, 0),
    -- Rejected PWG
    (35, 'Rejected PWG Rep',   'rejected.pwg@fixmycity.in',@PwdHash, '+919811000035', 'Rejected NGO Office',           11, 4, 1, 0, 0);
SET IDENTITY_INSERT dbo.Users OFF;

SET IDENTITY_INSERT dbo.Organisations ON;
INSERT INTO dbo.Organisations (OrgId, UserId, OrgName, OrgType, RegistrationNo,
                                ContactEmail, ContactPhone, Address,
                                ApprovalStatus, ApprovedAt) VALUES
    (4,  27, 'Bangalore Bicycle Brigade',  'Community Association', 'CA-KA-2020-2001',
         'contact@bbb.org',           '+918022104001', 'BBB Office, Indiranagar',          'Approved', SYSDATETIME()),
    (5,  28, 'Welfare for All Trust',      'Welfare Group',         'WG-KA-2018-2002',
         'help@welfareforall.org',    '+918022104002', 'Welfare Trust, Banashankari',      'Approved', SYSDATETIME()),
    (6,  29, 'Citizens for Civic Action',  'NGO',                   'NGO-KA-2017-2003',
         'info@cca.org',              '+918022104003', 'CCA HQ, HSR Layout',               'Approved', SYSDATETIME()),
    (7,  30, 'Aravind Eye Hospital CSR',   'CSR',                   'CSR-KA-2016-2004',
         'csr@aravind.org',           '+918022104004', 'Aravind CSR Cell, Sarjapur',       'Approved', SYSDATETIME()),
    (8,  31, 'Saahasi Volunteers',         'Student Group',         'STU-KA-2022-2005',
         'volunteer@saahasi.org',     '+918022104005', 'Saahasi Volunteers, Jayanagar',    'Approved', SYSDATETIME()),
    (9,  32, 'Tech Mahindra Foundation',   'CSR',                   'CSR-KA-2015-2006',
         'foundation@techm.com',      '+918022104006', 'Tech Mahindra Foundation, BTM',    'Approved', SYSDATETIME()),
    (10, 33, 'Sankalp Initiative',         'NGO',                   'NGO-KA-2019-2007',
         'support@sankalp.org',       '+918022104007', 'Sankalp Initiative, Hebbal',       'Approved', SYSDATETIME()),
    (11, 34, 'Pending Approval NGO',       'NGO',                   'NGO-KA-2026-2008',
         'contact@pendingngo.org',    '+918022104008', 'Pending NGO Office, Bellandur',    'Pending',  NULL),
    (12, 35, 'Rejected Org',               'Other',                 'OTH-KA-2025-2009',
         'contact@rejectedorg.org',   '+918022104009', 'Rejected Org Office, RT Nagar',    'Rejected', NULL);
SET IDENTITY_INSERT dbo.Organisations OFF;

PRINT '✓ PWGs: +9 users (7 approved + 1 pending + 1 rejected), +9 organisations';
GO

-- ═══════════════════════════════════════════════════════════════════════════
-- SECTION 5 — Special-case citizens (edge cases for admin/auth testing)
-- UserIds 36-40
-- ═══════════════════════════════════════════════════════════════════════════

USE FixMyCityDB;
GO
SET NOCOUNT ON;
DECLARE @PwdHash VARCHAR(64) =
    LOWER(CONVERT(VARCHAR(64), HASHBYTES('SHA2_256', 'Password123!'), 2));

SET IDENTITY_INSERT dbo.Users ON;

-- 36: SSO-only citizen (no password)
INSERT INTO dbo.Users (UserId, FullName, Email, PasswordHash, Phone, Address,
                       LocalityId, RoleId, IsActive, IsApproved, IsBanned,
                       AadhaarNo, SSOProvider, SSOExternalId) VALUES
    (36, 'SSO Google User', 'sso.user@gmail.com', NULL, '+919811000036',
         'Google SSO - address unknown', 1, 2, 1, 1, 0,
         NULL, 'Google', 'google-sub-1234567890');

-- 37: Banned citizen (IsBanned=1 AND IsActive=0 per CHECK)
INSERT INTO dbo.Users (UserId, FullName, Email, PasswordHash, Phone, Address,
                       LocalityId, RoleId, IsActive, IsApproved, IsBanned,
                       BanReason, BannedAt, AadhaarNo) VALUES
    (37, 'Banned Spammer', 'banned.spammer@example.com', @PwdHash, '+919811000037',
         'Banned, address frozen', 2, 2, 0, 1, 1,
         'Repeated submission of false complaints', DATEADD(DAY,-20, SYSDATETIME()),
         '987654321099');

-- 38: Suspended citizen (IsSuspended=1)
INSERT INTO dbo.Users (UserId, FullName, Email, PasswordHash, Phone, Address,
                       LocalityId, RoleId, IsActive, IsApproved, IsBanned,
                       IsSuspended, AadhaarNo) VALUES
    (38, 'Suspended Citizen', 'suspended.user@example.com', @PwdHash, '+919811000038',
         '12 Suspended Lane', 3, 2, 1, 1, 0, 1, '987654321098');

-- 39: Locked-out citizen (LockoutUntil in the future)
INSERT INTO dbo.Users (UserId, FullName, Email, PasswordHash, Phone, Address,
                       LocalityId, RoleId, IsActive, IsApproved, IsBanned,
                       FailedLoginAttempts, LockoutUntil, AadhaarNo) VALUES
    (39, 'Locked Out User', 'locked.user@example.com', @PwdHash, '+919811000039',
         '45 Lockout Street', 4, 2, 1, 1, 0,
         5, DATEADD(MINUTE, 30, SYSDATETIME()), '987654321097');

-- 40: Deactivated citizen (IsActive=0 but not banned)
INSERT INTO dbo.Users (UserId, FullName, Email, PasswordHash, Phone, Address,
                       LocalityId, RoleId, IsActive, IsApproved, IsBanned,
                       AadhaarNo) VALUES
    (40, 'Deactivated User', 'deactivated.user@example.com', @PwdHash, '+919811000040',
         '67 Deactivated Rd', 5, 2, 0, 1, 0, '987654321096');

SET IDENTITY_INSERT dbo.Users OFF;

PRINT '✓ Special-case citizens: 5 (SSO, banned, suspended, locked, deactivated)';

-- ═══════════════════════════════════════════════════════════════════════════
-- SECTION 5b — Regular citizens (60 active citizens, UserIds 41-100)
-- ═══════════════════════════════════════════════════════════════════════════

SET IDENTITY_INSERT dbo.Users ON;
INSERT INTO dbo.Users (UserId, FullName, Email, PasswordHash, Phone, Address,
                       LocalityId, RoleId, IsActive, IsApproved, IsBanned, AadhaarNo) VALUES
    (41, 'Vikram Singh',       'vikram.s2@example.com',     @PwdHash, '+919815000041', '15 5th Cross, Indiranagar',          1, 2, 1, 1, 0, '987654322001'),
    (42, 'Pradeep Pawar',      'pradeep.p@example.com',     @PwdHash, '+919815000042', '23 6th A Main, Koramangala',         2, 2, 1, 1, 0, '987654322002'),
    (43, 'Naveen Kumar',       'naveen.k@example.com',      @PwdHash, '+919815000043', '99 ITPL Service Rd, Whitefield',     3, 2, 1, 1, 0, '987654322003'),
    (44, 'Aakash Mehta',       'aakash.m@example.com',      @PwdHash, '+919815000044', 'Sector 7, HSR Layout',               4, 2, 1, 1, 0, '987654322004'),
    (45, 'Riya Kapoor',        'riya.k@example.com',        @PwdHash, '+919815000045', '6th Block, Jayanagar',               5, 2, 1, 1, 0, '987654322005'),
    (46, 'Tanvi Desai',        'tanvi.d@example.com',       @PwdHash, '+919815000046', 'Brookefield, Marathahalli',          6, 2, 1, 1, 0, '987654322006'),
    (47, 'Manisha Gupta',      'manisha.g@example.com',     @PwdHash, '+919815000047', '3rd Stage, BTM Layout',              7, 2, 1, 1, 0, '987654322007'),
    (48, 'Neeraj Khanna',      'neeraj.k@example.com',      @PwdHash, '+919815000048', 'Phase 2, Electronic City',           8, 2, 1, 1, 0, '987654322008'),
    (49, 'Harsh Vardhan',      'harsh.v@example.com',       @PwdHash, '+919815000049', 'Sector 1, Yelahanka',                9, 2, 1, 1, 0, '987654322009'),
    (50, 'Aditi Sinha',        'aditi.sinha@example.com',   @PwdHash, '+919815000050', 'Outer Ring Rd, Hebbal',             10, 2, 1, 1, 0, '987654322010'),
    (51, 'Rajiv Bhat',         'rajiv.b@example.com',       @PwdHash, '+919815000051', 'RT Nagar 2nd Block',                11, 2, 1, 1, 0, '987654322011'),
    (52, 'Sunita Pandey',      'sunita.p@example.com',      @PwdHash, '+919815000052', 'BSK 1st Stage, Banashankari',       12, 2, 1, 1, 0, '987654322012'),
    (53, 'Asha Krishnan',      'asha.k@example.com',        @PwdHash, '+919815000053', '4th Phase, JP Nagar',               13, 2, 1, 1, 0, '987654322013'),
    (54, 'Manish Tiwari Jr',   'manish.tj@example.com',     @PwdHash, '+919815000054', 'Bellandur Lake Rd',                 14, 2, 1, 1, 0, '987654322014'),
    (55, 'Geeta Yadav',        'geeta.y@example.com',       @PwdHash, '+919815000055', 'Sarjapur Main Rd',                  15, 2, 1, 1, 0, '987654322015'),
    (56, 'Ravi Shastri',       'ravi.s@example.com',        @PwdHash, '+919815000056', 'CV Raman Nagar Phase 1',            16, 2, 1, 1, 0, '987654322016'),
    (57, 'Akash Verma',        'akash.v@example.com',       @PwdHash, '+919815000057', '5th Cross, Indiranagar',             1, 2, 1, 1, 0, '987654322017'),
    (58, 'Aarti Saxena',       'aarti.s@example.com',       @PwdHash, '+919815000058', '1st Block, Koramangala',             2, 2, 1, 1, 0, '987654322018'),
    (59, 'Sandeep Rao',        'sandeep.r@example.com',     @PwdHash, '+919815000059', 'Hope Farm, Whitefield',              3, 2, 1, 1, 0, '987654322019'),
    (60, 'Ramesh Kannan',      'ramesh.k@example.com',      @PwdHash, '+919815000060', 'Sector 3, HSR',                      4, 2, 1, 1, 0, '987654322020'),
    (61, 'Lalitha Devi',       'lalitha.d@example.com',     @PwdHash, '+919815000061', '9th Block, Jayanagar',               5, 2, 1, 1, 0, '987654322021'),
    (62, 'Saurabh Jain',       'saurabh.j@example.com',     @PwdHash, '+919815000062', 'Marathahalli Bridge',                6, 2, 1, 1, 0, '987654322022'),
    (63, 'Rishi Aggarwal',     'rishi.a@example.com',       @PwdHash, '+919815000063', '2nd Stage, BTM',                     7, 2, 1, 1, 0, '987654322023'),
    (64, 'Anita Bose',         'anita.b@example.com',       @PwdHash, '+919815000064', 'Phase 1, Electronic City',           8, 2, 1, 1, 0, '987654322024'),
    (65, 'Vineet Khanna',      'vineet.k@example.com',      @PwdHash, '+919815000065', 'Yelahanka New Town',                 9, 2, 1, 1, 0, '987654322025'),
    (66, 'Tanya Bansal',       'tanya.b@example.com',       @PwdHash, '+919815000066', 'Mekhri Circle, Hebbal',             10, 2, 1, 1, 0, '987654322026'),
    (67, 'Mohan Lal',          'mohan.l@example.com',       @PwdHash, '+919815000067', '4th Cross, RT Nagar',               11, 2, 1, 1, 0, '987654322027'),
    (68, 'Pankaj Joshi Jr',    'pankaj.jj@example.com',     @PwdHash, '+919815000068', 'BSK 2nd Stage',                     12, 2, 1, 1, 0, '987654322028'),
    (69, 'Manoj Bhargava',     'manoj.b@example.com',       @PwdHash, '+919815000069', '6th Phase, JP Nagar',               13, 2, 1, 1, 0, '987654322029'),
    (70, 'Aman Trivedi',       'aman.t@example.com',        @PwdHash, '+919815000070', 'Outer Ring Rd, Bellandur',          14, 2, 1, 1, 0, '987654322030'),
    (71, 'Shilpa Sengupta',    'shilpa.s@example.com',      @PwdHash, '+919815000071', 'Sarjapur Junction',                 15, 2, 1, 1, 0, '987654322031'),
    (72, 'Sushma Bhardwaj Jr', 'sushma.bj@example.com',     @PwdHash, '+919815000072', 'CV Raman Nagar Phase 2',            16, 2, 1, 1, 0, '987654322032'),
    (73, 'Naina Kapoor',       'naina.k@example.com',       @PwdHash, '+919815000073', '12th Main, Indiranagar',             1, 2, 1, 1, 0, '987654322033'),
    (74, 'Tarun Singla',       'tarun.s@example.com',       @PwdHash, '+919815000074', '8th Block, Koramangala',             2, 2, 1, 1, 0, '987654322034'),
    (75, 'Sushil Tomar',       'sushil.t@example.com',      @PwdHash, '+919815000075', 'Kundalahalli, Whitefield',           3, 2, 1, 1, 0, '987654322035'),
    (76, 'Bhavna Acharya Jr',  'bhavna.aj@example.com',     @PwdHash, '+919815000076', '14th Cross, HSR Layout',             4, 2, 1, 1, 0, '987654322036'),
    (77, 'Ashok Kannan',       'ashok.k@example.com',       @PwdHash, '+919815000077', '11th Block, Jayanagar',              5, 2, 1, 1, 0, '987654322037'),
    (78, 'Vineetha Pillai',    'vineetha.p@example.com',    @PwdHash, '+919815000078', 'Outer Ring Rd, Marathahalli',        6, 2, 1, 1, 0, '987654322038'),
    (79, 'Vishnu Sundaram',    'vishnu.s@example.com',      @PwdHash, '+919815000079', '1st Stage, BTM',                     7, 2, 1, 1, 0, '987654322039'),
    (80, 'Madhu Madhavan',     'madhu.m@example.com',       @PwdHash, '+919815000080', 'Hosur Rd, Electronic City',          8, 2, 1, 1, 0, '987654322040'),
    (81, 'Praveen Hegde',      'praveen.h@example.com',     @PwdHash, '+919815000081', 'Sector 5, Yelahanka',                9, 2, 1, 1, 0, '987654322041'),
    (82, 'Roopa Shetty',       'roopa.s@example.com',       @PwdHash, '+919815000082', 'Bellary Rd, Hebbal',                10, 2, 1, 1, 0, '987654322042'),
    (83, 'Ashwin Bhat Jr',     'ashwin.bj@example.com',     @PwdHash, '+919815000083', '6th Cross, RT Nagar',               11, 2, 1, 1, 0, '987654322043'),
    (84, 'Sushma Acharya',     'sushma.a@example.com',      @PwdHash, '+919815000084', 'BSK 3rd Stage',                     12, 2, 1, 1, 0, '987654322044'),
    (85, 'Vasanth Kumar',      'vasanth.k@example.com',     @PwdHash, '+919815000085', '7th Phase, JP Nagar',               13, 2, 1, 1, 0, '987654322045'),
    (86, 'Lalita Subramanian', 'lalita.s@example.com',      @PwdHash, '+919815000086', 'AECS Layout, Bellandur',            14, 2, 1, 1, 0, '987654322046'),
    (87, 'Krishna Iyer',       'krishna.i@example.com',     @PwdHash, '+919815000087', 'Sarjapur Service Rd',               15, 2, 1, 1, 0, '987654322047'),
    (88, 'Madhuri Mehta',      'madhuri.m@example.com',     @PwdHash, '+919815000088', 'CV Raman Nagar 3rd Cross',          16, 2, 1, 1, 0, '987654322048'),
    (89, 'Sumit Dey',          'sumit.d@example.com',       @PwdHash, '+919815000089', '6th Main, Indiranagar',              1, 2, 1, 1, 0, '987654322049'),
    (90, 'Anjali Banerjee',    'anjali.b2@example.com',     @PwdHash, '+919815000090', '4th Block, Koramangala',             2, 2, 1, 1, 0, '987654322050'),
    (91, 'Pooja Saxena',       'pooja.sax@example.com',     @PwdHash, '+919815000091', 'Varthur, Whitefield',                3, 2, 1, 1, 0, '987654322051'),
    (92, 'Rajesh Sengupta',    'rajesh.se@example.com',     @PwdHash, '+919815000092', '20th Main, HSR',                     4, 2, 1, 1, 0, '987654322052'),
    (93, 'Kiran Achar',        'kiran.a@example.com',       @PwdHash, '+919815000093', '2nd Block, Jayanagar',               5, 2, 1, 1, 0, '987654322053'),
    (94, 'Hemant Patel',       'hemant.p@example.com',      @PwdHash, '+919815000094', 'AECS Layout, Marathahalli',          6, 2, 1, 1, 0, '987654322054'),
    (95, 'Pavan Krishnamurthy','pavan.kr@example.com',      @PwdHash, '+919815000095', '4th Stage, BTM',                     7, 2, 1, 1, 0, '987654322055'),
    (96, 'Bhanu Pratap',       'bhanu.p@example.com',       @PwdHash, '+919815000096', 'Phase 3, Electronic City',           8, 2, 1, 1, 0, '987654322056'),
    (97, 'Sandhya Iyer',       'sandhya.i@example.com',     @PwdHash, '+919815000097', 'Yelahanka Doddaballapur',            9, 2, 1, 1, 0, '987654322057'),
    (98, 'Madhav Iyengar',     'madhav.iy@example.com',     @PwdHash, '+919815000098', '2nd Phase, Hebbal',                 10, 2, 1, 1, 0, '987654322058'),
    (99, 'Diksha Pradhan',     'diksha.pr@example.com',     @PwdHash, '+919815000099', '5th Block, RT Nagar',               11, 2, 1, 1, 0, '987654322059'),
    (100,'Ajay Bhardwaj',      'ajay.bj@example.com',       @PwdHash, '+919815000100', 'BSK 6th Block',                     12, 2, 1, 1, 0, '987654322060');
SET IDENTITY_INSERT dbo.Users OFF;

PRINT '✓ Regular citizens: +60 (UserIds 41-100)';

-- ═══════════════════════════════════════════════════════════════════════════
-- SECTION 6 — Notification preferences for all new users (20-100)
-- ═══════════════════════════════════════════════════════════════════════════

INSERT INTO dbo.NotificationPreferences (UserId, InAppEnabled, PushEnabled, EmailDigestEnabled, DigestFrequencyDays)
SELECT u.UserId, 1, 1, 1, 7
FROM   dbo.Users u
WHERE  u.UserId BETWEEN 20 AND 100
  AND  NOT EXISTS (SELECT 1 FROM dbo.NotificationPreferences np WHERE np.UserId = u.UserId);

PRINT '✓ NotificationPreferences inserted for new users';

-- ═══════════════════════════════════════════════════════════════════════════
-- SECTION 7 — User interests (diverse pattern for AI recs)
-- Each active citizen gets 2-3 category interests + 1-2 locality preferences.
-- ═══════════════════════════════════════════════════════════════════════════

;WITH ActiveCitizens AS (
    SELECT UserId, ROW_NUMBER() OVER (ORDER BY UserId) AS RN
    FROM   dbo.Users
    WHERE  UserId BETWEEN 36 AND 100
      AND  IsActive = 1 AND IsBanned = 0 AND RoleId = 2
)
INSERT INTO dbo.UserInterests (UserId, CategoryId, PreferredLocalityId)
SELECT UserId, CAST((RN % 10) + 1 AS SMALLINT), NULL FROM ActiveCitizens
UNION ALL
SELECT UserId, CAST(((RN + 3) % 10) + 1 AS SMALLINT), NULL FROM ActiveCitizens
UNION ALL
SELECT UserId, NULL, ((RN - 1) % 16) + 1 FROM ActiveCitizens
UNION ALL
SELECT UserId, NULL, ((RN + 5) % 16) + 1 FROM ActiveCitizens;

PRINT '✓ UserInterests inserted for new active citizens';
GO

-- ═══════════════════════════════════════════════════════════════════════════
-- SECTION 8 — Complaints (170 new, IDs 31-200)
-- Distribution: 20 Submitted, 45 In Progress, 50 Resolved, 12 Re-opened,
--               12 Escalated, 10 Rejected, 8 Linked, 13 misc filler
-- Spread across 16 localities & 10 categories.
-- ═══════════════════════════════════════════════════════════════════════════

USE FixMyCityDB;
GO
SET NOCOUNT ON;
SET IDENTITY_INSERT dbo.Complaints ON;

-- ── SUBMITTED block (IDs 31-50, fresh, awaiting solver) ──
INSERT INTO dbo.Complaints (ComplaintId, CitizenUserId, CategoryId, Title, Description,
    LocalityId, Address, Criticality, Latitude, Longitude, Status, DeptId, SubmittedAt) VALUES
(31, 41, 1, 'Pothole crater on 5th Cross',           'Deep crater forming after recent rains, dangerous for two-wheelers.', 1, '5th Cross, Indiranagar', 'High',     12.9784, 77.6408, 'Submitted', 1, DATEADD(HOUR,-3,  SYSDATETIME())),
(32, 42, 2, 'Drainage choked near 6th A Main',       'Storm drain blocked, sewage backing up onto road.',                  2, '6th A Main, Koramangala', 'Critical', 12.9352, 77.6245, 'Submitted', 2, DATEADD(HOUR,-6,  SYSDATETIME())),
(33, 43, 3, 'Streetlight dead on ITPL Service Rd',   'A 400m stretch unlit — joggers using mobile torches.',                3, 'ITPL Service Rd, Whitefield', 'High', 12.9698, 77.7500, 'Submitted', 3, DATEADD(HOUR,-10, SYSDATETIME())),
(34, 44, 4, 'Bin overflowing for 4 days',            'Apartment association reporting no pickup since Friday.',             4, 'Sector 7, HSR Layout', 'Medium',     12.9082, 77.6476, 'Submitted', 4, DATEADD(DAY,-1,  SYSDATETIME())),
(35, 45, 7, 'Loud construction past 11 PM',          'Concrete mixer running till midnight, residents losing sleep.',       5, '6th Block, Jayanagar', 'Medium',     12.9234, 77.5836, 'Submitted', 6, DATEADD(DAY,-1,  SYSDATETIME())),
(36, 46, 6, 'Park benches vandalised',               'Three benches in Brookefield Park smashed last weekend.',             6, 'Brookefield, Marathahalli', 'Low',  12.9591, 77.6974, 'Submitted', 5, DATEADD(HOUR,-14, SYSDATETIME())),
(37, 47, 9, 'Bus route 335E skipped today',          'No 335E since 6 AM, commuters stranded at BTM 3rd Stage.',            7, '3rd Stage, BTM Layout', 'Medium',    12.9165, 77.6101, 'Submitted', 7, DATEADD(HOUR,-4,  SYSDATETIME())),
(38, 48, 10,'Injured stray dog on Hosur Rd',         'Limping stray near Phase 2 entrance, needs rescue.',                  8, 'Phase 2, Electronic City', 'High',  12.8456, 77.6601, 'Submitted', 8, DATEADD(HOUR,-2,  SYSDATETIME())),
(39, 49, 1, 'Speed breaker missing markings',        'Painted lines worn off — vehicles hit at high speed.',                9, 'Sector 1, Yelahanka', 'Medium',      12.9990, 77.5963, 'Submitted', 1, DATEADD(HOUR,-7,  SYSDATETIME())),
(40, 50, 2, 'Brown muddy tap water',                 'Last 2 days the water has been brown — undrinkable.',                10, 'Outer Ring Rd, Hebbal', 'High',      13.0356, 77.5970, 'Submitted', 2, DATEADD(HOUR,-9,  SYSDATETIME())),
(41, 51, 4, 'Stray cattle on RT Nagar 2nd Block',    'Herd grazing on main road, traffic affected.',                       11, 'RT Nagar 2nd Block', 'Low',          13.0163, 77.5961, 'Submitted', 4, DATEADD(HOUR,-15, SYSDATETIME())),
(42, 52, 5, 'Damaged guardrail on 50ft road',        'Rail broken, drop into nallah dangerous.',                           12, 'BSK 1st Stage', 'High',              12.9255, 77.5468, 'Submitted', 1, DATEADD(HOUR,-12, SYSDATETIME())),
(43, 53, 3, 'Transformer humming and sparking',      'Loud noise + sparks visible after rain, fire risk.',                 13, '4th Phase, JP Nagar', 'Critical',    12.9069, 77.5847, 'Submitted', 3, DATEADD(HOUR,-2,  SYSDATETIME())),
(44, 54, 1, 'New pothole near Bellandur Lake',       'Just formed today, deepening rapidly.',                              14, 'Bellandur Lake Rd', 'Medium',        12.9255, 77.6760, 'Submitted', 1, DATEADD(HOUR,-1,  SYSDATETIME())),
(45, 55, 2, 'Sewage smell near Sarjapur signal',     'Foul smell residents complain about for 3 days.',                    15, 'Sarjapur Main Rd', 'High',           12.8993, 77.7081, 'Submitted', 2, DATEADD(HOUR,-5,  SYSDATETIME())),
(46, 56, 6, 'Dead tree leaning over road',           'Large tree partially uprooted, will fall in next storm.',            16, 'CV Raman Nagar Phase 1', 'High',     12.9866, 77.6614, 'Submitted', 5, DATEADD(HOUR,-8,  SYSDATETIME())),
(47, 57, 4, 'Garbage truck skipped 3 days',          'BBMP collection vehicle has not come since Saturday.',                1, '5th Cross, Indiranagar', 'Medium',   12.9784, 77.6408, 'Submitted', 4, DATEADD(DAY,-1,  SYSDATETIME())),
(48, 58, 7, 'Loud DJ at marriage hall till 1 AM',    'Wedding hall plays loud music every weekend.',                       2, '1st Block, Koramangala', 'Medium',   12.9352, 77.6245, 'Submitted', 6, DATEADD(HOUR,-20, SYSDATETIME())),
(49, 59, 9, 'BMTC bus broke down on Hope Farm Rd',   'Stranded for 2 hours, passengers paying for autos.',                  3, 'Hope Farm, Whitefield', 'Low',       12.9698, 77.7500, 'Submitted', 7, DATEADD(HOUR,-3,  SYSDATETIME())),
(50, 60, 10,'Bird trapped in mesh near Sector 3',    'Pigeon stuck in construction mesh for a day.',                       4, 'Sector 3, HSR', 'Medium',             12.9082, 77.6476, 'Submitted', 8, DATEADD(HOUR,-6,  SYSDATETIME()));

-- ── IN PROGRESS block (IDs 51-95) ──
INSERT INTO dbo.Complaints (ComplaintId, CitizenUserId, CategoryId, Title, Description,
    LocalityId, Address, Criticality, Latitude, Longitude, Status, DeptId, SubmittedAt, UpdatedAt) VALUES
(51, 61, 1, 'Crater on 9th Block road',              'Patch repair scheduled, awaiting materials.',                        5, '9th Block, Jayanagar', 'Medium',     12.9234, 77.5836, 'In Progress', 1, DATEADD(DAY,-3,  SYSDATETIME()), DATEADD(DAY,-2, SYSDATETIME())),
(52, 62, 2, 'Pipeline leakage on Marathahalli Brdg', 'Repair crew on site since morning.',                                  6, 'Marathahalli Bridge', 'High',         12.9591, 77.6974, 'In Progress', 2, DATEADD(DAY,-4,  SYSDATETIME()), DATEADD(DAY,-3, SYSDATETIME())),
(53, 63, 3, 'Faulty streetlight pole replaced',      'New pole erected, wiring pending.',                                   7, '2nd Stage, BTM', 'Low',                12.9165, 77.6101, 'In Progress', 3, DATEADD(DAY,-2,  SYSDATETIME()), DATEADD(DAY,-1, SYSDATETIME())),
(54, 64, 4, 'Garbage clearance in progress',         'Team deployed, 60% cleared.',                                         8, 'Phase 1, Electronic City', 'Medium', 12.8456, 77.6601, 'In Progress', 4, DATEADD(DAY,-3,  SYSDATETIME()), DATEADD(DAY,-2, SYSDATETIME())),
(55, 65, 5, 'Broken pavement near Yelahanka school', 'Concrete being repoured today.',                                      9, 'Yelahanka New Town', 'High',          12.9990, 77.5963, 'In Progress', 1, DATEADD(DAY,-5,  SYSDATETIME()), DATEADD(DAY,-2, SYSDATETIME())),
(56, 66, 7, 'Construction noise complaint logged',   'Builder warned, hours restricted to 9-7.',                           10, 'Mekhri Circle, Hebbal', 'Medium',    13.0356, 77.5970, 'In Progress', 6, DATEADD(DAY,-4,  SYSDATETIME()), DATEADD(DAY,-2, SYSDATETIME())),
(57, 67, 9, 'BMTC AC bus rerouting under review',    'BMTC HQ evaluating new feeder route.',                               11, '4th Cross, RT Nagar', 'Low',         13.0163, 77.5961, 'In Progress', 7, DATEADD(DAY,-6,  SYSDATETIME()), DATEADD(DAY,-3, SYSDATETIME())),
(58, 68, 10,'Stray dog ABC drive scheduled',         'Animal welfare team scheduled for Sunday drive.',                    12, 'BSK 2nd Stage', 'Low',                12.9255, 77.5468, 'In Progress', 8, DATEADD(DAY,-7,  SYSDATETIME()), DATEADD(DAY,-1, SYSDATETIME())),
(59, 69, 1, 'Road resurfacing 30% done',             'JP Nagar 6th Phase main road in progress.',                          13, '6th Phase, JP Nagar', 'High',         12.9069, 77.5847, 'In Progress', 1, DATEADD(DAY,-8,  SYSDATETIME()), DATEADD(DAY,-2, SYSDATETIME())),
(60, 70, 2, 'Sewer cleaning by jet machine',         'BWSSB jet vac on site.',                                             14, 'Outer Ring Rd, Bellandur', 'Medium', 12.9255, 77.6760, 'In Progress', 2, DATEADD(DAY,-5,  SYSDATETIME()), DATEADD(DAY,-1, SYSDATETIME())),
(61, 71, 3, 'Power restoration WIP',                 'BESCOM team replacing burned-out transformer.',                      15, 'Sarjapur Junction', 'Critical',       12.8993, 77.7081, 'In Progress', 3, DATEADD(DAY,-2,  SYSDATETIME()), DATEADD(HOUR,-12, SYSDATETIME())),
(62, 72, 4, 'Dump site relocation in progress',      'Old illegal dump cleared, new tags awaited.',                        16, 'CV Raman Nagar Phase 2', 'Medium',    12.9866, 77.6614, 'In Progress', 4, DATEADD(DAY,-9,  SYSDATETIME()), DATEADD(DAY,-3, SYSDATETIME())),
(63, 73, 6, 'Park tree pruning underway',            'Heavy branches being trimmed today.',                                 1, '12th Main, Indiranagar', 'Low',       12.9784, 77.6408, 'In Progress', 5, DATEADD(DAY,-3,  SYSDATETIME()), DATEADD(DAY,-1, SYSDATETIME())),
(64, 74, 5, 'Pedestrian crossing repainting',        'BBMP marking team on site.',                                          2, '8th Block, Koramangala', 'Medium',    12.9352, 77.6245, 'In Progress', 1, DATEADD(DAY,-4,  SYSDATETIME()), DATEADD(DAY,-2, SYSDATETIME())),
(65, 75, 7, 'Industrial noise — KSPCB inspecting',   'Inspector visited, awaiting report.',                                 3, 'Kundalahalli, Whitefield', 'High',   12.9698, 77.7500, 'In Progress', 6, DATEADD(DAY,-10, SYSDATETIME()), DATEADD(DAY,-3, SYSDATETIME())),
(66, 76, 9, 'New bus shelter under construction',    'Shelter foundation poured, roof pending.',                            4, '14th Cross, HSR Layout', 'Low',       12.9082, 77.6476, 'In Progress', 7, DATEADD(DAY,-12, SYSDATETIME()), DATEADD(DAY,-4, SYSDATETIME())),
(67, 77, 10,'Cattle pound transfer scheduled',       'Cattle being moved to pound this week.',                              5, '11th Block, Jayanagar', 'Low',        12.9234, 77.5836, 'In Progress', 8, DATEADD(DAY,-6,  SYSDATETIME()), DATEADD(DAY,-2, SYSDATETIME())),
(68, 78, 1, 'Bridge expansion joint repair',         'Engineers casting new joint.',                                        6, 'Outer Ring Rd, Marathahalli', 'High',12.9591, 77.6974, 'In Progress', 1, DATEADD(DAY,-7,  SYSDATETIME()), DATEADD(DAY,-2, SYSDATETIME())),
(69, 79, 2, 'Tanker supply restored',                'Emergency tankers running 3 trips/day.',                              7, '1st Stage, BTM', 'High',               12.9165, 77.6101, 'In Progress', 2, DATEADD(DAY,-3,  SYSDATETIME()), DATEADD(DAY,-1, SYSDATETIME())),
(70, 80, 3, 'Cable underground burial — Phase 1',    'BESCOM laying underground HT cable.',                                 8, 'Hosur Rd, Electronic City', 'Medium',12.8456, 77.6601, 'In Progress', 3, DATEADD(DAY,-15, SYSDATETIME()), DATEADD(DAY,-5, SYSDATETIME())),
(71, 81, 4, 'Compost plant capacity upgrade',        'New windrow turners installed.',                                      9, 'Sector 5, Yelahanka', 'Low',          12.9990, 77.5963, 'In Progress', 4, DATEADD(DAY,-20, SYSDATETIME()), DATEADD(DAY,-5, SYSDATETIME())),
(72, 82, 6, 'Playground equipment delivery',         'New swings + slides being installed.',                               10, 'Bellary Rd, Hebbal', 'Medium',        13.0356, 77.5970, 'In Progress', 5, DATEADD(DAY,-9,  SYSDATETIME()), DATEADD(DAY,-3, SYSDATETIME())),
(73, 83, 5, 'CCTV pole installation begun',          'KSP installing pole on RT Nagar 6th Cross.',                         11, '6th Cross, RT Nagar', 'High',         13.0163, 77.5961, 'In Progress', 1, DATEADD(DAY,-11, SYSDATETIME()), DATEADD(DAY,-4, SYSDATETIME())),
(74, 84, 7, 'Noise barrier proposal under review',   'Highway authority evaluating barriers.',                             12, 'BSK 3rd Stage', 'Medium',             12.9255, 77.5468, 'In Progress', 6, DATEADD(DAY,-13, SYSDATETIME()), DATEADD(DAY,-5, SYSDATETIME())),
(75, 85, 9, 'Bus stop digitisation in progress',     'LED display board being installed.',                                 13, '7th Phase, JP Nagar', 'Low',          12.9069, 77.5847, 'In Progress', 7, DATEADD(DAY,-14, SYSDATETIME()), DATEADD(DAY,-5, SYSDATETIME())),
(76, 86, 10,'Bird rescue underway',                  'Vet on site for trapped bird rescue.',                               14, 'AECS Layout, Bellandur', 'Medium',   12.9255, 77.6760, 'In Progress', 8, DATEADD(DAY,-2,  SYSDATETIME()), DATEADD(DAY,-1, SYSDATETIME())),
(77, 87, 1, 'Patchwork crew at Sarjapur Service Rd', '2 crews working day shift on potholes.',                              15, 'Sarjapur Service Rd', 'High',         12.8993, 77.7081, 'In Progress', 1, DATEADD(DAY,-5,  SYSDATETIME()), DATEADD(DAY,-2, SYSDATETIME())),
(78, 88, 2, 'Borewell drilling at CV Raman Nagar',   'Backup source being added to grid.',                                 16, 'CV Raman Nagar 3rd Cross', 'High',   12.9866, 77.6614, 'In Progress', 2, DATEADD(DAY,-22, SYSDATETIME()), DATEADD(DAY,-7, SYSDATETIME())),
(79, 89, 3, 'Streetlight LED retrofit — Indiranagar','Project covers 200 poles.',                                           1, '6th Main, Indiranagar', 'Low',        12.9784, 77.6408, 'In Progress', 3, DATEADD(DAY,-18, SYSDATETIME()), DATEADD(DAY,-6, SYSDATETIME())),
(80, 90, 4, 'Door-to-door bag distribution',         'Two-bin segregation bags being distributed.',                         2, '4th Block, Koramangala', 'Low',       12.9352, 77.6245, 'In Progress', 4, DATEADD(DAY,-16, SYSDATETIME()), DATEADD(DAY,-5, SYSDATETIME())),
(81, 91, 5, 'School zone speed signage upgrade',     'Reflective signs being installed.',                                   3, 'Varthur, Whitefield', 'Medium',       12.9698, 77.7500, 'In Progress', 1, DATEADD(DAY,-19, SYSDATETIME()), DATEADD(DAY,-6, SYSDATETIME())),
(82, 92, 7, 'Loudspeaker permit revoked',            'Repeat offender — KSPCB action taken.',                              4, '20th Main, HSR', 'Medium',             12.9082, 77.6476, 'In Progress', 6, DATEADD(DAY,-7,  SYSDATETIME()), DATEADD(DAY,-2, SYSDATETIME())),
(83, 93, 9, 'Auto-rickshaw stand demarcation',       'BMTC + BBMP marking dedicated stands.',                              5, '2nd Block, Jayanagar', 'Low',         12.9234, 77.5836, 'In Progress', 7, DATEADD(DAY,-21, SYSDATETIME()), DATEADD(DAY,-7, SYSDATETIME())),
(84, 94, 10,'Stray cattle relocation drive',         'Drive scheduled this weekend.',                                       6, 'AECS Layout, Marathahalli', 'Low',   12.9591, 77.6974, 'In Progress', 8, DATEADD(DAY,-4,  SYSDATETIME()), DATEADD(DAY,-1, SYSDATETIME())),
(85, 95, 1, 'Footpath levelling — 4th Stage BTM',    'Concrete cured, tiles being laid.',                                   7, '4th Stage, BTM', 'Medium',            12.9165, 77.6101, 'In Progress', 1, DATEADD(DAY,-8,  SYSDATETIME()), DATEADD(DAY,-3, SYSDATETIME())),
(86, 96, 2, 'Boring & Cauvery line connectivity',    'Joint commissioning underway.',                                       8, 'Phase 3, Electronic City', 'High',   12.8456, 77.6601, 'In Progress', 2, DATEADD(DAY,-12, SYSDATETIME()), DATEADD(DAY,-4, SYSDATETIME())),
(87, 97, 3, 'High-mast light commissioning',         'Pole erected at Yelahanka Doddaballapur.',                            9, 'Yelahanka Doddaballapur', 'Medium',  12.9990, 77.5963, 'In Progress', 3, DATEADD(DAY,-25, SYSDATETIME()), DATEADD(DAY,-8, SYSDATETIME())),
(88, 98, 4, 'Wet-waste composting pilot',            'Pilot for 50 apartments at Mekhri.',                                 10, '2nd Phase, Hebbal', 'Low',            13.0356, 77.5970, 'In Progress', 4, DATEADD(DAY,-15, SYSDATETIME()), DATEADD(DAY,-5, SYSDATETIME())),
(89, 99, 5, 'Crime watch cam at RT Nagar 5th Block', 'Pole bracket installed, cam shipping.',                              11, '5th Block, RT Nagar', 'High',         13.0163, 77.5961, 'In Progress', 1, DATEADD(DAY,-10, SYSDATETIME()), DATEADD(DAY,-3, SYSDATETIME())),
(90, 100,6, 'Walking trail tree census',             'BBMP volunteers tagging trees.',                                     12, 'BSK 6th Block', 'Low',                12.9255, 77.5468, 'In Progress', 5, DATEADD(DAY,-17, SYSDATETIME()), DATEADD(DAY,-6, SYSDATETIME())),
(91, 41, 7, 'Restaurant amplifier complaint',        'Bar amplifier on patio violates decibel limits.',                     1, '15 5th Cross, Indiranagar', 'Medium',12.9784, 77.6408, 'In Progress', 6, DATEADD(DAY,-9,  SYSDATETIME()), DATEADD(DAY,-3, SYSDATETIME())),
(92, 42, 9, 'BMTC route 500D delays',                'Frequent breakdowns — fleet under maintenance.',                      2, '23 6th A Main, Koramangala', 'Medium',12.9352, 77.6245, 'In Progress', 7, DATEADD(DAY,-11, SYSDATETIME()), DATEADD(DAY,-4, SYSDATETIME())),
(93, 43, 10,'Snake rescue near ITPL',                'Forest dept dispatched.',                                             3, '99 ITPL Service Rd, Whitefield', 'High',12.9698, 77.7500, 'In Progress', 8, DATEADD(HOUR,-30, SYSDATETIME()), DATEADD(HOUR,-12, SYSDATETIME())),
(94, 44, 1, 'Resurfacing Sector 7 internal road',    'Asphalt laid, line-painting pending.',                                4, 'Sector 7, HSR Layout', 'Medium',      12.9082, 77.6476, 'In Progress', 1, DATEADD(DAY,-13, SYSDATETIME()), DATEADD(DAY,-4, SYSDATETIME())),
(95, 45, 2, 'Pipeline relocation under metro work',  'BWSSB shifting line clear of pillar 47.',                             5, '6th Block, Jayanagar', 'High',        12.9234, 77.5836, 'In Progress', 2, DATEADD(DAY,-20, SYSDATETIME()), DATEADD(DAY,-7, SYSDATETIME()));

PRINT '✓ Complaints: +20 Submitted (IDs 31-50) + 45 In Progress (IDs 51-95)';
GO

USE FixMyCityDB;
GO
SET NOCOUNT ON;

-- ── RESOLVED block (IDs 96-145, 50 items, all with ResolvedAt) ──
INSERT INTO dbo.Complaints (ComplaintId, CitizenUserId, CategoryId, Title, Description,
    LocalityId, Address, Criticality, Latitude, Longitude, Status, DeptId, SubmittedAt, ResolvedAt) VALUES
(96, 46, 1,  'Pothole filled near Brookefield',     'Filled within 3 days.',                                  6, 'Brookefield', 'Medium',  12.9591, 77.6974, 'Resolved', 1, DATEADD(DAY,-25, SYSDATETIME()), DATEADD(DAY,-18, SYSDATETIME())),
(97, 47, 2,  'Leak fixed on BTM 3rd Stage',         'BWSSB sealed the joint.',                                7, '3rd Stage, BTM', 'High',   12.9165, 77.6101, 'Resolved', 2, DATEADD(DAY,-30, SYSDATETIME()), DATEADD(DAY,-22, SYSDATETIME())),
(98, 48, 3,  'EC Phase 2 power stable',             'Old line replaced.',                                     8, 'Phase 2, EC', 'Medium',    12.8456, 77.6601, 'Resolved', 3, DATEADD(DAY,-35, SYSDATETIME()), DATEADD(DAY,-25, SYSDATETIME())),
(99, 49, 4,  'Yelahanka cleanup drive done',        'Garbage cleared, 4 truckloads.',                         9, 'Yelahanka', 'Medium',      12.9990, 77.5963, 'Resolved', 4, DATEADD(DAY,-28, SYSDATETIME()), DATEADD(DAY,-20, SYSDATETIME())),
(100,50, 6,  'Hebbal park trees pruned',            'Dangerous branches removed.',                           10, 'Hebbal', 'Low',             13.0356, 77.5970, 'Resolved', 5, DATEADD(DAY,-40, SYSDATETIME()), DATEADD(DAY,-32, SYSDATETIME())),
(101,51, 7,  'Construction noise hours enforced',   'Notice served, builder complied.',                      11, 'RT Nagar', 'Medium',       13.0163, 77.5961, 'Resolved', 6, DATEADD(DAY,-22, SYSDATETIME()), DATEADD(DAY,-12, SYSDATETIME())),
(102,52, 9,  'Bus stop seat replaced',              'New steel bench installed.',                            12, 'Banashankari', 'Low',     12.9255, 77.5468, 'Resolved', 7, DATEADD(DAY,-45, SYSDATETIME()), DATEADD(DAY,-35, SYSDATETIME())),
(103,53, 10, 'Stray cat sterilisation done',        'ABC drive completed in JP Nagar.',                      13, 'JP Nagar', 'Low',          12.9069, 77.5847, 'Resolved', 8, DATEADD(DAY,-50, SYSDATETIME()), DATEADD(DAY,-40, SYSDATETIME())),
(104,54, 1,  'Bellandur road re-tarred',            'Major resurfacing done.',                               14, 'Bellandur', 'High',         12.9255, 77.6760, 'Resolved', 1, DATEADD(DAY,-55, SYSDATETIME()), DATEADD(DAY,-42, SYSDATETIME())),
(105,55, 2,  'Sarjapur drainage cleaned',           'Storm drains de-silted.',                               15, 'Sarjapur', 'High',          12.8993, 77.7081, 'Resolved', 2, DATEADD(DAY,-60, SYSDATETIME()), DATEADD(DAY,-48, SYSDATETIME())),
(106,56, 3,  'Transformer upgraded',                'Higher-capacity unit installed.',                       16, 'CV Raman Nagar', 'High',  12.9866, 77.6614, 'Resolved', 3, DATEADD(DAY,-30, SYSDATETIME()), DATEADD(DAY,-21, SYSDATETIME())),
(107,57, 4,  'Indiranagar bin replaced',            'Old rusty bin replaced.',                                1, 'Indiranagar', 'Low',       12.9784, 77.6408, 'Resolved', 4, DATEADD(DAY,-18, SYSDATETIME()), DATEADD(DAY,-10, SYSDATETIME())),
(108,58, 5,  'CCTV pole at Koramangala done',       'Pole + camera operational.',                             2, 'Koramangala', 'Medium',    12.9352, 77.6245, 'Resolved', 1, DATEADD(DAY,-65, SYSDATETIME()), DATEADD(DAY,-50, SYSDATETIME())),
(109,59, 6,  'Whitefield gardener hired',           'Park has dedicated maintenance now.',                    3, 'Whitefield', 'Low',         12.9698, 77.7500, 'Resolved', 5, DATEADD(DAY,-70, SYSDATETIME()), DATEADD(DAY,-55, SYSDATETIME())),
(110,60, 7,  'HSR amplifier complaint resolved',    'Bar shifted speakers indoors.',                          4, 'HSR', 'Low',                12.9082, 77.6476, 'Resolved', 6, DATEADD(DAY,-32, SYSDATETIME()), DATEADD(DAY,-22, SYSDATETIME())),
(111,61, 9,  'Jayanagar bus shelter built',         'New shelter installed.',                                 5, 'Jayanagar', 'Medium',       12.9234, 77.5836, 'Resolved', 7, DATEADD(DAY,-80, SYSDATETIME()), DATEADD(DAY,-60, SYSDATETIME())),
(112,62, 10, 'Stray dog cage rescue',               'Trapped dog rescued from drain.',                        6, 'Marathahalli', 'High',     12.9591, 77.6974, 'Resolved', 8, DATEADD(DAY,-12, SYSDATETIME()), DATEADD(DAY, -6, SYSDATETIME())),
(113,63, 1,  'BTM internal road patched',           'All potholes filled in 6th cross.',                      7, 'BTM', 'Medium',             12.9165, 77.6101, 'Resolved', 1, DATEADD(DAY,-26, SYSDATETIME()), DATEADD(DAY,-16, SYSDATETIME())),
(114,64, 2,  'Electronic City line cleaned',        'Sewage line cleared.',                                   8, 'EC', 'High',                12.8456, 77.6601, 'Resolved', 2, DATEADD(DAY,-19, SYSDATETIME()), DATEADD(DAY,-11, SYSDATETIME())),
(115,65, 3,  'Yelahanka line restored',             'Power back after 18 hours.',                             9, 'Yelahanka', 'Critical',    12.9990, 77.5963, 'Resolved', 3, DATEADD(DAY,-9,  SYSDATETIME()), DATEADD(DAY, -8, SYSDATETIME())),
(116,66, 4,  'Hebbal compost yard cleaned',         'Foul smell eliminated.',                                10, 'Hebbal', 'Medium',          13.0356, 77.5970, 'Resolved', 4, DATEADD(DAY,-23, SYSDATETIME()), DATEADD(DAY,-14, SYSDATETIME())),
(117,67, 5,  'RT Nagar streetlight near temple',    'Light restored.',                                       11, 'RT Nagar', 'Medium',       13.0163, 77.5961, 'Resolved', 1, DATEADD(DAY,-14, SYSDATETIME()), DATEADD(DAY, -7, SYSDATETIME())),
(118,68, 6,  'Banashankari tree replanting',        '20 saplings planted.',                                  12, 'Banashankari', 'Low',     12.9255, 77.5468, 'Resolved', 5, DATEADD(DAY,-85, SYSDATETIME()), DATEADD(DAY,-65, SYSDATETIME())),
(119,69, 7,  'JP Nagar noise — band warned',        'Marriage band hours restricted.',                       13, 'JP Nagar', 'Low',          12.9069, 77.5847, 'Resolved', 6, DATEADD(DAY,-7,  SYSDATETIME()), DATEADD(DAY, -3, SYSDATETIME())),
(120,70, 9,  'BMTC new route 333A launched',        'Bellandur-Whitefield-EC loop.',                         14, 'Bellandur', 'Medium',      12.9255, 77.6760, 'Resolved', 7, DATEADD(DAY,-90, SYSDATETIME()), DATEADD(DAY,-70, SYSDATETIME())),
(121,71, 10, 'Sarjapur cat trap rescue',            'Cat freed from car bonnet.',                            15, 'Sarjapur', 'Medium',        12.8993, 77.7081, 'Resolved', 8, DATEADD(DAY,-15, SYSDATETIME()), DATEADD(DAY, -9, SYSDATETIME())),
(122,72, 1,  'CV Raman Nagar median repaired',      'Broken median fixed.',                                  16, 'CV Raman Nagar', 'Medium', 12.9866, 77.6614, 'Resolved', 1, DATEADD(DAY,-28, SYSDATETIME()), DATEADD(DAY,-19, SYSDATETIME())),
(123,73, 2,  'Indiranagar borewell augmentation',   'Two new borewells dug.',                                 1, 'Indiranagar', 'Medium',    12.9784, 77.6408, 'Resolved', 2, DATEADD(DAY,-55, SYSDATETIME()), DATEADD(DAY,-40, SYSDATETIME())),
(124,74, 3,  'Koramangala signal timing fixed',     'Synchronised with adjacent signals.',                    2, 'Koramangala', 'Medium',    12.9352, 77.6245, 'Resolved', 3, DATEADD(DAY,-44, SYSDATETIME()), DATEADD(DAY,-30, SYSDATETIME())),
(125,75, 4,  'Whitefield wet-waste pickup fixed',   'New contractor onboarded.',                              3, 'Whitefield', 'Medium',     12.9698, 77.7500, 'Resolved', 4, DATEADD(DAY,-38, SYSDATETIME()), DATEADD(DAY,-25, SYSDATETIME())),
(126,76, 6,  'HSR park fencing',                    'Boundary fence repaired.',                               4, 'HSR Layout', 'Low',         12.9082, 77.6476, 'Resolved', 5, DATEADD(DAY,-48, SYSDATETIME()), DATEADD(DAY,-35, SYSDATETIME())),
(127,77, 7,  'Jayanagar mic complaint',             'Religious event timing adjusted.',                       5, 'Jayanagar', 'Low',          12.9234, 77.5836, 'Resolved', 6, DATEADD(DAY,-12, SYSDATETIME()), DATEADD(DAY, -6, SYSDATETIME())),
(128,78, 9,  'Marathahalli stop signage',           'New route maps posted.',                                 6, 'Marathahalli', 'Low',      12.9591, 77.6974, 'Resolved', 7, DATEADD(DAY,-72, SYSDATETIME()), DATEADD(DAY,-55, SYSDATETIME())),
(129,79, 10, 'BTM duck rescue from canal',          'Ducklings reunited.',                                    7, 'BTM', 'Medium',             12.9165, 77.6101, 'Resolved', 8, DATEADD(DAY,-21, SYSDATETIME()), DATEADD(DAY,-14, SYSDATETIME())),
(130,80, 1,  'EC speed-bump installed',             'Near school gate.',                                      8, 'EC', 'High',                12.8456, 77.6601, 'Resolved', 1, DATEADD(DAY,-95, SYSDATETIME()), DATEADD(DAY,-70, SYSDATETIME())),
(131,81, 2,  'Yelahanka tank cleaning',             'Annual cleaning done.',                                  9, 'Yelahanka', 'Medium',      12.9990, 77.5963, 'Resolved', 2, DATEADD(DAY,-99, SYSDATETIME()), DATEADD(DAY,-80, SYSDATETIME())),
(132,82, 3,  'Hebbal flyover lights all on',        'Spot fixed on flyover.',                                10, 'Hebbal', 'High',            13.0356, 77.5970, 'Resolved', 3, DATEADD(DAY,-66, SYSDATETIME()), DATEADD(DAY,-50, SYSDATETIME())),
(133,83, 4,  'RT Nagar 6th Cross dumping',          'Cleared and bin installed.',                            11, 'RT Nagar', 'Medium',       13.0163, 77.5961, 'Resolved', 4, DATEADD(DAY,-52, SYSDATETIME()), DATEADD(DAY,-40, SYSDATETIME())),
(134,84, 5,  'BSK 3rd Stage security pole',         'Pole + camera fixed.',                                  12, 'BSK 3rd Stage', 'High',   12.9255, 77.5468, 'Resolved', 1, DATEADD(DAY,-78, SYSDATETIME()), DATEADD(DAY,-60, SYSDATETIME())),
(135,85, 6,  'JP Nagar dog park inaugurated',       'New dedicated dog park opened.',                        13, 'JP Nagar', 'Low',          12.9069, 77.5847, 'Resolved', 5, DATEADD(DAY,-110, SYSDATETIME()),DATEADD(DAY,-85, SYSDATETIME())),
(136,86, 7,  'Bellandur DJ silenced post-10PM',     'Police took action.',                                   14, 'Bellandur', 'Low',          12.9255, 77.6760, 'Resolved', 6, DATEADD(DAY,-14, SYSDATETIME()), DATEADD(DAY, -6, SYSDATETIME())),
(137,87, 9,  'Sarjapur night bus pilot',            'BMTC running 11 PM service.',                           15, 'Sarjapur', 'Medium',        12.8993, 77.7081, 'Resolved', 7, DATEADD(DAY,-85, SYSDATETIME()), DATEADD(DAY,-65, SYSDATETIME())),
(138,88, 10, 'CV Raman dog adoption fair',          '12 dogs adopted.',                                      16, 'CV Raman Nagar', 'Low',   12.9866, 77.6614, 'Resolved', 8, DATEADD(DAY,-105, SYSDATETIME()),DATEADD(DAY,-85, SYSDATETIME())),
(139,89, 1,  'Indiranagar zebra crossings repainted','Refresh after monsoon.',                                1, 'Indiranagar', 'Low',       12.9784, 77.6408, 'Resolved', 1, DATEADD(DAY,-58, SYSDATETIME()), DATEADD(DAY,-44, SYSDATETIME())),
(140,90, 2,  'Koramangala house connection',         'New domestic supply line.',                              2, 'Koramangala', 'Medium',    12.9352, 77.6245, 'Resolved', 2, DATEADD(DAY,-118, SYSDATETIME()),DATEADD(DAY,-95, SYSDATETIME())),
(141,91, 3,  'Whitefield apartment line repaired',  'Internal HT cable replaced.',                            3, 'Whitefield', 'Medium',     12.9698, 77.7500, 'Resolved', 3, DATEADD(DAY,-46, SYSDATETIME()), DATEADD(DAY,-30, SYSDATETIME())),
(142,92, 4,  'HSR plastic ban awareness',           'Awareness drive completed.',                             4, 'HSR Layout', 'Low',         12.9082, 77.6476, 'Resolved', 4, DATEADD(DAY,-37, SYSDATETIME()), DATEADD(DAY,-24, SYSDATETIME())),
(143,93, 6,  'Jayanagar park play area',            'New rubber mat under swings.',                           5, 'Jayanagar', 'Low',          12.9234, 77.5836, 'Resolved', 5, DATEADD(DAY,-91, SYSDATETIME()), DATEADD(DAY,-72, SYSDATETIME())),
(144,94, 5,  'Marathahalli signal CCTV',            'Live feed at traffic police.',                           6, 'Marathahalli', 'High',     12.9591, 77.6974, 'Resolved', 1, DATEADD(DAY,-43, SYSDATETIME()), DATEADD(DAY,-28, SYSDATETIME())),
(145,95, 7,  'BTM construction time-window set',    'Hours: 9 AM - 6 PM only.',                              7, 'BTM', 'Medium',             12.9165, 77.6101, 'Resolved', 6, DATEADD(DAY,-29, SYSDATETIME()), DATEADD(DAY,-18, SYSDATETIME())),

-- ── RE-OPENED block (IDs 146-157, 12 items: each citizen rated <3 before re-open) ──
(146,46, 1,  'Pothole patched poorly, broken again','Same pothole back within a week.',                       6, 'Brookefield', 'High',      12.9591, 77.6974, 'Re-opened', 1, DATEADD(DAY,-40, SYSDATETIME()), DATEADD(DAY,-30, SYSDATETIME())),
(147,47, 2,  'Leak reappeared after fix',           'Same leak — joint failed again.',                        7, 'BTM',         'High',      12.9165, 77.6101, 'Re-opened', 2, DATEADD(DAY,-45, SYSDATETIME()), DATEADD(DAY,-32, SYSDATETIME())),
(148,48, 3,  'Power cuts continued post repair',    'Issue recurred next day.',                               8, 'EC',          'Critical',  12.8456, 77.6601, 'Re-opened', 3, DATEADD(DAY,-50, SYSDATETIME()), DATEADD(DAY,-35, SYSDATETIME())),
(149,49, 4,  'Garbage dump back',                   'Cleared but dumping continues.',                         9, 'Yelahanka',   'Medium',    12.9990, 77.5963, 'Re-opened', 4, DATEADD(DAY,-44, SYSDATETIME()), DATEADD(DAY,-30, SYSDATETIME())),
(150,50, 6,  'Park trees pruned but mess left',     'Trimmed branches left on footpath.',                    10, 'Hebbal',      'Low',       13.0356, 77.5970, 'Re-opened', 5, DATEADD(DAY,-55, SYSDATETIME()), DATEADD(DAY,-40, SYSDATETIME())),
(151,51, 7,  'Construction noise resumed',          'Builder restarted after notice expired.',               11, 'RT Nagar',    'Medium',    13.0163, 77.5961, 'Re-opened', 6, DATEADD(DAY,-30, SYSDATETIME()), DATEADD(DAY,-15, SYSDATETIME())),
(152,52, 9,  'Bus seat broken again',               'New bench vandalised within a month.',                  12, 'Banashankari','Low',       12.9255, 77.5468, 'Re-opened', 7, DATEADD(DAY,-50, SYSDATETIME()), DATEADD(DAY,-40, SYSDATETIME())),
(153,53, 10, 'Stray cats reappeared',               'New batch of strays after ABC drive.',                  13, 'JP Nagar',    'Low',       12.9069, 77.5847, 'Re-opened', 8, DATEADD(DAY,-60, SYSDATETIME()), DATEADD(DAY,-45, SYSDATETIME())),
(154,54, 1,  'Bellandur road cracking again',       'Resurface failed in 6 months.',                         14, 'Bellandur',   'High',      12.9255, 77.6760, 'Re-opened', 1, DATEADD(DAY,-65, SYSDATETIME()), DATEADD(DAY,-50, SYSDATETIME())),
(155,55, 2,  'Drainage choke at Sarjapur',          'Choked again after first cleaning.',                    15, 'Sarjapur',    'High',      12.8993, 77.7081, 'Re-opened', 2, DATEADD(DAY,-70, SYSDATETIME()), DATEADD(DAY,-55, SYSDATETIME())),
(156,56, 3,  'Transformer humming returned',        'Noise back after replacement.',                         16, 'CV Raman Nagar','Medium',  12.9866, 77.6614, 'Re-opened', 3, DATEADD(DAY,-38, SYSDATETIME()), DATEADD(DAY,-25, SYSDATETIME())),
(157,57, 4,  'Indiranagar bin overflowing again',   'New bin too small.',                                     1, 'Indiranagar', 'Medium',    12.9784, 77.6408, 'Re-opened', 4, DATEADD(DAY,-22, SYSDATETIME()), DATEADD(DAY,-13, SYSDATETIME())),

-- ── ESCALATED block (IDs 158-169, 12 items, >30 days unresolved) ──
(158,41, 1,  'High-traffic pothole — 30+ days',     'Multiple complaints, no action for over a month.',       1, 'Indiranagar',     'Critical',12.9784, 77.6408, 'Escalated', 1, DATEADD(DAY,-45, SYSDATETIME()), NULL),
(159,42, 2,  'Sewage blockade unaddressed 45 days', 'Severe public health hazard.',                           2, 'Koramangala',     'Critical',12.9352, 77.6245, 'Escalated', 2, DATEADD(DAY,-48, SYSDATETIME()), NULL),
(160,43, 3,  'Transformer fire risk pending',       'Sparking unit not replaced in 5 weeks.',                 3, 'Whitefield',      'Critical',12.9698, 77.7500, 'Escalated', 3, DATEADD(DAY,-42, SYSDATETIME()), NULL),
(161,44, 4,  'Garbage mountain at HSR',             'Health Inspector visit pending.',                        4, 'HSR Layout',      'High',    12.9082, 77.6476, 'Escalated', 4, DATEADD(DAY,-50, SYSDATETIME()), NULL),
(162,45, 6,  'Dead tree leaning dangerously',       'Could fall on schoolchildren.',                          5, 'Jayanagar',       'Critical',12.9234, 77.5836, 'Escalated', 5, DATEADD(DAY,-38, SYSDATETIME()), NULL),
(163,46, 7,  'Industrial noise unresolved',         'Factory operating illegally.',                            6, 'Marathahalli',    'High',    12.9591, 77.6974, 'Escalated', 6, DATEADD(DAY,-55, SYSDATETIME()), NULL),
(164,47, 9,  'BMTC route cancelled, no replacement','Senior citizens stranded daily.',                        7, 'BTM',             'High',    12.9165, 77.6101, 'Escalated', 7, DATEADD(DAY,-60, SYSDATETIME()), NULL),
(165,48, 10, 'Stray pack reportedly biting',        '3 reported bites in a week.',                            8, 'EC',              'High',    12.8456, 77.6601, 'Escalated', 8, DATEADD(DAY,-35, SYSDATETIME()), NULL),
(166,49, 1,  'Yelahanka link road washed away',     'Heavy rain damage unrepaired.',                          9, 'Yelahanka',       'High',    12.9990, 77.5963, 'Escalated', 1, DATEADD(DAY,-44, SYSDATETIME()), NULL),
(167,50, 2,  'Hebbal supply contaminated',          'Reported brown water 7 weeks ago.',                     10, 'Hebbal',          'Critical',13.0356, 77.5970, 'Escalated', 2, DATEADD(DAY,-49, SYSDATETIME()), NULL),
(168,51, 3,  'RT Nagar transformer overheating',    'Visible smoke in evenings.',                            11, 'RT Nagar',        'Critical',13.0163, 77.5961, 'Escalated', 3, DATEADD(DAY,-32, SYSDATETIME()), NULL),
(169,52, 5,  'Open manhole — Banashankari',         'Reported 6 weeks ago, still uncovered.',                12, 'Banashankari',    'Critical',12.9255, 77.5468, 'Escalated', 1, DATEADD(DAY,-46, SYSDATETIME()), NULL),

-- ── REJECTED block (IDs 170-179, 10 items) ──
(170,53, 8,  'Loud party last night',               'Neighbour party, one-off event.',                       13, 'JP Nagar',        'Low',     12.9069, 77.5847, 'Rejected', 1, DATEADD(DAY,-12, SYSDATETIME()), NULL),
(171,54, 8,  'Cab driver rude',                     'Out of platform scope — file with operator.',          14, 'Bellandur',       'Low',     12.9255, 77.6760, 'Rejected', 1, DATEADD(DAY,-15, SYSDATETIME()), NULL),
(172,55, 5,  'Neighbour dispute',                   'Civil matter — refer to police.',                      15, 'Sarjapur',        'Low',     12.8993, 77.7081, 'Rejected', 1, DATEADD(DAY,-9,  SYSDATETIME()), NULL),
(173,56, 8,  'Restaurant menu issue',               'Not a civic matter.',                                  16, 'CV Raman Nagar',  'Low',     12.9866, 77.6614, 'Rejected', 1, DATEADD(DAY,-22, SYSDATETIME()), NULL),
(174,57, 8,  'Online shopping refund',              'E-commerce complaint, not civic.',                       1, 'Indiranagar',     'Low',     12.9784, 77.6408, 'Rejected', 1, DATEADD(DAY,-18, SYSDATETIME()), NULL),
(175,58, 5,  'Workplace harassment',                'Refer to police / labour office.',                      2, 'Koramangala',     'Low',     12.9352, 77.6245, 'Rejected', 1, DATEADD(DAY,-25, SYSDATETIME()), NULL),
(176,59, 8,  'Building society dispute',            'Internal RWA matter.',                                  3, 'Whitefield',      'Low',     12.9698, 77.7500, 'Rejected', 1, DATEADD(DAY,-30, SYSDATETIME()), NULL),
(177,60, 8,  'Loud TV in apartment',                'Private nuisance — RWA scope.',                         4, 'HSR Layout',      'Low',     12.9082, 77.6476, 'Rejected', 1, DATEADD(DAY,-16, SYSDATETIME()), NULL),
(178,61, 8,  'Personal vendetta against shop',      'Out of scope.',                                          5, 'Jayanagar',       'Low',     12.9234, 77.5836, 'Rejected', 1, DATEADD(DAY,-28, SYSDATETIME()), NULL),
(179,62, 8,  'Real-estate broker complaint',        'Refer to RERA.',                                         6, 'Marathahalli',    'Low',     12.9591, 77.6974, 'Rejected', 1, DATEADD(DAY,-20, SYSDATETIME()), NULL);

-- ── LINKED block (IDs 180-187, 8 items, linked to existing originals) ──
INSERT INTO dbo.Complaints (ComplaintId, CitizenUserId, CategoryId, Title, Description,
    LocalityId, Address, Criticality, Latitude, Longitude, Status, DeptId, LinkedToComplaintId, SubmittedAt) VALUES
(180, 63, 1, 'Same pothole reported again',      'Duplicate of issue near 5th Cross.', 1, 'Indiranagar',    'Medium', 12.9784, 77.6408, 'Linked', 1, 31,  DATEADD(DAY,-2,  SYSDATETIME())),
(181, 64, 2, 'Drainage blocked — same spot',     'Duplicate of 6th A Main report.',    2, 'Koramangala',    'High',   12.9352, 77.6245, 'Linked', 2, 32,  DATEADD(DAY,-1,  SYSDATETIME())),
(182, 65, 3, 'Streetlight dark same stretch',    'Duplicate of ITPL service road.',    3, 'Whitefield',     'Medium', 12.9698, 77.7500, 'Linked', 3, 33,  DATEADD(HOUR,-12, SYSDATETIME())),
(183, 66, 4, 'Same bin overflowing — duplicate', 'Duplicate of Sector 7 HSR.',         4, 'HSR Layout',     'Low',    12.9082, 77.6476, 'Linked', 4, 34,  DATEADD(HOUR,-18, SYSDATETIME())),
(184, 67, 1, 'Same pothole — different citizen', 'Duplicate of 31.',                   1, 'Indiranagar',    'Medium', 12.9785, 77.6409, 'Linked', 1, 31,  DATEADD(HOUR,-5,  SYSDATETIME())),
(185, 68, 2, 'No water — same area',             'Duplicate of escalated 159.',        2, 'Koramangala',    'High',   12.9351, 77.6244, 'Linked', 2, 159, DATEADD(HOUR,-8,  SYSDATETIME())),
(186, 69, 7, 'Construction noise duplicate',     'Duplicate of complaint 56.',         10,'Hebbal',         'Medium', 13.0357, 77.5969, 'Linked', 6, 56,  DATEADD(HOUR,-22, SYSDATETIME())),
(187, 70, 3, 'Transformer noise duplicate',      'Duplicate of escalated 168.',        11,'RT Nagar',       'High',   13.0162, 77.5960, 'Linked', 3, 168, DATEADD(HOUR,-10, SYSDATETIME()));

-- ── BACKFILL Submitted block (IDs 188-200) for variety ──
INSERT INTO dbo.Complaints (ComplaintId, CitizenUserId, CategoryId, Title, Description,
    LocalityId, Address, Criticality, Latitude, Longitude, Status, DeptId, SubmittedAt) VALUES
(188, 71, 1,  'Pothole near Sarjapur signal',     'Forming fast, lots of vehicles.',                15, 'Sarjapur',        'High',     12.8993, 77.7081, 'Submitted', 1, DATEADD(HOUR,-2,  SYSDATETIME())),
(189, 72, 2,  'Tank overflow at CV Raman Nagar',  'Tank valve stuck open.',                         16, 'CV Raman Nagar',  'High',     12.9866, 77.6614, 'Submitted', 2, DATEADD(HOUR,-5,  SYSDATETIME())),
(190, 73, 3,  'Bulb flickering on 6th Main',      'Could cause epileptic episodes.',                 1, 'Indiranagar',     'Medium',   12.9784, 77.6408, 'Submitted', 3, DATEADD(HOUR,-9,  SYSDATETIME())),
(191, 74, 4,  'Construction waste blocking road', 'Concrete debris all over road.',                  2, 'Koramangala',     'High',     12.9352, 77.6245, 'Submitted', 4, DATEADD(HOUR,-13, SYSDATETIME())),
(192, 75, 5,  'Unsafe high-tension wire low',     'Wire sags onto footpath height.',                 3, 'Whitefield',      'Critical', 12.9698, 77.7500, 'Submitted', 1, DATEADD(HOUR,-3,  SYSDATETIME())),
(193, 76, 6,  'Park gate hinges broken',          'Cannot close, dogs entering at night.',           4, 'HSR Layout',      'Low',      12.9082, 77.6476, 'Submitted', 5, DATEADD(HOUR,-20, SYSDATETIME())),
(194, 77, 7,  'Loud temple speakers 5 AM',        'Wakes elderly residents.',                        5, 'Jayanagar',       'Low',      12.9234, 77.5836, 'Submitted', 6, DATEADD(HOUR,-7,  SYSDATETIME())),
(195, 78, 8,  'Misc concern — unclear category',  'Citizen needs to add more detail.',               6, 'Marathahalli',    'Low',      12.9591, 77.6974, 'Submitted', 1, DATEADD(HOUR,-15, SYSDATETIME())),
(196, 79, 9,  'Last-mile auto refusal',           'Autos refusing trips from BTM stop.',             7, 'BTM',             'Medium',   12.9165, 77.6101, 'Submitted', 7, DATEADD(HOUR,-10, SYSDATETIME())),
(197, 80, 10, 'Goat fell into nallah',            'Animal needs rescue.',                            8, 'EC',              'High',     12.8456, 77.6601, 'Submitted', 8, DATEADD(HOUR,-1,  SYSDATETIME())),
(198, 81, 1,  'Sewer cover sunken',               'Vehicles bottoming out.',                         9, 'Yelahanka',       'High',     12.9990, 77.5963, 'Submitted', 1, DATEADD(HOUR,-8,  SYSDATETIME())),
(199, 82, 2,  'Pipe vibrating loudly',            'Pipe shake making banging noise.',               10, 'Hebbal',          'Low',      13.0356, 77.5970, 'Submitted', 2, DATEADD(HOUR,-6,  SYSDATETIME())),
(200, 83, 3,  'No street light entire lane',      'Whole lane dark.',                               11, 'RT Nagar',        'High',     13.0163, 77.5961, 'Submitted', 3, DATEADD(HOUR,-11, SYSDATETIME()));

SET IDENTITY_INSERT dbo.Complaints OFF;

PRINT '✓ Complaints: +50 Resolved (96-145) + 12 Re-opened (146-157) + 12 Escalated (158-169) + 10 Rejected (170-179) + 8 Linked (180-187) + 13 Submitted backfill (188-200)';
PRINT '   Total new complaints: 170. Grand total: 200 (existing 30 + new 170).';
GO

-- ═══════════════════════════════════════════════════════════════════════════
-- SECTION 9 — ComplaintTimeline (auto-generated from status flow)
-- ═══════════════════════════════════════════════════════════════════════════

USE FixMyCityDB;
GO
SET NOCOUNT ON;

-- Submission event for every new complaint
INSERT INTO dbo.ComplaintTimeline (ComplaintId, ActorUserId, OldStatus, NewStatus, Remark, CreatedAt)
SELECT c.ComplaintId, c.CitizenUserId, NULL, 'Submitted',
       'Complaint submitted by citizen.',
       c.SubmittedAt
FROM   dbo.Complaints c
WHERE  c.ComplaintId BETWEEN 31 AND 200;

-- In Progress event for any complaint that ever moved past 'Submitted'
INSERT INTO dbo.ComplaintTimeline (ComplaintId, ActorUserId, OldStatus, NewStatus, Remark, CreatedAt)
SELECT c.ComplaintId, d.UserId, 'Submitted', 'In Progress',
       'Acknowledged. Field team dispatched.',
       DATEADD(HOUR, 4, c.SubmittedAt)
FROM   dbo.Complaints c
JOIN   dbo.Departments d ON d.DeptId = c.DeptId
WHERE  c.ComplaintId BETWEEN 31 AND 200
  AND  c.Status IN ('In Progress','Resolved','Re-opened','Escalated','Rejected');

-- Resolved event for any complaint that was once resolved
INSERT INTO dbo.ComplaintTimeline (ComplaintId, ActorUserId, OldStatus, NewStatus, Remark, CreatedAt)
SELECT c.ComplaintId, d.UserId, 'In Progress', 'Resolved',
       'Work completed. Please verify.',
       c.ResolvedAt
FROM   dbo.Complaints c
JOIN   dbo.Departments d ON d.DeptId = c.DeptId
WHERE  c.ComplaintId BETWEEN 31 AND 200
  AND  c.Status IN ('Resolved','Re-opened')
  AND  c.ResolvedAt IS NOT NULL;

-- Re-opened event
INSERT INTO dbo.ComplaintTimeline (ComplaintId, ActorUserId, OldStatus, NewStatus, Remark, CreatedAt)
SELECT c.ComplaintId, c.CitizenUserId, 'Resolved', 'Re-opened',
       'Citizen unsatisfied with resolution — re-opened.',
       DATEADD(DAY, 3, c.ResolvedAt)
FROM   dbo.Complaints c
WHERE  c.ComplaintId BETWEEN 31 AND 200
  AND  c.Status = 'Re-opened';

-- Escalated event (system-triggered, ActorUserId NULL)
INSERT INTO dbo.ComplaintTimeline (ComplaintId, ActorUserId, OldStatus, NewStatus, Remark, CreatedAt)
SELECT c.ComplaintId, NULL, 'In Progress', 'Escalated',
       'Auto-escalated: complaint stale >30 days.',
       DATEADD(DAY, 32, c.SubmittedAt)
FROM   dbo.Complaints c
WHERE  c.ComplaintId BETWEEN 31 AND 200
  AND  c.Status = 'Escalated';

-- Rejected event (with mandatory remark — usp_UpdateComplaintStatus guard)
INSERT INTO dbo.ComplaintTimeline (ComplaintId, ActorUserId, OldStatus, NewStatus, Remark, CreatedAt)
SELECT c.ComplaintId, d.UserId, 'In Progress', 'Rejected',
       'Not within department scope. Refer to appropriate authority.',
       DATEADD(HOUR, 24, c.SubmittedAt)
FROM   dbo.Complaints c
JOIN   dbo.Departments d ON d.DeptId = c.DeptId
WHERE  c.ComplaintId BETWEEN 31 AND 200
  AND  c.Status = 'Rejected';

-- Linked event (admin-driven)
INSERT INTO dbo.ComplaintTimeline (ComplaintId, ActorUserId, OldStatus, NewStatus, Remark, CreatedAt)
SELECT c.ComplaintId, 1, 'Submitted', 'Linked',
       'Linked to original complaint #' + CAST(c.LinkedToComplaintId AS VARCHAR(10)) + ' (duplicate).',
       DATEADD(HOUR, 6, c.SubmittedAt)
FROM   dbo.Complaints c
WHERE  c.ComplaintId BETWEEN 31 AND 200
  AND  c.Status = 'Linked';

PRINT '✓ ComplaintTimeline rows generated for all new complaints';

-- ═══════════════════════════════════════════════════════════════════════════
-- SECTION 10 — ComplaintAttachments
-- ═══════════════════════════════════════════════════════════════════════════

-- Submission photo for ~half of all new complaints (every 2nd ID)
INSERT INTO dbo.ComplaintAttachments
    (ComplaintId, TimelineId, UploadedByUserId, AttachmentType, FilePath, FileName, FileSizeKB, UploadedAt)
SELECT c.ComplaintId,
       (SELECT TOP 1 t.TimelineId FROM dbo.ComplaintTimeline t
         WHERE t.ComplaintId = c.ComplaintId AND t.NewStatus = 'Submitted'),
       c.CitizenUserId, 'Complaint',
       'complaints/' + CAST(c.ComplaintId AS VARCHAR(10)) + '/Complaint/photo.jpg',
       'photo_' + CAST(c.ComplaintId AS VARCHAR(10)) + '.jpg',
       250 + (c.ComplaintId % 350),
       c.SubmittedAt
FROM   dbo.Complaints c
WHERE  c.ComplaintId BETWEEN 31 AND 200
  AND  c.ComplaintId % 2 = 0;

-- Resolution photo for resolved/re-opened complaints
INSERT INTO dbo.ComplaintAttachments
    (ComplaintId, TimelineId, UploadedByUserId, AttachmentType, FilePath, FileName, FileSizeKB, UploadedAt)
SELECT c.ComplaintId,
       (SELECT TOP 1 t.TimelineId FROM dbo.ComplaintTimeline t
         WHERE t.ComplaintId = c.ComplaintId AND t.NewStatus = 'Resolved'),
       d.UserId, 'Resolution',
       'complaints/' + CAST(c.ComplaintId AS VARCHAR(10)) + '/Resolution/after.jpg',
       'after_' + CAST(c.ComplaintId AS VARCHAR(10)) + '.jpg',
       400 + (c.ComplaintId % 500),
       c.ResolvedAt
FROM   dbo.Complaints c
JOIN   dbo.Departments d ON d.DeptId = c.DeptId
WHERE  c.ComplaintId BETWEEN 31 AND 200
  AND  c.Status IN ('Resolved','Re-opened')
  AND  c.ResolvedAt IS NOT NULL;

PRINT '✓ ComplaintAttachments seeded';

-- ═══════════════════════════════════════════════════════════════════════════
-- SECTION 11 — ComplaintRatings
-- Most resolved complaints get a rating. Re-opened ones get a low rating
-- (which is what enabled re-opening per F17 guard).
-- ═══════════════════════════════════════════════════════════════════════════

-- Re-opened: rating 1 or 2 stars (F17 guard requires <3 for re-open)
INSERT INTO dbo.ComplaintRatings (ComplaintId, CitizenUserId, Stars, Comment, RatedAt)
SELECT c.ComplaintId, c.CitizenUserId,
       CAST(1 + (c.ComplaintId % 2) AS TINYINT),
       'Resolution was unsatisfactory — issue recurred.',
       DATEADD(DAY, 2, c.ResolvedAt)
FROM   dbo.Complaints c
WHERE  c.ComplaintId BETWEEN 146 AND 157
  AND  c.Status = 'Re-opened';

-- Resolved (not re-opened): rating 3-5 stars
INSERT INTO dbo.ComplaintRatings (ComplaintId, CitizenUserId, Stars, Comment, RatedAt)
SELECT c.ComplaintId, c.CitizenUserId,
       CAST(3 + (c.ComplaintId % 3) AS TINYINT),   -- 3, 4 or 5
       CASE (c.ComplaintId % 3)
         WHEN 0 THEN 'Resolved well, thank you.'
         WHEN 1 THEN 'Quick response, satisfied.'
         ELSE        'Excellent work by the team.'
       END,
       DATEADD(DAY, 1, c.ResolvedAt)
FROM   dbo.Complaints c
WHERE  c.ComplaintId BETWEEN 96 AND 145
  AND  c.Status = 'Resolved'
  AND  c.ComplaintId % 5 <> 0;   -- skip 1 in 5 to leave some unrated

PRINT '✓ ComplaintRatings seeded';

-- ═══════════════════════════════════════════════════════════════════════════
-- SECTION 12 — Contributions (varied PaymentStatus)
-- ═══════════════════════════════════════════════════════════════════════════

INSERT INTO dbo.Contributions
    (ComplaintId, CitizenUserId, Amount, TransactionRef, PaymentStatus, CompletedAt, ContributedAt) VALUES
    (31,  41, 1000.00, 'TXN-MS-B001', 'Success',  DATEADD(HOUR, -2,  SYSDATETIME()), DATEADD(HOUR, -3, SYSDATETIME())),
    (31,  42, 500.00,  'TXN-MS-B002', 'Success',  DATEADD(HOUR, -1,  SYSDATETIME()), DATEADD(HOUR, -2, SYSDATETIME())),
    (32,  43, 2500.00, 'TXN-MS-B003', 'Success',  DATEADD(HOUR, -4,  SYSDATETIME()), DATEADD(HOUR, -5, SYSDATETIME())),
    (33,  44, 750.00,  'TXN-MS-B004', 'Success',  DATEADD(DAY,  -1,  SYSDATETIME()), DATEADD(HOUR, -8, SYSDATETIME())),
    (38,  48, 1500.00, 'TXN-MS-B005', 'Success',  DATEADD(HOUR, -1,  SYSDATETIME()), DATEADD(HOUR, -2, SYSDATETIME())),
    (43,  53, 5000.00, 'TXN-MS-B006', 'Success',  DATEADD(HOUR, -2,  SYSDATETIME()), DATEADD(HOUR, -3, SYSDATETIME())),
    (51,  61, 2000.00, 'TXN-MS-B007', 'Success',  DATEADD(DAY,  -2,  SYSDATETIME()), DATEADD(DAY,  -3, SYSDATETIME())),
    (59,  69, 800.00,  'TXN-MS-B008', 'Success',  DATEADD(DAY,  -1,  SYSDATETIME()), DATEADD(DAY,  -2, SYSDATETIME())),
    (61,  71, 3000.00, 'TXN-MS-B009', 'Success',  DATEADD(DAY,  -1,  SYSDATETIME()), DATEADD(DAY,  -2, SYSDATETIME())),
    (68,  78, 1200.00, 'TXN-MS-B010', 'Success',  DATEADD(DAY,  -3,  SYSDATETIME()), DATEADD(DAY,  -4, SYSDATETIME())),
    (96,  46, 600.00,  'TXN-MS-B011', 'Success',  DATEADD(DAY, -18,  SYSDATETIME()), DATEADD(DAY, -19, SYSDATETIME())),
    (104, 54, 8000.00, 'TXN-MS-B012', 'Success',  DATEADD(DAY, -42,  SYSDATETIME()), DATEADD(DAY, -43, SYSDATETIME())),
    (130, 80, 12000.00,'TXN-MS-B013', 'Success',  DATEADD(DAY, -70,  SYSDATETIME()), DATEADD(DAY, -71, SYSDATETIME())),
    (135, 85, 25000.00,'TXN-MS-B014', 'Success',  DATEADD(DAY, -85,  SYSDATETIME()), DATEADD(DAY, -86, SYSDATETIME())),
    -- Pending / Failed / Refunded edge cases
    (44,  54, 1500.00, 'TXN-MS-B015', 'Pending',  NULL,                              DATEADD(HOUR, -1, SYSDATETIME())),
    (45,  55, 2200.00, 'TXN-MS-B016', 'Failed',   NULL,                              DATEADD(HOUR, -4, SYSDATETIME())),
    (46,  56, 900.00,  'TXN-MS-B017', 'Refunded', DATEADD(DAY, -1,  SYSDATETIME()),  DATEADD(DAY,  -3, SYSDATETIME())),
    (158, 41, 15000.00,'TXN-MS-B018', 'Success',  DATEADD(DAY, -10, SYSDATETIME()),  DATEADD(DAY, -12, SYSDATETIME())),
    (159, 42, 20000.00,'TXN-MS-B019', 'Success',  DATEADD(DAY, -15, SYSDATETIME()),  DATEADD(DAY, -16, SYSDATETIME())),
    (160, 43, 18000.00,'TXN-MS-B020', 'Success',  DATEADD(DAY, -8,  SYSDATETIME()),  DATEADD(DAY,  -9, SYSDATETIME())),
    (167, 50, 7500.00, 'TXN-MS-B021', 'Success',  DATEADD(DAY, -20, SYSDATETIME()),  DATEADD(DAY, -22, SYSDATETIME())),
    (168, 51, 6000.00, 'TXN-MS-B022', 'Success',  DATEADD(DAY, -5,  SYSDATETIME()),  DATEADD(DAY,  -7, SYSDATETIME())),
    (169, 52, 9000.00, 'TXN-MS-B023', 'Success',  DATEADD(DAY, -12, SYSDATETIME()),  DATEADD(DAY, -14, SYSDATETIME()));

PRINT '✓ Contributions: 23 new (mix Success/Pending/Failed/Refunded)';

-- ═══════════════════════════════════════════════════════════════════════════
-- SECTION 13 — PWGParticipationRequests (varied Pending/Approved/Rejected)
-- ═══════════════════════════════════════════════════════════════════════════

INSERT INTO dbo.PWGParticipationRequests
    (ComplaintId, OrgId, SolverUserId, Status, RequestNote, DecisionNote, RequestedAt, DecidedAt) VALUES
    (34,  1, 4,  'Pending',  'Clean Bengaluru ready to clear bin overflow.',                NULL, DATEADD(HOUR,-12, SYSDATETIME()), NULL),
    (36,  2, 21, 'Pending',  'IISc Civic group offers park bench repair volunteers.',       NULL, DATEADD(HOUR,-8,  SYSDATETIME()), NULL),
    (41,  4, 20, 'Approved', 'Bangalore Bicycle Brigade can manage cattle herd diversion.', 'Approved — coordinate with traffic police.', DATEADD(DAY,-2,  SYSDATETIME()), DATEADD(DAY,-1,  SYSDATETIME())),
    (44,  5, 1,  'Approved', 'Welfare for All to repair pothole site with concrete.',       'Approved.',                                   DATEADD(DAY,-1,  SYSDATETIME()), DATEADD(HOUR,-12, SYSDATETIME())),
    (47,  6, 20, 'Approved', 'Citizens for Civic Action will run cleanliness drive.',      'Approved.',                                   DATEADD(DAY,-2,  SYSDATETIME()), DATEADD(DAY,-1,  SYSDATETIME())),
    (54,  1, 20, 'Approved', 'Clean Bengaluru to assist EC garbage clearance.',             'Approved with field manager assignment.',     DATEADD(DAY,-3,  SYSDATETIME()), DATEADD(DAY,-2,  SYSDATETIME())),
    (58,  8, 24, 'Approved', 'Saahasi volunteers will help with ABC drive.',                'Approved.',                                   DATEADD(DAY,-5,  SYSDATETIME()), DATEADD(DAY,-3,  SYSDATETIME())),
    (61,  9, 4,  'Approved', 'Tech Mahindra CSR to fund LED conversion.',                   'Approved with PO #TMF-2024-118.',              DATEADD(DAY,-4,  SYSDATETIME()), DATEADD(DAY,-2,  SYSDATETIME())),
    (66,  7, 23, 'Approved', 'Aravind CSR offers signage funding.',                         'Approved with logo placement clause.',         DATEADD(DAY,-6,  SYSDATETIME()), DATEADD(DAY,-3,  SYSDATETIME())),
    (72,  10,21, 'Approved', 'Sankalp to fund playground equipment.',                       'Approved.',                                   DATEADD(DAY,-7,  SYSDATETIME()), DATEADD(DAY,-4,  SYSDATETIME())),
    (78,  3, 3,  'Approved', 'Infosys CSR funds borewell drilling cost.',                   'Approved with engineering audit.',             DATEADD(DAY,-15, SYSDATETIME()), DATEADD(DAY,-12, SYSDATETIME())),
    (88,  10,20, 'Pending',  'Sankalp wants to expand wet-waste composting pilot.',          NULL,                                          DATEADD(DAY,-3,  SYSDATETIME()), NULL),
    (93,  8, 24, 'Approved', 'Saahasi will deliver awareness in apartments.',               'Approved.',                                   DATEADD(DAY,-1,  SYSDATETIME()), DATEADD(HOUR,-12, SYSDATETIME())),
    (95,  6, 3,  'Pending',  'CCA proposes pipeline pre-marking.',                          NULL,                                          DATEADD(DAY,-5,  SYSDATETIME()), NULL),
    (110, 9, 1,  'Approved', 'Tech Mahindra CSR donates indoor speakers.',                  'Approved.',                                   DATEADD(DAY,-25, SYSDATETIME()), DATEADD(DAY,-23, SYSDATETIME())),
    (135, 5, 21, 'Approved', 'Welfare Group adopts dog park maintenance.',                  'Approved.',                                   DATEADD(DAY,-95, SYSDATETIME()), DATEADD(DAY,-90, SYSDATETIME())),
    (161, 4, 1,  'Approved', 'Bangalore Bicycle Brigade for crowd-source cleanup.',         'Approved — assist BBMP team.',                 DATEADD(DAY,-30, SYSDATETIME()), DATEADD(DAY,-28, SYSDATETIME())),
    (162, 7, 21, 'Approved', 'Aravind CSR funds emergency tree pruning.',                   'Approved.',                                   DATEADD(DAY,-25, SYSDATETIME()), DATEADD(DAY,-23, SYSDATETIME())),
    (163, 2, 22, 'Rejected', 'IISc Volunteer Group requests industrial noise review.',      'Rejected — outside student volunteer scope.', DATEADD(DAY,-35, SYSDATETIME()), DATEADD(DAY,-33, SYSDATETIME())),
    (165, 8, 24, 'Approved', 'Saahasi to assist Animal Welfare with rescue.',               'Approved.',                                   DATEADD(DAY,-20, SYSDATETIME()), DATEADD(DAY,-18, SYSDATETIME())),
    (166, 1, 1,  'Approved', 'Clean Bengaluru emergency road clearance.',                   'Approved.',                                   DATEADD(DAY,-30, SYSDATETIME()), DATEADD(DAY,-28, SYSDATETIME()));

PRINT '✓ PWGParticipationRequests: 21 new (mix Pending/Approved/Rejected)';

-- ═══════════════════════════════════════════════════════════════════════════
-- SECTION 14 — PWGReports (admin oversight)
-- ═══════════════════════════════════════════════════════════════════════════

INSERT INTO dbo.PWGReports
    (ComplaintId, ReportedOrgId, ReportedByUserId, ReportReason, AdminReviewedByUserId,
     AdminAction, AdminNote, ReportedAt, ReviewedAt, Status) VALUES
    (66,  7, 23, 'Aravind CSR delivered signage on time — exemplary work.',     1, 'Dismissed', 'Praise filed.', DATEADD(DAY,-2,  SYSDATETIME()), DATEADD(DAY,-1, SYSDATETIME()), 'Reviewed'),
    (72,  10,21, 'Sankalp finished playground equipment ahead of schedule.',    1, 'Dismissed', 'No action.',    DATEADD(DAY,-3,  SYSDATETIME()), DATEADD(DAY,-2, SYSDATETIME()), 'Reviewed'),
    (104, 5, 1,  'Welfare for All did poor compacting on Bellandur road.',      1, 'Warned',    'Verbal warning issued.', DATEADD(DAY,-40, SYSDATETIME()), DATEADD(DAY,-38, SYSDATETIME()), 'Closed'),
    (135, 5, 21, 'Welfare for All maintains dog park beyond scope.',            1, 'Dismissed', 'Out of scope — not a complaint.', DATEADD(DAY,-80, SYSDATETIME()), DATEADD(DAY,-78, SYSDATETIME()), 'Closed'),
    (110, 9, 1,  'Tech Mahindra CSR speakers came in late.',                    1, 'Warned',    'Warning issued to vendor.', DATEADD(DAY,-22, SYSDATETIME()), DATEADD(DAY,-20, SYSDATETIME()), 'Closed'),
    (165, 8, 24, 'Saahasi paused mid-rescue — needs review.',                   NULL, NULL,     NULL, DATEADD(DAY,-15, SYSDATETIME()), NULL, 'Pending'),
    (54,  1, 20, 'Clean Bengaluru completed EC drive — feedback positive.',     1, 'Dismissed', 'Praise filed.', DATEADD(DAY,-5,  SYSDATETIME()), DATEADD(DAY,-3, SYSDATETIME()), 'Reviewed'),
    (47,  6, 20, 'CCA drive had minor litter — feedback for next time.',       1, 'Warned',    'Coordination tips shared.', DATEADD(DAY,-12, SYSDATETIME()), DATEADD(DAY,-9, SYSDATETIME()), 'Closed');

PRINT '✓ PWGReports: 8 new';

-- ═══════════════════════════════════════════════════════════════════════════
-- SECTION 15 — EscalationLog (Auto + Manual)
-- ═══════════════════════════════════════════════════════════════════════════

INSERT INTO dbo.EscalationLog
    (ComplaintId, EscalationTrigger, ActorUserId, OriginalDeptId, ReassignedToDeptId, Reason, EscalatedAt) VALUES
    (158, 'Auto',   NULL, 1, NULL,  'Stale >30 days in Indiranagar.',                                  DATEADD(DAY,-15, SYSDATETIME())),
    (159, 'Auto',   NULL, 2, NULL,  'Sewage backlog >45 days.',                                        DATEADD(DAY,-18, SYSDATETIME())),
    (160, 'Auto',   NULL, 3, NULL,  'Transformer hazard pending >35 days.',                            DATEADD(DAY,-12, SYSDATETIME())),
    (161, 'Auto',   NULL, 4, NULL,  'HSR garbage pile >50 days.',                                       DATEADD(DAY,-20, SYSDATETIME())),
    (162, 'Manual', 1,    5, 1,     'Admin escalated to BBMP — danger imminent. Reassigned to roads.',  DATEADD(DAY,-7,  SYSDATETIME())),
    (163, 'Auto',   NULL, 6, NULL,  'Industrial noise unresolved >55 days.',                            DATEADD(DAY,-25, SYSDATETIME())),
    (164, 'Manual', 1,    7, NULL,  'BMTC route cancellation — admin pressure.',                       DATEADD(DAY,-15, SYSDATETIME())),
    (165, 'Auto',   NULL, 8, NULL,  'Stray pack — 35 days unaddressed.',                                DATEADD(DAY,-5,  SYSDATETIME())),
    (166, 'Auto',   NULL, 1, NULL,  'Yelahanka road damage >44 days.',                                  DATEADD(DAY,-14, SYSDATETIME())),
    (167, 'Auto',   NULL, 2, NULL,  'Hebbal contaminated water >49 days.',                              DATEADD(DAY,-19, SYSDATETIME())),
    (168, 'Auto',   NULL, 3, NULL,  'RT Nagar transformer overheating >32 days.',                       DATEADD(DAY,-2,  SYSDATETIME())),
    (169, 'Manual', 1,    1, NULL,  'Open manhole — admin escalated for emergency cover.',              DATEADD(DAY,-16, SYSDATETIME()));

PRINT '✓ EscalationLog: 12 new (10 Auto + 2 Manual)';

-- ═══════════════════════════════════════════════════════════════════════════
-- SECTION 16 — DuplicateComplaintLinks
-- ═══════════════════════════════════════════════════════════════════════════

INSERT INTO dbo.DuplicateComplaintLinks (OriginalComplaintId, LinkedComplaintId, LinkedByUserId, LinkedAt) VALUES
    (31,  180, 1, DATEADD(DAY,-1,  SYSDATETIME())),
    (32,  181, 1, DATEADD(HOUR,-20, SYSDATETIME())),
    (33,  182, 1, DATEADD(HOUR,-10, SYSDATETIME())),
    (34,  183, 1, DATEADD(HOUR,-16, SYSDATETIME())),
    (31,  184, 1, DATEADD(HOUR,-4,  SYSDATETIME())),
    (159, 185, 1, DATEADD(HOUR,-6,  SYSDATETIME())),
    (56,  186, 1, DATEADD(HOUR,-20, SYSDATETIME())),
    (168, 187, 1, DATEADD(HOUR,-8,  SYSDATETIME()));

PRINT '✓ DuplicateComplaintLinks: 8 new';
GO

-- ═══════════════════════════════════════════════════════════════════════════
-- SECTION 17 — Notifications (~250 entries across users)
-- ═══════════════════════════════════════════════════════════════════════════

USE FixMyCityDB;
GO
SET NOCOUNT ON;

-- Citizen: notification when their complaint is submitted
INSERT INTO dbo.Notifications (UserId, ComplaintId, Message, NotificationType, Channel, IsRead, CreatedAt)
SELECT c.CitizenUserId, c.ComplaintId,
       'Your complaint #' + CAST(c.ComplaintId AS VARCHAR(10)) + ' has been submitted.',
       'StatusChange', 'InApp',
       CASE WHEN c.SubmittedAt < DATEADD(DAY,-3, SYSDATETIME()) THEN 1 ELSE 0 END,
       c.SubmittedAt
FROM   dbo.Complaints c
WHERE  c.ComplaintId BETWEEN 31 AND 200;

-- Solver: new assignment notification (only for routed complaints)
INSERT INTO dbo.Notifications (UserId, ComplaintId, Message, NotificationType, Channel, IsRead, CreatedAt)
SELECT d.UserId, c.ComplaintId,
       'New complaint #' + CAST(c.ComplaintId AS VARCHAR(10)) + ' routed to your department.',
       'NewAssignment', 'InApp',
       CASE WHEN c.SubmittedAt < DATEADD(DAY,-5, SYSDATETIME()) THEN 1 ELSE 0 END,
       c.SubmittedAt
FROM   dbo.Complaints c
JOIN   dbo.Departments d ON d.DeptId = c.DeptId
WHERE  c.ComplaintId BETWEEN 31 AND 200;

-- Citizen: notification when complaint resolved
INSERT INTO dbo.Notifications (UserId, ComplaintId, Message, NotificationType, Channel, IsRead, CreatedAt)
SELECT c.CitizenUserId, c.ComplaintId,
       'Complaint #' + CAST(c.ComplaintId AS VARCHAR(10)) + ' marked as Resolved. Please rate.',
       'StatusChange', 'InApp', 0,
       c.ResolvedAt
FROM   dbo.Complaints c
WHERE  c.ComplaintId BETWEEN 96 AND 145
  AND  c.Status = 'Resolved';

-- SuperAdmin: notification for each escalation
INSERT INTO dbo.Notifications (UserId, ComplaintId, Message, NotificationType, Channel, IsRead, CreatedAt)
SELECT 1, c.ComplaintId,
       'Complaint #' + CAST(c.ComplaintId AS VARCHAR(10)) + ' auto-escalated — needs admin attention.',
       'StatusChange', 'InApp', 0,
       DATEADD(DAY, 32, c.SubmittedAt)
FROM   dbo.Complaints c
WHERE  c.ComplaintId BETWEEN 158 AND 169
  AND  c.Status = 'Escalated';

-- PWG users: PWGDecision notifications for approved/rejected requests
INSERT INTO dbo.Notifications (UserId, ComplaintId, Message, NotificationType, Channel, IsRead, CreatedAt)
SELECT o.UserId, r.ComplaintId,
       'Your participation request for complaint #' + CAST(r.ComplaintId AS VARCHAR(10)) + ' has been ' + r.Status + '.',
       'PWGDecision', 'InApp', 0,
       r.DecidedAt
FROM   dbo.PWGParticipationRequests r
JOIN   dbo.Organisations o ON o.OrgId = r.OrgId
WHERE  r.DecidedAt IS NOT NULL
  AND  r.Status IN ('Approved','Rejected');

-- WeeklyDigest sample notifications for active citizens
INSERT INTO dbo.Notifications (UserId, Message, NotificationType, Channel, IsRead, CreatedAt)
SELECT u.UserId,
       'Weekly digest: 3 new complaints in your areas of interest this week.',
       'WeeklyDigest', 'InApp', 0,
       DATEADD(DAY, -2, SYSDATETIME())
FROM   dbo.Users u
WHERE  u.RoleId = 2 AND u.IsActive = 1 AND u.IsBanned = 0
  AND  u.UserId BETWEEN 41 AND 60;

PRINT '✓ Notifications: large batch (~250 generated)';

-- ═══════════════════════════════════════════════════════════════════════════
-- SECTION 18 — ComplaintMLScores (every new complaint gets a score)
-- ═══════════════════════════════════════════════════════════════════════════

INSERT INTO dbo.ComplaintMLScores
    (ComplaintId, PredictedResolutionDate, ResolutionProbability, PriorityScore, PredictionModelVersion, ScoredAt)
SELECT c.ComplaintId,
       DATEADD(DAY, CASE c.Criticality
                       WHEN 'Critical' THEN 3
                       WHEN 'High'     THEN 7
                       WHEN 'Medium'   THEN 14
                       ELSE                 21
                   END, c.SubmittedAt),
       CASE c.Criticality
           WHEN 'Critical' THEN 0.55
           WHEN 'High'     THEN 0.72
           WHEN 'Medium'   THEN 0.83
           ELSE                 0.91
       END,
       CASE c.Criticality
           WHEN 'Critical' THEN 92.0 + ((c.ComplaintId % 9))
           WHEN 'High'     THEN 75.0 + ((c.ComplaintId % 12))
           WHEN 'Medium'   THEN 55.0 + ((c.ComplaintId % 15))
           ELSE                 30.0 + ((c.ComplaintId % 20))
       END,
       'v2.0.0-lgbm',
       DATEADD(MINUTE, 5, c.SubmittedAt)
FROM   dbo.Complaints c
WHERE  c.ComplaintId BETWEEN 31 AND 200
  AND  NOT EXISTS (SELECT 1 FROM dbo.ComplaintMLScores ml WHERE ml.ComplaintId = c.ComplaintId);

PRINT '✓ ComplaintMLScores: scored 170 new complaints';

-- ═══════════════════════════════════════════════════════════════════════════
-- SECTION 19 — ComplaintTags (3 tags per complaint based on category)
-- ═══════════════════════════════════════════════════════════════════════════

-- Category-keyed tag templates
INSERT INTO dbo.ComplaintTags (ComplaintId, Tag, Score, Source)
SELECT c.ComplaintId, t.Tag, t.Score, 'AI'
FROM   dbo.Complaints c
JOIN   (VALUES
         -- Category 1: Road & Infrastructure
         (1,'pothole',0.93),(1,'road damage',0.88),(1,'traffic hazard',0.81),
         -- Category 2: Water Supply
         (2,'water shortage',0.94),(2,'leak',0.89),(2,'drainage',0.83),
         -- Category 3: Electricity
         (3,'power outage',0.92),(3,'streetlight',0.87),(3,'transformer',0.82),
         -- Category 4: Garbage & Sanitation
         (4,'garbage',0.95),(4,'sanitation',0.88),(4,'collection',0.80),
         -- Category 5: Public Safety
         (5,'safety hazard',0.90),(5,'open infrastructure',0.85),(5,'risk',0.78),
         -- Category 6: Parks & Trees
         (6,'parks',0.91),(6,'trees',0.87),(6,'green space',0.79),
         -- Category 7: Noise Pollution
         (7,'noise',0.93),(7,'construction',0.86),(7,'disturbance',0.81),
         -- Category 8: Other
         (8,'general civic',0.70),(8,'misc',0.65),(8,'other',0.60),
         -- Category 9: Public Transport
         (9,'bus',0.92),(9,'BMTC',0.87),(9,'route',0.80),
         -- Category 10: Animal Welfare
         (10,'animal rescue',0.93),(10,'stray',0.86),(10,'welfare',0.80)
       ) AS t(CategoryId, Tag, Score)
       ON t.CategoryId = c.CategoryId
WHERE  c.ComplaintId BETWEEN 31 AND 200;

PRINT '✓ ComplaintTags: ~510 new (3 per complaint)';

-- ═══════════════════════════════════════════════════════════════════════════
-- SECTION 20 — AIDecisionLog (categorisation + priority per complaint)
-- ═══════════════════════════════════════════════════════════════════════════

-- Categorisation decision
INSERT INTO dbo.AIDecisionLog
    (ComplaintId, DecisionType, InputSummary, OutputSummary, Confidence, ModelVersion, CreatedAt)
SELECT c.ComplaintId, 'Categorization',
       LEFT(c.Title + ' ' + CAST(c.Description AS NVARCHAR(200)), 200),
       ic.CategoryName + ' (' + CAST(0.85 + (c.ComplaintId % 10) * 0.01 AS VARCHAR(10)) + ')',
       0.85 + (c.ComplaintId % 10) * 0.01,
       'v1.1.0-knn',
       DATEADD(MINUTE, 2, c.SubmittedAt)
FROM   dbo.Complaints c
JOIN   dbo.IssueCategories ic ON ic.CategoryId = c.CategoryId
WHERE  c.ComplaintId BETWEEN 31 AND 200;

-- PriorityScore decision
INSERT INTO dbo.AIDecisionLog
    (ComplaintId, DecisionType, InputSummary, OutputSummary, Confidence, ModelVersion, CreatedAt)
SELECT c.ComplaintId, 'PriorityScore',
       'cat=' + CAST(c.CategoryId AS VARCHAR(5)) + ' crit=' + c.Criticality,
       'priority=' + CAST(ISNULL(ml.PriorityScore, 50) AS VARCHAR(10)) +
       ' prob='   + CAST(ISNULL(ml.ResolutionProbability, 0.5) AS VARCHAR(10)),
       ml.ResolutionProbability,
       'v2.0.0-lgbm',
       DATEADD(MINUTE, 4, c.SubmittedAt)
FROM   dbo.Complaints c
LEFT   JOIN dbo.ComplaintMLScores ml ON ml.ComplaintId = c.ComplaintId
WHERE  c.ComplaintId BETWEEN 31 AND 200;

-- DuplicateFlag decisions for linked complaints
INSERT INTO dbo.AIDecisionLog
    (ComplaintId, DecisionType, InputSummary, OutputSummary, Confidence, ModelVersion, CreatedAt)
SELECT c.ComplaintId, 'DuplicateFlag',
       LEFT(c.Title, 200),
       'is_dup=True linked_to=' + CAST(c.LinkedToComplaintId AS VARCHAR(10)) + ' sim=0.91',
       0.91,
       'all-MiniLM-L6-v2',
       DATEADD(MINUTE, 5, c.SubmittedAt)
FROM   dbo.Complaints c
WHERE  c.ComplaintId BETWEEN 31 AND 200
  AND  c.Status = 'Linked';

PRINT '✓ AIDecisionLog: ~350 entries (Categorization + PriorityScore + DuplicateFlag)';

-- ═══════════════════════════════════════════════════════════════════════════
-- SECTION 21 — ComplaintEmbeddings (sample — 20 representative complaints)
-- Synthetic 8-dim embeddings (Python service will overwrite with real 384-dim
-- when re-scored). Just enough to exercise the dedup endpoint end-to-end.
-- ═══════════════════════════════════════════════════════════════════════════

INSERT INTO dbo.ComplaintEmbeddings (ComplaintId, EmbeddingJson, ModelVersion, GeneratedAt)
SELECT c.ComplaintId,
       '[' +
       CAST((c.ComplaintId % 100) / 100.0 AS VARCHAR(10)) + ',' +
       CAST(((c.ComplaintId+3) % 100) / 100.0 AS VARCHAR(10)) + ',' +
       CAST(((c.ComplaintId+7) % 100) / 100.0 AS VARCHAR(10)) + ',' +
       CAST(((c.ComplaintId+11) % 100) / 100.0 AS VARCHAR(10)) + ',' +
       CAST(((c.ComplaintId+13) % 100) / 100.0 AS VARCHAR(10)) + ',' +
       CAST(((c.ComplaintId+17) % 100) / 100.0 AS VARCHAR(10)) + ',' +
       CAST(((c.ComplaintId+19) % 100) / 100.0 AS VARCHAR(10)) + ',' +
       CAST(((c.ComplaintId+23) % 100) / 100.0 AS VARCHAR(10)) + ']',
       'placeholder-8d',
       SYSDATETIME()
FROM   dbo.Complaints c
WHERE  c.ComplaintId IN (31,32,33,34,38,43,44,51,59,68,96,104,130,158,159,160,165,168,180,184);

PRINT '✓ ComplaintEmbeddings: 20 placeholder vectors (overwritten by real AI on next score)';
GO

-- ═══════════════════════════════════════════════════════════════════════════
-- SECTION 22 — UserPoints (for new citizens 41-100; existing 12 covered by 03)
-- Distribution: tiered points so milestones can be triggered across users.
-- ═══════════════════════════════════════════════════════════════════════════

USE FixMyCityDB;
GO
SET NOCOUNT ON;

INSERT INTO dbo.UserPoints (UserId, Points)
SELECT u.UserId,
       CASE
         WHEN u.UserId BETWEEN 41 AND 50  THEN 75  + (u.UserId * 3)
         WHEN u.UserId BETWEEN 51 AND 65  THEN 160 + (u.UserId * 2)
         WHEN u.UserId BETWEEN 66 AND 80  THEN 320 + ((u.UserId % 20) * 5)
         ELSE                                  60  + (u.UserId * 2)
       END
FROM   dbo.Users u
WHERE  u.RoleId = 2
  AND  u.UserId BETWEEN 36 AND 100
  AND  u.IsActive = 1 AND u.IsBanned = 0
  AND  NOT EXISTS (SELECT 1 FROM dbo.UserPoints up WHERE up.UserId = u.UserId);

PRINT '✓ UserPoints: seeded for new active citizens';

-- ═══════════════════════════════════════════════════════════════════════════
-- SECTION 23 — PointsLedger (history of awards)
-- ═══════════════════════════════════════════════════════════════════════════

-- ComplaintSubmitted: 10 points each
INSERT INTO dbo.PointsLedger (UserId, PointsDelta, Reason, RefComplaintId, EarnedAt)
SELECT c.CitizenUserId, 10, 'ComplaintSubmitted', c.ComplaintId, c.SubmittedAt
FROM   dbo.Complaints c
WHERE  c.ComplaintId BETWEEN 31 AND 200
  AND  c.CitizenUserId BETWEEN 36 AND 100;

-- ComplaintRated: 5 points each (citizens earn for rating)
INSERT INTO dbo.PointsLedger (UserId, PointsDelta, Reason, RefComplaintId, EarnedAt)
SELECT cr.CitizenUserId, 5, 'ComplaintRated', cr.ComplaintId, cr.RatedAt
FROM   dbo.ComplaintRatings cr
WHERE  cr.ComplaintId BETWEEN 31 AND 200;

-- PWGProgressUpdate: 2 points per PWG progress event (simulated)
INSERT INTO dbo.PointsLedger (UserId, PointsDelta, Reason, RefComplaintId, EarnedAt)
SELECT o.UserId, 2, 'PWGProgressUpdate', r.ComplaintId, DATEADD(DAY, 2, r.DecidedAt)
FROM   dbo.PWGParticipationRequests r
JOIN   dbo.Organisations o ON o.OrgId = r.OrgId
WHERE  r.Status = 'Approved' AND r.DecidedAt IS NOT NULL;

-- Bonus manual awards for top performers
INSERT INTO dbo.PointsLedger (UserId, PointsDelta, Reason, RefComplaintId, EarnedAt) VALUES
    (41, 50, 'ManualAward',  NULL, DATEADD(DAY,-10, SYSDATETIME())),
    (50, 75, 'ManualAward',  NULL, DATEADD(DAY,-15, SYSDATETIME())),
    (66,100, 'ManualAward',  NULL, DATEADD(DAY,-20, SYSDATETIME())),
    (70, 60, 'ManualAward',  NULL, DATEADD(DAY,-12, SYSDATETIME())),
    (78,150, 'ManualAward',  NULL, DATEADD(DAY,-25, SYSDATETIME())),
    (85,200, 'ManualAward',  NULL, DATEADD(DAY,-30, SYSDATETIME()));

PRINT '✓ PointsLedger: ledger entries for all new awards';

-- ═══════════════════════════════════════════════════════════════════════════
-- SECTION 24 — Certificates (issued for milestone thresholds)
-- ═══════════════════════════════════════════════════════════════════════════

-- For each citizen who exceeds 50 / 150 / 300 points, issue the matching cert
-- (filtered unique index uix_Cert_UserMilestone prevents duplicates)

DECLARE @SilverMs INT, @GoldMs INT, @PlatinumMs INT;
SELECT @SilverMs   = MilestoneId FROM dbo.MilestoneDefinitions WHERE MilestoneName = 'Silver Citizen';
SELECT @GoldMs     = MilestoneId FROM dbo.MilestoneDefinitions WHERE MilestoneName = 'Gold Citizen';
SELECT @PlatinumMs = MilestoneId FROM dbo.MilestoneDefinitions WHERE MilestoneName = 'Platinum Citizen';

-- Silver: points >= 50
INSERT INTO dbo.Certificates (UserId, MilestoneId, Milestone, VerificationCode, IssuedAt)
SELECT up.UserId, @SilverMs, 'Silver Citizen',
       'FMC-SLVR-' + LEFT(REPLACE(CAST(NEWID() AS VARCHAR(36)),'-',''), 8),
       DATEADD(DAY,-5, SYSDATETIME())
FROM   dbo.UserPoints up
WHERE  up.Points >= 50
  AND  up.UserId BETWEEN 36 AND 100
  AND  NOT EXISTS (SELECT 1 FROM dbo.Certificates c WHERE c.UserId = up.UserId AND c.MilestoneId = @SilverMs);

-- Gold: points >= 150
INSERT INTO dbo.Certificates (UserId, MilestoneId, Milestone, VerificationCode, IssuedAt)
SELECT up.UserId, @GoldMs, 'Gold Citizen',
       'FMC-GOLD-' + LEFT(REPLACE(CAST(NEWID() AS VARCHAR(36)),'-',''), 8),
       DATEADD(DAY,-3, SYSDATETIME())
FROM   dbo.UserPoints up
WHERE  up.Points >= 150
  AND  up.UserId BETWEEN 36 AND 100
  AND  NOT EXISTS (SELECT 1 FROM dbo.Certificates c WHERE c.UserId = up.UserId AND c.MilestoneId = @GoldMs);

-- Platinum: points >= 300
INSERT INTO dbo.Certificates (UserId, MilestoneId, Milestone, VerificationCode, IssuedAt)
SELECT up.UserId, @PlatinumMs, 'Platinum Citizen',
       'FMC-PLAT-' + LEFT(REPLACE(CAST(NEWID() AS VARCHAR(36)),'-',''), 8),
       DATEADD(DAY,-1, SYSDATETIME())
FROM   dbo.UserPoints up
WHERE  up.Points >= 300
  AND  up.UserId BETWEEN 36 AND 100
  AND  NOT EXISTS (SELECT 1 FROM dbo.Certificates c WHERE c.UserId = up.UserId AND c.MilestoneId = @PlatinumMs);

PRINT '✓ Certificates: milestone certs issued';

-- ═══════════════════════════════════════════════════════════════════════════
-- SECTION 25 — UserRecommendationCache
-- For every active citizen, cache up to 10 open complaints from their interests.
-- ═══════════════════════════════════════════════════════════════════════════

;WITH ActiveCitizens AS (
    SELECT UserId
    FROM   dbo.Users
    WHERE  RoleId = 2 AND IsActive = 1 AND IsBanned = 0
      AND  UserId BETWEEN 36 AND 100
), CitizenInterests AS (
    SELECT ui.UserId, ui.CategoryId, ui.PreferredLocalityId
    FROM   dbo.UserInterests ui
    JOIN   ActiveCitizens ac ON ac.UserId = ui.UserId
), CandidateScores AS (
    SELECT ac.UserId, c.ComplaintId,
           ROW_NUMBER() OVER (PARTITION BY ac.UserId ORDER BY c.SubmittedAt DESC) AS RN,
           CAST(0.50 + ((c.ComplaintId * 7) % 100) / 200.0 AS DECIMAL(8,4)) AS Score
    FROM   ActiveCitizens ac
    JOIN   CitizenInterests ci ON ci.UserId = ac.UserId
    JOIN   dbo.Complaints   c  ON (c.CategoryId = ci.CategoryId OR c.LocalityId = ci.PreferredLocalityId)
    WHERE  c.Status NOT IN ('Resolved','Rejected','Linked')
      AND  c.ComplaintId BETWEEN 1 AND 200
)
INSERT INTO dbo.UserRecommendationCache (UserId, ComplaintId, Score, GeneratedAt)
SELECT cs.UserId, cs.ComplaintId, cs.Score, SYSDATETIME()
FROM   CandidateScores cs
WHERE  cs.RN <= 10
  AND  NOT EXISTS (
      SELECT 1 FROM dbo.UserRecommendationCache urc
      WHERE urc.UserId = cs.UserId
        AND urc.ComplaintId = cs.ComplaintId
  );

PRINT '✓ UserRecommendationCache: top-10 cache filled per active citizen';

-- ═══════════════════════════════════════════════════════════════════════════
-- SECTION 26 — AuditLog (admin actions)
-- ═══════════════════════════════════════════════════════════════════════════

INSERT INTO dbo.AuditLog (ActorUserId, ActionType, TargetUserId, TargetOrgId, TargetDeptId, TargetComplaintId, Reason, CreatedAt) VALUES
    (1, 'SolverApproved',    20,   NULL, 4,    NULL, 'Approved BBMP SWM solver.',            DATEADD(DAY,-30, SYSDATETIME())),
    (1, 'SolverApproved',    21,   NULL, 5,    NULL, 'Approved BBMP Parks solver.',          DATEADD(DAY,-29, SYSDATETIME())),
    (1, 'SolverApproved',    22,   NULL, 6,    NULL, 'Approved KSPCB solver.',                DATEADD(DAY,-28, SYSDATETIME())),
    (1, 'SolverApproved',    23,   NULL, 7,    NULL, 'Approved BMTC solver.',                 DATEADD(DAY,-27, SYSDATETIME())),
    (1, 'SolverApproved',    24,   NULL, 8,    NULL, 'Approved Animal Welfare solver.',      DATEADD(DAY,-26, SYSDATETIME())),
    (1, 'SolverRejected',    26,   NULL, 10,   NULL, 'Disputed civic body rejected.',         DATEADD(DAY,-25, SYSDATETIME())),
    (1, 'PWGApproved',       27,   4,    NULL, NULL, 'Bangalore Bicycle Brigade approved.',  DATEADD(DAY,-24, SYSDATETIME())),
    (1, 'PWGApproved',       28,   5,    NULL, NULL, 'Welfare for All approved.',             DATEADD(DAY,-23, SYSDATETIME())),
    (1, 'PWGApproved',       29,   6,    NULL, NULL, 'Citizens for Civic Action approved.',  DATEADD(DAY,-22, SYSDATETIME())),
    (1, 'PWGApproved',       30,   7,    NULL, NULL, 'Aravind CSR approved.',                 DATEADD(DAY,-21, SYSDATETIME())),
    (1, 'PWGApproved',       31,   8,    NULL, NULL, 'Saahasi Volunteers approved.',          DATEADD(DAY,-20, SYSDATETIME())),
    (1, 'PWGApproved',       32,   9,    NULL, NULL, 'Tech Mahindra Foundation approved.',   DATEADD(DAY,-19, SYSDATETIME())),
    (1, 'PWGApproved',       33,   10,   NULL, NULL, 'Sankalp Initiative approved.',          DATEADD(DAY,-18, SYSDATETIME())),
    (1, 'PWGRejected',       35,   12,   NULL, NULL, 'Rejected: incomplete documentation.',  DATEADD(DAY,-17, SYSDATETIME())),
    (1, 'UserBanned',        37,   NULL, NULL, NULL, 'Repeated false complaints — banned.',   DATEADD(DAY,-20, SYSDATETIME())),
    (1, 'UserDeactivated',   40,   NULL, NULL, NULL, 'Inactive >180 days.',                   DATEADD(DAY,-15, SYSDATETIME())),
    (1, 'ComplaintReassigned',NULL,NULL,NULL, 162,   'Reassigned to BBMP Roads — emergency.', DATEADD(DAY,-7,  SYSDATETIME())),
    (1, 'ComplaintReassigned',NULL,NULL,NULL, 164,   'Reassigned BMTC route under pressure.', DATEADD(DAY,-15, SYSDATETIME())),
    (1, 'ComplaintReassigned',NULL,NULL,NULL, 169,   'Open manhole — urgent reassign BBMP.',  DATEADD(DAY,-16, SYSDATETIME())),
    (1, 'PWGReportActioned', NULL, 5,    NULL, 104,  'Warned Welfare for All on compacting.', DATEADD(DAY,-38, SYSDATETIME())),
    (1, 'PWGReportActioned', NULL, 9,    NULL, 110,  'Warned Tech Mahindra on delivery.',    DATEADD(DAY,-20, SYSDATETIME()));

PRINT '✓ AuditLog: 21 entries';

-- ═══════════════════════════════════════════════════════════════════════════
-- SECTION 27 — Refresh ScoreboardSnapshot
-- ═══════════════════════════════════════════════════════════════════════════

EXEC dbo.usp_RefreshScoreboard;
PRINT '✓ ScoreboardSnapshot refreshed';

-- ═══════════════════════════════════════════════════════════════════════════
-- SECTION 28 — PlatformStatsSnapshot (14 days history for trend graphs)
-- ═══════════════════════════════════════════════════════════════════════════

;WITH Days AS (
    SELECT TOP (14) DATEADD(DAY, -1 - (ROW_NUMBER() OVER (ORDER BY (SELECT NULL))), CAST(SYSDATETIME() AS DATE)) AS D
    FROM   sys.all_objects
)
INSERT INTO dbo.PlatformStatsSnapshot
    (TotalComplaints, Submitted, InProgress, Resolved, Rejected, Reopened, Escalated, Linked, ActiveUsers, SnapshotDate)
SELECT
    35 + ((ABS(CHECKSUM(D)) % 8)),                       -- TotalComplaints
    5  + ((ABS(CHECKSUM(D, 'sub')) % 4)),                 -- Submitted
    7  + ((ABS(CHECKSUM(D, 'ip'))  % 6)),                 -- InProgress
    12 + ((ABS(CHECKSUM(D, 'res')) % 5)),                 -- Resolved
    1  + ((ABS(CHECKSUM(D, 'rej')) % 2)),                 -- Rejected
    1  + ((ABS(CHECKSUM(D, 'ro'))  % 2)),                 -- Reopened
    2  + ((ABS(CHECKSUM(D, 'esc')) % 2)),                 -- Escalated
    1  + ((ABS(CHECKSUM(D, 'lnk')) % 2)),                 -- Linked
    90 + ((ABS(CHECKSUM(D, 'au'))  % 10)),                -- ActiveUsers
    D
FROM   Days
WHERE  NOT EXISTS (SELECT 1 FROM dbo.PlatformStatsSnapshot p WHERE p.SnapshotDate = Days.D);

-- Today's snapshot
INSERT INTO dbo.PlatformStatsSnapshot
    (TotalComplaints, Submitted, InProgress, Resolved, Rejected, Reopened, Escalated, Linked, ActiveUsers, SnapshotDate)
SELECT
    (SELECT COUNT(*) FROM dbo.Complaints),
    (SELECT COUNT(*) FROM dbo.Complaints WHERE Status = 'Submitted'),
    (SELECT COUNT(*) FROM dbo.Complaints WHERE Status = 'In Progress'),
    (SELECT COUNT(*) FROM dbo.Complaints WHERE Status = 'Resolved'),
    (SELECT COUNT(*) FROM dbo.Complaints WHERE Status = 'Rejected'),
    (SELECT COUNT(*) FROM dbo.Complaints WHERE Status = 'Re-opened'),
    (SELECT COUNT(*) FROM dbo.Complaints WHERE Status = 'Escalated'),
    (SELECT COUNT(*) FROM dbo.Complaints WHERE Status = 'Linked'),
    (SELECT COUNT(*) FROM dbo.Users      WHERE IsActive = 1),
    CAST(SYSDATETIME() AS DATE)
WHERE NOT EXISTS (
    SELECT 1 FROM dbo.PlatformStatsSnapshot WHERE SnapshotDate = CAST(SYSDATETIME() AS DATE)
);

PRINT '✓ PlatformStatsSnapshot: 14 days history + today';
GO

-- ═══════════════════════════════════════════════════════════════════════════
-- FINAL SUMMARY
-- ═══════════════════════════════════════════════════════════════════════════

USE FixMyCityDB;
GO
SET NOCOUNT ON;

PRINT '';
PRINT '═══════════════════════════════════════════════════════════════════════';
PRINT '  MASSIVE SEED COMPLETE — Phase 1';
PRINT '═══════════════════════════════════════════════════════════════════════';
PRINT '';

DECLARE @u INT, @c INT, @d INT, @o INT, @cmp INT, @rat INT, @con INT, @pwg INT,
        @esc INT, @dup INT, @ml INT, @tag INT, @ai INT, @rec INT, @cer INT, @aud INT;
SELECT @u   = COUNT(*) FROM dbo.Users;
SELECT @c   = COUNT(*) FROM dbo.Users WHERE RoleId = 2;
SELECT @d   = COUNT(*) FROM dbo.Departments;
SELECT @o   = COUNT(*) FROM dbo.Organisations;
SELECT @cmp = COUNT(*) FROM dbo.Complaints;
SELECT @rat = COUNT(*) FROM dbo.ComplaintRatings;
SELECT @con = COUNT(*) FROM dbo.Contributions;
SELECT @pwg = COUNT(*) FROM dbo.PWGParticipationRequests;
SELECT @esc = COUNT(*) FROM dbo.EscalationLog;
SELECT @dup = COUNT(*) FROM dbo.DuplicateComplaintLinks;
SELECT @ml  = COUNT(*) FROM dbo.ComplaintMLScores;
SELECT @tag = COUNT(*) FROM dbo.ComplaintTags;
SELECT @ai  = COUNT(*) FROM dbo.AIDecisionLog;
SELECT @rec = COUNT(*) FROM dbo.UserRecommendationCache;
SELECT @cer = COUNT(*) FROM dbo.Certificates;
SELECT @aud = COUNT(*) FROM dbo.AuditLog;

PRINT 'Totals (cumulative):';
PRINT '  Users:                   ' + CAST(@u   AS VARCHAR);
PRINT '    of which Citizens:     ' + CAST(@c   AS VARCHAR);
PRINT '  Departments:             ' + CAST(@d   AS VARCHAR);
PRINT '  Organisations:           ' + CAST(@o   AS VARCHAR);
PRINT '  Complaints:              ' + CAST(@cmp AS VARCHAR);
PRINT '  Ratings:                 ' + CAST(@rat AS VARCHAR);
PRINT '  Contributions:           ' + CAST(@con AS VARCHAR);
PRINT '  PWGRequests:             ' + CAST(@pwg AS VARCHAR);
PRINT '  Escalations:             ' + CAST(@esc AS VARCHAR);
PRINT '  DuplicateLinks:          ' + CAST(@dup AS VARCHAR);
PRINT '  MLScores:                ' + CAST(@ml  AS VARCHAR);
PRINT '  Tags:                    ' + CAST(@tag AS VARCHAR);
PRINT '  AIDecisionLog:           ' + CAST(@ai  AS VARCHAR);
PRINT '  RecommendationCache:     ' + CAST(@rec AS VARCHAR);
PRINT '  Certificates:            ' + CAST(@cer AS VARCHAR);
PRINT '  AuditLog:                ' + CAST(@aud AS VARCHAR);
PRINT '';
PRINT 'All passwords (any seeded user): Password123!';
PRINT '';
PRINT 'New solver logins:';
PRINT '  anita.bbmp2@fixmycity.in    (BBMP SWM)';
PRINT '  venkat.bbmp3@fixmycity.in   (BBMP Parks)';
PRINT '  ranjit.kspcb@fixmycity.in   (KSPCB)';
PRINT '  nikhil.bmtc@fixmycity.in    (BMTC)';
PRINT '  divya.animal@fixmycity.in   (Animal Welfare)';
PRINT '  pending.solver@fixmycity.in (PENDING — login blocked)';
PRINT '';
PRINT 'New PWG logins (all approved unless noted):';
PRINT '  manish@bbb.org, sushma@welfareforall.org, rajiv@cca.org,';
PRINT '  pankaj@aravindcsr.org, bhavna@saahasi.org, ashwin@techmcsr.org, aditi@sankalp.org';
PRINT '  pending.pwg@fixmycity.in (PENDING — login blocked)';
PRINT '';
PRINT 'Edge-case logins:';
PRINT '  sso.user@gmail.com           (SSO only, no password)';
PRINT '  banned.spammer@example.com   (BANNED — login blocked)';
PRINT '  suspended.user@example.com   (SUSPENDED — login blocked)';
PRINT '  locked.user@example.com      (LOCKED — login blocked for 30 min)';
PRINT '  deactivated.user@example.com (DEACTIVATED — login blocked)';
PRINT '';
PRINT 'Regular citizen sample: vikram.s2@example.com .. ajay.bj@example.com (60 citizens)';
PRINT '═══════════════════════════════════════════════════════════════════════';
GO
