-- ============================================================================
-- BudgetPilot API — Final Complete Setup Script
-- Drops and recreates everything from scratch.
-- Run in SSMS. Safe to re-run.
-- ============================================================================

IF DB_ID(N'BudgetPilot') IS NULL
BEGIN
    CREATE DATABASE BudgetPilot;
END
GO

USE BudgetPilot;
GO

-- ============================================================================
-- DROP existing objects (child tables first, then parents, then SPs)
-- ============================================================================
DROP PROCEDURE IF EXISTS dbo.sp_GetAccountSummary;
DROP PROCEDURE IF EXISTS dbo.sp_ApplyMonthlyInterest;
DROP TABLE IF EXISTS dbo.refresh_tokens;
DROP TABLE IF EXISTS dbo.cards;
DROP TABLE IF EXISTS dbo.budgets;
DROP TABLE IF EXISTS dbo.transactions;
DROP TABLE IF EXISTS dbo.categories;
DROP TABLE IF EXISTS dbo.accounts;
DROP TABLE IF EXISTS dbo.users;
DROP TABLE IF EXISTS dbo.roles;
GO

-- ============================================================================
-- 1. roles
-- ============================================================================
CREATE TABLE dbo.roles (
    id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    name NVARCHAR(50) NOT NULL UNIQUE
);
GO

INSERT INTO dbo.roles (id, name) VALUES (NEWID(), 'Admin');
INSERT INTO dbo.roles (id, name) VALUES (NEWID(), 'User');
GO

-- ============================================================================
-- 2. users
-- ============================================================================
CREATE TABLE dbo.users (
    id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    name NVARCHAR(100) NOT NULL,
    email NVARCHAR(256) NOT NULL UNIQUE,
    password_hash NVARCHAR(256) NOT NULL,
    role_id UNIQUEIDENTIFIER NOT NULL,
    is_active BIT NOT NULL DEFAULT 1,
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_users_roles FOREIGN KEY (role_id) REFERENCES dbo.roles(id)
);
GO

CREATE INDEX IX_users_is_active ON dbo.users(is_active);
GO

-- ============================================================================
-- 3. accounts
-- ============================================================================
CREATE TABLE dbo.accounts (
    id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    user_id UNIQUEIDENTIFIER NOT NULL,
    name NVARCHAR(100) NOT NULL,
    type NVARCHAR(50) NOT NULL,
    balance DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
    interest_rate DECIMAL(5, 2) NULL,
    is_active BIT NOT NULL DEFAULT 1,
    currency NVARCHAR(3) NOT NULL DEFAULT 'USD',
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_accounts_users FOREIGN KEY (user_id) REFERENCES dbo.users(id)
);
GO

CREATE INDEX IX_accounts_user_id ON dbo.accounts(user_id);
CREATE INDEX IX_accounts_is_active ON dbo.accounts(is_active);
GO

-- ============================================================================
-- 4. categories
-- ============================================================================
CREATE TABLE dbo.categories (
    id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    user_id UNIQUEIDENTIFIER NOT NULL,
    name NVARCHAR(100) NOT NULL,
    type NVARCHAR(50) NOT NULL,
    is_active BIT NOT NULL DEFAULT 1,
    CONSTRAINT FK_categories_users FOREIGN KEY (user_id) REFERENCES dbo.users(id)
);
GO

CREATE INDEX IX_categories_user_id ON dbo.categories(user_id);
CREATE INDEX IX_categories_is_active ON dbo.categories(is_active);
GO

-- ============================================================================
-- 5. transactions (NO is_active column — cannot be soft-deleted)
-- ============================================================================
CREATE TABLE dbo.transactions (
    id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    user_id UNIQUEIDENTIFIER NOT NULL,
    account_id UNIQUEIDENTIFIER NOT NULL,
    category_id UNIQUEIDENTIFIER NOT NULL,
    amount DECIMAL(18, 2) NOT NULL,
    type NVARCHAR(50) NOT NULL,
    description NVARCHAR(500) NULL,
    date DATE NOT NULL,
    CONSTRAINT FK_transactions_users FOREIGN KEY (user_id) REFERENCES dbo.users(id),
    CONSTRAINT FK_transactions_accounts FOREIGN KEY (account_id) REFERENCES dbo.accounts(id),
    CONSTRAINT FK_transactions_categories FOREIGN KEY (category_id) REFERENCES dbo.categories(id)
);
GO

CREATE INDEX IX_transactions_user_id ON dbo.transactions(user_id);
CREATE INDEX IX_transactions_account_id ON dbo.transactions(account_id);
CREATE INDEX IX_transactions_category_id ON dbo.transactions(category_id);
GO

-- ============================================================================
-- 6. cards
-- ============================================================================
CREATE TABLE dbo.cards (
    id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    user_id UNIQUEIDENTIFIER NOT NULL,
    account_id UNIQUEIDENTIFIER NOT NULL,
    type NVARCHAR(50) NOT NULL,
    card_number NVARCHAR(256) NOT NULL,
    expiration_date DATE NOT NULL,
    cvc NVARCHAR(128) NOT NULL,
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
GO

CREATE INDEX IX_cards_user_id ON dbo.cards(user_id);
CREATE INDEX IX_cards_account_id ON dbo.cards(account_id);
CREATE INDEX IX_cards_is_active ON dbo.cards(is_active);
GO

-- ============================================================================
-- 7. budgets
-- ============================================================================
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
GO

CREATE INDEX IX_budgets_user_id ON dbo.budgets(user_id);
CREATE INDEX IX_budgets_category_id ON dbo.budgets(category_id);
CREATE INDEX IX_budgets_is_active ON dbo.budgets(is_active);
GO

-- ============================================================================
-- 8. refresh_tokens
-- ============================================================================
CREATE TABLE dbo.refresh_tokens (
    id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    user_id UNIQUEIDENTIFIER NOT NULL,
    token NVARCHAR(500) NOT NULL,
    expires_at DATETIME2 NOT NULL,
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    revoked_at DATETIME2 NULL,
    CONSTRAINT FK_refresh_tokens_users FOREIGN KEY (user_id) REFERENCES dbo.users(id)
);
GO

CREATE INDEX IX_refresh_tokens_user_id ON dbo.refresh_tokens(user_id);
CREATE INDEX IX_refresh_tokens_token ON dbo.refresh_tokens(token);
GO

-- ============================================================================
-- 9. Stored Procedures
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
-- Returns account details + last 10 transactions in one round-trip.
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

PRINT '============================================================================';
PRINT 'BudgetPilot DB setup completed successfully.';
PRINT '============================================================================';
GO

-- ============================================================================
-- Verification
-- ============================================================================
PRINT 'Tables created:';
SELECT TABLE_NAME
FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_SCHEMA = 'dbo'
ORDER BY TABLE_NAME;
GO

PRINT 'Roles seeded:';
SELECT id, name FROM dbo.roles;
GO

PRINT 'Stored procedures created:';
SELECT ROUTINE_NAME
FROM INFORMATION_SCHEMA.ROUTINES
WHERE ROUTINE_SCHEMA = 'dbo' AND ROUTINE_TYPE = 'PROCEDURE'
ORDER BY ROUTINE_NAME;
GO
