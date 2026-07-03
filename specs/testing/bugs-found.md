# BudgetPilot API — Bugs Found During QA

## Bug 1: `[ApiController]` returns default `ValidationProblemDetails` instead of the project error envelope

- **Module:** All (Users, Accounts, Categories, Transactions)
- **Endpoint:** Any endpoint accepting a DTO (e.g. `POST /api/v1/users/register`)
- **Expected:** `400 Bad Request` with the project envelope `{ statusCode, message, errors[] }`.
- **Actual:** `400 Bad Request` with ASP.NET Core's default `ValidationProblemDetails` shape (`type`, `title`, `status`, `errors` as an object keyed by field).
- **Root cause:** Controllers use `[ApiController]`, which automatically validates `ModelState` and short-circuits to `ValidationProblemDetails` before the controller's `ModelState.IsValid` branch can run.
- **Fix:** Configure `ApiBehaviorOptions` to suppress the automatic model-state invalid filter (`SuppressModelStateInvalidFilter = true`) so the controllers' own envelope helpers are always executed.
- **Regression test:** `UsersApiTests.Register_Invalid_Input_Returns_400_Field_Errors`.

## Bug 2: Validation error `field` names use PascalCase instead of camelCase

- **Module:** All (Users, Accounts, Categories, Transactions)
- **Endpoint:** Any endpoint returning `400 Bad Request` due to DTO validation
- **Expected:** Error objects use camelCase field names, e.g. `{ "field": "name", ... }`.
- **Actual:** Error objects used PascalCase property names from `ModelState` keys, e.g. `{ "field": "Name", ... }`.
- **Root cause:** The `ValidationError` helpers in every controller assigned `entry.Key` directly to the `field` property without converting to camelCase.
- **Fix:** Apply `JsonNamingPolicy.CamelCase.ConvertName(...)` to every validation field name in all controllers.
- **Regression test:** `UsersApiTests.Register_Invalid_Input_Returns_400_Field_Errors`.
