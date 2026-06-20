-- Run this against FixMyCityDB after your Sprint 2 schema.
-- Creates the UserRefreshTokens table used by JwtService for refresh token rotation.
USE FixMyCityDB
GO

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'UserRefreshTokens')
BEGIN
    CREATE TABLE dbo.UserRefreshTokens (
        TokenId    INT              NOT NULL IDENTITY(1,1) PRIMARY KEY,
        UserId     INT              NOT NULL,
        TokenHash  CHAR(64)         NOT NULL,           -- SHA-256 hex (64 chars)
        ExpiresAt  DATETIME2(0)     NOT NULL,
        CreatedAt  DATETIME2(0)     NOT NULL DEFAULT SYSUTCDATETIME(),
        RevokedAt  DATETIME2(0)     NULL,

        CONSTRAINT FK_RefreshToken_User FOREIGN KEY (UserId)
            REFERENCES dbo.Users(UserId) ON DELETE CASCADE
    );

    CREATE UNIQUE INDEX UX_RefreshToken_Hash
        ON dbo.UserRefreshTokens (TokenHash);

    CREATE INDEX IX_RefreshToken_UserId
        ON dbo.UserRefreshTokens (UserId);

    -- Purge expired/revoked tokens older than 30 days (run as a scheduled job)
    -- DELETE FROM dbo.UserRefreshTokens
    -- WHERE (ExpiresAt < DATEADD(DAY, -30, SYSUTCDATETIME())
    --        OR RevokedAt IS NOT NULL);
END;
GO

PRINT 'UserRefreshTokens table ready.';
