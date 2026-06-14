# Users API — Contract Specification
**Module:** Users | **Base Path:** `/api/v1/users` | **Auth:** Public (register/login) + Bearer JWT (protected)

---

## Implementation Prerequisites

Before writing any code for this module, load these agent skills in order:

| # | Skill | Why needed |
|---|-------|------------|
| 1 | `aspnet-core` | Controller patterns, JWT auth middleware (`security-and-identity.md`), DI configuration (`program-and-pipeline.md`), API conventions (`apis-minimal-and-controllers.md`) |
| 2 | `csharp-async` | All service methods touching the database MUST be async and never block |
| 3 | `csharp-docs` | XML `<summary>` comments on every class, method, and property |

---

## 1. Entity Schema — `User`

Mapped to the `users` table as defined in `specs/dbschema.md`.

| Field        | Type                    | Required | DB Column       | Description                                      |
|--------------|-------------------------|----------|-----------------|--------------------------------------------------|
| id           | string (UUID v4)        | auto     | `id`            | Unique identifier generated server-side          |
| name         | string                  | no       | `name`          | Display name                                     |
| email        | string                  | no       | `email`         | Email address. UNIQUE constraint in DB           |
| passwordHash | string                  | server   | `password_hash` | BCrypt hash. NEVER exposed in any API response   |
| createdAt    | string (ISO 8601)       | server   | `created_at`    | Timestamp of registration                        |

### 1.1 Response vs Database Fields

| Field        | In DB | In API response      |
|--------------|-------|----------------------|
| id           | ✅    | ✅                   |
| name         | ✅    | ✅                   |
| email        | ✅    | ✅                   |
| passwordHash | ✅    | ❌ (`[JsonIgnore]`)  |
| createdAt    | ✅    | ✅                   |

---

## 2. Endpoints

### 2.1 Register User `POST /api/v1/users/register`

Creates a new user account. The password is hashed with BCrypt before storage.

**Request Body** *(application/json, camelCase)*

```json
{
  "name": "John Doe",
  "email": "john@example.com",
  "password": "securePass123"
}
```

| Field    | Type   | Required | Validation                                    |
|----------|--------|----------|-----------------------------------------------|
| name     | string | yes      | 1–100 characters, not whitespace-only         |
| email    | string | yes      | Valid email format, must be unique in the system |
| password | string | yes      | 8–128 characters                              |

**Response `201 Created`**
```json
{
  "id": "a1b2c3d4-...",
  "name": "John Doe",
  "email": "john@example.com",
  "createdAt": "2026-06-11T10:00:00Z"
}
```
- `Location` header set to `/api/v1/users/{id}`

**Error Responses**

| Code | When                                         |
|------|----------------------------------------------|
| 400  | Validation errors (missing fields, bad email) |
| 409  | Email address is already registered           |

**Example `400 Bad Request`**
```json
{
  "statusCode": 400,
  "message": "Validation failed.",
  "errors": [
    { "field": "email", "message": "A valid email address is required." },
    { "field": "password", "message": "Password must be at least 8 characters." }
  ]
}
```

**Example `409 Conflict`**
```json
{
  "statusCode": 409,
  "message": "A user with this email already exists.",
  "errors": []
}
```

---

### 2.2 Login `POST /api/v1/users/login`

Authenticates a user with email and password. Returns a JWT access token on success.

**Request Body** *(application/json, camelCase)*

```json
{
  "email": "john@example.com",
  "password": "securePass123"
}
```

| Field    | Type   | Required | Validation       |
|----------|--------|----------|------------------|
| email    | string | yes      | Must not be empty |
| password | string | yes      | Must not be empty |

**Response `200 OK`**
```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "tokenType": "Bearer",
  "expiresAt": "2026-06-12T10:00:00Z"
}
```

**Error Responses**

| Code | When                                         |
|------|----------------------------------------------|
| 400  | Validation errors (empty email or password)  |
| 401  | Invalid email or password                    |

**Example `401 Unauthorized`**
```json
{
  "statusCode": 401,
  "message": "Invalid email or password.",
  "errors": []
}
```

---

### 2.3 List Users `GET /api/v1/users`

Returns a paginated list of users with optional filters. Useful for searching by name or email.

**Auth:** Bearer JWT (required)

**Query Parameters**

| Param    | Type    | Required | Default | Description              |
|----------|---------|----------|---------|--------------------------|
| name     | string  | no       | —       | Partial match on name    |
| email    | string  | no       | —       | Partial match on email   |
| page     | integer | no       | 1       | Page number (1-based)    |
| pageSize | integer | no       | 20      | Items per page (1–100)   |

> If both `name` and `email` are provided, results must match both filters (AND logic).
> Results are ordered by `createdAt` descending (newest first).

**Response `200 OK`**
```json
{
  "data": [
    {
      "id": "a1b2c3d4-...",
      "name": "John Doe",
      "email": "john@example.com",
      "createdAt": "2026-06-11T10:00:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1
}
```

**Error Responses**

| Code | When                               |
|------|------------------------------------|
| 401  | Missing / invalid / expired JWT    |

---

### 2.4 Get User by ID `GET /api/v1/users/{id}`

Retrieves a single user by their unique identifier.

**Auth:** Bearer JWT (required)

**Path Parameters**

| Param | Type          | Description             |
|-------|---------------|-------------------------|
| id    | string (UUID) | User unique identifier  |

