-- FixMyCity — AI/ML Table Additions (Sprint 3)
-- Run this against FixMyCityDB after Sprint 2 schema is in place.
-- All tables use DATETIME2(7) and SYSDATETIME() consistent with Sprint 2.
-- ─────────────────────────────────────────────────────────────────────────────

-- 1. Extend PredictionModelVersion to hold semantic version strings e.g. "v2.1.0-lgbm"
ALTER TABLE dbo.ComplaintMLScores
    ALTER COLUMN PredictionModelVersion VARCHAR(50) NULL;
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 2. ComplaintEmbeddings
--    Stores the 384-float sentence-transformer vector for each complaint
--    as a JSON array so no vector DB infrastructure is needed.
--    Used for cosine-similarity duplicate detection.
-- ─────────────────────────────────────────────────────────────────────────────
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
CREATE INDEX ix_CE_GeneratedAt ON dbo.ComplaintEmbeddings (ComplaintId, GeneratedAt DESC);
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 3. UserRecommendationCache
--    Nightly pre-computed top-N complaint IDs per user.
--    Consumed by GET api/ML/GetRecommendedComplaints instead of the
--    expensive real-time JOIN on UserInterests.
-- ─────────────────────────────────────────────────────────────────────────────
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
CREATE INDEX ix_URC_User ON dbo.UserRecommendationCache (UserId, Score DESC);
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 4. AIDecisionLog
--    Every AI inference is logged here. Supports debugging, retraining,
--    and explainability for solvers/admins ("why did AI flag this?").
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE dbo.AIDecisionLog (
    LogId         INT           NOT NULL IDENTITY(1,1),
    ComplaintId   INT           NULL,
    UserId        INT           NULL,
    DecisionType  VARCHAR(50)   NOT NULL,   -- 'Categorization' | 'DuplicateFlag' | 'ToxicityFlag' | 'PriorityScore' | 'ResolutionPrediction' | 'ImageClassification' | 'AutoTag' | 'Recommendation'
    InputSummary  NVARCHAR(500) NULL,       -- first 200 chars of description or image filename
    OutputSummary NVARCHAR(500) NULL,       -- predicted label + confidence
    Confidence    DECIMAL(5,4)  NULL,
    ModelVersion  VARCHAR(50)   NULL,
    WasOverridden BIT           NOT NULL CONSTRAINT df_ADL_Override DEFAULT 0,
    OverriddenBy  INT           NULL,       -- UserId who corrected the AI
    CreatedAt     DATETIME2(7)  NOT NULL CONSTRAINT df_ADL_CreatedAt DEFAULT SYSDATETIME(),

    CONSTRAINT pk_ADL PRIMARY KEY CLUSTERED (LogId),
    CONSTRAINT fk_ADL_Complaint   FOREIGN KEY (ComplaintId) REFERENCES dbo.Complaints(ComplaintId),
    CONSTRAINT fk_ADL_User        FOREIGN KEY (UserId)      REFERENCES dbo.Users(UserId),
    CONSTRAINT fk_ADL_Overrider   FOREIGN KEY (OverriddenBy) REFERENCES dbo.Users(UserId),
    CONSTRAINT ck_ADL_Type        CHECK (DecisionType IN (
        'Categorization','DuplicateFlag','ToxicityFlag','PriorityScore',
        'ResolutionPrediction','ImageClassification','AutoTag','Recommendation'))
);
CREATE INDEX ix_ADL_Complaint ON dbo.AIDecisionLog (ComplaintId, CreatedAt DESC);
CREATE INDEX ix_ADL_Type      ON dbo.AIDecisionLog (DecisionType, CreatedAt DESC);
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 5. ComplaintTags
--    AI-generated or manually added keyword tags per complaint.
--    KeyBERT extracts these from Title + Description.
--    Enriches SearchComplaints beyond LIKE matching.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE dbo.ComplaintTags (
    TagId       INT          NOT NULL IDENTITY(1,1),
    ComplaintId INT          NOT NULL,
    Tag         VARCHAR(80)  NOT NULL,
    Score       DECIMAL(5,4) NULL,          -- KeyBERT relevance score
    Source      VARCHAR(20)  NOT NULL CONSTRAINT df_CT_Source DEFAULT 'AI',   -- 'AI' | 'Manual'

    CONSTRAINT pk_CT          PRIMARY KEY CLUSTERED (TagId),
    CONSTRAINT fk_CT_Complaint FOREIGN KEY (ComplaintId) REFERENCES dbo.Complaints(ComplaintId),
    CONSTRAINT ck_CT_Source   CHECK (Source IN ('AI','Manual')),
    INDEX ix_CT_Tag (Tag)
);
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 6. PlatformStatsCategorySnapshot
--    Extends the daily PlatformStatsSnapshot with per-category counts.
--    Required for Prophet per-category trend forecasting (Section 2.13).
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE dbo.PlatformStatsCategorySnapshot (
    SnapshotId    INT      NOT NULL,
    CategoryId    SMALLINT NOT NULL,
    NewComplaints INT      NOT NULL CONSTRAINT df_PSCS_New     DEFAULT 0,
    Resolved      INT      NOT NULL CONSTRAINT df_PSCS_Res     DEFAULT 0,
    InProgress    INT      NOT NULL CONSTRAINT df_PSCS_Prog    DEFAULT 0,

    CONSTRAINT pk_PSCS          PRIMARY KEY CLUSTERED (SnapshotId, CategoryId),
    CONSTRAINT fk_PSCS_Category FOREIGN KEY (CategoryId)  REFERENCES dbo.IssueCategories(CategoryId)
);
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 7. AIPendingScoreQueue
--    Retry queue for complaints that could not be scored because the
--    Python AI service was down at submission time.
--    A background job (or Azure Function) polls this and rescores.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE TABLE dbo.AIPendingScoreQueue (
    QueueId      INT          NOT NULL IDENTITY(1,1),
    ComplaintId  INT          NOT NULL UNIQUE,
    AttemptCount TINYINT      NOT NULL CONSTRAINT df_APSQ_Attempts DEFAULT 0,
    LastAttempt  DATETIME2(7) NULL,
    CreatedAt    DATETIME2(7) NOT NULL CONSTRAINT df_APSQ_Created  DEFAULT SYSDATETIME(),
    ErrorMessage NVARCHAR(500) NULL,

    CONSTRAINT pk_APSQ          PRIMARY KEY CLUSTERED (QueueId),
    CONSTRAINT fk_APSQ_Complaint FOREIGN KEY (ComplaintId) REFERENCES dbo.Complaints(ComplaintId)
);
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

PRINT 'FixMyCity AI tables and procedures created successfully.';
GO
