<p align="center">
  <picture>
    <source media="(prefers-color-scheme: dark)" srcset="https://img.shields.io/badge/.NET_10-512BD4?logo=dotnet&logoColor=fff">
    <img alt=".NET 10" src="https://img.shields.io/badge/.NET_10-512BD4?logo=dotnet&logoColor=fff">
  </picture>
  <img src="https://img.shields.io/badge/EF_Core_10-512BD4?logo=entity-framework&logoColor=fff" alt="EF Core 10">
  <img src="https://img.shields.io/badge/SQL_Server-CC2927?logo=microsoft-sql-server&logoColor=fff" alt="SQL Server">
  <img src="https://img.shields.io/badge/Docker-2496ED?logo=docker&logoColor=fff" alt="Docker">
  <img src="https://img.shields.io/badge/JWT_Auth-000000?logo=json-web-tokens&logoColor=fff" alt="JWT Auth">
  <img src="https://img.shields.io/badge/xUnit-006B3F?logo=xunit&logoColor=fff" alt="xUnit">
  <br>
  <img src="https://img.shields.io/badge/status-active-success" alt="Status: Active">
  <img src="https://img.shields.io/badge/license-MIT-blue" alt="License: MIT">
</p>

# BudgetPilot API

A production-ready REST API for personal finance management — built with ASP.NET Core 10, Entity Framework Core 10, and SQL Server. Designed for front-end clients, mobile apps, and AI agents.

---

## Features

| Module | Endpoints | Highlights |
|--------|-----------|------------|
| **Users** | `POST register`, `POST login`, `POST refresh`, `POST logout`, `GET /me`, `GET /{id}`, `PUT /{id}`, `DELETE /{id}` | BCrypt password hashing, JWT access + refresh tokens, admin/user roles, soft delete |
| **Accounts** | Full CRUD + `GET /{id}/summary` | Cash, credit card, bank account types; pagination + filters; stored-procedure-driven summary |
| **Categories** | Full CRUD | Income/expense types, uniqueness per user+name+type, pagination |
| **Transactions** | Full CRUD | Auto-balance update (income adds, expense subtracts), type derived from category, server-set immutable date, admin hard-delete reverses balance |
| **Cards** | Full CRUD | AES-256-CBC encrypted card number & CVC, smart defaults from config, debit/credit types |
| **Roles** | Full CRUD | Admin-only controller, seeded Admin/User roles |
| **Admin** | `POST apply-monthly-interest` | Stored procedure updates savings account balances |

### Cross-Cutting

- **Consistent error envelope** — every response follows `{ statusCode, message, errors[] }` with camelCase field names on validation errors
- **Ownership isolation** — all data scoped to authenticated user via JWT claims; 403 vs 404 correctly distinguished to avoid leaking entity existence
- **Soft delete** — non-admin users set `is_active = false`; admins hard-delete (transactions balance reversal only on admin delete)
- **Encryption at rest** — sensitive card data encrypted with AES-256-CBC via ASP.NET Core Data Protection
- **Stored procedures** — ADO.NET calls to `sp_GetAccountSummary` (two result sets) and `sp_ApplyMonthlyInterest` for performance-critical paths
- **Role-based authorization** — `[Authorize(Roles = "Admin")]` on admin endpoints

---

## Tech Stack

