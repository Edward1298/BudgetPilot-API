# BudgetPilot API — Database Schema

## Overview

| Item        | Detail                                                        |
|-------------|---------------------------------------------------------------|
| Engine      | SQL Server (local instance)                                   |
| Primary key | `uniqueidentifier` on every table                             |
| Soft delete | Not implemented                                               |
| Timestamps  | `created_at datetime2 NOT NULL DEFAULT SYSUTCDATETIME()`      |

## Naming Convention

| Layer        | Case        | Example          |
|--------------|-------------|------------------|
| C# property  | PascalCase  | `UserId`         |
| JSON field   | camelCase   | `userId`         |
| DB column    | snake_case  | `user_id`        |

Entity classes use `[Table("table_name")]` and `[Column("column_name")]` attributes.

---

## Tables

### users

| Column        | Type                          | Nullable | Default            | Notes              |
|---------------|-------------------------------|----------|--------------------|--------------------|
| `id`          | `uniqueidentifier`            | NO       | `NEWID()`          | PRIMARY KEY        |
| `name`        | `nvarchar(100)`               | NO       | —                  |                    |
| `email`       | `nvarchar(256)`               | NO       | —                  | UNIQUE             |
| `password_hash` | `nvarchar(256)`             | NO       | —                  | BCrypt hashed      |
| `created_at`  | `datetime2`                   | NO       | `SYSUTCDATETIME()` |                    |

### accounts

| Column       | Type                          | Nullable | Default            | Notes                          |
|--------------|-------------------------------|----------|--------------------|--------------------------------|
| `id`         | `uniqueidentifier`            | NO       | `NEWID()`          | PRIMARY KEY                    |
| `user_id`    | `uniqueidentifier`            | NO       | —                  | FK → `users(id)`               |
| `name`       | `nvarchar(100)`               | NO       | —                  |                                |
| `type`       | `nvarchar(50)`                | NO       | —                  | cash, creditCard, bankAccount  |
| `balance`    | `decimal(18, 2)`              | NO       | `0.00`             |                                |
| `created_at` | `datetime2`                   | NO       | `SYSUTCDATETIME()` |                                |

### categories

| Column    | Type           | Nullable | Default | Notes                            |
|-----------|----------------|----------|---------|----------------------------------|
| `id`      | `uniqueidentifier`| NO    | `NEWID()`| PRIMARY KEY                     |
| `user_id` | `uniqueidentifier`| NO    | —       | FK → `users(id)`                 |
| `name`    | `nvarchar(100)`| NO       | —       |                                  |
| `type`    | `nvarchar(50)` | NO       | —       | income / expense                 |

### transactions

| Column        | Type              | Nullable | Default   | Notes                    |
|---------------|-------------------|----------|-----------|--------------------------|
| `id`          | `uniqueidentifier`| NO       | `NEWID()` | PRIMARY KEY              |
| `user_id`     | `uniqueidentifier`| NO       | —         | FK → `users(id)`         |
| `account_id`  | `uniqueidentifier`| NO       | —         | FK → `accounts(id)`      |
| `category_id` | `uniqueidentifier`| NO       | —         | FK → `categories(id)`    |
| `amount`      | `decimal(18, 2)`  | NO       | —         |                          |
| `type`        | `nvarchar(50)`    | NO       | —         | income / expense         |
| `description` | `nvarchar(500)`   | YES      | —         | Optional                 |
| `date`        | `date`            | NO       | —         | Transaction date         |

### budgets

| Column       | Type                | Nullable | Default   | Notes                    |
|--------------|---------------------|----------|-----------|--------------------------|
| `id`         | `uniqueidentifier`  | NO       | `NEWID()` | PRIMARY KEY              |
| `user_id`    | `uniqueidentifier`  | NO       | —         | FK → `users(id)`         |
| `category_id`| `uniqueidentifier`  | NO       | —         | FK → `categories(id)`    |
| `amount`     | `decimal(18, 2)`    | NO       | —         | Budget limit             |
| `month`      | `int`               | NO       | —         | 1–12                     |
| `year`       | `int`               | NO       | —         | e.g. 2026                |

> Budgets are **future scope** — entity is defined in DB but not yet implemented in the API.

---

## Relationships (ERD)

```
users 1───* accounts
users 1───* categories
users 1───* transactions
accounts 1───* transactions
categories 1───* transactions
users 1───* budgets
categories 1───* budgets
```

All foreign keys use `uniqueidentifier` and reference `users(id)` as the root owner.
Transactions are the central entity linking accounts and categories.

---

## Setup

Run `scripts/setup-sqlserver.sql` in SQL Server Management Studio (SSMS) to create the `BudgetPilot` database and tables. No EF Core migrations are used.
