# Transactions API — Contract Specification
**Module:** Transactions | **Base Path:** `/api/v1/transactions` | **Auth:** Bearer JWT (all endpoints)

> **Note:** Every endpoint in this module is JWT-protected. A valid `Authorization: Bearer <token>` header is mandatory on all requests. The authenticated user's `userId` is always extracted from the JWT and is never accepted from the client. There are no public endpoints in the Transactions module.

---

## Implementation Prerequisites

Before writing any code for this module, load these agent skills in order:

| # | Skill | Why needed |
|---|-------|------------|
| 1 | `aspnet-core` | Controller patterns, `[Authorize]` enforcement, DI registration, EF Core integration (`data-state-and-services.md`) |
| 2 | `csharp-async` | All service/controller methods touching the database MUST be async, return `Task<T>`, and never block with `.Result` or `.Wait()` |
| 3 | `csharp-docs` | Every Controller, Service, DTO, and Entity class/member requires XML `<summary>` comments following the csharp-docs conventions |
| 4 | `supabase-postgres-best-practices` | Indexing strategy on `user_id`, `account_id`, `category_id`; FK-index best practices; pagination patterns |

> **Module dependency:** This contract assumes the Accounts and Categories modules are already implemented. The Transactions service verifies FK ownership against the `accounts` and `categories` tables, so their entities and DbSets must be present in `AppDbContext`. Shipping this module also activates the linked-transactions conflict checks (409) that the Accounts and Categories contracts defer as a TODO.

---

## 1. Entity Schema — `Transaction`

Mapped to the `transactions` table as defined in `specs/dbschema.md`. The table already exists — no raw SQL script is required. Match the columns exactly as documented.

| Field       | Type             | Required | DB Column     | Description                                                                    |
|-------------|------------------|----------|---------------|--------------------------------------------------------------------------------|
| id          | string (UUID v4) | auto     | `id`          | Unique identifier generated server-side                                        |
| userId      | string (UUID v4) | server  | `user_id`     | Owner. FK → `users(id)`. Derived from the JWT; never exposed to clients        |
| accountId   | string (UUID v4) | yes      | `account_id`  | FK → `accounts(id)`. Must belong to the authenticated user                    |
| categoryId  | string (UUID v4) | yes      | `category_id` | FK → `categories(id)`. Must belong to the authenticated user                   |
| amount      | number (decimal) | yes      | `amount`      | Must be strictly greater than 0                                                |
| type        | string           | yes      | `type`        | `income` or `expense` (lowercase). Must match the referenced category's `type` |
| description | string           | no       | `description` | Optional note. Max 500 characters                                              |
| date        | date (ISO 8601)  | server   | `date`        | Set server-side from `DateTime.UtcNow.Date` on creation. Never client-supplied, never updated |

### 1.1 `type` Values

The `type` column stores lowercase categorical identifiers:

| Value     | Description                |
|-----------|----------------------------|
| `income`  | Revenue transaction        |
| `expense` | Spending transaction       |

Validated on the DTO with the RegEx pattern `^(income|expense)$` (matches the convention in `AccountsDTO` and `CategoriesDTO`).

### 1.2 Response vs Database Fields

| Field       | In DB | In API response     |
|-------------|-------|----------------------|
| id          | ✅    | ✅                   |
| userId      | ✅    | ❌ (`[JsonIgnore]`)  |
| accountId   | ✅    | ✅                   |
| categoryId  | ✅    | ✅                   |
| amount      | ✅    | ✅                   |
| type        | ✅    | ✅                   |
| description | ✅    | ✅ (nullable)        |
| date        | ✅    | ✅ (read-only)       |

> **No timestamp column.** The `transactions` table in `dbschema.md` does not define a `created_at` column. The `TransactionsOBJ` entity and API responses MUST NOT include a `createdAt` field. The only time-related field is `date`, which is server-set and never modified after creation.

---

## 2. Endpoints

### 2.1 List Transactions `GET /api/v1/transactions`

Returns a paginated list of transactions belonging to the authenticated user, with multiple optional filters.

**Query Parameters**

| Param       | Type     | Required | Default | Description                                   |
|-------------|----------|----------|---------|-----------------------------------------------|
| page        | int      | no       | 1       | Page number (≥ 1)                             |
| pageSize    | int      | no       | 20      | Items per page (1–100)                        |
| type        | string   | no       | —       | Filter by `income` or `expense`              |
| accountId   | string   | no       | —       | Filter by linked account (UUID)              |
| categoryId  | string   | no       | —       | Filter by linked category (UUID)             |
| from        | date     | no       | —       | Lower bound on `date` (ISO 8601, inclusive)  |
| to          | date     | no       | —       | Upper bound on `date` (ISO 8601, inclusive)  |
| search      | string   | no       | —       | Partial match on `description`              |

