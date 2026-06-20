-- FixMyCity — AI/ML Table Additions (Sprint 3)
-- Run this against FixMyCityDB after Sprint 2 schema is in place.
-- All tables use DATETIME2(7) and SYSDATETIME() consistent with Sprint 2.
--
-- Phase-1 patch (2026-05-19): every CREATE TABLE / CREATE INDEX is now
-- wrapped in an existence guard so the file is fully re-runnable.
-- All stored procedures already used CREATE OR ALTER (idempotent).
-- ─────────────────────────────────────────────────────────────────────────────

USE FixMyCityDB;
GO

-- 1. Extend PredictionModelVersion to hold semantic version strings e.g. "v2.1.0-lgbm"
--    Guarded so re-runs are no-ops once the column is already 50 chars.
IF EXISTS (
    SELECT 1 FROM sys.columns c
    JOIN sys.types t ON t.user_type_id = c.user_type_id
    WHERE c.object_id = OBJECT_ID('dbo.ComplaintMLScores')
      AND c.name      = 'PredictionModelVersion'
      AND t.name      = 'varchar'
      AND c.max_length < 50
)
BEGIN
    ALTER TABLE dbo.ComplaintMLScores
        ALTER COLUMN PredictionModelVersion VARCHAR(50) NULL;
END;
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 2. ComplaintEmbeddings
--    Stores the 384-float sentence-transformer vector for each complaint
--    as a JSON array so no vector DB infrastructure is needed.
--    Used for cosine-similarity duplicate detection.
-- ─────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ComplaintEmbeddings' AND SCHEMA_NAME(schema_id) = 'dbo')
BEGIN
    CREATE TABLE dbo.ComplaintEmbeddings (
        EmbeddingId   INT           NOT NULL IDENTITY(1,1),
        ComplaintId   INT           NOT NULL,
        EmbeddingJson NVARCHAR(MAX) NOT NULL,   -- JSON array of 384 floats (~3 KB)
        ModelVersion  VARCHAR(50)   NOT NULL,
        GeneratedAt   DATETIME2(7)  NOT NULL CONSTRAINT df_CE_GeneratedAt DEFAULT SYSDATETIME(),

        CONSTRAINT pk_CE            PRIMARY KEY CLUSTERED (EmbeddingId),
        CONSTRAINT uq_CE_Complaint  UNIQUE (ComplaintId),
        CONSTRAINT fk_CE_Complaint  FOREIGN KEY (ComplaintId) REFERENCES dbo.Complaints(ComplaintId)
    );
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_CE_GeneratedAt' AND object_id = OBJECT_ID('dbo.ComplaintEmbeddings'))
    CREATE INDEX ix_CE_GeneratedAt ON dbo.ComplaintEmbeddings (ComplaintId, GeneratedAt DESC);
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 3. UserRecommendationCache
--    Nightly pre-computed top-N complaint IDs per user.
--    Consumed by GET api/ML/GetRecommendedComplaints instead of the
--    expensive real-time JOIN on UserInterests.
-- ─────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'UserRecommendationCache' AND SCHEMA_NAME(schema_id) = 'dbo')
BEGIN
    CREATE TABLE dbo.UserRecommendationCache (
        CacheId     INT          NOT NULL IDENTITY(1,1),
        UserId      INT          NOT NULL,
        ComplaintId INT          NOT NULL,
        Score       DECIMAL(8,4) NOT NULL,
        GeneratedAt DATETIME2(7) NOT NULL CONSTRAINT df_URC_GeneratedAt DEFAULT SYSDATETIME(),

        CONSTRAINT pk_URC               PRIMARY KEY CLUSTERED (CacheId),
        CONSTRAINT uq_URC_UserComplaint UNIQUE (UserId, ComplaintId),
        CONSTRAINT fk_URC_User          FOREIGN KEY (UserId)      REFERENCES dbo.Users(UserId),
        CONSTRAINT fk_URC_Complaint     FOREIGN KEY (ComplaintId) REFERENCES dbo.Complaints(ComplaintId)
    );
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_URC_User' AND object_id = OBJECT_ID('dbo.UserRecommendationCache'))
    CREATE INDEX ix_URC_User ON dbo.UserRecommendationCache (UserId, Score DESC);
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 4. AIDecisionLog
--    Every AI inference is logged here. Supports debugging, retraining,
--    and explainability for solvers/admins ("why did AI flag this?").
-- ─────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AIDecisionLog' AND SCHEMA_NAME(schema_id) = 'dbo')
BEGIN
    CREATE TABLE dbo.AIDecisionLog (
        LogId         INT           NOT NULL IDENTITY(1,1),
        ComplaintId   INT           NULL,
        UserId        INT           NULL,
        DecisionType  VARCHAR(50)   NOT NULL,
        InputSummary  NVARCHAR(500) NULL,
        OutputSummary NVARCHAR(500) NULL,
        Confidence    DECIMAL(5,4)  NULL,
        ModelVersion  VARCHAR(50)   NULL,
        WasOverridden BIT           NOT NULL CONSTRAINT df_ADL_Override DEFAULT 0,
        OverriddenBy  INT           NULL,
        CreatedAt     DATETIME2(7)  NOT NULL CONSTRAINT df_ADL_CreatedAt DEFAULT SYSDATETIME(),

        CONSTRAINT pk_ADL PRIMARY KEY CLUSTERED (LogId),
        CONSTRAINT fk_ADL_Complaint   FOREIGN KEY (ComplaintId) REFERENCES dbo.Complaints(ComplaintId),
        CONSTRAINT fk_ADL_User        FOREIGN KEY (UserId)      REFERENCES dbo.Users(UserId),
        CONSTRAINT fk_ADL_Overrider   FOREIGN KEY (OverriddenBy) REFERENCES dbo.Users(UserId),
        CONSTRAINT ck_ADL_Type        CHECK (DecisionType IN (
            'Categorization','DuplicateFlag','ToxicityFlag','PriorityScore',
            'ResolutionPrediction','ImageClassification','AutoTag','Recommendation'))
    );
