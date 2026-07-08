# Categories API — Contract Specification
**Module:** Categories | **Base Path:** `/api/v1/categories` | **Auth:** Bearer JWT (all endpoints)

> **Note:** Every endpoint in this module is JWT-protected. A valid `Authorization: Bearer <token>` header is mandatory on all requests. The authenticated user's `userId` is always extracted from the JWT and is never accepted from the client. There are no public endpoints in the Categories module.

---

## Implementation Prerequisite

Before writing any code for this module, load these agent skills in order:

| # | Skill | Why needed |
|---|-------|------------|
| 1 | `aspnet-core` | Controller patterns, `[Authorize]` enforcement, DI registration (`program-and-pipeline.md`), API conventions (`apis-minimal-and-controllers.md`) |
| 2 | `csharp-async` | All service methods and controller actions that touch the database MUST be async, return `Task<T>`, and never block with `.Result` or `.Wait()` |
| 3 | `csharp-docs` | Every Controller, Service, DTO, and Entity class/member requires XML `<summary>` comments following the csharp-docs conventions |

---

## 1. Entity Schema — `Category`

Mapped to the `categories` table as defined in `specs/dbschema.md`. The table already exists — no raw SQL script is required. Match the columns exactly as documented.

| Field  | Type             | Required | DB Column | Description                                                                   |
|--------|------------------|----------|-----------|-------------------------------------------------------------------------------|
| id     | string (UUID v4) | auto     | `id`      | Unique identifier generated server-side                                      |
| userId | string (UUID v4) | server  | `user_id` | Owner. FK → `users(id)`. Derived from the JWT; never exposed to clients      |
| name   | string           | yes      | `name`    | Human-readable label (e.g. "Food", "Salary"). 1–100 chars, not whitespace-only |
| type   | string           | yes      | `type`    | `income` or `expense` (lowercase)                                            |

### 1.1 `type` Values

The `type` column stores lowercase categorical identifiers:

| Value     | Description                |
|-----------|----------------------------|
| `income`  | Revenue category (e.g. Salary)  |
| `expense` | Spending category (e.g. Food)  |

Validated on the DTO with the RegEx pattern `^(income|expense)$` (mirrors the Accounts pattern `^(cash|creditCard|bankAccount)$`).

### 1.2 Response vs Database Fields

| Field  | In DB | In API response     |
|--------|-------|----------------------|
| id     | ✅    | ✅                   |
| userId | ✅    | ❌ (`[JsonIgnore]`)  |
| name   | ✅    | ✅                   |
| type   | ✅    | ✅                   |

> **No timestamp column.** The `categories` table in `dbschema.md` does not define a `created_at` column. The `CategoriesOBJ` entity and API responses MUST NOT include a `createdAt` field. Do not add the column without an explicit schema change request.

---

## 2. Endpoints

### 2.1 List Categories `GET /api/v1/categories`

Returns a paginated list of categories belonging to the authenticated user, optionally filtered by type and/or a partial name match.

**Query Parameters**

| Param    | Type   | Required | Default | Description                      |
|----------|--------|----------|---------|----------------------------------|
| page     | int    | no       | 1       | Page number (≥ 1)                |
| pageSize | int    | no       | 20      | Items per page (1–100)          |
| type     | string | no       | —       | Filter by `income` or `expense` |
| search   | string | no       | —       | Partial match on `name`          |

