-- BudgetPilot API SQL Server setup script
-- Run this script in SQL Server Management Studio (SSMS) to create the database
-- and tables from scratch. It uses Windows-friendly defaults and can be re-run
-- because it drops existing tables before recreating them.

-- Create the database if it does not already exist
IF DB_ID(N'BudgetPilot') IS NULL
BEGIN
    CREATE DATABASE BudgetPilot;
END
GO

USE BudgetPilot;
GO

-- Clean start: drop tables if they already exist (child tables first)
DROP TABLE IF EXISTS dbo.transactions;
DROP TABLE IF EXISTS dbo.categories;
DROP TABLE IF EXISTS dbo.accounts;
DROP TABLE IF EXISTS dbo.users;
GO

CREATE TABLE dbo.users (
    id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    name NVARCHAR(100) NOT NULL,
    email NVARCHAR(256) NOT NULL UNIQUE,
    password_hash NVARCHAR(256) NOT NULL,
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
);
GO

CREATE TABLE dbo.accounts (
    id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    user_id UNIQUEIDENTIFIER NOT NULL,
    name NVARCHAR(100) NOT NULL,
    type NVARCHAR(50) NOT NULL,
    balance DECIMAL(18, 2) NOT NULL DEFAULT 0.00,
    created_at DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT FK_accounts_users FOREIGN KEY (user_id) REFERENCES dbo.users(id)
);
GO

CREATE INDEX IX_accounts_user_id ON dbo.accounts(user_id);
GO

CREATE TABLE dbo.categories (
    id UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() PRIMARY KEY,
    user_id UNIQUEIDENTIFIER NOT NULL,
    name NVARCHAR(100) NOT NULL,
    type NVARCHAR(50) NOT NULL,
    CONSTRAINT FK_categories_users FOREIGN KEY (user_id) REFERENCES dbo.users(id)
);
GO

CREATE INDEX IX_categories_user_id ON dbo.categories(user_id);
GO

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
