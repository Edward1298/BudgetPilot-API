# BudgetPilot API — Testing Plan
**Scope:** MVP validation (Users, Accounts, Categories, Transactions).
**Strategy:** Automated unit + integration tests backed by SQLite in-memory, plus a manual smoke-test checklist.

---

## 1. Verification Command

```powershell
dotnet build "BudgetPilot API.slnx"
dotnet test "BudgetPilot API.Tests/BudgetPilot API.Tests.csproj"
```

The MVP is declared "tested green" only when both commands succeed with zero failures. The automated suite is the source of truth; the manual checklist (Section 6) is a final human-verification gate.

---

## 2. Decisions Locked

| Area            | Decision                                                                 |
|-----------------|--------------------------------------------------------------------------|
| Test scope      | Automated unit + integration + manual smoke checklist                    |
| Test database   | SQLite in-memory provider, swapped in via `WebApplicationFactory`       |
| CI              | Local-only for now (`dotnet test` by hand)                               |
| Project location | Same repo, new folder `BudgetPilot API.Tests/`, added to root `.slnx`   |
| Framework       | xUnit + `Microsoft.AspNetCore.Mvc.Testing` + `FluentAssertions`         |

---

## 3. Prerequisites

Before executing this plan, the MVP implementation must be complete:

- Users, Accounts, Categories, and Transactions modules fully implemented per their respective contracts in `specs/contracts/`.
- Every entity (`UsersOBJ`, `AccountsOBJ`, `CategoriesOBJ`, `TransactionsOBJ`) is correctly mapped with `[Table]` and `[Column]` attributes and registered as a `DbSet` in `AppDbContext`. `EnsureCreated()` builds the schema from these mappings, so missing or misconfigured entities will surface here.
- The application builds cleanly: `dotnet build "BudgetPilot API/BudgetPilot API.csproj"` succeeds.

> **Note:** This plan deliberately uses SQLite in-memory and **does NOT** require Supabase to be running. Tests are fully isolated and leave zero artifacts behind. A live database is only required for the optional manual smoke checklist (Section 6), which can be deferred until Supabase is unpaused.

---

## 4. Test Project Setup

### 4.1 New Project

Create `BudgetPilot API.Tests/BudgetPilot API.Tests.csproj` (xUnit, .NET 10) and add it to the root `BudgetPilot API.slnx`.

**NuGet references:**

| Package                                         | Version  | Purpose                                |
|-------------------------------------------------|----------|----------------------------------------|
| `Microsoft.NET.Test.Sdk`                        | latest   | Test runner                            |
| `xunit`                                         | latest   | Test framework                         |
| `xunit.runner.visualstudio`                     | latest   | VS/test explorer adapter               |
| `Microsoft.AspNetCore.Mvc.Testing`              | latest   | `WebApplicationFactory<Program>`       |
| `Microsoft.EntityFrameworkCore.Sqlite`          | latest   | SQLite EF Core provider (test DB only) |
| `FluentAssertions`                              | latest   | Readable assertions                    |

### 4.2 Folder Layout

```
BudgetPilot API.Tests/
├── BudgetPilot API.Tests.csproj
├── Integration/
│   ├── UsersApiTests.cs
│   ├── AccountsApiTests.cs
│   ├── CategoriesApiTests.cs
│   ├── TransactionsApiTests.cs
│   ├── EndToEndFlowTests.cs
│   └── TestWebAppFactory.cs        ← the DB swap lives here
├── Services/
│   ├── AccountsServiceTests.cs
│   ├── CategoriesServiceTests.cs
│   └── TransactionsServiceTests.cs
├── Unit/
│   └── DtoValidationTests.cs
├── Fixtures/
│   └── TestUserFixture.cs
└── Usings.cs
```

No `DbCleanupFixture`, no `appsettings.Test.json`, no orphan-sweep scaffolding — SQLite in-memory is discarded at the end of each run, so cleanup is automatic.

