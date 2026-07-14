# BudgetPilot API — Stored Procedures Contract

**Status:** Active | **Phase:** 7 | **Owner:** Development

---

## Overview

Two stored procedures are exposed via REST endpoints for performance-critical operations:

1. **Account Summary** — returns account details + last 10 transactions in one round-trip.
2. **Apply Monthly Interest** — applies interest to all active savings accounts (admin only).

---

## Endpoints

### 1. GET /api/v1/accounts/{id}/summary

Returns account details and the last 10 active transactions in a single response.

**Auth:** `[Authorize]` — requires valid JWT.

**Path Parameters:**

| Parameter | Type   | Required | Description                          |
|-----------|--------|----------|--------------------------------------|
| `id`      | `guid` | Yes      | The account identifier to summarize. |

**Success Response (200):**

```json
{
  "account": {
    "id": "guid",
    "userId": "guid",
    "name": "Savings Account",
    "type": "savingsAccount",
    "balance": 1050.00,
    "interestRate": 2.50,
    "isActive": true,
    "createdAt": "2026-01-01T00:00:00Z"
  },
  "recentTransactions": [
    {
      "id": "guid",
      "accountId": "guid",
      "categoryId": "guid",
      "amount": 50.00,
      "type": "expense",
      "description": "Groceries",
      "date": "2026-07-01",
      "isActive": true
    }
  ]
}
```

**Error Responses:**

| Status | Condition                         | Body                                                                 |
|--------|-----------------------------------|----------------------------------------------------------------------|
| 401    | Missing or invalid JWT            | `{ statusCode: 401, message: "Authentication required.", errors: [] }` |
| 403    | Account belongs to another user   | `{ statusCode: 403, message: "You do not have access to this account.", errors: [] }` |
| 404    | Account not found or inactive     | `{ statusCode: 404, message: "Account not found.", errors: [] }`     |

**Implementation:**
- Controller calls `AccountsService.GetAccountById(id)` for ownership verification.
- Then calls `StoredProcedureService.GetAccountSummaryAsync(id, userId)`.
- The SP returns two result sets: account row + up to 10 transaction rows.

---

### 2. POST /api/v1/admin/apply-monthly-interest

Triggers the monthly interest calculation for all active savings accounts.

**Auth:** `[Authorize(Roles = "Admin")]` — admin only.

**Request Body:** None.

**Success Response (200):**

```json
{
  "rowsAffected": 5
}
```

**Error Responses:**

| Status | Condition                      | Body                                                                 |
|--------|--------------------------------|----------------------------------------------------------------------|
| 401    | Missing or invalid JWT         | `{ statusCode: 401, message: "Authentication required.", errors: [] }` |
| 403    | User is not an Admin           | `{ statusCode: 403, message: "Forbidden.", errors: [] }`             |

**Implementation:**
- Controller calls `StoredProcedureService.ApplyMonthlyInterestAsync()`.
- The SP updates `balance = balance + (balance * interest_rate / 100)` for all active savings accounts.
- Returns the number of rows affected.

---

## Stored Procedures (SQL)

### sp_GetAccountSummary

```sql
CREATE OR ALTER PROCEDURE dbo.sp_GetAccountSummary
    @AccountId UNIQUEIDENTIFIER,
    @UserId UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    SELECT id, user_id, name, type, balance, interest_rate, is_active, created_at
    FROM dbo.accounts
    WHERE id = @AccountId AND user_id = @UserId;

    SELECT TOP 10 id, user_id, account_id, category_id, amount, type, description, date, is_active
    FROM dbo.transactions
    WHERE account_id = @AccountId AND user_id = @UserId AND is_active = 1
    ORDER BY date DESC;
END
```

### sp_ApplyMonthlyInterest

```sql
CREATE OR ALTER PROCEDURE dbo.sp_ApplyMonthlyInterest
AS
BEGIN
    SET NOCOUNT ON;

    UPDATE dbo.accounts
    SET balance = balance + (balance * interest_rate / 100)
    WHERE type = 'savingsAccount'
      AND is_active = 1
      AND interest_rate IS NOT NULL
      AND interest_rate > 0;
END
```

---

## Service Layer

**`Services/StoredProcedureService.cs`**

| Method | Return Type | Description |
|--------|-------------|-------------|
| `GetAccountSummaryAsync(Guid accountId, Guid userId)` | `AccountSummaryDTO?` | Calls `sp_GetAccountSummary`, reads two result sets. Returns null if account not found. |
| `ApplyMonthlyInterestAsync()` | `ApplyInterestResultDTO` | Calls `sp_ApplyMonthlyInterest`, returns rows affected. |

**Dependency injection:** Registered as `AddScoped<StoredProcedureService>()` in `Program.cs`.

**Connection handling:** Uses `AppDbContext.Database.GetConnectionString()` to obtain the connection string, then opens a `SqlConnection` directly for ADO.NET calls.

---

## DTOs

### AccountSummaryDTO

```csharp
public class AccountSummaryDTO
{
    public AccountInfoDTO Account { get; set; }
    public List<TransactionInfoDTO> RecentTransactions { get; set; }
}

public class AccountInfoDTO
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; }
    public string Type { get; set; }
    public decimal Balance { get; set; }
    public decimal? InterestRate { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TransactionInfoDTO
{
    public Guid Id { get; set; }
    public Guid AccountId { get; set; }
    public Guid CategoryId { get; set; }
    public decimal Amount { get; set; }
    public string Type { get; set; }
    public string? Description { get; set; }
    public DateOnly Date { get; set; }
}
```

### ApplyInterestResultDTO

```csharp
public class ApplyInterestResultDTO
{
    public int RowsAffected { get; set; }
}
```