**Response `200 OK`**
```json
{
  "data": [
    {
      "id": "c1d2e3f4-...",
      "name": "Food",
      "type": "expense"
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

### 2.2 Get Category by ID `GET /api/v1/categories/{id}`

**Path Parameters**

| Param | Type          | Description                  |
|-------|---------------|------------------------------|
| id    | string (UUID) | Category unique identifier   |

**Response `200 OK`**
```json
{
  "id": "c1d2e3f4-...",
  "name": "Food",
  "type": "expense"
}
```

**Error Responses**

| Code | When                                          |
|------|-----------------------------------------------|
| 401  | Missing / invalid / expired JWT              |
| 403  | Category belongs to a different user          |
| 404  | Category not found                            |

---

### 2.3 Create Category `POST /api/v1/categories`

**Request Body** *(application/json, camelCase)*

```json
{
  "name": "Food",
  "type": "expense"
}
```

| Field | Type   | Required | Validation                                    |
|-------|--------|----------|-----------------------------------------------|
| name  | string | yes      | 1–100 characters, not whitespace-only         |
| type  | string | yes      | Must match `^(income|expense)$`               |

> **Note:** `userId` is extracted from the JWT token and assigned server-side. The client must never supply it.

**Response `201 Created`**
```json
{
  "id": "c1d2e3f4-...",
  "name": "Food",
  "type": "expense"
}
```
- `Location` header set to `/api/v1/categories/{id}`

**Error Responses**

| Code | When                                                                   |
|------|------------------------------------------------------------------------|
| 400  | Validation errors (see error format below)                             |
| 401  | Missing / invalid / expired JWT                                        |
| 409  | A category with the same `name` and `type` already exists for this user |

**Example `400 Bad Request`**
```json
{
  "statusCode": 400,
  "message": "Validation failed.",
  "errors": [
    { "field": "name", "message": "Name is required." },
    { "field": "type", "message": "Type must be one of: income, expense." }
  ]
}
```

**Example `409 Conflict`**
```json
{
  "statusCode": 409,
  "message": "A category with this name and type already exists.",
  "errors": []
}
```

---

### 2.4 Update Category `PUT /api/v1/categories/{id}`

Full replacement update. All required fields must be supplied. Uses the same DTO shape as POST.

**Request Body** *(application/json, camelCase)*

```json
{
  "name": "Groceries",
  "type": "expense"
}
```

| Field | Type   | Required | Validation                            |
|-------|--------|----------|---------------------------------------|
| name  | string | yes      | 1–100 characters, not whitespace-only |
| type  | string | yes      | Must match `^(income|expense)$`       |

**Response `200 OK`**
```json
{
  "id": "c1d2e3f4-...",
  "name": "Groceries",
  "type": "expense"
}
```

**Error Responses**

| Code | When                                                                          |
|------|-------------------------------------------------------------------------------|
| 400  | Validation errors                                                             |
| 401  | Missing / invalid / expired JWT                                               |
| 403  | Category belongs to a different user                                          |
| 404  | Category not found                                                            |
| 409  | Another category with the same `name` and `type` already exists for this user |

---

### 2.5 Delete Category `DELETE /api/v1/categories/{id}`

Permanently deletes the category from the database after verifying ownership.

**Response `204 No Content`**
- Empty body. No content returned on successful deletion.

**Error Responses**

| Code | When                                                                                    |
|------|-----------------------------------------------------------------------------------------|
| 401  | Missing / invalid / expired JWT                                                         |
| 403  | Category belongs to a different user                                                    |
| 404  | Category not found                                                                      |
| 409  | Category has linked transactions — cannot be deleted while transactions reference it (*pending Transactions module*) |

---

## 3. Endpoints Summary

| Method | Path                       | Auth | Purpose           |
|--------|----------------------------|------|-------------------|
| GET    | /api/v1/categories         | JWT  | List categories    |
| GET    | /api/v1/categories/{id}    | JWT  | Get category by ID |
| POST   | /api/v1/categories         | JWT  | Create category    |
| PUT    | /api/v1/categories/{id}    | JWT  | Update category    |
| DELETE | /api/v1/categories/{id}    | JWT  | Delete category    |

---

## 4. Cross-Cutting Rules

1. **JWT-mandatory** — every endpoint requires a valid Bearer JWT. The controller class MUST be decorated with `[Authorize]`. There are no public endpoints in this module.
2. **Ownership isolation** — every query scopes data to the authenticated user. `userId` is inferred from the JWT `ClaimTypes.NameIdentifier` claim (same `GetUserId()` helper pattern used in `AccountsController`), never from the request body or query string.
3. **Uniqueness** — the tuple `(userId, name, type)` MUST be unique. POST and PUT perform the duplicate check before writing and return `409 Conflict` if a matching category already exists (the PUT check excludes the category being edited). No unique DB constraint exists, so this is enforced in the service layer.
4. **Type values** — strictly lowercase `income` / `expense`, enforced by a `[RegularExpression]` attribute on the DTO. No title case, no transformation layer.
5. **No timestamps** — the `categories` table has no `created_at` column. The entity and responses MUST NOT include `createdAt`. Do not add the column without an approved schema change request.
6. **Conflict detection (409 on DELETE)** — DELETE returns 409 if the category is referenced by at least one transaction. This check is pending the Transactions module implementation (must be marked as a `TODO` in the service, mirroring the existing TODO in `AccountsService.DeleteAccount`).
7. **Error envelope** — all error responses follow `{ statusCode, message, errors[] }`. On non-validation errors (401, 403, 404, 409), `errors` is an empty array `[]`.
8. **No soft delete** — the `categories` table has no `is_deleted` column. Deletion is permanent.
9. **DTO reuse** — a single `CategoriesDTO` serves both POST (create) and PUT (update), matching the `AccountsDTO` convention.

---

## 5. Files to Create

| File | Purpose |
|------|---------|
| `BudgetPilot API/Entities/CategoriesOBJ.cs` | Entity mapped to the `categories` table. `[Table("categories")]`, `[Column("...")]` on each property, `[JsonIgnore]` on `UserId`. XML `<summary>` on class and every property. Suffix `OBJ` per AGENTS.md. |
| `BudgetPilot API/Dtos/CategoriesDTO.cs` | Request DTO for create and update. `name` with `[Required] [MaxLength(100)]`, `type` with `[Required] [RegularExpression("^(income\\\\|expense)$")]`. Suffix `DTO` per AGENTS.md. |
| `BudgetPilot API/Services/CategoriesService.cs` | Business logic: CRUD scoped to `userId`, pagination, uniqueness check (409), ownership-aware get-by-id, DELETE TODO for transaction conflict. `AppDbContext` injected directly (no repository pattern). All methods async. XML docs on every method. |
| `BudgetPilot API/Controllers/CategoriesController.cs` | `[ApiController] [Authorize] [Route("api/v1/[controller]")]`. Five actions mapped to the endpoints above. `GetUserId()` helper plus the four private error-envelope helpers (`UnauthorizedError`, `ForbiddenError`, `NotFoundError`, `ValidationError`) copied from `AccountsController` for consistency. |

---

## 6. Files to Modify

| File | What changes |
|------|-------------|
| `BudgetPilot API/Data/AppDbContext.cs` | Uncomment / add `public DbSet<CategoriesOBJ> Categories { get; set; }`. Leave the `EnableRetryOnFailure()` configuration untouched. Do **not** run `dotnet ef migrations add` — schema is already in the DB. |
| `BudgetPilot API/Program.cs` | Register `builder.Services.AddScoped<CategoriesService>();` next to the existing `AccountsService` registration. No auth or pipeline changes required — JWT is already wired from the Users module. |

---

## 7. Implementation Order

1. Re-read `specs/dbschema.md` (the `categories` table section) and `specs/contracts/accounts-api.md` (reference for the established pattern).
2. Create `Entities/CategoriesOBJ.cs` — four properties (`Id`, `UserId`, `Name`, `Type`) with `[Column]` attributes, `[JsonIgnore]` on `UserId`, XML docs. No `CreatedAt`.
3. Create `Dtos/CategoriesDTO.cs` — `Name` and `Type` with validation attributes per the rules above. No `Id` (server-generated), no `UserId` (from JWT).
4. Update `Data/AppDbContext.cs` — add the `Categories` DbSet. Preserve the `EnableRetryOnFailure()` retry strategy and existing DbSets.
5. Create `Services/CategoriesService.cs` — `GetCategories` (paged/filtered), `GetCategoryById`, `CreateCategory` (with uniqueness check → throw `'CategoryConflictException'` or return a sentinel; controller maps to 409), `UpdateCategory` (uniqueness check excluding self), `DeleteCategory` (with the transactions TODO). All async.
6. Create `Controllers/CategoriesController.cs` — five `[Http*]` actions, `[Authorize]` on the class, `GetUserId()` + the four private error helpers. Map service conflict signals to `409` with `errors: []`.
7. Register `CategoriesService` in `Program.cs` next to `AccountsService`.
8. Run `dotnet build "BudgetPilot API/BudgetPilot API.csproj"` — must compile with zero errors before the task is reported complete.