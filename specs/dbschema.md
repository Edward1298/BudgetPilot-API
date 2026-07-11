# BudgetPilot API — Database Schema

## Overview

| Item        | Detail                                                        |
|-------------|---------------------------------------------------------------|
| Engine      | SQL Server (local instance)                                   |
| Primary key | `uniqueidentifier` on every table                             |
| Soft delete | `is_active` column (`bit NOT NULL DEFAULT 1`) on users, accounts, categories, cards, budgets. Set to `0` instead of hard-deleting for non-admin users |
| Timestamps  | `created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME()` on most tables |

## Naming Convention

| Layer        | Case        | Example          |
|--------------|-------------|------------------|
| C# property  | PascalCase  | `UserId`         |
| JSON field   | camelCase   | `userId`         |
| DB column    | snake_case  | `user_id`        |

Entity classes use `[Table("table_name")]` and `[Column("column_name")]` attributes.

---

## Tables (in dependency order)

### roles

| Column | Type                | Nullable | Default           | Notes                    |
|--------|---------------------|----------|-------------------|--------------------------|
| `id`   | `uniqueidentifier`  | NO       | `NEWID()`         | PRIMARY KEY              |
| `name` | `nvarchar(50)`      | NO       | —                 | UNIQUE. `Admin` or `User` |

**Seed data** (inserted on setup):
- `Admin`
- `User`

---

### users

| Column         | Type                | Nullable | Default            | Notes                         |
|----------------|---------------------|----------|--------------------|-------------------------------|
| `id`           | `uniqueidentifier`  | NO       | `NEWID()`          | PRIMARY KEY                   |
| `name`         | `nvarchar(100)`     | NO       | —                  |                               |
| `email`        | `nvarchar(256)`     | NO       | —                  | UNIQUE                        |
| `password_hash`| `nvarchar(256)`     | NO       | —                  | BCrypt hashed                 |
| `role_id`      | `uniqueidentifier`  | NO       | —                  | FK → `roles(id)`              |
| `is_active`    | `bit`               | NO       | `1`                | Soft delete flag              |
| `created_at`   | `datetime2`         | NO       | `SYSUTCDATETIME()` |                               |

**Indexes:** `IX_users_is_active` on `is_active`.

---

### accounts

| Column          | Type                | Nullable | Default            | Notes                                |
|-----------------|---------------------|----------|--------------------|--------------------------------------|
| `id`            | `uniqueidentifier`  | NO       | `NEWID()`          | PRIMARY KEY                          |
| `user_id`       | `uniqueidentifier`  | NO       | —                  | FK → `users(id)`                     |
| `name`          | `nvarchar(100)`     | NO       | —                  |                                      |
| `type`          | `nvarchar(50)`      | NO       | —                  | `cash`, `bankAccount`, `savingsAccount` |
| `balance`       | `decimal(18, 2)`    | NO       | `0.00`             |                                      |
| `interest_rate` | `decimal(5, 2)`     | YES      | —                  | Only for `savingsAccount`            |
| `is_active`     | `bit`               | NO       | `1`                | Soft delete flag                     |
| `currency`      | `nvarchar(3)`       | NO       | `'USD'`            | `CRC`, `USD`, or `EUR`               |
| `created_at`    | `datetime2`         | NO       | `SYSUTCDATETIME()` |                                      |

**Indexes:** `IX_accounts_user_id` on `user_id`, `IX_accounts_is_active` on `is_active`.

---

### categories

| Column      | Type                | Nullable | Default  | Notes                         |
|-------------|---------------------|----------|----------|-------------------------------|
| `id`        | `uniqueidentifier`  | NO       | `NEWID()`| PRIMARY KEY                   |
| `user_id`   | `uniqueidentifier`  | NO       | —        | FK → `users(id)`              |
| `name`      | `nvarchar(100)`     | NO       | —        |                               |
| `type`      | `nvarchar(50)`      | NO       | —        | `income` / `expense`          |
| `is_active` | `bit`               | NO       | `1`      | Soft delete flag              |

**Indexes:** `IX_categories_user_id` on `user_id`, `IX_categories_is_active` on `is_active`.

---

### transactions

| Column        | Type                | Nullable | Default   | Notes                      |
|---------------|---------------------|----------|-----------|----------------------------|
| `id`          | `uniqueidentifier`  | NO       | `NEWID()` | PRIMARY KEY                |
| `user_id`     | `uniqueidentifier`  | NO       | —         | FK → `users(id)`           |
| `account_id`  | `uniqueidentifier`  | NO       | —         | FK → `accounts(id)`        |
| `category_id` | `uniqueidentifier`  | NO       | —         | FK → `categories(id)`      |
| `amount`      | `decimal(18, 2)`    | NO       | —         |                            |
| `type`        | `nvarchar(50)`      | NO       | —         | `income` / `expense`       |
| `description` | `nvarchar(500)`     | YES      | —         | Optional                   |
| `date`        | `date`              | NO       | —         | Transaction date           |

> **Note:** `transactions` does **not** have `is_active` — transactions cannot be soft-deleted. Only admins may hard-delete them.

