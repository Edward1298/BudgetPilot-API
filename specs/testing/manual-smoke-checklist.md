# BudgetPilot API — Manual Smoke-Test Checklist

> **Prerequisites:** A running API instance connected to the local SQL Server database. This checklist verifies things automation cannot easily assert, such as Swagger UI behavior, visual response inspection, and real network flow.

## 1. Swagger UI

- [ ] Navigate to `http://localhost:5253/swagger` (http profile).
- [ ] The Swagger page loads without JavaScript errors.
- [ ] Click **Authorize**, enter a JWT token, and click **Authorize** again.
- [ ] The lock icon appears closed on protected endpoints.

## 2. Users Module

- [ ] `POST /api/v1/users/register` with valid payload returns `201 Created` and a response body without `passwordHash`.
- [ ] `POST /api/v1/users/register` with a duplicate email returns `409 Conflict` with `{ statusCode, message, errors: [] }`.
- [ ] `POST /api/v1/users/login` returns `200 OK` with `{ token, tokenType, expiresAt }`.
- [ ] `GET /api/v1/users/me` with the token returns the current user object.
- [ ] `GET /api/v1/users/me` without a token returns `401 Unauthorized`.

## 3. Accounts Module

- [ ] `POST /api/v1/accounts` creates an account and sets the `Location` header.
- [ ] `POST /api/v1/accounts` with negative balance on `cash` returns `400` with `field: "balance"`.
- [ ] `POST /api/v1/accounts` with negative balance on `creditCard` succeeds.
- [ ] `GET /api/v1/accounts` returns `{ data, page, pageSize, totalCount }`.
- [ ] `GET /api/v1/accounts/{id}` for another user's account returns `403 Forbidden`.
- [ ] `DELETE /api/v1/accounts/{id}` with linked transactions returns `409 Conflict`.

## 4. Categories Module

- [ ] `POST /api/v1/categories` with `type: "expense"` returns `201 Created`.
- [ ] `POST /api/v1/categories` with duplicate `(name, type)` returns `409 Conflict`.
- [ ] `POST /api/v1/categories` with `type: "Income"` returns `400` (strict lowercase).
- [ ] `GET /api/v1/categories?type=income` only returns income categories.
- [ ] `DELETE /api/v1/categories/{id}` with linked transactions returns `409 Conflict`.

## 5. Transactions Module

- [ ] `POST /api/v1/transactions` returns `201 Created` with `date` set to today (UTC).
- [ ] Response body does NOT contain `userId`.
- [ ] `POST /api/v1/transactions` with `type` mismatching category returns `400` with `field: "type"`.
- [ ] After creating an expense, `GET /api/v1/accounts/{id}` shows balance decreased.
- [ ] After creating an income, `GET /api/v1/accounts/{id}` shows balance increased.
- [ ] `PUT /api/v1/transactions/{id}` updates amount/account and balances are recomputed.
- [ ] `DELETE /api/v1/transactions/{id}` restores the account balance.

## 6. Error Envelope

- [ ] Every `400` response follows `{ statusCode: 400, message: "Validation failed.", errors: [{ field, message }] }`.
- [ ] Every `401`, `403`, `404`, `409` response follows `{ statusCode, message, errors: [] }`.
- [ ] No response includes `passwordHash`.

## 7. REST Client / `.http` File

- [ ] Open `BudgetPilot API/BudgetPilot API.http` in VS Code REST Client.
- [ ] Set variables (baseUrl, token) and execute a register + login flow.
- [ ] Execute a chained create-account + create-category + create-transaction flow.

## 8. Full Real-User Flow

- [ ] Register a new user.
- [ ] Login and copy the token.
- [ ] Create a `cash` account with balance 100.
- [ ] Create an `expense` category "Food".
- [ ] Create a 25 expense transaction for "Lunch".
- [ ] Verify account balance is 75.
- [ ] Delete the transaction.
- [ ] Verify account balance is back to 100.
- [ ] Delete the account and category successfully.

---

**Result:** ☐ Pass / ☐ Fail
