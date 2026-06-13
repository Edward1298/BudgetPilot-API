# BudgetPilot API - Product Scope
Version 1.0 | Portfolio Project

---

## 1. Purpose
A REST API for personal finance management.
Any consumer (Swagger, Postman, mobile app, frontend, AI agent) can manage users, accounts, transactions, and categories through a clean, documented interface.

---

## 2. Target Audience
- Young professionals and freelancers (22-35) who want a simple finance tracker.
- Front-end developers looking to integrate a personal finance backend.
- AI agents that need structured financial data.
- Primary data language: English.

---

## 3. MVP Scope

### 3.1 Authentication
- User registration (email + password).
- Login returns a JWT access token (no refresh token in MVP).

### 3.2 Accounts
- CRUD for financial accounts.
- Supported types: Cash, Credit Card, Bank Account.
- Each account belongs to one user.

### 3.3 Categories
- CRUD for income/expense categories (e.g., Transport, Food, Salary).
- Scoped per user.
- NOTE: Categories are part of MVP because transactions reference them. Budget rules per category are deferred to the final product.

### 3.4 Transactions
- Record income and expense transactions.
- Each transaction links to an Account and a Category.
- Fields: amount, type (income/expense), date, description (optional), accountId, categoryId.

---

## 4. Future Scope
- Monthly budget limit per category.
- Simple account statement (balance + transaction history).
- Expense reports grouped by category and time period.
- JWT refresh token support.

---

## 5. Out of Scope (Do NOT implement)
- Automatic sync with real banks.
- Multi-currency support (single currency per user).
- Investments or stock portfolios.
- Third-party OAuth (Google, Apple, etc.).
- Push notifications or scheduled jobs.

---

## 6. Core Entities & Relationships

| Entity      | Key Fields                                              | Relationship                        |
|-------------|----------------------------------------------------------|-------------------------------------|
| User        | Id, Email, PasswordHash, Currency                       | Owns Accounts and Categories        |
| Account     | Id, Name, Type, Balance, UserId                         | Belongs to User; has Transactions   |
| Category    | Id, Name, Type (income/expense), UserId                 | Belongs to User; used by Transactions |
| Transaction | Id, Amount, Type, Date, Description, AccountId, CategoryId | Belongs to Account and Category  |
| Budget*     | Id, CategoryId, MonthYear, LimitAmount                  | Belongs to Category (*future)       |

---

## 7. API Conventions
- Base path: /api/v1/
- JSON format: camelCase
- Auth: Authorization: Bearer <token> on protected endpoints.
- Errors return: { statusCode, message, errors[] }
- Lists support: ?page=1&pageSize=20 with totalCount in response.

---

## 8. Related Documents
- ARCHITECTURE.md.txt — Full stack definition, versions, and infrastructure decisions.