**Indexes:** `IX_transactions_user_id` on `user_id`, `IX_transactions_account_id` on `account_id`, `IX_transactions_category_id` on `category_id`.

---

### cards

| Column                     | Type                | Nullable | Default            | Notes                          |
|----------------------------|---------------------|----------|--------------------|--------------------------------|
| `id`                       | `uniqueidentifier`  | NO       | `NEWID()`          | PRIMARY KEY                    |
| `user_id`                  | `uniqueidentifier`  | NO       | —                  | FK → `users(id)`               |
| `account_id`               | `uniqueidentifier`  | NO       | —                  | FK → `accounts(id)`            |
| `type`                     | `nvarchar(50)`      | NO       | —                  | `debit` / `credit`             |
| `card_number`              | `nvarchar(256)`     | NO       | —                  | AES-256-CBC encrypted          |
| `expiration_date`          | `date`              | NO       | —                  |                                |
| `cvc`                      | `nvarchar(128)`     | NO       | —                  | AES-256-CBC encrypted          |
| `name_on_card`             | `nvarchar(100)`     | NO       | —                  |                                |
| `credit_limit`             | `decimal(18, 2)`    | YES      | —                  | Credit cards only              |
| `apr`                      | `decimal(5, 2)`     | YES      | —                  | Credit cards only              |
| `statement_date`           | `int`               | YES      | —                  | 1–31, credit cards only        |
| `due_date`                 | `int`               | YES      | —                  | 1–31, credit cards only        |
| `minimum_payment_percentage`| `decimal(5, 2)`    | YES      | —                  | Credit cards only              |
| `current_balance`          | `decimal(18, 2)`    | NO       | `0.00`             | Credit cards only              |
| `is_active`                | `bit`               | NO       | `1`                | Soft delete flag               |
| `created_at`               | `datetime2`         | NO       | `SYSUTCDATETIME()` |                                |

**Indexes:** `IX_cards_user_id` on `user_id`, `IX_cards_account_id` on `account_id`, `IX_cards_is_active` on `is_active`.

---

### budgets

| Column        | Type                | Nullable | Default            | Notes                     |
|---------------|---------------------|----------|--------------------|---------------------------|
| `id`          | `uniqueidentifier`  | NO       | `NEWID()`          | PRIMARY KEY               |
| `user_id`     | `uniqueidentifier`  | NO       | —                  | FK → `users(id)`          |
| `category_id` | `uniqueidentifier`  | NO       | —                  | FK → `categories(id)`     |
| `amount`      | `decimal(18, 2)`    | NO       | —                  | Budget limit              |
| `month`       | `int`               | NO       | —                  | 1–12                      |
| `year`        | `int`               | NO       | —                  | e.g. 2026                 |
| `is_active`   | `bit`               | NO       | `1`                | Soft delete flag          |
| `created_at`  | `datetime2`         | NO       | `SYSUTCDATETIME()` |                           |

**Indexes:** `IX_budgets_user_id` on `user_id`, `IX_budgets_category_id` on `category_id`, `IX_budgets_is_active` on `is_active`.

**Uniqueness:** One budget per `(user_id, category_id, month, year)` — enforced at the application layer.

---

### refresh_tokens

| Column       | Type                | Nullable | Default            | Notes                         |
|--------------|---------------------|----------|--------------------|-------------------------------|
| `id`         | `uniqueidentifier`  | NO       | `NEWID()`          | PRIMARY KEY                   |
| `user_id`    | `uniqueidentifier`  | NO       | —                  | FK → `users(id)`              |
| `token`      | `nvarchar(500)`     | NO       | —                  | BCrypt hash of the raw token  |
| `expires_at` | `datetime2`         | NO       | —                  | Expiration timestamp          |
| `created_at` | `datetime2`         | NO       | `SYSUTCDATETIME()` |                               |
| `revoked_at` | `datetime2`         | YES      | —                  | Null until revoked on logout  |

**Indexes:** `IX_refresh_tokens_user_id` on `user_id`, `IX_refresh_tokens_token` on `token`.

---

## Relationships (ERD)

```
roles   1───* users
users   1───* accounts
users   1───* categories
users   1───* transactions
users   1───* cards
users   1───* budgets
users   1───* refresh_tokens
accounts 1───* transactions
accounts 1───* cards
categories 1───* transactions
categories 1───* budgets
```

All foreign keys use `uniqueidentifier` and reference `users(id)` as the root owner.

---

## Stored Procedures

| Procedure | Description |
|-----------|-------------|
| `sp_ApplyMonthlyInterest` | Updates `balance` for all active savings accounts with `interest_rate > 0`. Formula: `balance = balance + (balance * interest_rate / 100)` |
| `sp_GetAccountSummary` | Returns a single account row + the last 10 transactions in two result sets. Accepts `@AccountId` and `@UserId` |

---

## Setup

Run `scripts/sql_final.sql` in SQL Server Management Studio (SSMS) to create the `BudgetPilot` database and all tables from scratch. No EF Core migrations are used.