### 4.3 Only Production-Code Change

Append to `BudgetPilot API/Program.cs`:

```csharp
public partial class Program { }
```

Standard Microsoft pattern that lets `WebApplicationFactory<Program>` target the top-level `Program`. No runtime effect. No other production file is touched as part of the testing task itself.

> **Important:** If tests reveal a bug, the fix is a separate follow-up step via the bug-triage workflow (Section 7), not part of this setup.

---

## 5. Test Layers & Coverage

### 5.1 Integration Tests (primary layer)

Use `WebApplicationFactory<Program>` with a real `HttpClient` against the live HTTP pipeline. The test factory swaps the EF Core provider from Npgsql to SQLite in-memory and runs `EnsureCreated()` to build the schema from the entity model.

Each test class is responsible for registering a fresh user via `POST /api/v1/users/register`, logging in via `POST /api/v1/users/login`, and setting the Bearer token on the `HttpClient` before exercising the endpoints under test. The whole DB is scoped per `WebApplicationFactory` instance (or reset between tests via `EnsureDeleted` + `EnsureCreated`), so isolation is automatic.

**`TestWebAppFactory.cs`** (the DB swap mechanism):

```csharp
protected override void ConfigureServices(IServiceCollection services)
{
    var descriptor = services.SingleOrDefault(
        d => d.ServiceType == typeof(DbContextOptions<AppDbContext>));
    if (descriptor != null)
        services.Remove(descriptor);

    services.AddDbContext<AppDbContext>(options =>
        options.UseSqlite("DataSource=:memory:"));

    using var scope = services.BuildServiceProvider().CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();
}
```

> The `NpgsqlRetryingExecutionStrategy` is Npgsql-specific and is intentionally **not** registered for SQLite. It is a config concern, not business logic, so this is acceptable for the test surface.

### 5.2 Service-Level Tests (secondary layer)

Instantiate the service directly (`TransactionsService`, `AccountsService`, etc.) with the real `AppDbContext` resolved from DI. Used for logic that's awkward to drive through HTTP — verifying exact balance math after an update that changes both account and amount, or asserting that a DELETE reversal restores the original balance to the cent.

### 5.3 Pure Unit Tests (optional, cheap win)

Hand-instantiate `ValidationContext` against each DTO (`AccountsDTO`, `CategoriesDTO`, `TransactionsDTO`, `RegisterDTO`, `LoginDTO`) and assert validation RegExes, `[Range]`, `[MaxLength]`. No DB required. Catches misconfigured attributes before a request can reach the controller.

---

## 6. Test Coverage Per Module

For each module, three categories: **Happy path**, **Validation/ownership**, **Cross-cutting rules**.

### 6.1 Users
- Register → 201, location header, BCrypt hash format (DB hash length / format).
- Register with duplicate email → 409.
- Register with invalid email / short password → 400 with field-tagged errors.
- Login with correct credentials → 200 + JWT structure (3 dot-separated parts).
- Login with wrong password → 401.
- `GET /users/me` with valid JWT → 200, correct user.
- `GET /users/me` without JWT → 401.
- Email normalization: register `John@Example.com`, login with `john@example.com` → 200.
- Pagination on `GET /users` (page/pageSize/totalCount shape).

### 6.2 Accounts
- Create → 201, location, balance defaults to 0 when omitted.
- Create with negative balance on `cash`/`bankAccount` → 400 (balance field).
- Create with negative balance on `creditCard` → 201 (allowed).
- List with filter `type` and `search` → only matching rows.
- GET by id not owned → 403.
- GET by id not found → 404.
- PUT full replace → balance updated.
- DELETE → 204; subsequent GET → 404.
- DELETE account with linked transactions → 409 (active after Transactions module ships).
- Ownership isolation: JWT of user B cannot see user A's account by id.