> Results are ordered by `date` descending (newest first).

**Response `200 OK`**
```json
{
  "data": [
    {
      "id": "d1e2f3a4-...",
      "accountId": "a1b2c3d4-...",
      "categoryId": "c1d2e3f4-...",
      "amount": 50.00,
      "type": "expense",
      "description": "Lunch",
      "date": "2026-07-01"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1
}
```

**Error Responses**

| Code | When                              |
|------|-----------------------------------|
| 401  | Missing / invalid / expired JWT   |

---

### 2.2 Get Transaction by ID `GET /api/v1/transactions/{id}`

**Path Parameters**

| Param | Type          | Description                      |
|-------|---------------|----------------------------------|
| id    | string (UUID) | Transaction unique identifier    |

**Response `200 OK`**
```json
{
  "id": "d1e2f3a4-...",
  "accountId": "a1b2c3d4-...",
  "categoryId": "c1d2e3f4-...",
  "amount": 50.00,
  "type": "expense",
  "description": "Lunch",
  "date": "2026-07-01"
}
```

**Error Responses**

| Code | When                                            |
|------|-------------------------------------------------|
| 401  | Missing / invalid / expired JWT                |
| 403  | Transaction belongs to a different user        |
| 404  | Transaction not found                          |

---

### 2.3 Create Transaction `POST /api/v1/transactions`

Records a new transaction and automatically adjusts the linked account's balance.

**Request Body** *(application/json, camelCase)*

```json
{
  "accountId": "a1b2c3d4-...",
  "categoryId": "c1d2e3f4-...",
  "amount": 50.00,
  "type": "expense",
  "description": "Lunch"
}
```

| Field       | Type            | Required | Validation                                              |
|-------------|-----------------|----------|---------------------------------------------------------|
| accountId   | string (UUID)   | yes      | Must reference an account owned by the user             |
| categoryId  | string (UUID)   | yes      | Must reference a category owned by the user            |
| amount      | number (decimal)| yes      | Must be greater than 0                                  |
| type        | string          | yes      | Must match `^(income|expense)$` and equal the category's `type` |
| description | string          | no       | Max 500 characters                                      |

> **Note:** `userId` is extracted from the JWT token and assigned server-side. `date` is set server-side from `DateTime.UtcNow.Date` on creation. The client must never supply either field.

**Server-side validation and effect application (in the service):**
1. Load the referenced account by `(accountId, userId)`. If not found/not owned → 404.
2. Load the referenced category by `(categoryId, userId)`. If not found/not owned → 404.
3. If `dto.Type != category.Type` → 400 validation error.
4. Set `userId`, `date`, and `id` server-side.
5. Apply the balance effect on the account: `income` adds the amount, `expense` subtracts the amount.
6. Persist both the new transaction and the account balance change in a single `SaveChangesAsync` call (one EF transaction) so the data stays consistent.

**Response `201 Created`**
```json
{
  "id": "d1e2f3a4-...",
  "accountId": "a1b2c3d4-...",
  "categoryId": "c1d2e3f4-...",
  "amount": 50.00,
  "type": "expense",
  "description": "Lunch",
  "date": "2026-07-01"
}
```
- `Location` header set to `/api/v1/transactions/{id}`

**Error Responses**

| Code | When                                                                                  |
|------|---------------------------------------------------------------------------------------|
| 400  | Validation errors (amount, type mismatch with category, bad RegEx, etc.)             |
| 401  | Missing / invalid / expired JWT                                                      |
| 404  | Referenced account or category does not exist or does not belong to the user          |

**Example `400 Bad Request`**
```json
{
  "statusCode": 400,
  "message": "Validation failed.",
  "errors": [
    { "field": "amount", "message": "Amount must be greater than 0." },
    { "field": "type", "message": "Transaction type must match the referenced category's type." }
  ]
}
```

---

### 2.4 Update Transaction `PUT /api/v1/transactions/{id}`

Full replacement update. All required fields must be supplied. **`date` is not editable** — it was set at creation and stays unchanged.

**Request Body** *(application/json, camelCase)*

```json
{
  "accountId": "a1b2c3d4-...",
  "categoryId": "c1d2e3f4-...",
  "amount": 75.00,
  "type": "expense",
  "description": "Dinner"
}
```

