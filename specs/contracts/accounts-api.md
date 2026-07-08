# Accounts API — Contract Specification
**Module:** Accounts | **Base Path:** `/api/v1/accounts` | **Auth:** Bearer JWT (all endpoints)

---

## Implementation Prerequisites

Before writing any code for this module, load these agent skills in order:

| # | Skill | Why needed |
|---|-------|------------|
| 1 | `aspnet-core` | Controller patterns, dependency injection, middleware pipeline, API conventions (`apis-minimal-and-controllers.md`), EF Core integration (`data-state-and-services.md`), auth (`security-and-identity.md`) |
| 2 | `csharp-async` | All service methods and controller actions that touch the database MUST be async, return `Task<T>`, and never block with `.Result` or `.Wait()` |
| 3 | `csharp-docs` | Every Controller, Service, DTO, and Entity class/member requires XML `<summary>` comments following the csharp-docs conventions |

---

## 1. Entity Schema — `Account`

Mapped to the `accounts` table as defined in `specs/dbschema.md`.

| Field     | Type                    | Required | DB Column    | Description                                    |
|-----------|-------------------------|----------|--------------|------------------------------------------------|
| id        | string (UUID v4)        | auto     | `id`         | Unique identifier generated server-side        |
| userId    | string (UUID v4)        | server   | `user_id`    | Owner. FK → users(id). Derived from JWT, never exposed to clients |
| name      | string                  | yes      | `name`       | Human-readable account label (e.g. "BBVA Debit") |
| type      | string                  | yes      | `type`       | Cash, Credit Card, Bank Account                |
| balance   | number (decimal)        | optional | `balance`    | Current balance. Defaults to `0` on creation   |
| createdAt | string (ISO 8601)       | server   | `created_at` | Timestamp of creation                          |

### 1.1 `type` Values

The `type` column stores camelCase values validated by the API:

| Value            | Description    |
|------------------|----------------|
| `cash`           | Physical money |
| `creditCard`     | Credit card    |
| `bankAccount`    | Bank account   |

> **Note:** The API accepts and stores these exact lowercase/camelCase values. They are serialized as-is in JSON responses.

---

## 2. Endpoints

### 2.1 List Accounts `GET /api/v1/accounts`

Returns a paginated list of accounts belonging to the authenticated user.

**Query Parameters**

| Param    | Type   | Required | Default | Description              |
|----------|--------|----------|---------|--------------------------|
| page     | int    | no       | 1       | Page number (≥ 1)        |
| pageSize | int    | no       | 20      | Items per page (1–100)   |
| type     | string | no       | —       | Filter by account type   |
| search   | string | no       | —       | Partial match on `name`  |

**Response `200 OK`**
```json
{
  "data": [
    {
      "id": "a1b2c3d4-...",
      "name": "BBVA Debit",
      "type": "Bank Account",
      "balance": 1250.75,
      "createdAt": "2026-06-10T14:30:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1
}
```

**Error Responses**

| Code | When                                         |
|------|----------------------------------------------|
| 401  | Missing / invalid / expired JWT              |

---

### 2.2 Get Account by ID `GET /api/v1/accounts/{id}`

**Path Parameters**

| Param | Type            | Description             |
|-------|-----------------|-------------------------|
| id    | string (UUID)   | Account unique identifier |

**Response `200 OK`**
```json
{
  "id": "a1b2c3d4-...",
  "name": "BBVA Debit",
  "type": "Bank Account",
  "balance": 1250.75,
  "createdAt": "2026-06-10T14:30:00Z"
}
```

**Error Responses**

| Code | When                                         |
|------|----------------------------------------------|
| 401  | Missing / invalid / expired JWT              |
| 403  | Account belongs to a different user          |
| 404  | Account not found                            |

---

### 2.3 Create Account `POST /api/v1/accounts`

**Request Body** *(application/json, camelCase)*

```json
{
  "name": "BBVA Debit",
  "type": "Bank Account",
  "balance": 0
}
```

