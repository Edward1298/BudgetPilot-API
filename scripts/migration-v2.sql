-- ============================================================================
-- BudgetPilot API — v2.0 Migration Script
-- ============================================================================
-- Run this script in SSMS against the existing BudgetPilot database.
-- Safe to re-run: all statements are idempotent.
-- Handles existing data: NOT NULL columns are added with defaults or
-- populated before being made NOT NULL.
-- ============================================================================

USE BudgetPilot;
GO

-- ============================================================================
-- STEP 1: Create dbo.roles table
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'roles')
BEGIN
    CREATE TABLE dbo.roles (
        id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        name NVARCHAR(50) NOT NULL UNIQUE
    );
END
GO

-- ============================================================================
-- STEP 2: Seed roles (Admin, User)
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM dbo.roles WHERE name = 'Admin')
    INSERT INTO dbo.roles (id, name) VALUES (NEWID(), 'Admin');
GO

IF NOT EXISTS (SELECT 1 FROM dbo.roles WHERE name = 'User')
    INSERT INTO dbo.roles (id, name) VALUES (NEWID(), 'User');
GO

-- ============================================================================
-- STEP 3: Modify dbo.users — add role_id and is_active
-- ============================================================================

-- 3a. Add role_id as NULL first (safe for existing rows)
IF COL_LENGTH('dbo.users', 'role_id') IS NULL
    ALTER TABLE dbo.users ADD role_id UNIQUEIDENTIFIER NULL;
GO

-- 3b. Populate existing users with the 'User' role
UPDATE dbo.users
SET role_id = (SELECT id FROM dbo.roles WHERE name = 'User')
WHERE role_id IS NULL;
GO

-- 3c. Make role_id NOT NULL now that all rows have a value
IF COL_LENGTH('dbo.users', 'role_id') IS NOT NULL
BEGIN
    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('dbo.users') AND name = 'role_id' AND is_nullable = 1)
        ALTER TABLE dbo.users ALTER COLUMN role_id UNIQUEIDENTIFIER NOT NULL;
END
GO

-- 3d. Add foreign key to roles
IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_users_roles')
    ALTER TABLE dbo.users ADD CONSTRAINT FK_users_roles FOREIGN KEY (role_id) REFERENCES dbo.roles(id);
GO

-- 3e. Add is_active column
IF COL_LENGTH('dbo.users', 'is_active') IS NULL
    ALTER TABLE dbo.users ADD is_active BIT NOT NULL DEFAULT 1;
GO

-- 3f. Index on is_active
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_users_is_active' AND object_id = OBJECT_ID('dbo.users'))
    CREATE INDEX IX_users_is_active ON dbo.users(is_active);
GO

-- ============================================================================
-- STEP 4: Modify dbo.accounts — add is_active and interest_rate
-- ============================================================================

-- 4a. Add is_active column
IF COL_LENGTH('dbo.accounts', 'is_active') IS NULL
    ALTER TABLE dbo.accounts ADD is_active BIT NOT NULL DEFAULT 1;
GO

-- 4b. Add interest_rate column (NULL allowed — only savings accounts use it)
IF COL_LENGTH('dbo.accounts', 'interest_rate') IS NULL
    ALTER TABLE dbo.accounts ADD interest_rate DECIMAL(5, 2) NULL;
GO

-- 4c. Index on is_active
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_accounts_is_active' AND object_id = OBJECT_ID('dbo.accounts'))
    CREATE INDEX IX_accounts_is_active ON dbo.accounts(is_active);
GO

-- ============================================================================
-- STEP 5: Modify dbo.categories — add is_active
-- ============================================================================

-- 5a. Add is_active column
IF COL_LENGTH('dbo.categories', 'is_active') IS NULL
    ALTER TABLE dbo.categories ADD is_active BIT NOT NULL DEFAULT 1;
GO

-- 5b. Index on is_active
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_categories_is_active' AND object_id = OBJECT_ID('dbo.categories'))
    CREATE INDEX IX_categories_is_active ON dbo.categories(is_active);
GO

-- ============================================================================
-- STEP 6: Modify dbo.transactions — add is_active
-- ============================================================================

-- 6a. Add is_active column
IF COL_LENGTH('dbo.transactions', 'is_active') IS NULL
    ALTER TABLE dbo.transactions ADD is_active BIT NOT NULL DEFAULT 1;
GO

-- 6b. Index on is_active
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_transactions_is_active' AND object_id = OBJECT_ID('dbo.transactions'))
    CREATE INDEX IX_transactions_is_active ON dbo.transactions(is_active);
GO

