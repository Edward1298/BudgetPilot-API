# AGENTS.md — BudgetPilot API

## Project at a Glance

- **.NET 10.0** ASP.NET Core Web API, single project
- **Database**: PostgreSQL on Supabase via Npgsql EF Core
- **Auth**: BCrypt password hashing, JWT planned but not yet implemented
- **Docs**: Swagger at `/swagger` (dev only)

## Essential Commands

```powershell
# Build (run from the inner project directory)
dotnet build "BudgetPilot API/BudgetPilot API.csproj"

# Run (http profile, launches Swagger in browser)
dotnet run --project "BudgetPilot API" --launch-profile http
```

No tests, no linter, no formatter configured.

## Spec-Driven Development (SDD) Order

Before writing ANY code, read in this exact order:
1. `specs/PRODUCT.md` — scope, entities, what's in/out
2. `specs/ARCHITECTURE.md` — stack versions, patterns
3. `specs/contracts/<module>-api.md` — endpoint contract for the module
4. `specs/features/` — active user stories (currently empty)

Full rules live in `opencode/rules.md`. This file only covers things an agent would miss.

## Architecture — What Rules Don't Make Obvious

| Fact | Why it matters |
|---|---|
| **NO repository pattern** — services inject `AppDbContext` directly | Don't create `IRepository<T>` or similar abstractions |
| **No EF Core migrations** — DB tables are created via raw SQL scripts | Don't run `dotnet ef migrations add`. Use the SQL scripts from `specs/contracts/` |
| **Entity suffix is `OBJ`, not `Entity`** | Files go in `Entities/`, class names end in `OBJ` (e.g. `AccountsOBJ.cs`) |
| **DTO suffix is `DTO`** | Files go in `Dtos/`, class names end in `DTO` (e.g. `AccountsDTO.cs`) |
| **Request flow**: Controller → Service → DbContext | Don't inject services into other services, don't call DbContext from controllers |
| **Users module is the only implemented module** | Accounts, Categories, Transactions, Budgets are commented out in `AppDbContext` |
| **Route prefix: rules say `/api/v1/[controller]`, code currently has `api/[controller]`** | When adding new controllers, use `[Route("api/v1/[controller]")]` per the spec |
| **IPv6 disabled** — `AppContext.SetSwitch("System.Net.Sockets.Socket.OSSupportsIPv6", false)` in `Program.cs` | Don't remove it; required for Supabase IPv4 pooler |
| **HTTPS redirection disabled** — commented out in Program.cs | Leave it commented out |

## Database Naming Mismatch

C# (PascalCase) ↔ JSON (camelCase) ↔ PostgreSQL (snake_case):

| C# Property | JSON field | DB Column |
|---|---|---|
| `Id` | `id` | `id` |
| `UserId` | `userId` | `user_id` |
| `CreatedAt` | `createdAt` | `created_at` |
| `IsDeleted` | — (hidden) | `is_deleted` |

Use `[Table("table_name")]` and `[Column("column_name")]` attributes on entity classes. EF Core maps properties to columns automatically otherwise.

## Database Credentials

- `appsettings.json` has a placeholder password (`YOUR_PASSWORD`)
- `appsettings.Development.json` has real credentials and **is gitignored**
- Connection uses Supabase pooler: `aws-0-us-west-2.pooler.supabase.com`
- Retry strategy: `NpgsqlRetryingExecutionStrategy` is already configured in Program.cs

## Response Envelope Convention

All error responses MUST follow this shape:
```json
{
  "statusCode": 400,
  "message": "Validation failed.",
  "errors": [
    { "field": "name", "message": "Name is required." }
  ]
}
```
On non-validation errors (401, 403, 404, 409), `errors` is an empty array `[]`.

## Scope Boundaries

- **MVP**: Users, Accounts, Categories, Transactions
- **Future** (do NOT implement without explicit request): budgets, statements, reports, JWT refresh tokens
- **Out of scope** (never implement): bank sync, multi-currency, investments, OAuth, push notifications

## Solution Files

Two solution files exist:
- `BudgetPilot API.slnx` (new VS format)
- `BudgetPilot API/BudgetPilot API.sln` (legacy)

Target the `.slnx` at root when using solution-level commands.