| Field       | Type            | Required | Validation                                              |
|-------------|-----------------|----------|---------------------------------------------------------|
| accountId   | string (UUID)   | yes      | Must reference an account owned by the user             |
| categoryId  | string (UUID)   | yes      | Must reference a category owned by the user            |
| amount      | number (decimal)| yes      | Must be greater than 0                                  |
| type        | string          | yes      | Must match `^(income|expense)$` and equal the category's `type` |
| description | string          | no       | Max 500 characters                                      |

**Server-side effect handling (in the service):**
1. Load the existing transaction by `(id, userId)`. If not found → 404.
2. Reverse the old transaction's balance effect on the (old) linked account.
3. Load the new account (may be the same one) and verify ownership → 404 otherwise.
4. Load the new category and verify ownership → 404 otherwise.
5. If `dto.Type != category.Type` → 400.
6. Apply the new effect on the new account.
7. Update transaction fields (`accountId`, `categoryId`, `amount`, `type`, `description`). `date` is left untouched.
8. Commit everything in a single `SaveChangesAsync` (one EF transaction).

**Response `200 OK`**
```json
{
  "id": "d1e2f3a4-...",
  "accountId": "a1b2c3d4-...",
  "categoryId": "c1d2e3f4-...",
  "amount": 75.00,
  "type": "expense",
  "description": "Dinner",
  "date": "2026-07-01"
}
```

**Error Responses**

| Code | When                                                                                  |
|------|---------------------------------------------------------------------------------------|
| 400  | Validation errors (amount, type mismatch with category, bad RegEx, etc.)             |
| 401  | Missing / invalid / expired JWT                                                      |
| 403  | Transaction belongs to a different user                                              |
| 404  | Transaction not found, or referenced account/category not owned by the user          |

---

### 2.5 Delete Transaction `DELETE /api/v1/transactions/{id}`

Permanently deletes the transaction and reverses its balance effect on the linked account.

**Server-side effect handling (in the service):**
1. Load the transaction by `(id, userId)`. Return not-found sentinel if missing.
2. Reverse the transaction's balance effect on its linked account (`income` → subtract, `expense` → add).
3. Remove the transaction and commit everything in a single `SaveChangesAsync`.

**Response `204 No Content`**
- Empty body. No content returned on successful deletion.

**Error Responses**

| Code | When                                            |
|------|-------------------------------------------------|
| 401  | Missing / invalid / expired JWT                |
| 403  | Transaction belongs to a different user        |
| 404  | Transaction not found                          |

---

## 3. Endpoints Summary

| Method | Path                        | Auth | Purpose                  |
|--------|-----------------------------|------|--------------------------|
| GET    | /api/v1/transactions        | JWT  | List transactions        |
| GET    | /api/v1/transactions/{id}   | JWT  | Get transaction by ID   |
| POST   | /api/v1/transactions        | JWT  | Create transaction       |
| PUT    | /api/v1/transactions/{id}   | JWT  | Update transaction       |
| DELETE | /api/v1/transactions/{id}   | JWT  | Delete transaction       |

---

## 4. Cross-Cutting Rules

