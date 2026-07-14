# BudgetPilot API — Future Scope Implementation

**Status:** Draft | **Target:** Complete remaining PRODUCT.md Future Scope | **Owner:** Development

---

## Table of Contents

1. [Phase 0 — Database Schema Migration (v4)](#phase-0--database-schema-migration-v4)
2. [Phase 1 — Budgets Module](#phase-1--budgets-module)
3. [Phase 2 — Expense/Income Reports](#phase-2--expenseincome-reports)
4. [Phase 3 — JWT Refresh Token Support](#phase-3--jwt-refresh-token-support)
5. [Implementation Order](#implementation-order)
6. [Files Summary](#files-summary)

---

## Phase 0 — Database Schema Migration (v4)

**Before any code is written**, run `scripts/migration-v4.sql` in SSMS.

### New Table: `refresh_tokens`

```sql
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

CREATE INDEX IX_refresh_tokens_user_id ON dbo.refresh_tokens(user_id);
GO

CREATE INDEX IX_refresh_tokens_token ON dbo.refresh_tokens(token);
GO

-- STEP 2: Add RefreshTokenExpirationDays to Jwt config note
PRINT 'Remember to add "RefreshTokenExpirationDays": 7 to the Jwt section in appsettings.';
GO

PRINT 'Migration v4.0 completed successfully.';
GO
```

---

## Phase 1 — Budgets Module

**Module:** Budgets | **Base Path:** `/api/v1/budgets` | **Auth:** Bearer JWT (all endpoints)

### 1.1 Entity

`Entities/BudgetsOBJ.cs` already exists with:
- `Id` (Guid), `UserId` (Guid, `[JsonIgnore]`), `CategoryId` (Guid), `Amount` (decimal), `Month` (int, 1–12), `Year` (int), `IsActive` (bool), `CreatedAt` (DateTime)

No changes needed.

### 1.2 DTO — Create (`Dtos/BudgetsDTO.cs`)

```csharp
public class BudgetsDTO
{
    [Required]
    public Guid CategoryId { get; set; }

    [Required]
    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
    public decimal Amount { get; set; }

    [Required]
    [Range(1, 12, ErrorMessage = "Month must be between 1 and 12.")]
    public int Month { get; set; }

    [Required]
    [Range(2000, 2100, ErrorMessage = "Year must be a valid year.")]
    public int Year { get; set; }
}
```

### 1.3 DTO — Update (`Dtos/BudgetUpdateDTO.cs`)

```csharp
public class BudgetUpdateDTO
{
    public Guid? CategoryId { get; set; }

    [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
    public decimal? Amount { get; set; }

    [Range(1, 12, ErrorMessage = "Month must be between 1 and 12.")]
    public int? Month { get; set; }

    [Range(2000, 2100, ErrorMessage = "Year must be a valid year.")]
    public int? Year { get; set; }
}
```

### 1.4 Service (`Services/BudgetsService.cs`)

Methods:

| Method | Description |
|--------|-------------|
| `GetBudgets(userId, page, pageSize, month?, year?, categoryId?)` | Paginated list, filtered. Only `IsActive == true` by default (except admin query param) |
| `GetBudgetById(id)` | Single budget, no ownership filter (controller handles 403/404) |
| `CreateBudget(dto, userId)` | Validate `CategoryId` belongs to user; uniqueness check on `(userId, categoryId, month, year)` → 409 on conflict |
| `UpdateBudget(id, dto, userId)` | Partial update; re-check uniqueness if category/month/year changed |
| `DeleteBudget(id, userId, isAdmin)` | Admin = hard delete, User = soft delete. Check linked transactions? Not needed — budgets don't have FKs from transactions |

**Uniqueness rule:** One budget per `(userId, categoryId, month, year)`. Enforced in service with `AnyAsync` check.

### 1.5 Controller (`Controllers/BudgetsController.cs`)

Follows the exact same pattern as `AccountsController` / `CategoriesController`:

```csharp
[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class BudgetsController : ControllerBase
{
    private readonly BudgetsService _budgetsService;

    // Constructor, GetUserId(), IsAdmin(), error helpers...

    [HttpGet]
    public async Task<IActionResult> GetBudgets(...) { }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetBudgetById(Guid id) { }

    [HttpPost]
    public async Task<IActionResult> CreateBudget([FromBody] BudgetsDTO dto) { }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateBudget(Guid id, [FromBody] BudgetUpdateDTO dto) { }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteBudget(Guid id) { }
}
```

### 1.6 Transaction Budget Check

**`Services/TransactionsService.cs`** — in `CreateTransaction`:

When the transaction type is `expense`:
1. Load the active budget for `(userId, categoryId, DateTime.UtcNow.Year, DateTime.UtcNow.Month)`
2. If a budget exists, sum all existing active transactions for that category/month/year + the new amount
3. If the sum exceeds `budget.Amount`, throw `InvalidOperationException("Budget limit exceeded for this category.")`

```csharp
// In CreateTransaction, after determining category.Type == "expense"
var budget = await _context.Budgets
    .FirstOrDefaultAsync(b => b.UserId == userId
        && b.CategoryId == dto.CategoryId
        && b.Year == DateTime.UtcNow.Year
        && b.Month == DateTime.UtcNow.Month
        && b.IsActive);

if (budget != null)
{
    var spentThisMonth = await _context.Transactions
        .Where(t => t.UserId == userId
            && t.CategoryId == dto.CategoryId
            && t.Type == "expense"
            && t.Date.Year == DateTime.UtcNow.Year
            && t.Date.Month == DateTime.UtcNow.Month)
        .SumAsync(t => t.Amount);

    if (spentThisMonth + dto.Amount > budget.Amount)
        throw new InvalidOperationException("Budget limit exceeded for this category.");
}
```

### 1.7 Files

**Create:**

| File | Purpose |
|------|---------|
| `Dtos/BudgetsDTO.cs` | Create DTO |
| `Dtos/BudgetUpdateDTO.cs` | Update DTO |
| `Services/BudgetsService.cs` | Business logic |
| `Controllers/BudgetsController.cs` | HTTP endpoints |

**Modify:**

| File | Change |
|------|--------|
| `Program.cs` | Register `builder.Services.AddScoped<BudgetsService>()` |
| `Services/TransactionsService.cs` | Add budget limit check on expense creation |

---

## Phase 2 — Expense/Income Reports

**Module:** Reports | **Base Path:** `/api/v1/reports` | **Auth:** Bearer JWT

### 2.1 Endpoints

| Method | Path | Description |
|--------|------|-------------|
| `GET` | `/api/v1/reports/summary` | Aggregated totals grouped by category for a date range |

### 2.2 Query Parameters

| Param | Type | Required | Default | Description |
|-------|------|----------|---------|-------------|
| `from` | DateOnly | Yes | — | Start date (inclusive) |
| `to` | DateOnly | Yes | — | End date (inclusive) |
| `type` | string | No | `"expense"` | Filter by `income` or `expense` |

### 2.3 Response

```json
{
  "from": "2026-01-01",
  "to": "2026-12-31",
  "type": "expense",
  "totalAmount": 5000.00,
  "totalTransactions": 42,
  "categories": [
    {
      "categoryId": "guid",
      "categoryName": "Food",
      "totalAmount": 1500.00,
      "percentage": 30.0,
      "transactionCount": 25
    },
    {
      "categoryId": "guid",
      "categoryName": "Transport",
      "totalAmount": 500.00,
      "percentage": 10.0,
      "transactionCount": 10
    }
  ]
}
```

### 2.4 DTO (`Dtos/ReportsDTOs.cs`)

```csharp
public class ReportRequestDTO
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public string? Type { get; set; } // income or expense, defaults to expense
}

public class CategoryReportDTO
{
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal Percentage { get; set; }
    public int TransactionCount { get; set; }
}

public class ReportResponseDTO
{
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public string Type { get; set; }
    public decimal TotalAmount { get; set; }
    public int TotalTransactions { get; set; }
    public List<CategoryReportDTO> Categories { get; set; }
}
```

### 2.5 Service (`Services/ReportsService.cs`)

```csharp
public async Task<ReportResponseDTO> GetSummary(Guid userId, DateOnly from, DateOnly to, string? type)
{
    var effectiveType = string.IsNullOrWhiteSpace(type) ? "expense" : type;

    var transactions = await _context.Transactions
        .Where(t => t.UserId == userId
            && t.Type == effectiveType
            && t.Date >= from
            && t.Date <= to)
        .Join(_context.Categories,
            t => t.CategoryId,
            c => c.Id,
            (t, c) => new { t.Amount, t.CategoryId, CategoryName = c.Name })
        .GroupBy(x => new { x.CategoryId, x.CategoryName })
        .Select(g => new
        {
            CategoryId = g.Key.CategoryId,
            CategoryName = g.Key.CategoryName,
            TotalAmount = g.Sum(x => x.Amount),
            TransactionCount = g.Count()
        })
        .OrderByDescending(x => x.TotalAmount)
        .ToListAsync();

    var totalAmount = transactions.Sum(x => x.TotalAmount);
    var totalCount = transactions.Sum(x => x.TransactionCount);

    return new ReportResponseDTO
    {
        From = from,
        To = to,
        Type = effectiveType,
        TotalAmount = totalAmount,
        TotalTransactions = totalCount,
        Categories = transactions.Select(x => new CategoryReportDTO
        {
            CategoryId = x.CategoryId,
            CategoryName = x.CategoryName,
            TotalAmount = x.TotalAmount,
            Percentage = totalAmount > 0 ? Math.Round(x.TotalAmount / totalAmount * 100, 1) : 0,
            TransactionCount = x.TransactionCount
        }).ToList()
    };
}
```

### 2.6 Controller (`Controllers/ReportsController.cs`)

```csharp
[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly ReportsService _reportsService;

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] string? type = null)
    {
        if (from > to)
            return BadRequest(new { statusCode = 400, message = "'from' must be before or equal to 'to'.", errors = Array.Empty<object>() });

        var userId = GetUserId();
        if (userId == null)
            return UnauthorizedError();

        var report = await _reportsService.GetSummary(userId.Value, from, to, type);
        return Ok(report);
    }

    // GetUserId(), UnauthorizedError() helpers...
}
```

### 2.7 Files

**Create:**

| File | Purpose |
|------|---------|
| `Dtos/ReportsDTOs.cs` | Report DTOs |
| `Services/ReportsService.cs` | Aggregation logic |
| `Controllers/ReportsController.cs` | HTTP endpoint |

**Modify:**

| File | Change |
|------|--------|
| `Program.cs` | Register `builder.Services.AddScoped<ReportsService>()` |

---

## Phase 3 — JWT Refresh Token Support

### 3.1 Entity (`Entities/RefreshTokensOBJ.cs`)

```csharp
[Table("refresh_tokens")]
public class RefreshTokensOBJ
{
    [Column("id")]
    public Guid Id { get; set; }

    [Column("user_id")]
    public Guid UserId { get; set; }

    [Column("token")]
    public string Token { get; set; } = string.Empty;

    [Column("expires_at")]
    public DateTime ExpiresAt { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("revoked_at")]
    public DateTime? RevokedAt { get; set; }
}
```

### 3.2 AppSettings Changes

Add to `Jwt` section in `appsettings.Development.json`:

```json
"Jwt": {
    "Key": "...",
    "Issuer": "BudgetPilotAPI",
    "Audience": "BudgetPilotClients",
    "ExpirationMinutes": 1440,
    "RefreshTokenExpirationDays": 7
}
```

### 3.3 DTOs

**`Dtos/RefreshTokenDTO.cs`:**

```csharp
public class RefreshTokenRequestDTO
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}

public class RefreshTokenResponseDTO
{
    public string Token { get; set; } = string.Empty;
    public string TokenType { get; set; } = "Bearer";
    public DateTime ExpiresAt { get; set; }
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime RefreshExpiresAt { get; set; }
}

public class LogoutDTO
{
    [Required]
    public string RefreshToken { get; set; } = string.Empty;
}
```

### 3.4 Service Changes (`Services/UsersService.cs`)

**New private method — GenerateRefreshToken():**
```csharp
private string GenerateRefreshToken()
{
    var randomBytes = new byte[64];
    using var rng = RandomNumberGenerator.Create();
    rng.GetBytes(randomBytes);
    return Convert.ToBase64String(randomBytes);
}
```

**Modified `Login()`:**
After generating the JWT access token, also:
1. Generate a cryptographically random refresh token
2. Persist it to `refresh_tokens` table
3. Return both tokens in the response

```csharp
var refreshToken = GenerateRefreshToken();
var refreshExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpirationDays); // add this prop to JwtOptions

_context.RefreshTokens.Add(new RefreshTokensOBJ
{
    Id = Guid.NewGuid(),
    UserId = user.Id,
    Token = BCrypt.Net.BCrypt.HashPassword(refreshToken), // hash it for storage
    ExpiresAt = refreshExpiresAt,
    CreatedAt = DateTime.UtcNow
});
await _context.SaveChangesAsync();

return (
    Token: token,
    TokenType: "Bearer",
    ExpiresAt: expiresAt,
    RefreshToken: refreshToken,
    RefreshExpiresAt: refreshExpiresAt
);
```

> **Note:** The refresh token is stored as a BCrypt hash (same as passwords). The plain value is returned to the client once and never stored. This way, if the DB is compromised, refresh tokens can't be stolen in plain text.

**New method — `RefreshToken(refreshToken)`:**

1. Load all non-revoked, non-expired refresh tokens for the user (iterate or find by user)
2. For each, verify with `BCrypt.Net.BCrypt.Verify(refreshToken, stored.Token)`
3. If match found:
   - Revoke the old token: `stored.RevokedAt = DateTime.UtcNow`
   - Generate a new JWT (re-use existing `GenerateJwtToken`)
   - Generate and persist a new refresh token
   - SaveChanges
   - Return new token pair
4. If no match → return null (controller returns 401)

**New method — `RevokeRefreshToken(refreshToken)`:**

1. Same lookup logic
2. Set `RevokedAt = DateTime.UtcNow` on the found token
3. SaveChanges

**Update `JwtOptions`:** Add `RefreshTokenExpirationDays` property.

### 3.5 Controller Endpoints (`Controllers/UsersController.cs`)

**`POST /api/v1/users/login`** — update response to include refresh token:
```json
{
  "token": "eyJ...",
  "tokenType": "Bearer",
  "expiresAt": "2026-06-12T10:00:00Z",
  "refreshToken": "a1b2c3...base64...",
  "refreshExpiresAt": "2026-07-17T10:00:00Z"
}
```

**`POST /api/v1/users/refresh`** — public endpoint:

```csharp
[HttpPost("refresh")]
public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDTO dto)
{
    if (!ModelState.IsValid)
        return ValidationError();

    var result = await _userService.RefreshToken(dto.RefreshToken);

    if (result == null)
        return Unauthorized(new
        {
            statusCode = 401,
            message = "Invalid or expired refresh token.",
            errors = Array.Empty<object>()
        });

    return Ok(result);
}
```

**`POST /api/v1/users/logout`** — JWT-protected:

```csharp
[HttpPost("logout")]
[Authorize]
public async Task<IActionResult> Logout([FromBody] LogoutDTO dto)
{
    if (!ModelState.IsValid)
        return ValidationError();

    await _userService.RevokeRefreshToken(dto.RefreshToken);
    return Ok(new { statusCode = 200, message = "Logged out successfully.", errors = Array.Empty<object>() });
}
```

### 3.6 DbContext

Add to `AppDbContext`:
```csharp
public DbSet<RefreshTokensOBJ> RefreshTokens { get; set; }
```

### 3.7 Files

**Create:**

| File | Purpose |
|------|---------|
| `Entities/RefreshTokensOBJ.cs` | Entity for refresh_tokens table |
| `Dtos/RefreshTokenDTO.cs` | Request/Response DTOs |

**Modify:**

| File | Change |
|------|--------|
| `Data/AppDbContext.cs` | Add `DbSet<RefreshTokensOBJ> RefreshTokens` |
| `Services/UsersService.cs` | Add refresh token generation in Login; add `RefreshToken()` and `RevokeRefreshToken()` methods; update return type |
| `Controllers/UsersController.cs` | Update Login response; add `/refresh` and `/logout` endpoints |
| `JwtOptions.cs` | Add `RefreshTokenExpirationDays` property |

---

## Implementation Order

```
Phase 0  →  migration-v4.sql (run in SSMS)
Phase 1  →  Budgets: DTOs → Service → Controller → DI → Transaction check
Phase 2  →  Reports: DTOs → Service → Controller → DI
Phase 3  →  Refresh Tokens: Entity → DbContext → Service logic → Controller endpoints
Step 4   →  dotnet build + verify
```

---

## Files Summary

### Files to Create (7)

| File | Phase |
|------|-------|
| `scripts/migration-v4.sql` | 0 |
| `Dtos/BudgetsDTO.cs` | 1 |
| `Dtos/BudgetUpdateDTO.cs` | 1 |
| `Services/BudgetsService.cs` | 1 |
| `Controllers/BudgetsController.cs` | 1 |
| `Dtos/ReportsDTOs.cs` | 2 |
| `Services/ReportsService.cs` | 2 |
| `Controllers/ReportsController.cs` | 2 |
| `Entities/RefreshTokensOBJ.cs` | 3 |
| `Dtos/RefreshTokenDTO.cs` | 3 |

### Files to Modify (7)

| File | Phase | Changes |
|------|-------|---------|
| `Data/AppDbContext.cs` | 3 | Add `DbSet<RefreshTokensOBJ> RefreshTokens` |
| `Services/TransactionsService.cs` | 1 | Add budget limit check on expense creation |
| `Services/UsersService.cs` | 3 | Add refresh token generation, `RefreshToken()`, `RevokeRefreshToken()` |
| `Controllers/UsersController.cs` | 3 | Update login response, add `/refresh` + `/logout` |
| `JwtOptions.cs` | 3 | Add `RefreshTokenExpirationDays` |
| `Program.cs` | 1, 2 | Register `BudgetsService`, `ReportsService` |
