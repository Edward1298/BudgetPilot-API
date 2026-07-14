# BudgetPilot API — Budgets Contract

**Status:** Future Scope | **Phase:** 8 | **Owner:** Development

---

## Overview

The `budgets` table and entity (`BudgetsOBJ`) exist in the codebase. This contract documents the intended API surface for when budget endpoints are implemented.

> **Current state:** Entity + DbSet are registered. No endpoints, DTOs, service, or controller exist yet.

---

## Entity

**`Entities/BudgetsOBJ.cs`** — already implemented.

| Property | Column | Type | Notes |
|----------|--------|------|-------|
| `Id` | `id` | `Guid` | PK, auto-generated |
| `UserId` | `user_id` | `Guid` | FK → users, `[JsonIgnore]` |
| `CategoryId` | `category_id` | `Guid` | FK → categories |
| `Amount` | `amount` | `decimal` | Budget limit |
| `Month` | `month` | `int` | 1–12 |
| `Year` | `year` | `int` | e.g. 2026 |
| `IsActive` | `is_active` | `bool` | Soft delete flag, default `true` |
| `CreatedAt` | `created_at` | `DateTime` | Auto-set |

---

## Intended Endpoints (Future)

When implemented, the budgets module will follow the same patterns as other modules:

### GET /api/v1/budgets

Paginated list of budgets for the authenticated user.

**Query params:** `page`, `pageSize`, `month?`, `year?`, `categoryId?`

**Response (200):**
```json
{
  "data": [
    {
      "id": "guid",
      "categoryId": "guid",
      "amount": 500.00,
      "month": 7,
      "year": 2026,
      "isActive": true,
      "createdAt": "2026-01-01T00:00:00Z"
    }
  ],
  "page": 1,
  "pageSize": 20,
  "totalCount": 1
}
```

### GET /api/v1/budgets/{id}

Single budget by ID.

### POST /api/v1/budgets

Create a new budget.

**Request body:**
```json
{
  "categoryId": "guid",
  "amount": 500.00,
  "month": 7,
  "year": 2026
}
```

**Validation:**
- `categoryId` must reference an existing category owned by the user.
- `amount` must be > 0.
- `month` must be 1–12.
- `year` must be a valid year.
- Duplicate check: one budget per (userId, categoryId, month, year).

### PUT /api/v1/budgets/{id}

Partial update of a budget.

### DELETE /api/v1/budgets/{id}

Soft/hard delete based on role (same pattern as other modules).

---

## Intended DTOs (Future)

### BudgetsDTO

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

### BudgetUpdateDTO

```csharp
public class BudgetUpdateDTO
{
    public Guid? CategoryId { get; set; }
    public decimal? Amount { get; set; }
    public int? Month { get; set; }
    public int? Year { get; set; }
}
```

---

## Intended Service (Future)

**`Services/BudgetsService.cs`**

| Method | Return Type | Description |
|--------|-------------|-------------|
| `GetBudgets(userId, page, pageSize, month?, year?, categoryId?)` | `(List<BudgetsOBJ>, int)` | Paginated, filtered |
| `GetBudgetById(id)` | `BudgetsOBJ?` | No ownership filter |
| `CreateBudget(dto, userId)` | `BudgetsOBJ` | Validates category ownership, duplicate check |
| `UpdateBudget(id, dto, userId)` | `BudgetsOBJ?` | Partial update |
| `DeleteBudget(id, userId, isAdmin)` | `(bool Deleted, bool HasConflict)` | Soft/hard delete |

---

## Database

The `budgets` table is created in `scripts/migration-v2.sql` (Step 8).

| Column | Type | Constraints |
|--------|------|-------------|
| `id` | `uniqueidentifier` | PK, DEFAULT NEWID() |
| `user_id` | `uniqueidentifier` | NOT NULL, FK → users(id) |
| `category_id` | `uniqueidentifier` | NOT NULL, FK → categories(id) |
| `amount` | `decimal(18,2)` | NOT NULL |
| `month` | `int` | NOT NULL (1–12) |
| `year` | `int` | NOT NULL |
| `is_active` | `bit` | NOT NULL DEFAULT 1 |
| `created_at` | `datetime2` | NOT NULL DEFAULT SYSUTCDATETIME() |

Indexes: `IX_budgets_user_id`, `IX_budgets_category_id`, `IX_budgets_is_active`.
