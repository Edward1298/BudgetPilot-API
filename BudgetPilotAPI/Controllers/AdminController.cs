using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BudgetPilot_API.Services;
using System.Text.Json;

/// <summary>
/// Handles administrative HTTP requests that are restricted to users with the Admin role.
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/v1/admin")]
public class AdminController : ControllerBase
{
    private readonly StoredProcedureService _storedProcedureService;
    private readonly AccountsService _accountsService;
    private readonly CardsService _cardsService;
    private readonly CategoriesService _categoriesService;
    private readonly UserService _userService;
    private readonly TransactionsService _transactionsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminController"/> class
    /// with the specified services.
    /// </summary>
    public AdminController(
        StoredProcedureService storedProcedureService,
        AccountsService accountsService,
        CardsService cardsService,
        CategoriesService categoriesService,
        UserService userService,
        TransactionsService transactionsService)
    {
        _storedProcedureService = storedProcedureService;
        _accountsService = accountsService;
        _cardsService = cardsService;
        _categoriesService = categoriesService;
        _userService = userService;
        _transactionsService = transactionsService;
    }

    /// <summary>
    /// Triggers the monthly interest calculation for all active savings accounts
    /// using the sp_ApplyMonthlyInterest stored procedure.
    /// </summary>
    [HttpPost("apply-monthly-interest")]
    public async Task<IActionResult> ApplyMonthlyInterest()
    {
        var result = await _storedProcedureService.ApplyMonthlyInterestAsync();
        return Ok(result);
    }

    /// <summary>
    /// Reactivates an account. If already active, returns a message.
    /// </summary>
    [HttpPost("accounts/{id:guid}/reactivate")]
    public async Task<IActionResult> ReactivateAccount(Guid id)
    {
        var (found, alreadyActive) = await _accountsService.ReactivateAccount(id);
        if (!found)
            return NotFoundError("Account not found.");
        if (alreadyActive)
            return Ok(new { statusCode = 200, message = "Account is already active.", errors = Array.Empty<object>() });
        return Ok(new { statusCode = 200, message = "Account reactivated successfully.", errors = Array.Empty<object>() });
    }

    /// <summary>
    /// Reactivates a card. If already active, returns a message.
    /// </summary>
    [HttpPost("cards/{id:guid}/reactivate")]
    public async Task<IActionResult> ReactivateCard(Guid id)
    {
        var (found, alreadyActive) = await _cardsService.ReactivateCard(id);
        if (!found)
            return NotFoundError("Card not found.");
        if (alreadyActive)
            return Ok(new { statusCode = 200, message = "Card is already active.", errors = Array.Empty<object>() });
        return Ok(new { statusCode = 200, message = "Card reactivated successfully.", errors = Array.Empty<object>() });
    }

    /// <summary>
    /// Reactivates a category. If already active, returns a message.
    /// </summary>
    [HttpPost("categories/{id:guid}/reactivate")]
    public async Task<IActionResult> ReactivateCategory(Guid id)
    {
        var (found, alreadyActive) = await _categoriesService.ReactivateCategory(id);
        if (!found)
            return NotFoundError("Category not found.");
        if (alreadyActive)
            return Ok(new { statusCode = 200, message = "Category is already active.", errors = Array.Empty<object>() });
        return Ok(new { statusCode = 200, message = "Category reactivated successfully.", errors = Array.Empty<object>() });
    }

    /// <summary>
    /// Reactivates a user. If already active, returns a message.
    /// </summary>
    [HttpPost("users/{id:guid}/reactivate")]
    public async Task<IActionResult> ReactivateUser(Guid id)
    {
        var (found, alreadyActive) = await _userService.ReactivateUser(id);
        if (!found)
            return NotFoundError("User not found.");
        if (alreadyActive)
            return Ok(new { statusCode = 200, message = "User is already active.", errors = Array.Empty<object>() });
        return Ok(new { statusCode = 200, message = "User reactivated successfully.", errors = Array.Empty<object>() });
    }

    /// <summary>
    /// Returns all accounts for a specific user, optionally filtered by active status.
    /// </summary>
    [HttpGet("users/{userId:guid}/accounts")]
    public async Task<IActionResult> GetUserAccounts(Guid userId, [FromQuery] bool? isActive = null)
    {
        var items = await _accountsService.GetAccountsByUserId(userId, isActive);
        return Ok(new { data = items });
    }

    /// <summary>
    /// Returns all cards for a specific user, optionally filtered by active status.
    /// </summary>
    [HttpGet("users/{userId:guid}/cards")]
    public async Task<IActionResult> GetUserCards(Guid userId, [FromQuery] bool? isActive = null)
    {
        var items = await _cardsService.GetCardsByUserId(userId, isActive);
        return Ok(new { data = items });
    }

    /// <summary>
    /// Returns all categories for a specific user, optionally filtered by active status.
    /// </summary>
    [HttpGet("users/{userId:guid}/categories")]
    public async Task<IActionResult> GetUserCategories(Guid userId, [FromQuery] bool? isActive = null)
    {
        var items = await _categoriesService.GetCategoriesByUserId(userId, isActive);
        return Ok(new { data = items });
    }

    /// <summary>
    /// Returns all transactions for a specific account, optionally filtered by month, type, and date range.
    /// </summary>
    [HttpGet("accounts/{accountId:guid}/transactions")]
    public async Task<IActionResult> GetAccountTransactions(
        Guid accountId,
        [FromQuery] int? month = null,
        [FromQuery] string? type = null,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null)
    {
        var items = await _transactionsService.GetTransactionsByAccountId(accountId, month, type, from, to);
        return Ok(new { data = items });
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
