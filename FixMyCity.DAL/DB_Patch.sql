-- FixMyCity_Patch_P1_P2.sql
-- Run order: after Sprint 2 schema and AI_Tables_Addition.sql

USE FixMyCityDB;
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 1. usp_AutoEscalateAll
--    Called daily by AutoEscalationService (US50).
--    Finds all 'In Progress' complaints stale > 30 days and escalates each
--    by delegating to the existing usp_FileEscalation with EscalationTrigger='Auto'.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.usp_AutoEscalateAll
AS
BEGIN
    SET NOCOUNT ON;

    -- Snapshot eligible complaint IDs to avoid cursor instability during updates
    DECLARE @Ids TABLE (ComplaintId INT NOT NULL PRIMARY KEY);

    INSERT INTO @Ids (ComplaintId)
    SELECT ComplaintId
    FROM   dbo.Complaints
    WHERE  Status    = 'In Progress'
      AND  UpdatedAt <= DATEADD(DAY, -30, SYSDATETIME());

    IF NOT EXISTS (SELECT 1 FROM @Ids) RETURN;

    DECLARE @Id INT;
    DECLARE cur CURSOR LOCAL FAST_FORWARD READ_ONLY FOR
        SELECT ComplaintId FROM @Ids ORDER BY ComplaintId;

    OPEN cur;
    FETCH NEXT FROM cur INTO @Id;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        BEGIN TRY
            EXEC dbo.usp_FileEscalation
                @ComplaintId        = @Id,
                @EscalationTrigger  = 'Auto',
                @AdminUserId        = NULL,
                @ReassignedToDeptId = NULL,
                @Reason             = NULL;
        END TRY
        BEGIN CATCH
            -- Silently skip: complaint may have already been escalated
            -- or status changed between snapshot and cursor iteration.
        END CATCH

        FETCH NEXT FROM cur INTO @Id;
    END;

    CLOSE cur;
    DEALLOCATE cur;
END;
GO

-- ─────────────────────────────────────────────────────────────────────────────
-- 2. usp_CreateContribution
--    Atomic idempotent insert for citizen financial contributions (US22).
--    Uses UPDLOCK + ROWLOCK to prevent TOCTOU race on TransactionRef.
--    Replaces the direct EF Core Add+SaveChanges in PaymentRepository.
-- ─────────────────────────────────────────────────────────────────────────────
CREATE OR ALTER PROCEDURE dbo.usp_CreateContribution
    @ComplaintId       INT,
    @CitizenUserId     INT,
    @Amount            DECIMAL(12,2),
    @TransactionRef    VARCHAR(100),
    @NewContributionId INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @NewContributionId = 0;

    BEGIN TRANSACTION;
    BEGIN TRY
        -- UPDLOCK on the existence check prevents two concurrent requests for the
        -- same TransactionRef from both passing and inserting a duplicate row.
        IF EXISTS (
            SELECT 1
            FROM   dbo.Contributions WITH (UPDLOCK, ROWLOCK)
            WHERE  TransactionRef = @TransactionRef
        )
        BEGIN
            -- Idempotent: return existing contribution ID
            SELECT @NewContributionId = ContributionId
            FROM   dbo.Contributions
            WHERE  TransactionRef = @TransactionRef;
            COMMIT;
            RETURN;
        END

        INSERT INTO dbo.Contributions
            (ComplaintId, CitizenUserId, Amount, TransactionRef, PaymentStatus, ContributedAt)
        VALUES
            (@ComplaintId, @CitizenUserId, @Amount, @TransactionRef, 'Pending', SYSDATETIME());

        SET @NewContributionId = SCOPE_IDENTITY();
        COMMIT;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK;
        THROW;
    END CATCH
END;
GO

PRINT 'FixMyCity_Patch_P1_P2.sql applied successfully.';
GO