| Layer | Technology |
|-------|-----------|
| Runtime | .NET 10.0 (C#, nullable enabled, implicit usings) |
| ORM | Entity Framework Core 10.0.5 |
| Database | SQL Server (local or Docker) |
| Auth | JWT Bearer tokens + Refresh tokens |
| Password Hashing | BCrypt.Net-Next 4.1.0 |
| Card Encryption | ASP.NET Core Data Protection (AES-256-CBC) |
| Testing | xUnit 2.9.3, FluentAssertions 8.0, WebApplicationFactory, SQLite in-memory |
| API Docs | Swashbuckle / Swagger UI |
| Containerization | Docker, Docker Compose (multi-stage build) |

---

## Architecture

```
Client → [JWT] → Controller → Service → AppDbContext → SQL Server
```

- **No repository pattern** — services inject `AppDbContext` directly
- **Naming convention** — C# PascalCase → JSON camelCase → SQL snake_case via `[Table]` and `[Column]` attributes
- **Entity suffix `OBJ`**, **DTO suffix `DTO`** — consistent naming across the codebase
- **Spec-driven development** — every module is designed from a written contract in `specs/contracts/` before implementation

```
BudgetPilotAPI/
├── Controllers/        # HTTP layer — [ApiController], [Authorize], error helpers
├── Services/           # Business logic — directly injects AppDbContext
├── Data/               # AppDbContext — EF Core configuration
├── Entities/           # Domain models — mapped to DB tables via attributes
├── Dtos/               # Request/response DTOs — DataAnnotations validation
├── Program.cs          # Bootstrap, DI, JWT config, middleware pipeline
└── Properties/         # launchSettings
```

---

## Quick Start

### Docker (recommended)

```powershell
docker compose up -d
```

This starts three containers:
1. **SQL Server 2022** — database engine (port 1433)
2. **db-setup** — runs the schema script and exits
3. **BudgetPilot API** — the application (port 5253)

The API is available at `http://localhost:5253`. Swagger UI at `http://localhost:5253/swagger`.

### Local Development

1. Run `scripts/sql_final.sql` in SQL Server Management Studio to create the `BudgetPilot` database
2. Configure `ConnectionStrings:DefaultConnection` in `appsettings.Development.json` (windows auth or SQL auth)
3. Set a `Jwt:Key` (minimum 32 characters) and `Encryption:Key` (32-byte base64)
4. Start the API:

```powershell
dotnet run --project BudgetPilotAPI --launch-profile http
```

---

## API Overview

All endpoints are prefixed with `/api/v1`. Protected endpoints require `Authorization: Bearer <token>`.

| Method | Path | Auth | Description |
|--------|------|------|-------------|
| POST | `/api/v1/users/register` | Public | Create account |
| POST | `/api/v1/users/login` | Public | Authenticate, returns JWT + refresh token |
| POST | `/api/v1/users/refresh` | Public | Exchange refresh token for new JWT pair |
| POST | `/api/v1/users/logout` | JWT | Revoke refresh token |
| GET | `/api/v1/users/me` | JWT | Current user profile |
| GET | `/api/v1/users` | Admin | List all users (paginated, filterable) |
| GET | `/api/v1/users/{id}` | Admin | Get user by ID |
| PUT | `/api/v1/users/{id}` | JWT | Update profile (self or admin) |
| DELETE | `/api/v1/users/{id}` | JWT | Delete account (soft/hard depending on role) |
| GET | `/api/v1/accounts` | JWT | List user accounts (paginated, filterable) |
| GET | `/api/v1/accounts/{id}` | JWT | Get account by ID |
| GET | `/api/v1/accounts/{id}/summary` | JWT | Account details + last 10 transactions (via SP) |
| POST | `/api/v1/accounts` | JWT | Create account |
| PUT | `/api/v1/accounts/{id}` | JWT | Update account |
| DELETE | `/api/v1/accounts/{id}` | JWT | Delete account (409 if linked transactions) |
| GET | `/api/v1/categories` | JWT | List categories (paginated, filterable) |
| POST | `/api/v1/categories` | JWT | Create category (409 if duplicate) |
| PUT | `/api/v1/categories/{id}` | JWT | Update category |
| DELETE | `/api/v1/categories/{id}` | JWT | Delete category (409 if linked transactions) |
| GET | `/api/v1/transactions` | JWT | List transactions (paginated, multi-filter) |
| POST | `/api/v1/transactions` | JWT | Create transaction (auto-updates balance) |
| PUT | `/api/v1/transactions/{id}` | JWT | Update transaction (reverses + re-applies balance) |
| DELETE | `/api/v1/transactions/{id}` | JWT | Delete transaction (reverses balance) |
| GET | `/api/v1/cards` | JWT | List user cards |
| POST | `/api/v1/cards` | JWT | Create card (encrypted storage) |
| PUT | `/api/v1/cards/{id}` | JWT | Update card |
| DELETE | `/api/v1/cards/{id}` | JWT | Soft-delete card |
| POST | `/api/v1/admin/apply-monthly-interest` | Admin | Apply interest to savings accounts (via SP) |

---

## Testing

The project includes **30+ integration tests** covering the full API surface.

| Tool | Usage |
|------|-------|
| xUnit | Test framework |
| WebApplicationFactory<Program> | In-memory test server |
| SQLite (in-memory) | Stand-in for SQL Server in tests |
| FluentAssertions | Readable assertions |
| TestUserFixture | Helper for user registration + JWT acquisition |

```powershell
dotnet test "BudgetPilot API.slnx"
```

Test categories:
- **Unit tests** — isolated service logic
- **Integration tests** — full HTTP request/response cycle via `WebApplicationFactory`
- **End-to-end flow tests** — multi-step scenarios (register → login → create account → create transaction)

---

## Project Structure

```
BudgetPilot API/
├── BudgetPilotAPI/                 # Main application
│   ├── Controllers/                # API endpoints
│   │   ├── UsersController.cs
│   │   ├── AccountsController.cs
│   │   ├── CategoriesController.cs
│   │   ├── TransactionsController.cs
│   │   ├── CardsController.cs
│   │   ├── RolesController.cs
│   │   ├── AdminController.cs
│   │   ├── ReportsController.cs
│   │   └── BudgetsController.cs
│   ├── Services/                   # Business logic
│   │   ├── UserService.cs
│   │   ├── AccountsService.cs
│   │   ├── CategoriesService.cs
│   │   ├── TransactionsService.cs
│   │   ├── CardsService.cs
│   │   ├── RolesService.cs
│   │   ├── StoredProcedureService.cs
│   │   ├── DataProtectionService.cs
│   │   ├── BudgetsService.cs
│   │   └── ReportsService.cs
│   ├── Entities/                   # Domain models (suffix: OBJ)
│   ├── Dtos/                       # Data transfer objects (suffix: DTO)
│   ├── Data/
│   │   └── AppDbContext.cs
│   ├── Program.cs                  # App bootstrap
│   └── appsettings*.json
├── BudgetPilotAPI.Tests/           # Test project
│   ├── Integration/                # WebApplicationFactory-based tests
│   ├── Unit/                       # Isolated service tests
│   └── Fixtures/                   # Test helpers
├── specs/                          # Spec-driven development docs
│   ├── PRODUCT.md
│   ├── ARCHITECTURE.md
│   ├── dbschema.md
│   └── contracts/                  # Per-module API contracts
├── scripts/                        # SQL setup + migration scripts
├── Dockerfile                      # Multi-stage build
├── docker-compose.yml              # Full environment
└── AGENTS.md                       # AI-assisted dev conventions
```

---

## Why This Project Matters

From a hiring perspective, BudgetPilot API demonstrates:

- **Security-first mindset** — JWT authentication with refresh token rotation, BCrypt for passwords (not plain SHA), AES-256-CBC encryption for PCI-sensitive card data
- **Production-quality testing** — integration tests that exercise the full HTTP pipeline with a real in-memory database, not just unit tests with mocks
- **Clean architecture decisions** — explicit choice to skip the repository anti-pattern, clear separation of concerns (Controller → Service → DbContext), consistent response envelopes
- **Edge case awareness** — ownership checks that distinguish 403 from 404 (no information leakage), soft-delete vs hard-delete by role, FK conflict detection on deletes (409)
- **Developer experience** — Docker Compose one-command setup, Swagger documentation, spec-driven contracts that serve as living documentation
- **Modern .NET** — targets .NET 10 with nullable reference types, uses the latest EF Core, follows current ASP.NET Core conventions

---

## License

[MIT](LICENSE)