**Response `200 OK`**
```json
{
  "id": "a1b2c3d4-...",
  "name": "John Doe",
  "email": "john@example.com",
  "createdAt": "2026-06-11T10:00:00Z"
}
```

**Error Responses**

| Code | When                               |
|------|------------------------------------|
| 401  | Missing / invalid / expired JWT    |
| 404  | User not found                     |

---

### 2.5 Get Current User `GET /api/v1/users/me`

Returns the profile of the currently authenticated user. The user ID is inferred from the JWT token.

**Auth:** Bearer JWT (required)

**Response `200 OK`**
```json
{
  "id": "a1b2c3d4-...",
  "name": "John Doe",
  "email": "john@example.com",
  "createdAt": "2026-06-11T10:00:00Z"
}
```

**Error Responses**

| Code | When                                                    |
|------|---------------------------------------------------------|
| 401  | Missing / invalid / expired JWT                         |
| 404  | User from JWT no longer exists in the database          |

---

## 3. Endpoints Summary

| Method | Path                    | Auth   | Purpose              |
|--------|-------------------------|--------|----------------------|
| POST   | /api/v1/users/register  | public | Create user account  |
| POST   | /api/v1/users/login     | public | Authenticate, get JWT |
| GET    | /api/v1/users           | JWT    | List users (filtered)|
| GET    | /api/v1/users/{id}      | JWT    | Get user by ID       |
| GET    | /api/v1/users/me        | JWT    | Get current profile  |

---

## 4. JWT Configuration

### 4.1 appsettings.Development.json

Add a `Jwt` section (gitignored, use a key at least 32 characters):

```json
{
  "Jwt": {
    "Key": "YOUR_DEV_SECRET_KEY_AT_LEAST_32_CHARS",
    "Issuer": "BudgetPilotAPI",
    "Audience": "BudgetPilotClients",
    "ExpirationMinutes": 1440
  }
}
```

### 4.2 Program.cs Changes

1. Bind `Jwt` configuration section to a strongly-typed options class
2. Register `AddAuthentication().AddJwtBearer()` with the key, issuer, and audience
3. Register `AddAuthorization()`
4. Call `app.UseAuthentication()` **before** `app.UseAuthorization()` in the pipeline

> **Security:** The JWT key must be at least 32 characters. Never commit production secrets. The development key lives in `appsettings.Development.json` which is already gitignored.

---

## 5. Cross-Cutting Rules

1. **Password hashing** — registration hashes with `BCrypt.Net.BCrypt.HashPassword()`. Login verifies with `BCrypt.Net.BCrypt.Verify()`. Never re-hash and compare strings.
2. **PasswordHash isolation** — the `PasswordHash` property on `UsersOBJ` must have `[JsonIgnore]` so it is never serialized in any API response.
3. **Email uniqueness** — check for existing email before insert. The database `UNIQUE` constraint on `email` serves as the final safety net.
4. **Email normalization** — emails MUST be trimmed and lowercased before storage and before comparison (login and duplicate check). This prevents `John@Example.com` and `john@example.com` from being treated as different users.
5. **Error envelope** — all error responses follow `{ statusCode, message, errors[] }`. On non-validation errors (401, 409), `errors` is an empty array `[]`.
6. **Route migration** — the existing `[Route("api/[controller]")]` must be changed to `[Route("api/v1/[controller]")]`.
7. **No refresh tokens** — the JWT issued at login is a single access token. Refresh token logic is deferred to future scope.
8. **Request body binding** — all POST endpoints MUST use `[FromBody]` on the DTO parameter to ensure correct model binding.

---

## 6. Files to Create

| File | Purpose |
|------|---------|
| `Dtos/LoginDTO.cs` | Request DTO for login (email + password) with DataAnnotations |
| `Dtos/RegisterDTO.cs` | Request DTO for registration (name + email + password) with DataAnnotations |

## 7. Files to Modify

| File | What changes |
|------|-------------|
| `Entities/UsersOBJ.cs` | Add `[JsonIgnore]` on `PasswordHash`. Update XML comments. (Columns already match the DB.) |
| `Services/UsersService.cs` | Add `Register()` (duplicate email check + BCrypt hash), `Login()` (BCrypt verify + JWT generation), `GetUsers()` (with optional name/email filters), `GetUserById()`, `GetCurrentUser()`. |
| `Controllers/UsersController.cs` | Replace existing GET/POST with 5 endpoints. Update route to `api/v1/[controller]`. Add `[Authorize]` on protected endpoints. Error envelope on all responses. |
| `Program.cs` | Add JWT options binding, `AddAuthentication().AddJwtBearer()`, `app.UseAuthentication()`. |

## 8. Files to Delete

| File | Reason |
|------|--------|
| `Dtos/UsersDTO.cs` | Replaced by `RegisterDTO.cs` (registration) and `LoginDTO.cs` (login). The old combined DTO is no longer used by any endpoint. |

---

## 9. Implementation Order

1. **Create DTOs** — `LoginDTO.cs` and `RegisterDTO.cs`
2. **Update entity** — Add `[JsonIgnore]` to `UsersOBJ.PasswordHash`
3. **Rewrite service** — Implement Register, Login, GetUsers (with filters), GetUserById, GetCurrentUser
4. **Rewrite controller** — v1 route, 5 endpoints, auth attributes, error envelope
5. **Configure JWT** — Program.cs middleware and appsettings
6. **Delete obsolete DTO** — Remove `UsersDTO.cs`
7. **Run `dotnet build`** — verify zero errors
