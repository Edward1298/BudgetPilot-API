# BudgetPilot API — v2.1 Fixes & Improvements

**Status:** Draft | **Target:** Post-MVP improvements | **Owner:** Development

---

## Table of Contents

1. [Phase 0 — Database Schema Migration (v3)](#phase-0--database-schema-migration-v3)
2. [Phase 1 — Users: Authorization by Role](#phase-1--users-authorization-by-role)
3. [Phase 2 — Accounts: Interest Rate Validation + Currency](#phase-2--accounts-interest-rate-validation--currency)
4. [Phase 3 — Admin: Centralized Reactivation Endpoints](#phase-3--admin-centralized-reactivation-endpoints)
5. [Phase 4 — Cards: Fix POST](#phase-4--cards-fix-post)
6. [Phase 5 — Categories: Inactive Categories Endpoint](#phase-5--categories-inactive-categories-endpoint)
7. [Phase 6 — Transactions: Remove is_active, Admin-only Delete](#phase-6--transactions-remove-is_active-admin-only-delete)
8. [Phase 7 — Admin: New Query Endpoints](#phase-7--admin-new-query-endpoints)
9. [Implementation Order](#implementation-order)
10. [Files Summary](#files-summary)

---

## Phase 0 — Database Schema Migration (v3)

**Before any code is written**, the user must run `scripts/migration-v3.sql` in SSMS.

### 0.1 Table Modifications

| Table | Change |
|-------|--------|
| `accounts` | `ADD currency NVARCHAR(3) NOT NULL DEFAULT 'USD'` |
| `transactions` | `DROP COLUMN IF EXISTS is_active` |

### 0.2 Stored Procedure Updates

#### sp_GetAccountSummary

Remove `AND is_active = 1` from the transactions result set (the column no longer exists):

```sql
CREATE OR ALTER PROCEDURE dbo.sp_GetAccountSummary
    @AccountId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT id, user_id, name, type, balance, interest_rate, is_active, created_at
    FROM dbo.accounts
    WHERE id = @AccountId AND user_id = @UserId;

    SELECT TOP 10 id, user_id, account_id, category_id, amount, type, description, date
    FROM dbo.transactions
    WHERE account_id = @AccountId AND user_id = @UserId
    ORDER BY date DESC;
END
```

### 0.3 Full Migration Script

```sql
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
```

---

## Phase 1 — Users: Authorization by Role

### 1.1 Controller Changes (`Controllers/UsersController.cs`)

| Endpoint | Current Auth | New Auth | Notes |
|----------|-------------|----------|-------|
| `POST /api/v1/users/register` | Public | Public | No change |
| `POST /api/v1/users/login` | Public | Public | No change |
| `GET /api/v1/users` | `[Authorize]` | `[Authorize(Roles = "Admin")]` | Restrict to Admin only |
| `GET /api/v1/users/{id}` | `[Authorize]` | `[Authorize(Roles = "Admin")]` | Restrict to Admin only |
| `PUT /api/v1/users/{id}` | `[Authorize]` | `[Authorize]` | No change — User can edit self, Admin can edit any |
| `DELETE /api/v1/users/{id}` | `[Authorize]` | `[Authorize]` | No change — Admin=hard, User=soft |
| `GET /api/v1/users/me` | `[Authorize]` | `[Authorize]` | No change — both Admin and User |

### 1.2 Files to Modify

| File | Changes |
|------|---------|
| `Controllers/UsersController.cs` | Add `Roles = "Admin"` on `[HttpGet]` and `[HttpGet("{id:guid}")]` |

---

## Phase 2 — Accounts: Interest Rate Validation + Currency

### 2.1 Interest Rate Rule

If the account `type` is **not** `savingsAccount`, the `InterestRate` must be forced to `null` regardless of what the client sends. Only `savingsAccount` type may carry an interest rate.

**Affected methods:**
- `AccountsService.CreateAccount` — after type validation, if `dto.Type != "savingsAccount"` set `InterestRate = null`
- `AccountsService.UpdateAccount` — same rule when resolving the new type

### 2.2 Currency Field

Add a `Currency` field to the `accounts` entity/DTO/DB. Allowed values: `CRC`, `USD`, `EUR`. Default: `USD`.

#### Entity (`Entities/AccountsOBJ.cs`)

```csharp
/// <summary>
/// Gets or sets the currency of the account.
/// Valid values are CRC (Costa Rican Colón), USD (US Dollar), and EUR (Euro).
/// </summary>
[Column("currency")]
public string Currency { get; set; } = "USD";
```

#### DTO — Create (`Dtos/AccountsDTO.cs`)

```csharp
/// <summary>
/// Gets or sets the currency of the account.
/// Must be one of: CRC, USD, EUR. Defaults to USD.
/// </summary>
[RegularExpression("^(CRC|USD|EUR)$",
    ErrorMessage = "Currency must be one of: CRC, USD, EUR.")]
public string? Currency { get; set; }
```

#### DTO — Update (`Dtos/AccountUpdateDTO.cs`)

```csharp
/// <summary>
/// Gets or sets the updated currency of the account.
/// When provided, must be one of: CRC, USD, EUR.
/// </summary>
[RegularExpression("^(CRC|USD|EUR)$",
    ErrorMessage = "Currency must be one of: CRC, USD, EUR.")]
public string? Currency { get; set; }
```

#### Service (`Services/AccountsService.cs`)

In `CreateAccount`:
```csharp
account.Currency = dto.Currency ?? "USD";

if (dto.Type != "savingsAccount")
    account.InterestRate = null; // force null for non-savings types
```

In `UpdateAccount`:
```csharp
if (dto.Currency != null)
    account.Currency = dto.Currency;

// When resolving new type for validation
var newType = dto.Type ?? account.Type;
if (newType != "savingsAccount")
    account.InterestRate = null;
```

### 2.3 Files to Modify

| File | Changes |
|------|---------|
| `Entities/AccountsOBJ.cs` | Add `Currency` property with `[Column("currency")]` |
| `Dtos/AccountsDTO.cs` | Add `Currency` (string?, regex `^(CRC\|USD\|EUR)$`) |
| `Dtos/AccountUpdateDTO.cs` | Add `Currency` (string?, same regex) |
| `Services/AccountsService.cs` | Force InterestRate=null when type != savingsAccount; map Currency from DTO |
| `Dtos/AccountSummaryDTO.cs` | Add `Currency` to `AccountInfoDTO` |

---

## Phase 3 — Admin: Centralized Reactivation Endpoints

All reactivation endpoints live in `AdminController` (`[Authorize(Roles = "Admin")]`).

| Endpoint | Method | Description |
|----------|--------|-------------|
| `POST /api/v1/admin/accounts/{id}/reactivate` | Reactivate an account | If `is_active = false` → set `true`, return `200`. If already active → return `200` with message |
| `POST /api/v1/admin/cards/{id}/reactivate` | Reactivate a card | Same logic |
| `POST /api/v1/admin/categories/{id}/reactivate` | Reactivate a category | Same logic |
| `POST /api/v1/admin/users/{id}/reactivate` | Reactivate a user | Same logic |

### 3.1 Service Methods Needed

Each service needs a new method that:
1. Fetches the entity **without** the `IsActive` filter (so it can find inactive ones)
2. If entity not found → return null (controller returns 404)
3. If `entity.IsActive == true` → return a sentinel signaling "already active" (controller returns 200 with message)
4. If `entity.IsActive == false` → set to `true`, save, return success

**Signature pattern** (example for Accounts):
```csharp
// Returns: null if not found, true if reactivated, false if already active
public async Task<bool?> ReactivateAccount(Guid id)
```

Alternatively, return a result DTO or tuple:
```csharp
public async Task<(bool Found, bool AlreadyActive)> ReactivateAccount(Guid id)
```

### 3.2 Controller Implementation (`Controllers/AdminController.cs`)

```csharp
/// <summary>
/// Reactivates an account. If already active, returns a message.
/// </summary>
[HttpPost("accounts/{id:guid}/reactivate")]
public async Task<IActionResult> ReactivateAccount(Guid id)
{
    var (found, alreadyActive) = await _accountsService.ReactivateAccount(id);
    if (!found)
        return NotFoundError("Account not found.");
    if (alreadyActive)
        return Ok(new { statusCode = 200, message = "Account is already active.", errors = Array.Empty<object>() });
    return Ok(new { statusCode = 200, message = "Account reactivated successfully.", errors = Array.Empty<object>() });
}
```

### 3.3 Files to Modify

| File | Changes |
|------|---------|
| `Services/AccountsService.cs` | Add `ReactivateAccount(Guid id)` → `(bool Found, bool AlreadyActive)` |
| `Services/CardsService.cs` | Add `ReactivateCard(Guid id)` |
| `Services/CategoriesService.cs` | Add `ReactivateCategory(Guid id)` |
| `Services/UsersService.cs` | Add `ReactivateUser(Guid id)` |
| `Controllers/AdminController.cs` | Inject all 4 services; add 4 reactivation endpoints |

---

## Phase 4 — Cards: Fix POST

### 4.1 Diagnosis

Potential failure points in `POST /api/v1/cards`:

1. **Model validation failure** — `ModelState.IsValid` may return false due to:
   - `ExpirationDate` as `DateOnly` — JSON must use format `"2026-12-31"` (ISO 8601 date)
   - `CardNumber` length validation (13–20 chars)
   - `Cvc` length validation (3–4 chars)

2. **DataProtectionService failure** — constructor throws if `Encryption:Key` is missing or not 32 bytes. Check `appsettings.Development.json` for the config key.

3. **Account ownership** — `InvalidOperationException("Account not found or does not belong to the user.")` is caught as 400.

### 4.2 Fixes

| File | Change |
|------|--------|
| `Controllers/CardsController.cs` | No structural changes needed. Ensure `try-catch` for `CryptographicException` in addition to `InvalidOperationException` |
| `Services/CardsService.cs` | In `CreateCard`, wrap encryption calls in try-catch for cryptographic errors. Add better error messages |
| `appsettings.Development.json` | Verify `Encryption:Key` is present (32-byte base64 string) |

### 4.3 Verification

```powershell
dotnet build "BudgetPilot API.slnx"
```

Send test POST to `/api/v1/cards` with:
```json
{
  "accountId": "valid-guid",
  "type": "debit",
  "cardNumber": "4111111111111111",
  "expirationDate": "2028-12-31",
  "cvc": "123",
  "nameOnCard": "Test User"
}
```

---

## Phase 5 — Categories: Inactive Categories Endpoint

### 5.1 Endpoint

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| `GET` | `/api/v1/categories/inactive` | `[Authorize]` | Returns paginated categories where `is_active = 0` for the authenticated user |

### 5.2 Service Method

In `CategoriesService`:
```csharp
public async Task<(List<CategoriesOBJ> Items, int TotalCount)> GetInactiveCategories(
    Guid userId, int page = 1, int pageSize = 20)
{
    var query = _context.Categories
        .Where(c => c.UserId == userId && !c.IsActive);

    var totalCount = await query.CountAsync();

    var items = await query
        .OrderBy(c => c.Name)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();

    return (items, totalCount);
}
```

### 5.3 Controller Method

In `CategoriesController`:
```csharp
[HttpGet("inactive")]
public async Task<IActionResult> GetInactiveCategories(
    [FromQuery] int page = 1,
    [FromQuery] int pageSize = 20)
{
    if (page < 1)
        return BadRequest(new { statusCode = 400, message = "Page must be 1 or greater.", errors = Array.Empty<object>() });

    var userId = GetUserId();
    if (userId == null)
        return UnauthorizedError();

    var (items, totalCount) = await _categoriesService.GetInactiveCategories(userId.Value, page, pageSize);

    return Ok(new
    {
        data = items,
        page,
        pageSize,
        totalCount
    });
}
```

### 5.4 Files to Modify

| File | Changes |
|------|---------|
| `Services/CategoriesService.cs` | Add `GetInactiveCategories(userId, page, pageSize)` |
| `Controllers/CategoriesController.cs` | Add `[HttpGet("inactive")]` endpoint |

---

## Phase 6 — Transactions: Remove is_active, Admin-only Delete

### 6.1 Remove IsActive from Entity

**`Entities/TransactionsOBJ.cs`** — Delete the `IsActive` property and its `[Column]` attribute.

### 6.2 Remove IsActive from All Queries

**`Services/TransactionsService.cs`** — Remove `.Where(t => t.IsActive)` from:
- `GetTransactions`
- `GetTransactionById` (also remove `&& t.IsActive`)

### 6.3 Delete Logic — Admin Only

In `DeleteTransaction`:
- If `!isAdmin` → throw or return sentinel so controller returns 403
- Admin → hard delete with balance reversal (current logic kept)

New implementation:
```csharp
public async Task<bool> DeleteTransaction(Guid id, Guid userId, bool isAdmin)
{
    if (!isAdmin)
        throw new UnauthorizedAccessException("Only administrators can delete transactions.");

    var transaction = await _context.Transactions
        .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

    if (transaction == null)
        return false;

    var account = await _context.Accounts
        .FirstOrDefaultAsync(a => a.Id == transaction.AccountId && a.UserId == userId);

    if (account == null)
        return false;

    ApplyBalanceEffect(account, transaction.Amount, transaction.Type, reverse: true);
    _context.Transactions.Remove(transaction);
    await _context.SaveChangesAsync();

    return true;
}
```

### 6.4 Controller Mapping

In `TransactionsController.DeleteTransaction`:
```csharp
try
{
    var deleted = await _transactionsService.DeleteTransaction(id, userId.Value, isAdmin);
    // ...
}
catch (UnauthorizedAccessException)
{
    return ForbiddenError("Only administrators can delete transactions.");
}
```

### 6.5 Update TransactionInfoDTO in AccountSummaryDTO

Since `is_active` no longer exists, ensure `TransactionInfoDTO` in `Dtos/AccountSummaryDTO.cs` does not reference it (it doesn't — no change needed).

### 6.6 Files to Modify

| File | Changes |
|------|---------|
| `Entities/TransactionsOBJ.cs` | Remove `IsActive` property and column attribute |
| `Services/TransactionsService.cs` | Remove all `.IsActive` filters; rewrite `DeleteTransaction` to reject non-admin |
| `Controllers/TransactionsController.cs` | Map `UnauthorizedAccessException` → 403 in `DeleteTransaction` |

---

## Phase 7 — Admin: New Query Endpoints

All endpoints in `Controllers/AdminController.cs`, restricted to `[Authorize(Roles = "Admin")]`.

### 7.1 Admin Sees User's Accounts

**`GET /api/v1/admin/users/{userId}/accounts`**

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `userId` | Guid (path) | Yes | The user whose accounts to fetch |
| `isActive` | bool? (query) | No | Filter by active/inactive. If omitted, return all |

**Service method** (`AccountsService`):
```csharp
public async Task<List<AccountsOBJ>> GetAccountsByUserId(Guid userId, bool? isActive = null)
{
    var query = _context.Accounts.Where(a => a.UserId == userId).AsQueryable();

    if (isActive.HasValue)
        query = query.Where(a => a.IsActive == isActive.Value);

    return await query.OrderBy(a => a.Name).ToListAsync();
}
```

### 7.2 Admin Sees User's Cards

**`GET /api/v1/admin/users/{userId}/cards`**

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `userId` | Guid (path) | Yes | The user whose cards to fetch |
| `isActive` | bool? (query) | No | Filter by active/inactive |

**Service method** (`CardsService`):
```csharp
public async Task<List<CardsOBJ>> GetCardsByUserId(Guid userId, bool? isActive = null)
{
    var query = _context.Cards.Where(c => c.UserId == userId).AsQueryable();

    if (isActive.HasValue)
        query = query.Where(c => c.IsActive == isActive.Value);

    var items = await query.OrderByDescending(c => c.CreatedAt).ToListAsync();

    foreach (var card in items)
    {
        card.CardNumber = _dataProtection.Decrypt(card.CardNumber);
        card.Cvc = _dataProtection.Decrypt(card.Cvc);
    }

    return items;
}
```

### 7.3 Admin Sees User's Categories

**`GET /api/v1/admin/users/{userId}/categories`**

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `userId` | Guid (path) | Yes | The user whose categories to fetch |
| `isActive` | bool? (query) | No | Filter by active/inactive |

**Service method** (`CategoriesService`):
```csharp
public async Task<List<CategoriesOBJ>> GetCategoriesByUserId(Guid userId, bool? isActive = null)
{
    var query = _context.Categories.Where(c => c.UserId == userId).AsQueryable();

    if (isActive.HasValue)
        query = query.Where(c => c.IsActive == isActive.Value);

    return await query.OrderBy(c => c.Name).ToListAsync();
}
```

### 7.4 Admin Sees Transactions by Account

**`GET /api/v1/admin/accounts/{accountId}/transactions`**

| Param | Type | Required | Description |
|-------|------|----------|-------------|
| `accountId` | Guid (path) | **Yes** | The account whose transactions to fetch |
| `month` | int? (query) | No | Filter by month (1–12) |
| `type` | string? (query) | No | Filter by "income" or "expense" |
| `from` | DateOnly? (query) | No | Lower bound on date |
| `to` | DateOnly? (query) | No | Upper bound on date |

**Service method** (`TransactionsService`):
```csharp
public async Task<List<TransactionsOBJ>> GetTransactionsByAccountId(
    Guid accountId, int? month = null, string? type = null,
    DateOnly? from = null, DateOnly? to = null)
{
    var query = _context.Transactions
        .Where(t => t.AccountId == accountId).AsQueryable();

    if (month.HasValue)
        query = query.Where(t => t.Date.Month == month.Value);

    if (!string.IsNullOrWhiteSpace(type))
        query = query.Where(t => t.Type == type);

    if (from.HasValue)
        query = query.Where(t => t.Date >= from.Value);

    if (to.HasValue)
        query = query.Where(t => t.Date <= to.Value);

    return await query
        .OrderByDescending(t => t.Date)
        .ToListAsync();
}
```

### 7.5 Controller Wiring (`Controllers/AdminController.cs`)

Inject `AccountsService`, `CardsService`, `CategoriesService`, `TransactionsService`.

Add 4 endpoints with standard error helpers (`GetUserId`, `UnauthorizedError`, `NotFoundError`, `ValidationError`).

---

## Implementation Order

```
Phase 0  →  migration-v3.sql (run in SSMS)
Phase 1  →  UsersController auth fix
Phase 2  →  AccountsOBJ + DTOs + InterestRate logic + Currency
Phase 3  →  Reactivation methods in all 4 services + AdminController endpoints
Phase 4  →  Cards POST fix (debug + harden)
Phase 5  →  Categories inactive endpoint
Phase 6  →  Transactions: remove IsActive + admin-only delete
Phase 7  →  Admin query endpoints (4 new GETs)
Step 8   →  dotnet build + verify
```

---

## Files Summary

### Files to Create

| File | Reason |
|------|--------|
| `scripts/migration-v3.sql` | Add currency column, drop is_active from transactions, update SP |

### Files to Modify

| File | Phase | Changes |
|------|-------|---------|
| `Entities/AccountsOBJ.cs` | 2 | Add `Currency` property |
| `Entities/TransactionsOBJ.cs` | 6 | Remove `IsActive` property |
| `Dtos/AccountsDTO.cs` | 2 | Add `Currency` (string?, regex) |
| `Dtos/AccountUpdateDTO.cs` | 2 | Add `Currency` (string?, regex) |
| `Dtos/AccountSummaryDTO.cs` | 2 | Add `Currency` to `AccountInfoDTO` |
| `Services/AccountsService.cs` | 2, 3, 7 | Force InterestRate, map Currency; Add `ReactivateAccount`, `GetAccountsByUserId` |
| `Services/CardsService.cs` | 3, 4, 7 | Add `ReactivateCard`, `GetCardsByUserId`; harden POST encryption |
| `Services/CategoriesService.cs` | 3, 5, 7 | Add `GetInactiveCategories`, `ReactivateCategory`, `GetCategoriesByUserId` |
| `Services/TransactionsService.cs` | 6, 7 | Remove IsActive filters; admin-only delete; add `GetTransactionsByAccountId` |
| `Services/UsersService.cs` | 3 | Add `ReactivateUser` method (finds user without IsActive filter) |
| `Controllers/UsersController.cs` | 1 | Restrict GET / and GET /{id} to Admin role |
| `Controllers/CategoriesController.cs` | 5 | Add `[HttpGet("inactive")]` endpoint |
| `Controllers/TransactionsController.cs` | 6 | Map admin-only delete → 403 for non-admin |
| `Controllers/AdminController.cs` | 3, 7 | Inject 4 services; add 4 reactivation + 4 query endpoints |