### 6.3 Categories
- Create income/expense → 201.
- Create with duplicate `(name, type)` for same user → 409.
- Create with same name but different type → 201 (allowed).
- Create with bad type → 400.
- List filtered by `type` and `search`.
- PUT rename to a name+type another of the user's categories has → 409.
- PUT on not-owned → 403.
- DELETE not-owned → 403; not-found → 404.
- DELETE category with linked transactions → 409 (active after Transactions ships).
- Lowercase `type` strictly enforced (POST with `Income` → 400).

### 6.4 Transactions (the meatiest)
- Create valid → 201, `date` equals `DateTime.UtcNow.Date`, `userId` not in response.
- Create with `accountId` not owned → 404.
- Create with `categoryId` not owned → 404.
- Create with `type=expense` but category is `income` → 400 with `field: "type"`.
- Create with `amount=0` or negative → 400.
- **Account balance effect after create:** income account balance += amount; expense balance -= amount. Assert via `GET /accounts/{id}`.
- PUT changes account → old account balance reversed, new account balance affected.
- PUT changes amount → balance delta correct to the cent.
- PUT `date` is ignored if supplied (date is immutable).
- DELETE → 204, account balance reversed, GET transaction → 404.
- List filters: `accountId`, `categoryId`, `type`, `from`/`to` date range, `search` on description.
- Pagination metadata shape.
- 403/404 disambiguation for not-owned vs not-found.

### 6.5 Cross-Module End-to-End Flow (one test per run)
Register → login → create account → create category → create transaction → list transactions → verify account balance → delete transaction → verify balance reverted → delete account (now allowed) → delete category (now allowed) → delete user. Catches wiring bugs between modules that isolated tests miss.

---

## 7. Manual Smoke-Test Checklist

> Requires a running API against some database. If Supabase is still paused, the manual smoke step can be deferred until it is unpaused — the automated suite is what proves correctness; the smoke checklist is a final human-verification gate. The checklist file is `specs/testing/manual-smoke-checklist.md`.

Covers things automation can't easily check:
- Swagger UI loads at `/swagger`, JWT bearer flow works in the UI.
- Each endpoint's request/response matches the contract markdowns verbatim (camelCase, envelope shape).
- 401 happens when the `Authorization` header is missing or malformed.
- Visual check that `passwordHash` never appears in any response.
- The existing `BudgetPilot API/BudgetPilot API.http` file's variables work in VS Code REST Client.
- A full real-user flow performed with your own eyes.

---

## 8. Bug-Triage Workflow (find → fix → verify)

When a test fails or a manual smoke check reveals a bug:

1. **Reproduce minimally** — extract the failing HTTP call into the `.http` file or a one-off test.
2. **Log** — add an entry to `specs/testing/bugs-found.md` (module, endpoint, expected vs actual, root cause once known).
3. **Add a regression test first (red)** — the same scenario the bug exposed. Verify it fails.
4. **Fix the code** — minimal change; rerun `dotnet build` then `dotnet test` until the regression test goes green.
5. **Re-run the full suite** — verify no new regressions.
6. **Record the fix** in `bugs-found.md` with the test name that covers it.

The suite grows with each real bug encountered — not by guessing edge cases.

---

## 9. Files to Create / Modify

### Create

