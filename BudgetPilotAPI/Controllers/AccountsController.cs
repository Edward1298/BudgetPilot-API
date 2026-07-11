using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BudgetPilot_API.Dtos;
using System.Text.Json;

/// <summary>
/// Handles HTTP requests for account management including listing, retrieval,
/// creation, update, and deletion of financial accounts.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly AccountsService _accountsService;
    private readonly StoredProcedureService _storedProcedureService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountsController"/> class
    /// with the specified accounts service and stored procedure service.
    /// </summary>
    /// <param name="accountsService">The service that provides account business logic.</param>
    /// <param name="storedProcedureService">The service that executes stored procedures.</param>
    public AccountsController(AccountsService accountsService, StoredProcedureService storedProcedureService)
    {
        _accountsService = accountsService;
        _storedProcedureService = storedProcedureService;
    }

    /// <summary>
    /// Returns a paginated list of accounts belonging to the authenticated user,
    /// optionally filtered by type and/or name.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetAccounts(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? type = null,
        [FromQuery] string? search = null)
    {
        if (page < 1)
            return BadRequest(new { statusCode = 400, message = "Page must be 1 or greater.", errors = Array.Empty<object>() });

        var userId = GetUserId();
        if (userId == null)
            return UnauthorizedError();

        var (items, totalCount) = await _accountsService.GetAccounts(
            userId.Value, page, pageSize, type, search);

        return Ok(new
        {
            data = items,
            page,
            pageSize,
            totalCount
        });
    }

    /// <summary>
    /// Retrieves a single account by its unique identifier.
    /// Returns 403 if the account belongs to a different user.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetAccountById(Guid id)
    {
        var userId = GetUserId();
        if (userId == null)
            return UnauthorizedError();

        var account = await _accountsService.GetAccountById(id);

        if (account == null)
            return NotFoundError("Account not found.");

        if (account.UserId != userId.Value)
            return ForbiddenError("You do not have access to this account.");

        return Ok(account);
    }

    /// <summary>
    /// Returns account details and the last 10 active transactions in one round-trip
    /// using the sp_GetAccountSummary stored procedure.
    /// </summary>
    [HttpGet("{id:guid}/summary")]
    public async Task<IActionResult> GetAccountSummary(Guid id)
    {
        var userId = GetUserId();
        if (userId == null)
            return UnauthorizedError();

        var account = await _accountsService.GetAccountById(id);

        if (account == null)
            return NotFoundError("Account not found.");

        if (account.UserId != userId.Value)
            return ForbiddenError("You do not have access to this account.");

        var summary = await _storedProcedureService.GetAccountSummaryAsync(id, userId.Value);

        if (summary == null)
            return NotFoundError("Account not found.");

        return Ok(summary);
    }

    /// <summary>
    /// Creates a new financial account for the authenticated user.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateAccount([FromBody] AccountsDTO dto)
    {
        var userId = GetUserId();
        if (userId == null)
            return UnauthorizedError();

        if (!ModelState.IsValid)
            return ValidationError();

        try
        {
            var account = await _accountsService.CreateAccount(dto, userId.Value);
            return CreatedAtAction(nameof(GetAccountById), new { id = account.Id }, account);
        }
        catch (ArgumentException ex)
        {
            return ValidationError(ex.Message, "balance");
        }
    }

    /// <summary>
    /// Partially updates an existing account with the provided non-null values.
    /// Returns 403 if the account belongs to a different user.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateAccount(Guid id, [FromBody] AccountUpdateDTO dto)
    {
        var userId = GetUserId();
        if (userId == null)
            return UnauthorizedError();

        if (!ModelState.IsValid)
            return ValidationError();

        try
        {
            var account = await _accountsService.UpdateAccount(id, dto, userId.Value);

            if (account == null)
            {
                var exists = await _accountsService.GetAccountById(id);
                if (exists != null && exists.UserId != userId.Value)
                    return ForbiddenError("You do not have access to this account.");

                return NotFoundError("Account not found.");
            }

            return Ok(account);
        }
        catch (ArgumentException ex)
        {
            return ValidationError(ex.Message, "balance");
        }
    }

    /// <summary>
    /// Deletes an account from the database. Admin users perform a hard delete;
    /// regular users perform a soft delete (IsActive = false).
    /// Returns 403 if the account belongs to a different user.
    /// Returns 409 if the account has linked transactions.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteAccount(Guid id)
    {
        var userId = GetUserId();
        if (userId == null)
            return UnauthorizedError();

        var isAdmin = IsAdmin();
        var (deleted, hasConflict) = await _accountsService.DeleteAccount(id, userId.Value, isAdmin);

        if (hasConflict)
        {
            return ConflictError("Account has linked transactions and cannot be deleted.");
        }

        if (!deleted)
        {
            var exists = await _accountsService.GetAccountById(id);
            if (exists != null && exists.UserId != userId.Value)
                return ForbiddenError("You do not have access to this account.");

            return NotFoundError("Account not found.");
        }

        return NoContent();
    }

    /// <summary>
    /// Extracts the authenticated user's identifier from the JWT claims.
    /// </summary>
    private Guid? GetUserId()
    {
        var claim = User.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(claim) || !Guid.TryParse(claim, out var userId))
            return null;

        return userId;
    }

    /// <summary>
    /// Determines whether the authenticated user has the Admin role.
    /// </summary>
    /// <returns>
    /// <see langword="true" /> if the user is in the Admin role; otherwise, <see langword="false" />.
    /// </returns>
    private bool IsAdmin()
    {
        return User.IsInRole("Admin");
    }

    /// <summary>
    /// Returns a 401 Unauthorized response in the standard error envelope format.
    /// </summary>
    private IActionResult UnauthorizedError()
    {
        return Unauthorized(new
        {
            statusCode = 401,
            message = "Authentication required.",
            errors = Array.Empty<object>()
        });
    }

    /// <summary>
    /// Returns a 403 Forbidden response in the standard error envelope format.
    /// </summary>
    private IActionResult ForbiddenError(string message)
    {
        return StatusCode(403, new
        {
            statusCode = 403,
            message,
            errors = Array.Empty<object>()
        });
    }

    /// <summary>
    /// Returns a 404 Not Found response in the standard error envelope format.
    /// </summary>
    private IActionResult NotFoundError(string message)
    {
        return NotFound(new
        {
            statusCode = 404,
            message,
            errors = Array.Empty<object>()
        });
    }

    /// <summary>
    /// Returns a 409 Conflict response in the standard error envelope format.
    /// </summary>
    private IActionResult ConflictError(string message)
    {
        return Conflict(new
        {
            statusCode = 409,
            message,
            errors = Array.Empty<object>()
        });
    }

    /// <summary>
    /// Returns a 400 Bad Request response with validation errors extracted from
    /// the ModelState or a custom field error, formatted in the standard error envelope
    /// with camelCase field names.
    /// </summary>
    private IActionResult ValidationError(string? customMessage = null, string? field = null)
    {
        var errors = new List<object>();

        if (customMessage != null && field != null)
        {
            errors.Add(new
            {
                field = JsonNamingPolicy.CamelCase.ConvertName(field),
                message = customMessage
            });
        }
        else
        {
            foreach (var entry in ModelState)
            {
                foreach (var error in entry.Value.Errors)
                {
                    errors.Add(new
                    {
                        field = JsonNamingPolicy.CamelCase.ConvertName(entry.Key),
                        message = error.ErrorMessage
                    });
                }
            }
        }

        return BadRequest(new
        {
            statusCode = 400,
            message = "Validation failed.",
            errors = errors.ToArray()
        });
    }
}