1. **JWT-mandatory** — every endpoint requires a valid Bearer JWT. The controller class MUST be decorated with `[Authorize]`. There are no public endpoints in this module.
2. **Ownership isolation** — every query scopes data to the authenticated user. `userId` is inferred from the JWT `ClaimTypes.NameIdentifier` claim (same `GetUserId()` helper pattern used in `AccountsController` and `CategoriesController`), never from the request body or query string.
3. **Account balance auto-update** — create applies the effect (`income` adds, `expense` subtracts), update reverses the old effect then applies the new one against the (possibly new) account, delete reverses the effect. All balance changes happen inside the same `SaveChangesAsync` call as the transaction write (one EF transaction) so the data stays consistent. > **Caveat:** transaction-driven balance changes are NOT subject to the non-negative balance rule enforced on the `cash` and `bankAccount` account types by the Accounts module — an expense larger than the current balance can push the account negative. This is by design: the Account's balance reflects real transaction history. Direct edits via `PUT /api/v1/accounts/{id}` still enforce the non-negative rule.
4. **FK ownership check** — POST and PUT verify that `accountId` and `categoryId` both belong to the authenticated user. A 404 (not 403) is returned when an FK target is missing or not owned, consistent with the ownership convention on Accounts and Categories (avoids leaking the existence of other users' records).
5. **Type/category match** — `transaction.type` MUST equal the referenced category's `type`. Mismatch returns a 400 validation error pointing at the `type` field. No title-case, no transformation layer.
6. **Date is server-set and immutable** — `date` is assigned from `DateTime.UtcNow.Date` on creation and NEVER modified afterward. The PUT request body MUST NOT include a `date` field. Responses DO include it as read-only data.
7. **Amount strictly positive** — `amount` must be greater than 0. Zero or negative values are a 400 validation error.
8. **No soft delete** — the `transactions` table has no `is_deleted` column. Deletion is permanent.
9. **Error envelope** — all error responses follow `{ statusCode, message, errors[] }`. On non-validation errors (401, 403, 404), `errors` is an empty array `[]`.
10. **Module dependency** — depends on the previously-implemented Accounts and Categories modules. Shipping this module activates the linked-transactions 409 conflict checks in `AccountsService.DeleteAccount` and `CategoriesService.DeleteCategory`.

---

## 5. Files to Create

### 5.1 `BudgetPilot API/Entities/TransactionsOBJ.cs`

**Purpose:** Entity mapped to the `transactions` table.

**Contents:**
- `[Table("transactions")]` on the class.
- 8 properties with `[Column("...")]` attributes:
  - `Id` (`Guid`) — primary key, server-generated.
  - `UserId` (`Guid`) — `[JsonIgnore]`, never exposed in responses.
  - `AccountId` (`Guid`) — FK to `accounts`.
  - `CategoryId` (`Guid`) — FK to `categories`.
  - `Amount` (`decimal`) — strictly positive.
  - `Type` (`string`) — `income` / `expense`.
  - `Description` (`string?`) — optional, max 500.
  - `Date` (`DateOnly` recommended — Npgsql maps cleanly to PostgreSQL `date`) — server-set, immutable.
- XML `<summary>` on the class and every property.
- No `CreatedAt` — the table has no such column.
- Suffix `OBJ` per AGENTS.md.

### 5.2 `BudgetPilot API/Dtos/TransactionsDTO.cs`

**Purpose:** Request DTO reused by POST (create) and PUT (update). Mirrors the `AccountsDTO` / `CategoriesDTO` single-DTO-for-both-operations convention.

**Contents:**
- `AccountId` — `Guid`, `[Required]`.
- `CategoryId` — `Guid`, `[Required]`.
- `Amount` — `decimal`, `[Required]`, `[Range(0.0001, double.MaxValue, ErrorMessage = "Amount must be greater than 0.")]`.
- `Type` — `string`, `[Required]`, `[RegularExpression("^(income|expense)$", ErrorMessage = "Type must be one of: income, expense.")]`.
- `Description` — `string?`, optional, `[MaxLength(500)]`.
- **No `Date` field** — date is server-set.
- **No `UserId` field** — comes from the JWT.
- **No `Id` field** — server-generated.
- XML `<summary>` on the class and every property.

### 5.3 `BudgetPilot API/Services/TransactionsService.cs`

**Purpose:** Business logic for the Transactions module. Injects `AppDbContext` directly (no repository pattern). All methods async, return `Task<T>`, XML docs on every method.

**Methods:**
- `GetTransactions(userId, page, pageSize, type, accountId, categoryId, from, to, search)` → `(List<TransactionsOBJ> Items, int TotalCount)` — paged, filtered, ordered by `Date` descending.
- `GetTransactionById(id)` → `TransactionsOBJ?` — ownership verified by the caller (same pattern as AccountsService).
- `CreateTransaction(dto, userId)` → `TransactionsOBJ`:
  1. Load account by `(accountId, userId)`; 404 if not found/not owned.
  2. Load category by `(categoryId, userId)`; 404 if not found/not owned.
  3. If `dto.Type != category.Type` → throw domain exception (controller maps to 400 with `field: "type"`).
  4. Build entity: `Id = Guid.NewGuid()`, `Date = DateOnly.FromDateTime(DateTime.UtcNow)`, `UserId = userId`, plus DTO fields.
  5. Apply balance effect on account: `income → balance += amount`, `expense → balance -= amount`.
  6. `SaveChangesAsync` (single call — atomic with the transaction insert).
- `UpdateTransaction(id, dto, userId)` → `TransactionsOBJ?`:
  1. Load existing transaction by `(id, userId)`; return `null` if not found.
  2. Reverse the old effect on the old account.
  3. Load the new account (may be the same one); verify ownership; 404 sentinel otherwise.
  4. Load the new category; verify ownership; 404 sentinel otherwise.
  5. If `dto.Type != category.Type` → throw domain exception (controller maps to 400).
  6. Apply the new effect on the new account.
  7. Update transaction fields (`AccountId`, `CategoryId`, `Amount`, `Type`, `Description`). **`Date` is NOT changed.**
  8. `SaveChangesAsync` (single call — atomic).
- `DeleteTransaction(id, userId)` → `bool`:
  1. Load transaction by `(id, userId)`; return `false` if not found/not owned.
  2. Reverse balance effect on its account.
  3. Remove the transaction.
  4. `SaveChangesAsync` (single call — atomic).

### 5.4 `BudgetPilot API/Controllers/TransactionsController.cs`

**Purpose:** HTTP layer.

**Contents:**
- `[ApiController] [Authorize] [Route("api/v1/[controller]")]`.
- Five `[Http*]` actions mapped to the endpoints above.
- `GetUserId()` helper copied from `AccountsController`.
- The four private error-envelope helpers (`UnauthorizedError`, `ForbiddenError`, `NotFoundError`, `ValidationError`) copied verbatim from `AccountsController` for envelope consistency.
- Maps service signals:
  - Account/Category not owned / not found → 404.
  - Type mismatch → 400 validation error with `{ field: "type", message: "Transaction type must match the referenced category's type." }`.
  - Transaction not found vs. owned-by-another-user → 404 vs. 403 (same disambiguator pattern as `AccountsController.DeleteAccount`: re-fetch and check `UserId`).

---

## 6. Files to Modify

| File | What changes |
|------|-------------|
| `BudgetPilot API/Data/AppDbContext.cs` | Uncomment / add `public DbSet<TransactionsOBJ> Transactions { get; set; }`. Preserve the `NpgsqlRetryingExecutionStrategy` configuration and existing DbSets. Do **not** run `dotnet ef migrations add` — the table already exists in the DB. |
| `BudgetPilot API/Program.cs` | Register `builder.Services.AddScoped<TransactionsService>();` next to the existing `AccountsService` registration. No auth or pipeline changes — JWT is already wired from the Users module. |
| `BudgetPilot API/Services/AccountsService.cs` | Activate the existing TODO in `DeleteAccount`: add `await _context.Transactions.AnyAsync(t => t.AccountId == id)` and return a conflict sentinel (e.g. `null` or a `(bool Deleted, bool HasConflict)` tuple) instead of deleting when linked transactions exist. The controller maps the conflict signal to `409`. |
| `BudgetPilot API/Services/CategoriesService.cs` | Same linked-transactions check on `DeleteCategory`; return a conflict sentinel the controller maps to `409`. Activates the TODO the Categories contract leaves open. |
| `BudgetPilot API/Controllers/AccountsController.cs` | Handle the new "has-linked-transactions" sentinel → return `409` with `{ statusCode: 409, message: "Account has linked transactions and cannot be deleted.", errors: [] }`. Add a private `ConflictError(string message)` helper mirroring the existing error helpers. |
| `BudgetPilot API/Controllers/CategoriesController.cs` | Same: add `ConflictError` helper, return 409 when the service reports linked transactions. |

---

## 7. Implementation Order

1. Re-read `specs/dbschema.md` (the `transactions` table) and `contracts/accounts-api.md` and `contracts/categories-api.md` for the established pattern.
2. Create `Entities/TransactionsOBJ.cs` — 8 properties with `[Column]`, `[JsonIgnore]` on `UserId`, `Date` as `DateOnly`, XML docs, no `CreatedAt`.
3. Create `Dtos/TransactionsDTO.cs` — `AccountId`, `CategoryId`, `Amount`, `Type`, `Description?` with validation attributes. No `Date`, `UserId`, or `Id` field.
4. Update `Data/AppDbContext.cs` — add the `Transactions` DbSet; leave the retry strategy and existing DbSets untouched.
5. Create `Services/TransactionsService.cs` — CRUD with FK ownership checks, type-match check, automatic `Date` set from `DateTime.UtcNow.Date`, automatic account-balance update on create/update/delete, atomic `SaveChangesAsync` calls.
6. Create `Controllers/TransactionsController.cs` — five actions, `[Authorize]`, `GetUserId()` + the four error helpers, mapping service signals to 400/401/403/404.
7. Wire `AccountsService.DeleteAccount` and `CategoriesService.DeleteCategory` to return a conflict sentinel when linked transactions exist; add a `ConflictError(string)` helper to both controllers and map the sentinel to `409` with `errors: []`.
8. Register `TransactionsService` in `Program.cs` next to `AccountsService`.
9. Run `dotnet build "BudgetPilot API/BudgetPilot API.csproj"` — must compile with zero errors before the task is reported complete.