| Field   | Type            | Required | Validation                                |
|---------|-----------------|----------|-------------------------------------------|
| name    | string          | yes      | 1–100 characters, not whitespace-only     |
| type    | string          | yes      | Must be `cash`, `creditCard`, or `bankAccount` |
| balance | number (decimal)| no       | Defaults to `0`. Must be ≥ 0 for `cash` and `bankAccount` |

> **Note:** `userId` is extracted from the JWT token and assigned server-side. The client must never supply it.

**Response `201 Created`**
```json
{
  "id": "a1b2c3d4-...",
  "name": "BBVA Debit",
  "type": "Bank Account",
  "balance": 0,
  "createdAt": "2026-06-10T14:30:00Z"
}
```
- `Location` header set to `/api/v1/accounts/{id}`

**Error Responses**

| Code | When                                         |
|------|----------------------------------------------|
| 400  | Validation errors (see error format below)   |
| 401  | Missing / invalid / expired JWT              |

**Example `400 Bad Request`**
```json
{
  "statusCode": 400,
  "message": "Validation failed.",
  "errors": [
    { "field": "name", "message": "Name is required and must not exceed 100 characters." },
    { "field": "type", "message": "Type must be a valid account type." }
  ]
}
```

---

### 2.4 Update Account `PUT /api/v1/accounts/{id}`

Full replacement update. All required fields must be supplied.

**Request Body** *(application/json, camelCase)*

```json
{
  "name": "BBVA Debit Updated",
  "type": "Bank Account",
  "balance": 500.00
}
```

| Field   | Type            | Required | Validation                                |
|---------|-----------------|----------|-------------------------------------------|
| name    | string          | yes      | 1–100 characters, not whitespace-only     |
| type    | string          | yes      | Must be `cash`, `creditCard`, or `bankAccount` |
| balance | number (decimal)| yes      | Must be ≥ 0 for `cash` and `bankAccount`  |

**Response `200 OK`**
```json
{
  "id": "a1b2c3d4-...",
  "name": "BBVA Debit Updated",
  "type": "Bank Account",
  "balance": 500.00,
  "createdAt": "2026-06-10T14:30:00Z"
}
```

**Error Responses**

| Code | When                                         |
|------|----------------------------------------------|
| 400  | Validation errors                            |
| 401  | Missing / invalid / expired JWT              |
| 403  | Account belongs to a different user          |
| 404  | Account not found                            |

---

### 2.5 Delete Account `DELETE /api/v1/accounts/{id}`

Permanently deletes the account from the database after verifying ownership.

**Response `204 No Content`**
- Empty body. No content returned on successful deletion.

**Error Responses**

| Code | When                                         |
|------|----------------------------------------------|
| 401  | Missing / invalid / expired JWT              |
| 403  | Account belongs to a different user          |
| 404  | Account not found                            |
| 409  | Account has linked transactions — cannot be deleted while transactions reference it (*pending Transactions module*) |

---

## 3. Endpoints Summary

| Method | Path                       | Auth | Purpose        |
|--------|----------------------------|------|----------------|
| GET    | /api/v1/accounts           | yes  | List accounts  |
| GET    | /api/v1/accounts/{id}      | yes  | Get by ID      |
| POST   | /api/v1/accounts           | yes  | Create account |
| PUT    | /api/v1/accounts/{id}      | yes  | Update account |
| DELETE | /api/v1/accounts/{id}      | yes  | Delete account |

---

## 4. Cross-Cutting Rules

1. **Ownership isolation** — every endpoint scopes data to the authenticated user. `userId` is always inferred from the JWT.
2. **Default balance** — for `cash` and `bankAccount`, balance defaults to `0` and cannot be negative. For `creditCard`, negative balance is allowed (representing debt).
3. **Conflict detection (409)** — DELETE returns 409 if the account is referenced by at least one transaction. This check is pending the Transactions module implementation (currently marked as a TODO in the service).
4. **Error envelope** — all error responses follow the shape `{ statusCode, message, errors[] }` where `errors` is an array of `{ field, message }` objects. On non‑validation errors (401, 403, 404, 409), `errors` is an empty array `[]`.
5. **No soft delete** — the `accounts` table in the database does not have soft-delete columns. Deletion is permanent. Soft delete may be added as a future enhancement via a schema migration.
