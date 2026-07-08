# AGENTS.md — BudgetPilot API

## Project at a Glance

- **.NET 10.0** ASP.NET Core Web API
- **Solution**: `BudgetPilot API.slnx` (root) includes `BudgetPilot API` and `BudgetPilot API.Tests`
- **Database**: Local SQL Server via EF Core SQL Server provider
- **Auth**: BCrypt password hashing + JWT Bearer tokens
- **Docs**: Swagger at `/swagger` (dev only)

## Essential Commands

```powershell
# Build solution
dotnet build "BudgetPilot API.slnx"

# Run API (http profile, launches Swagger)
dotnet run --project "BudgetPilot API" --launch-profile http

# Run all tests
dotnet test "BudgetPilot API.slnx"

# Run a focused test class
dotnet test "BudgetPilot API.Tests" --filter "FullyQualifiedName~UsersApiTests"
```

No linter or formatter is configured.

## Spec-Driven Development (SDD) Order

Before writing ANY code, read in this exact order:
1. `specs/PRODUCT.md` — scope, entities, in/out
2. `specs/ARCHITECTURE.md` — stack versions, patterns
3. `specs/contracts/<module>-api.md` — endpoint contract for the module
4. `specs/dbschema.md` — table/column definitions
5. `specs/features/` — active user stories (currently empty)

Full rules live in `opencode/rules.md`. This file only covers things an agent would miss.

## Architecture — What Rules Don't Make Obvious

| Fact | Why it matters |
|---|---|
| **NO repository pattern** — services inject `AppDbContext` directly | Don't create `IRepository<T>` abstractions |
| **No EF Core migrations** — schema is managed via raw SQL scripts | Don't run `dotnet ef migrations add`. For new tables, return the `CREATE TABLE` script to the user and wait for confirmation before writing code |
| **Entity suffix is `OBJ`, not `Entity`** | Files go in `Entities/`, class names end in `OBJ` (e.g., `AccountsOBJ.cs`) |
| **DTO suffix is `DTO`** | Files go in `Dtos/`, class names end in `DTO` (e.g., `AccountsDTO.cs`) |
| **Request flow**: Controller → Service → DbContext | Don't inject services into other services, don't call DbContext from controllers |
| **All MVP modules are implemented** | Users, Accounts, Categories, Transactions controllers/services/entities exist. Only `BudgetsOBJ` is commented out in `AppDbContext` |
| **All controllers already use `/api/v1/[controller]`** | Don't revert to `api/[controller]` |
| **HTTPS redirection disabled** | Leave `app.UseHttpsRedirection()` commented out |

## Database Naming Mismatch

C# (PascalCase) ↔ JSON (camelCase) ↔ SQL Server (PascalCase/snake_case in scripts):

| C# Property | JSON field | DB Column |
|---|---|---|
| `Id` | `id` | `id` |
| `UserId` | `userId` | `user_id` |
| `CreatedAt` | `createdAt` | `created_at` |

Use `[Table("table_name")]` and `[Column("column_name")]` attributes on entity classes. EF Core maps properties to columns automatically otherwise.

## Secrets & Configuration

- `appsettings.json` points to the local SQL Server instance using Windows Authentication
- `appsettings.Development.json` is gitignored and holds the JWT section with a placeholder key (`REPLACE_ME_WITH_A_SECURE_KEY_AT_LEAST_32_CHARS`)
- The project has a `UserSecretsId`; secrets may be stored via `dotnet user-secrets`
- Retry strategy: `EnableRetryOnFailure()` is configured in `Program.cs`

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

## Testing

- Test project: `BudgetPilot API.Tests` (xUnit)
- Integration tests use `WebApplicationFactory<Program>` with an in-memory SQLite database via `TestWebAppFactory`
- `TestUserFixture.RegisterAndLoginAsync(client)` creates an authenticated test user
- Unit tests cover DTO validation and service logic
- Tests replace the production SQL Server registration; no real SQL Server instance is needed to run them

## Scope Boundaries

- **MVP**: Users, Accounts, Categories, Transactions
- **Future** (do NOT implement without explicit request): budgets, statements, reports, JWT refresh tokens
- **Out of scope** (never implement): bank sync, multi-currency, investments, OAuth, push notifications
