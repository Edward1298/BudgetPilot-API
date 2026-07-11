-- ============================================================================
-- BudgetPilot API — v4.0 Migration Script
-- ============================================================================
USE BudgetPilot;
GO

-- STEP 1: Create refresh_tokens table
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'refresh_tokens')
BEGIN
    CREATE TABLE dbo.refresh_tokens (
        id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        user_id UNIQUEIDENTIFIER NOT NULL,
        token NVARCHAR(500) NOT NULL,
        expires_at DATETIME2 NOT NULL,
        created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        revoked_at DATETIME2 NULL,
        CONSTRAINT FK_refresh_tokens_users FOREIGN KEY (user_id) REFERENCES dbo.users(id)
    );
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_refresh_tokens_user_id' AND object_id = OBJECT_ID('dbo.refresh_tokens'))
    CREATE INDEX IX_refresh_tokens_user_id ON dbo.refresh_tokens(user_id);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_refresh_tokens_token' AND object_id = OBJECT_ID('dbo.refresh_tokens'))
    CREATE INDEX IX_refresh_tokens_token ON dbo.refresh_tokens(token);
GO

PRINT 'Migration v4.0 completed successfully.';
GO
