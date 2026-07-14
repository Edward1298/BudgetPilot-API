# BudgetPilot API — v2.0 Implementation Plan

**Status:** Draft | **Target:** Post-MVP refactor + new modules | **Owner:** Development

---

## Table of Contents

1. [Phase 0 — Database Schema Migration](#phase-0--database-schema-migration)
2. [Phase 1 — Foundation: Roles + Soft Delete](#phase-1--foundation-roles--soft-delete)
3. [Phase 2 — Partial Update Pattern](#phase-2--partial-update-pattern)
4. [Phase 3 — Account Types Refactor](#phase-3--account-types-refactor)
5. [Phase 4 — Transaction Improvements](#phase-4--transaction-improvements)
6. [Phase 5 — Page Validation Fix](#phase-5--page-validation-fix)
7. [Phase 6 — Cards Module](#phase-6--cards-module)
8. [Phase 7 — Stored Procedures](#phase-7--stored-procedures)
9. [Implementation Order](#implementation-order)
10. [Additional Recommendations (Accepted)](#additional-recommendations-accepted)

---

## Phase 0 — Database Schema Migration

**Before any code is written**, the user must run the migration script in SSMS. The script handles:

### 0.1 Existing Table Modifications

| Table | Changes |
|-------|---------|
| `users` | Add `role_id UNIQUEIDENTIFIER NOT NULL`, `is_active BIT NOT NULL DEFAULT 1` |
| `accounts` | Add `is_active BIT NOT NULL DEFAULT 1`, `interest_rate DECIMAL(5,2) NULL`; update type constraint to `bankAccount`, `savingsAccount`, `cash` only |
| `categories` | Add `is_active BIT NOT NULL DEFAULT 1` |
| `transactions` | Add `is_active BIT NOT NULL DEFAULT 1` |

### 0.2 New Tables

#### `roles`

| Column | Type | Constraints |
|--------|------|-------------|
| `id` | UNIQUEIDENTIFIER | PK, DEFAULT NEWID() |
| `name` | NVARCHAR(50) | NOT NULL, UNIQUE |

Seed data: `Admin`, `User`.

#### `cards`

| Column | Type | Constraints |
|--------|------|-------------|
| `id` | UNIQUEIDENTIFIER | PK, DEFAULT NEWID() |
| `user_id` | UNIQUEIDENTIFIER | NOT NULL, FK → users(id) |
| `account_id` | UNIQUEIDENTIFIER | NOT NULL, FK → accounts(id) |
| `type` | NVARCHAR(50) | NOT NULL — `debit` / `credit` |
| `card_number` | NVARCHAR(20) | NOT NULL (encrypted) |
| `expiration_date` | DATE | NOT NULL |
| `cvc` | NVARCHAR(10) | NOT NULL (encrypted) |
| `name_on_card` | NVARCHAR(100) | NOT NULL |
| `credit_limit` | DECIMAL(18,2) | NULL (credit only) |
| `apr` | DECIMAL(5,2) | NULL (credit only) |
| `statement_date` | INT | NULL (credit only, 1–31) |
| `due_date` | INT | NULL (credit only, 1–31) |
| `minimum_payment_percentage` | DECIMAL(5,2) | NULL (credit only) |
| `current_balance` | DECIMAL(18,2) | NOT NULL DEFAULT 0.00 |
| `is_active` | BIT | NOT NULL DEFAULT 1 |
| `created_at` | DATETIME2 | NOT NULL DEFAULT SYSUTCDATETIME() |

#### `budgets`

> The `budgets` table exists in the DB schema (`specs/dbschema.md`) but was never created in SSMS. A `CREATE TABLE` script is needed.

| Column | Type | Constraints |
|--------|------|-------------|
| `id` | UNIQUEIDENTIFIER | PK, DEFAULT NEWID() |
| `user_id` | UNIQUEIDENTIFIER | NOT NULL, FK → users(id) |
| `category_id` | UNIQUEIDENTIFIER | NOT NULL, FK → categories(id) |
| `amount` | DECIMAL(18,2) | NOT NULL |
| `month` | INT | NOT NULL (1–12) |
| `year` | INT | NOT NULL (e.g. 2026) |
| `is_active` | BIT | NOT NULL DEFAULT 1 |
| `created_at` | DATETIME2 | NOT NULL DEFAULT SYSUTCDATETIME() |

### 0.3 New Foreign Keys

```
users.role_id → roles.id
accounts.user_id → users.id (existing)
categories.user_id → users.id (existing)
transactions.user_id → users.id (existing)
cards.user_id → users.id
cards.account_id → accounts.id
budgets.user_id → users.id
budgets.category_id → categories.id
```

### 0.4 Stored Procedures (Phase 0)

- `sp_ApplyMonthlyInterest` — Applies monthly interest to all active Savings Accounts.
- `sp_GetAccountSummary` — Returns account balance + recent transactions in one round-trip.

---

## Phase 1 — Foundation: Roles + Soft Delete

### 1.1 Entities

| File | Action |
|------|--------|
| `Entities/RolesOBJ.cs` | **Create** — `[Table("roles")]`, props: `Id` (Guid), `Name` (string) |
| `Entities/UsersOBJ.cs` | **Modify** — Add `RoleId` (Guid), `IsActive` (bool) |
| `Entities/AccountsOBJ.cs` | **Modify** — Add `IsActive` (bool), `InterestRate` (decimal?) |
| `Entities/CategoriesOBJ.cs` | **Modify** — Add `IsActive` (bool) |
| `Entities/TransactionsOBJ.cs` | **Modify** — Add `IsActive` (bool) |

### 1.2 DTOs

| File | Action |
|------|--------|
| `Dtos/RegisterDTO.cs` | **Modify** — Add `RoleId` (Guid, Required) |
| `Dtos/RolesDTO.cs` | **Create** — `Name` (string, Required) |

### 1.3 Services

| File | Action |
|------|--------|
| `Services/RolesService.cs` | **Create** — CRUD scoped to admin only |
| `Services/UserService.cs` | **Modify** — Register assigns `RoleId`; Login adds `ClaimTypes.Role` to JWT; GetAll/GetById filter `IsActive == true` |
| `Services/AccountsService.cs` | **Modify** — GETs filter `IsActive == true`; DELETE performs soft delete unless admin |
| `Services/CategoriesService.cs` | **Modify** — Same pattern as Accounts |
| `Services/TransactionsService.cs` | **Modify** — Same pattern as Accounts |

**Soft delete logic:**
- If authenticated user's role is `Admin` → `_context.Remove(entity)` (hard delete)
- Otherwise → `entity.IsActive = false` (soft delete)
- All GET queries include `.Where(e => e.IsActive)` (Admin can opt to see inactive via query param)

### 1.4 Controllers

| File | Action |
|------|--------|
| `Controllers/RolesController.cs` | **Create** — `[Authorize(Roles = "Admin")]`, `/api/v1/roles` |
| `Controllers/UsersController.cs` | **Modify** — Add `PUT /{id}` (partial), `DELETE /{id}` (soft/hard based on role) |
| `Controllers/AccountsController.cs` | **Modify** — DELETE routes through soft/hard logic |
| `Controllers/CategoriesController.cs` | **Modify** — Same |
| `Controllers/TransactionsController.cs` | **Modify** — Same |

### 1.5 Authorization

- `Program.cs`: Register `[Authorize(Roles = "Admin")]` policies.
- Add `ClaimTypes.Role` to JWT claims in `UserService.Login`.
- Hard-delete endpoints decorated with `[Authorize(Roles = "Admin")]`.

---

## Phase 2 — Partial Update Pattern

### 2.1 Update DTOs (all nullable fields)

| DTO | Fields |
|-----|--------|
| `UserUpdateDTO` | `string? Name`, `string? Email` |
| `AccountUpdateDTO` | `string? Name`, `string? Type`, `decimal? Balance`, `decimal? InterestRate` |
| `CategoryUpdateDTO` | `string? Name`, `string? Type` |
| `TransactionUpdateDTO` | `Guid? AccountId`, `Guid? CategoryId`, `decimal? Amount`, `string? Description` |

### 2.2 Service Changes

Each service's update method:
1. Load existing entity.
2. Check ownership.
3. For each property on the DTO: if not null, update the entity.
4. Run validations for the final state.
5. `SaveChangesAsync`.

**Special case — Transactions.Update:**
- If `AccountId` or `CategoryId` changes, re-derive `type` from the new category and re-validate the balance effect.
- Reverse old effect, apply new effect.

### 2.3 Endpoint Decisions

- Current `PUT` endpoints are repurposed to accept the new update DTOs (breaking change for existing clients).
- The response returns the full updated object.

---

## Phase 3 — Account Types Refactor

### 3.1 Type Values

| Old Value | New Value | Notes |
|-----------|-----------|-------|
| `cash` | `cash` | Unchanged |
| `creditCard` | *(removed)* | Moved to Cards module |
| `bankAccount` | `bankAccount` | Now requires `balance > 0` |
| *(new)* | `savingsAccount` | New type, requires `interestRate` |

### 3.2 Validation Rules

| Type | Balance Rule | Interest Rate |
|------|-------------|---------------|
| `cash` | `>= 0` | N/A |
| `bankAccount` | **`> 0`** | N/A |
| `savingsAccount` | `>= 0` | Required (`> 0`) |

### 3.3 Changes

| File | Action |
|------|--------|
| `Dtos/AccountsDTO.cs` | Update regex to `^(bankAccount|savingsAccount|cash)$` |
| `Entities/AccountsOBJ.cs` | Add `InterestRate` (decimal?), XML docs |
| `Services/AccountsService.cs` | Update `ValidateBalanceForType` and add interest rate validation |
| `Spec update` | Update `specs/contracts/accounts-api.md` and `specs/dbschema.md` |

---

## Phase 4 — Transaction Improvements

### 4.1 Remove `type` from Transaction DTO

| File | Action |
|------|--------|
| `Dtos/TransactionsDTO.cs` | **Remove** the `Type` property. |
| `Dtos/TransactionUpdateDTO.cs` | Same — no `Type` field. |
| `Services/TransactionsService.cs` | In `CreateTransaction` and `UpdateTransaction`, load the `Category` first and derive `transaction.Type = category.Type`. If `category.Type` is neither `income` nor `expense`, throw validation error. |

### 4.2 Balance Preview in Response

The `POST /api/v1/transactions` and `PUT /api/v1/transactions/{id}` responses now include:

```json
{
  "id": "...",
  "accountId": "...",
  "categoryId": "...",
  "amount": 50.00,
  "type": "expense",
  "description": "Lunch",
  "date": "2026-07-01",
  "previousBalance": 500.00,
  "newBalance": 450.00
}
```

**Implementation:**
- `TransactionsService.CreateTransaction`: after loading the account, capture `account.Balance` as `previousBalance` before applying the effect. After applying, `newBalance = account.Balance`.
- Return a result object containing both the `TransactionsOBJ` and the two balance values.
- `TransactionsController` maps this into the response JSON.

### 4.3 NSF Check for Debit Cards

- `TransactionsService.CreateTransaction`: if `type == "expense"`, check if the account has an active Debit Card.
- If yes, verify `account.Balance >= dto.Amount`.
- If insufficient, throw `InvalidOperationException("Insufficient funds.")` → controller returns 400.

### 4.4 Credit Limit Check for Credit Cards

- `TransactionsService.CreateTransaction`: if `type == "expense"`, check if the account has an active Credit Card.
- If yes, verify `card.CurrentBalance + dto.Amount <= card.CreditLimit`.
- If exceeded, throw `InvalidOperationException("Credit limit exceeded.")` → controller returns 400.

---

## Phase 5 — Page Validation Fix

**Problem:** `Skip((page - 1) * pageSize)` throws on negative numbers when `page = 0`.

**Fix:** Add validation at controller or service level in all paginated GET endpoints:

- `AccountsController.GetAccounts`
- `CategoriesController.GetCategories`
- `TransactionsController.GetTransactions`
- `UsersController.GetUsers`

```csharp
if (page < 1)
    return BadRequest(new { statusCode = 400, message = "Page must be 1 or greater.", errors = Array.Empty<object>() });
```

Or handle it silently by clamping: `page = Math.Max(1, page)` — **recommendation**: return 400.

---

## Phase 6 — Cards Module

### 6.1 Entity

**`Entities/CardsOBJ.cs`**
- `[Table("cards")]`
- Properties: `Id`, `UserId` (`[JsonIgnore]`), `AccountId`, `Type` (`debit`/`credit`), `CardNumber`, `ExpirationDate` (`DateOnly`), `Cvc`, `NameOnCard`, `CreditLimit` (nullable), `Apr` (nullable), `StatementDate` (nullable, int), `DueDate` (nullable, int), `MinimumPaymentPercentage` (nullable), `CurrentBalance`, `IsActive`, `CreatedAt`

### 6.2 DTOs

| DTO | Fields |
|-----|--------|
| `CardsDTO` | `AccountId` (Guid), `Type` (string), `CardNumber` (string), `ExpirationDate` (DateOnly), `Cvc` (string), `NameOnCard` (string), `CreditLimit` (decimal?), `Apr` (decimal?), `StatementDate` (int?), `DueDate` (int?), `MinimumPaymentPercentage` (decimal?) |
| `CardUpdateDTO` | All fields nullable |

### 6.3 Service

**`Services/CardsService.cs`**

Methods:
- `GetCards(userId, page, pageSize, type?)` — paginated, filtered by type, `IsActive == true` by default.
- `GetCardById(id)` — no ownership filter (controller handles 403/404).
- `CreateCard(dto, userId)` — validate `AccountId` belongs to user; set `NameOnCard` from user's name if not provided.
- `UpdateCard(id, dto, userId)` — partial update.
- `DeleteCard(id, userId)` — soft/hard based on role.

### 6.4 Controller

**`Controllers/CardsController.cs`**
- `[Authorize]`, `[Route("api/v1/[controller]")]`
- Endpoints: `GET /`, `GET /{id}`, `POST /`, `PUT /{id}`, `DELETE /{id}`
- Error helpers: same pattern as `AccountsController`

### 6.5 Card Number Encryption

> See [Recommendation 2](#recommendation-2-card-number-and-cvc-encryption).

- A `CardEncryptionService` is registered in DI.
- On create: encrypt `CardNumber` and `Cvc` before saving.
- On read: decrypt both fields before returning.
- Encryption key stored in `appsettings.json` under `"Encryption": { "Key": "..." }` (32 bytes, base64).

---

## Phase 7 — Stored Procedures

### 7.1 `sp_ApplyMonthlyInterest`

**Purpose:** Run once per month (SQL Agent job or manual trigger) to apply interest to all active Savings Accounts.

```sql
CREATE PROCEDURE sp_ApplyMonthlyInterest
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
```

### 7.2 `sp_GetAccountSummary`

**Purpose:** Return account details plus last 10 transactions in one round-trip.

```sql
CREATE PROCEDURE sp_GetAccountSummary
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
```

---

## Implementation Order

```
Phase 0  →  SQL migration script (run in SSMS)
Phase 1  →  Roles + Soft Delete + Authorization
Phase 2  →  Partial Update Pattern (DTOs + service logic)
Phase 3  →  Account Types Refactor
Phase 4  →  Transaction Improvements (type derivation, balance preview, NSF)
Phase 5  →  Page Validation Fix
Phase 6  →  Cards Module
Phase 7  →  Stored Procedures (finalize any remaining SPs)
Phase 8  →  Budgets (uncomment DbSet, wire up entity)
```

**Phase 8 (Budgets) note:** Although budgets are technically "future scope", the table will be created in Phase 0. The entity `BudgetsOBJ` already exists in the codebase (commented out in `AppDbContext`). Phase 8 simply uncomments the DbSet and wires the entity — no endpoints are implemented unless explicitly requested.

---

## Additional Recommendations (Accepted)

### Recommendation 1: Seed Data

The `scripts/migration-v2.sql` must include seed inserts:

```sql
INSERT INTO dbo.roles (id, name) VALUES (NEWID(), 'Admin');
INSERT INTO dbo.roles (id, name) VALUES (NEWID(), 'User');
```

Optionally, seed an admin user with known credentials (configurable).

### Recommendation 2: Card Number and CVC Encryption

- Create a helper class `Services/DataProtectionService` that uses `System.Security.Cryptography.Aes` with a key from configuration.
- Intercept in `CardsService.CreateCard`: encrypt `CardNumber` and `Cvc`.
- Intercept in `CardsService.GetCardById` and list responses: decrypt both fields.
- Never log or expose raw encrypted values.
- Key location: `appsettings.json` → `"Encryption": { "Key": "base64-32-byte-key" }`.

### Recommendation 3: Index on `is_active`

Add to migration script:

```sql
CREATE INDEX IX_users_is_active ON dbo.users(is_active);
CREATE INDEX IX_accounts_is_active ON dbo.accounts(is_active);
CREATE INDEX IX_categories_is_active ON dbo.categories(is_active);
CREATE INDEX IX_transactions_is_active ON dbo.transactions(is_active);
CREATE INDEX IX_cards_is_active ON dbo.cards(is_active);
```

### Recommendation 4: Budgets

**Already addressed** — see [Phase 8](#implementation-order).

### Recommendation 5: JWT Contains Role

In `UserService.Login`, add to claims:

```csharp
new Claim(ClaimTypes.Role, roleName)
```

Where `roleName` is fetched from the `roles` table via the user's `RoleId`.

### Recommendation 6: Default APR for Credit Cards

Add to `appsettings.json`:

```json
"CardDefaults": {
    "DefaultApr": 24.99,
    "DefaultMinimumPaymentPercentage": 5.00
}
```

When creating a Credit Card, if `Apr` is not provided, fall back to `DefaultApr`. Same for `MinimumPaymentPercentage`.

### Recommendation 7: NSF / Credit Limit Logic in Transaction Service

The NSF check (debit) and credit limit check (credit) live inside `TransactionsService.CreateTransaction`, not in `CardsService`. The transaction service loads the card(s) linked to the account and validates. This keeps the transaction creation atomic and avoids circular service dependencies.

---

## Future Considerations (Not in Scope for v2.0)

- JWT refresh tokens
- Multi-currency support
- Bank sync integrations
- OAuth login
- Push notifications
- Expense reports / dashboards