END;
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_ADL_Complaint' AND object_id = OBJECT_ID('dbo.AIDecisionLog'))
    CREATE INDEX ix_ADL_Complaint ON dbo.AIDecisionLog (ComplaintId, CreatedAt DESC);
GO
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'ix_ADL_Type' AND object_id = OBJECT_ID('dbo.AIDecisionLog'))
    CREATE INDEX ix_ADL_Type      ON dbo.AIDecisionLog (DecisionType, CreatedAt DESC);
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 5. ComplaintTags
--    AI-generated or manually added keyword tags per complaint.
--    KeyBERT extracts these from Title + Description.
--    Enriches SearchComplaints beyond LIKE matching.
-- ─────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ComplaintTags' AND SCHEMA_NAME(schema_id) = 'dbo')
BEGIN
    CREATE TABLE dbo.ComplaintTags (
        TagId       INT          NOT NULL IDENTITY(1,1),
        ComplaintId INT          NOT NULL,
        Tag         VARCHAR(80)  NOT NULL,
        Score       DECIMAL(5,4) NULL,
        Source      VARCHAR(20)  NOT NULL CONSTRAINT df_CT_Source DEFAULT 'AI',

        CONSTRAINT pk_CT          PRIMARY KEY CLUSTERED (TagId),
        CONSTRAINT fk_CT_Complaint FOREIGN KEY (ComplaintId) REFERENCES dbo.Complaints(ComplaintId),
        CONSTRAINT ck_CT_Source   CHECK (Source IN ('AI','Manual')),
        INDEX ix_CT_Tag (Tag)
    );
END;
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 6. PlatformStatsCategorySnapshot
--    Extends the daily PlatformStatsSnapshot with per-category counts.
--    Required for Prophet per-category trend forecasting (Section 2.13).
-- ─────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'PlatformStatsCategorySnapshot' AND SCHEMA_NAME(schema_id) = 'dbo')
BEGIN
    CREATE TABLE dbo.PlatformStatsCategorySnapshot (
        SnapshotId    INT      NOT NULL,
        CategoryId    SMALLINT NOT NULL,
        NewComplaints INT      NOT NULL CONSTRAINT df_PSCS_New     DEFAULT 0,
        Resolved      INT      NOT NULL CONSTRAINT df_PSCS_Res     DEFAULT 0,
        InProgress    INT      NOT NULL CONSTRAINT df_PSCS_Prog    DEFAULT 0,

        CONSTRAINT pk_PSCS          PRIMARY KEY CLUSTERED (SnapshotId, CategoryId),
        CONSTRAINT fk_PSCS_Category FOREIGN KEY (CategoryId)  REFERENCES dbo.IssueCategories(CategoryId)
    );
END;
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 7. AIPendingScoreQueue
--    Retry queue for complaints that could not be scored because the
--    Python AI service was down at submission time.
--    A background job (or Azure Function) polls this and rescores.
-- ─────────────────────────────────────────────────────────────────────────────
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'AIPendingScoreQueue' AND SCHEMA_NAME(schema_id) = 'dbo')
BEGIN
    CREATE TABLE dbo.AIPendingScoreQueue (
        QueueId      INT          NOT NULL IDENTITY(1,1),
        ComplaintId  INT          NOT NULL UNIQUE,
        AttemptCount TINYINT      NOT NULL CONSTRAINT df_APSQ_Attempts DEFAULT 0,
        LastAttempt  DATETIME2(7) NULL,
        CreatedAt    DATETIME2(7) NOT NULL CONSTRAINT df_APSQ_Created  DEFAULT SYSDATETIME(),
        ErrorMessage NVARCHAR(500) NULL,

        CONSTRAINT pk_APSQ           PRIMARY KEY CLUSTERED (QueueId),
        CONSTRAINT fk_APSQ_Complaint FOREIGN KEY (ComplaintId) REFERENCES dbo.Complaints(ComplaintId)
    );
