# AGENTS.md — BudgetPilot API

## Essential Commands

```powershell
dotnet build "BudgetPilot API.slnx"
dotnet run --project "BudgetPilot API" --launch-profile http
dotnet test "BudgetPilot API.slnx"
dotnet test "BudgetPilot API.Tests" --filter "FullyQualifiedName~UsersApiTests"
```

No linter or formatter is configured.

## Spec-Driven Development Order

Read before writing ANY code: `specs/PRODUCT.md` → `specs/ARCHITECTURE.md` → `specs/contracts/<module>-api.md` → `specs/dbschema.md` → `specs/features/` (empty).

## Architecture

| Fact | Why it matters |
|---|---|
| **No repository pattern** — services inject `AppDbContext` directly | Don't create `IRepository<T>` abstractions |
| **No EF Core migrations** — schema via raw SQL scripts (`scripts/`) | Don't run `dotnet ef migrations add`. For new tables, return the `CREATE TABLE` script to the user first |
| **Entity suffix `OBJ`**, **DTO suffix `DTO`** | Files in `Entities/` and `Dtos/` respectively |
| **Request flow**: Controller → Service → DbContext | Don't inject services into other services, don't call DbContext from controllers |
| **Controllers use `/api/v1/[controller]`** | Already set, don't change |
| **HTTPS redirection disabled** | Leave `app.UseHttpsRedirection()` commented out |

## Modules

| Module | Status | Notes |
|---|---|---|
| Users, Accounts, Categories, Transactions | ✅ Full CRUD | MVP modules |
| Roles | ✅ Full CRUD | Admin-only controller |
| Cards | ✅ Full CRUD | CardNumber/Cvc encrypted via `DataProtectionService` (AES-256-CBC). `NameOnCard`, `Apr`, `MinimumPaymentPercentage` fall back to user's name / `CardDefaults` config when not provided |
| Stored Procedures | ✅ `StoredProcedureService` | ADO.NET calls to `sp_GetAccountSummary` + `sp_ApplyMonthlyInterest`. Summary endpoint at `GET /accounts/{id}/summary`. Interest endpoint at `POST /admin/apply-monthly-interest` (admin only) |
| Budgets | ⏳ Entity + DbSet only | `BudgetsOBJ` exists, `AppDbContext` has active `DbSet`. No endpoints, DTOs, or service. Not registered in DI |

## Database Naming

C# PascalCase → JSON camelCase → SQL Server snake_case. Use `[Table]` and `[Column]` attributes.

## Testing

- **xUnit + FluentAssertions**. Integration tests use `WebApplicationFactory<Program>` with SQLite in-memory (`TestWebAppFactory`).
- `TestUserFixture.RegisterAndLoginAsync(client)` registers a test user (name: "Test User", role: User) and returns a JWT.
- `TestWebAppFactory` exposes static `AdminRoleId` and `UserRoleId` GUIDs for test assertions.
- Stored procedures use SQL Server ADO.NET (`SqlConnection`) — **cannot run in integration tests** (SQLite). Auth/ownership SP test coverage only.
- No real SQL Server needed for tests.

## Controller Pattern

Each controller follows the same pattern:
- `[ApiController]`, `[Authorize]`, `[Route("api/v1/[controller]")]`
- Private helpers: `GetUserId()` (reads `ClaimTypes.NameIdentifier`), `IsAdmin()` (reads `ClaimTypes.Role`)
- Error helpers: `UnauthorizedError()` (401), `ForbiddenError(message)` (403), `NotFoundError(message)` (404), `ConflictError(message)` (409), `ValidationError()` (400)
- `page < 1` validation returns 400 immediately
- Ownership checks: service returns entity without userId filter; controller compares `entity.UserId != userId` to distinguish 403 vs 404

## Response Envelope

```json
{ "statusCode": 400, "message": "...", "errors": [{ "field": "name", "message": "..." }] }
```

401/403/404/409 use `"errors": []`. 400 validation errors use field-level entries with camelCase field names.

## Key Config Sections (appsettings.json)

- `Encryption:Key` — 32-byte base64 AES key for card encryption
- `CardDefaults:DefaultApr` (24.99) and `CardDefaults:DefaultMinimumPaymentPercentage` (5.00) — fallbacks for credit card creation

## Transaction Behavior

- `Type` is **derived from the category**, not from the DTO. Categories must be `income` or `expense`.
- Balance effect: income adds to account balance, expense subtracts.
- Create/Update response includes `previousBalance` and `newBalance`.
- Expense transactions check for an active Debit Card (`account.Balance >= amount`) or Credit Card (`card.CurrentBalance + amount <= card.CreditLimit`). Returns 400 on violation.
- Admin hard-delete reverses balance effect; regular user soft-delete does not.

## Scope Boundaries

- **Future** (do not implement without request): budgets, statements, reports, JWT refresh tokens
- **Never implement**: bank sync, multi-currency, investments, OAuth, push notifications