-- ============================================================================
-- STEP 7: Create dbo.cards table
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'cards')
BEGIN
    CREATE TABLE dbo.cards (
        id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        user_id UNIQUEIDENTIFIER NOT NULL,
        account_id UNIQUEIDENTIFIER NOT NULL,
        type NVARCHAR(50) NOT NULL,
        card_number NVARCHAR(20) NOT NULL,
        expiration_date DATE NOT NULL,
        cvc NVARCHAR(10) NOT NULL,
        name_on_card NVARCHAR(100) NOT NULL,
        credit_limit DECIMAL(18, 2) NULL,
        apr DECIMAL(5, 2) NULL,
        statement_date INT NULL,
        due_date INT NULL,
        minimum_payment_percentage DECIMAL(5, 2) NULL,
        current_balance DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
        is_active BIT NOT NULL DEFAULT 1,
        created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_cards_users FOREIGN KEY (user_id) REFERENCES dbo.users(id),
        CONSTRAINT FK_cards_accounts FOREIGN KEY (account_id) REFERENCES dbo.accounts(id)
    );
END
GO

-- 7b. Indexes on cards
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cards_user_id' AND object_id = OBJECT_ID('dbo.cards'))
    CREATE INDEX IX_cards_user_id ON dbo.cards(user_id);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cards_account_id' AND object_id = OBJECT_ID('dbo.cards'))
    CREATE INDEX IX_cards_account_id ON dbo.cards(account_id);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_cards_is_active' AND object_id = OBJECT_ID('dbo.cards'))
    CREATE INDEX IX_cards_is_active ON dbo.cards(is_active);
GO

-- ============================================================================
-- STEP 8: Create dbo.budgets table
-- ============================================================================
IF NOT EXISTS (SELECT 1 FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = 'budgets')
BEGIN
    CREATE TABLE dbo.budgets (
        id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
        user_id UNIQUEIDENTIFIER NOT NULL,
        category_id UNIQUEIDENTIFIER NOT NULL,
        amount DECIMAL(18, 2) NOT NULL,
        month INT NOT NULL,
        year INT NOT NULL,
        is_active BIT NOT NULL DEFAULT 1,
        created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
        CONSTRAINT FK_budgets_users FOREIGN KEY (user_id) REFERENCES dbo.users(id),
        CONSTRAINT FK_budgets_categories FOREIGN KEY (category_id) REFERENCES dbo.categories(id)
    );
END
GO

-- 8b. Indexes on budgets
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_budgets_user_id' AND object_id = OBJECT_ID('dbo.budgets'))
    CREATE INDEX IX_budgets_user_id ON dbo.budgets(user_id);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_budgets_category_id' AND object_id = OBJECT_ID('dbo.budgets'))
    CREATE INDEX IX_budgets_category_id ON dbo.budgets(category_id);
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_budgets_is_active' AND object_id = OBJECT_ID('dbo.budgets'))
    CREATE INDEX IX_budgets_is_active ON dbo.budgets(is_active);
GO

-- ============================================================================
-- STEP 9: Stored Procedures
-- ============================================================================

-- 9a. sp_ApplyMonthlyInterest
-- Applies monthly interest to all active Savings Accounts.
-- Formula: balance = balance + (balance * interest_rate / 100)
CREATE OR ALTER PROCEDURE dbo.sp_ApplyMonthlyInterest
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.accounts
    SET balance = balance + (balance * interest_rate / 100)
    WHERE type = 'savingsAccount'
      AND is_active = 1
      AND interest_rate IS NOT NULL
      AND interest_rate > 0;
END
GO

-- 9b. sp_GetAccountSummary
-- Returns account details + last 10 active transactions in one round-trip.
CREATE OR ALTER PROCEDURE dbo.sp_GetAccountSummary
    @AccountId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT id, user_id, name, type, balance, interest_rate, is_active, created_at
    FROM dbo.accounts
    WHERE id = @AccountId AND user_id = @UserId;

    SELECT TOP 10 id, user_id, account_id, category_id, amount, type, description, date, is_active
    FROM dbo.transactions
    WHERE account_id = @AccountId AND user_id = @UserId AND is_active = 1
    ORDER BY date DESC;
END
GO

-- ============================================================================
-- DONE — Verify results
-- ============================================================================
PRINT 'Migration v2.0 completed successfully.';
PRINT '';
PRINT 'Tables:';
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = 'dbo' ORDER BY TABLE_NAME;
PRINT '';
PRINT 'Roles:';
SELECT id, name FROM dbo.roles;
PRINT '';
PRINT 'Stored Procedures:';
SELECT ROUTINE_NAME FROM INFORMATION_SCHEMA.ROUTINES WHERE ROUTINE_SCHEMA = 'dbo' AND ROUTINE_TYPE = 'PROCEDURE';
GO