END;
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 8. Stored procedure: usp_SaveAIDecision
--    Used by the Python AI service (via the .NET API proxy) to log decisions.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.usp_SaveAIDecision
    @ComplaintId  INT           = NULL,
    @UserId       INT           = NULL,
    @DecisionType VARCHAR(50),
    @InputSummary NVARCHAR(500) = NULL,
    @OutputSummary NVARCHAR(500) = NULL,
    @Confidence   DECIMAL(5,4)  = NULL,
    @ModelVersion VARCHAR(50)   = NULL,
    @NewLogId     INT           OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.AIDecisionLog
        (ComplaintId, UserId, DecisionType, InputSummary, OutputSummary, Confidence, ModelVersion)
    VALUES
        (@ComplaintId, @UserId, @DecisionType, @InputSummary, @OutputSummary, @Confidence, @ModelVersion);
    SET @NewLogId = SCOPE_IDENTITY();
END;
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 9. Stored procedure: usp_SaveComplaintEmbedding
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.usp_SaveComplaintEmbedding
    @ComplaintId   INT,
    @EmbeddingJson NVARCHAR(MAX),
    @ModelVersion  VARCHAR(50)
AS
BEGIN
    SET NOCOUNT ON;
    MERGE dbo.ComplaintEmbeddings AS target
    USING (SELECT @ComplaintId AS ComplaintId) AS src ON target.ComplaintId = src.ComplaintId
    WHEN MATCHED THEN
        UPDATE SET EmbeddingJson = @EmbeddingJson,
                   ModelVersion  = @ModelVersion,
                   GeneratedAt   = SYSDATETIME()
    WHEN NOT MATCHED THEN
        INSERT (ComplaintId, EmbeddingJson, ModelVersion)
        VALUES (@ComplaintId, @EmbeddingJson, @ModelVersion);
END;
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 10. Stored procedure: usp_SaveComplaintTags (MERGE — idempotent)
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.usp_SaveComplaintTags
    @ComplaintId INT,
    @TagsJson    NVARCHAR(MAX)  -- JSON array: [{"tag":"pothole","score":0.87}, ...]
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.ComplaintTags WHERE ComplaintId = @ComplaintId AND Source = 'AI';

    INSERT INTO dbo.ComplaintTags (ComplaintId, Tag, Score, Source)
    SELECT @ComplaintId, j.tag, j.score, 'AI'
    FROM OPENJSON(@TagsJson)
        WITH (tag VARCHAR(80) '$.tag', score DECIMAL(5,4) '$.score') AS j
    WHERE j.tag IS NOT NULL;
END;
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 11. Stored procedure: usp_UpsertRecommendationCache
--     Replaces the cache for a single user atomically.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.usp_UpsertRecommendationCache
    @UserId          INT,
    @RecsJson        NVARCHAR(MAX)  -- JSON array: [{"complaint_id":5001,"score":0.93}, ...]
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
        DELETE FROM dbo.UserRecommendationCache WHERE UserId = @UserId;

        INSERT INTO dbo.UserRecommendationCache (UserId, ComplaintId, Score)
        SELECT @UserId, j.complaint_id, j.score
        FROM OPENJSON(@RecsJson)
            WITH (complaint_id INT '$.complaint_id', score DECIMAL(8,4) '$.score') AS j
        WHERE j.complaint_id IS NOT NULL
          AND EXISTS (SELECT 1 FROM dbo.Complaints c
                      WHERE c.ComplaintId = j.complaint_id
                        AND c.Status NOT IN ('Resolved','Rejected','Linked'));
    COMMIT;
END;
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 12. Stored procedure: usp_GetRecommendationsFromCache
--     Fast read path for GetRecommendedComplaints after nightly regen.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.usp_GetRecommendationsFromCache
    @UserId INT,
    @TopN   INT = 10
AS
BEGIN
    SET NOCOUNT ON;
    SELECT TOP (@TopN)
        c.ComplaintId, c.Title, c.CategoryId, c.LocalityId,
        c.Criticality, c.Status, c.SubmittedAt,
        ml.PriorityScore,
        urc.Score AS RelevanceScore
    FROM dbo.UserRecommendationCache urc
    INNER JOIN dbo.Complaints c ON c.ComplaintId = urc.ComplaintId
    LEFT  JOIN dbo.ComplaintMLScores ml ON ml.ComplaintId = c.ComplaintId
    WHERE urc.UserId = @UserId
      AND c.Status NOT IN ('Resolved','Rejected','Linked')
    ORDER BY urc.Score DESC, ISNULL(ml.PriorityScore, 0) DESC;
END;
GO

PRINT 'FixMyCity AI tables and procedures ready (idempotent).';
GO
