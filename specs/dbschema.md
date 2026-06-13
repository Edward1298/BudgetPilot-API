# BudgetPilot API — Database Schema

## Overview

| Item        | Detail                                                        |
|-------------|---------------------------------------------------------------|
| Engine      | PostgreSQL (hosted on Supabase)                               |
| Pooler      | `aws-0-us-west-2.pooler.supabase.com`                         |
| Primary key | `uuid` on every table                                         |
| Soft delete | Not yet in schema (column `is_deleted` planned per AGENTS.md) |
| Timestamps  | `created_at timestamp without time zone DEFAULT now()`        |

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

| Column        | Type                          | Nullable | Default | Notes              |
|---------------|-------------------------------|----------|---------|--------------------|
| `id`          | `uuid`                        | NO       | —       | PRIMARY KEY        |
| `name`        | `text`                        | YES      | —       |                    |
| `email`       | `text`                        | YES      | —       | UNIQUE             |
| `password_hash` | `text`                      | YES      | —       | BCrypt hashed      |
| `created_at`  | `timestamp without time zone` | YES      | `now()` |                    |

### accounts

| Column       | Type                          | Nullable | Default | Notes                          |
|--------------|-------------------------------|----------|---------|--------------------------------|
| `id`         | `uuid`                        | NO       | —       | PRIMARY KEY                    |
| `user_id`    | `uuid`                        | YES      | —       | FK → `users(id)`               |
| `name`       | `text`                        | YES      | —       |                                |
| `type`       | `text`                        | YES      | —       | Cash, Credit Card, Bank Account|
| `balance`    | `numeric`                     | YES      | —       |                                |
| `created_at` | `timestamp without time zone` | YES      | `now()` |                                |

### categories

| Column    | Type           | Nullable | Default | Notes                            |
|-----------|----------------|----------|---------|----------------------------------|
| `id`      | `uuid`         | NO       | —       | PRIMARY KEY                      |
| `user_id` | `uuid`         | YES      | —       | FK → `users(id)`                 |
| `name`    | `text`         | YES      | —       |                                  |
| `type`    | `text`         | YES      | —       | income / expense                 |

### transactions

| Column        | Type           | Nullable | Default | Notes                    |
|---------------|----------------|----------|---------|--------------------------|
| `id`          | `uuid`         | NO       | —       | PRIMARY KEY              |
| `user_id`     | `uuid`         | YES      | —       | FK → `users(id)`         |
| `account_id`  | `uuid`         | YES      | —       | FK → `accounts(id)`      |
| `category_id` | `uuid`         | YES      | —       | FK → `categories(id)`    |
| `amount`      | `numeric`      | YES      | —       |                          |
| `type`        | `text`         | YES      | —       | income / expense         |
| `description` | `text`         | YES      | —       | Optional                 |
| `date`        | `date`         | YES      | —       | Transaction date         |

### budgets

| Column       | Type       | Nullable | Default | Notes                    |
|--------------|------------|----------|---------|--------------------------|
| `id`         | `uuid`     | NO       | —       | PRIMARY KEY              |
| `user_id`    | `uuid`     | YES      | —       | FK → `users(id)`         |
| `category_id`| `uuid`     | YES      | —       | FK → `categories(id)`    |
| `amount`     | `numeric`  | YES      | —       | Budget limit             |
| `month`      | `integer`  | YES      | —       | 1–12                     |
| `year`       | `integer`  | YES      | —       | e.g. 2026                |

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

All foreign keys use `uuid` and reference `users(id)` as the root owner.
Transactions are the central entity linking accounts and categories.
