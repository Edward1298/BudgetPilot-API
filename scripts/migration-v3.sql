-- ============================================================================
-- BudgetPilot API — v2.1 Migration Script
-- ============================================================================
USE BudgetPilot;
GO

-- STEP 1: Add currency column to accounts
IF COL_LENGTH('dbo.accounts', 'currency') IS NULL
    ALTER TABLE dbo.accounts ADD currency NVARCHAR(3) NOT NULL DEFAULT 'USD';
GO

-- STEP 2: Drop is_active from transactions (with dependency cleanup)
IF COL_LENGTH('dbo.transactions', 'is_active') IS NOT NULL
BEGIN
    -- Drop the index first
    IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_transactions_is_active' AND object_id = OBJECT_ID('dbo.transactions'))
        DROP INDEX IX_transactions_is_active ON dbo.transactions;

    -- Drop the default constraint (name varies, find it dynamically)
    DECLARE @constraintName NVARCHAR(256);
    SELECT @constraintName = OBJECT_NAME(default_object_id)
    FROM sys.columns
    WHERE object_id = OBJECT_ID('dbo.transactions')
      AND name = 'is_active';

    IF @constraintName IS NOT NULL
        EXEC('ALTER TABLE dbo.transactions DROP CONSTRAINT ' + @constraintName);

    -- Now drop the column
    ALTER TABLE dbo.transactions DROP COLUMN is_active;
END
GO

-- STEP 3: Update sp_GetAccountSummary
CREATE OR ALTER PROCEDURE dbo.sp_GetAccountSummary
    @AccountId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT id, user_id, name, type, balance, interest_rate, is_active, created_at, currency
    FROM dbo.accounts
    WHERE id = @AccountId AND user_id = @UserId;

    SELECT TOP 10 id, user_id, account_id, category_id, amount, type, description, date
    FROM dbo.transactions
    WHERE account_id = @AccountId AND user_id = @UserId
    ORDER BY date DESC;
END
GO

PRINT 'Migration v2.1 completed successfully.';
GO