| File | Purpose |
|---|---|
| `BudgetPilot API.Tests/BudgetPilot API.Tests.csproj` | New xUnit test project with the refs above |
| `BudgetPilot API.Tests/Usings.cs` | Global usings |
| `BudgetPilot API.Tests/Integration/TestWebAppFactory.cs` | `WebApplicationFactory<Program>` with SQLite in-memory swap + `EnsureCreated` |
| `BudgetPilot API.Tests/Integration/UsersApiTests.cs` | Users module integration tests |
| `BudgetPilot API.Tests/Integration/AccountsApiTests.cs` | Accounts module integration tests |
| `BudgetPilot API.Tests/Integration/CategoriesApiTests.cs` | Categories module integration tests |
| `BudgetPilot API.Tests/Integration/TransactionsApiTests.cs` | Transactions module integration tests |
| `BudgetPilot API.Tests/Integration/EndToEndFlowTests.cs` | Single cross-module user-journey test |
| `BudgetPilot API.Tests/Services/AccountsServiceTests.cs` | Service-level tests for accounts logic |
| `BudgetPilot API.Tests/Services/CategoriesServiceTests.cs` | Service-level tests for categories logic |
| `BudgetPilot API.Tests/Services/TransactionsServiceTests.cs` | Service-level tests for balance math and reversal logic |
| `BudgetPilot API.Tests/Unit/DtoValidationTests.cs` | DTO validation attribute tests (no DB) |
| `BudgetPilot API.Tests/Fixtures/TestUserFixture.cs` | Register + login + token holder |
| `specs/testing/manual-smoke-checklist.md` | Manual checklist (referenced by Section 7) |
| `specs/testing/bugs-found.md` | Running bug log, grows as bugs are found and fixed |

### Modify

| File | Change |
|---|---|
| `BudgetPilot API/Program.cs` | Append `public partial class Program { }` (only production-code change) |
| `BudgetPilot API.slnx` | Add the test project reference |

**No changes** to existing controllers, services, entities, DTOs, or `AppDbContext` as part of the testing task itself. Bug fixes discovered through testing are separate follow-up changes via the bug-triage workflow.

---

## 10. Implementation Order

1. **Scaffold the test project** — create `BudgetPilot API.Tests.csproj`, add to `BudgetPilot API.slnx`, add NuGet refs (incl. `Microsoft.EntityFrameworkCore.Sqlite`).
2. **Expose `Program`** — append `public partial class Program { }` to `Program.cs`.
3. **DB swap** — implement `TestWebAppFactory` with the SQLite in-memory swap + `EnsureCreated`.
4. **Test scaffolding** — implement `TestUserFixture` (slim: register, login, token holder) and global `Usings.cs`.
5. **UsersApiTests** — foundation; every other module depends on auth working.
6. **AccountsApiTests**.
7. **CategoriesApiTests**.
8. **TransactionsApiTests** + `TransactionsServiceTests` (balance math).
9. **EndToEndFlowTests** — the single cross-module user journey.
10. **DtoValidationTests** — cheap win, no DB needed.
11. **Write `manual-smoke-checklist.md`**.
12. **Run `dotnet test`** (seconds, not minutes — no network) and iterate on failures.
13. **Fix each bug found** via the triage workflow; add a regression test per bug.
14. **Walk the manual smoke checklist** (deferred until Supabase is unpaused, if needed).
15. **All green** → MVP declared stable.

---

## 11. Caveats & Notes

- **SQLite type affinity:** SQLite uses dynamic typing, so PostgreSQL-specific column types (`date`, `numeric`, `uuid`) become SQLite affinity types (TEXT/NUMERIC). EF Core handles the conversions transparently in nearly all cases (`DateOnly` ↔ TEXT, `decimal` ↔ NUMERIC, `Guid` ↔ TEXT). The one thing these tests will **not** catch is PostgreSQL-specific SQL behavior, which the project largely avoids by relying on EF Core's abstractions. For a portfolio project this trade-off is well-justified.
- **Retry strategy not exercised:** `NpgsqlRetryingExecutionStrategy` is Npgsql-specific and is not registered under SQLite. It is a configuration concern, not business logic.
- **`budgets` table:** future scope per `PRODUCT.md`, not part of any contract. `EnsureCreated()` will simply not create it — correct behavior. No action needed.
- **Speed:** the full suite runs in seconds since there's no network round-trip. Excellent for tight test-fix cycles.
- **Repeatability:** the DB is created fresh per `WebApplicationFactory` instance; `totalCount` assertions are fully deterministic because the DB starts empty.