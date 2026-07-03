using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BudgetPilot_API.Dtos;
using System.Text.Json;

/// <summary>
/// Handles HTTP requests for transaction management including listing, retrieval,
/// creation, update, and deletion of financial transactions.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class TransactionsController : ControllerBase
{
    private readonly TransactionsService _transactionsService;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionsController"/> class
    /// with the specified transactions service.
    /// </summary>
    /// <param name="transactionsService">The service that provides transaction business logic.</param>
    public TransactionsController(TransactionsService transactionsService)
    {
        _transactionsService = transactionsService;
    }

    /// <summary>
    /// Returns a paginated list of transactions belonging to the authenticated user,
    /// optionally filtered by type, account, category, date range, and/or description.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetTransactions(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? type = null,
        [FromQuery] Guid? accountId = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] DateOnly? from = null,
        [FromQuery] DateOnly? to = null,
        [FromQuery] string? search = null)
    {
        var userId = GetUserId();
        if (userId == null)
            return UnauthorizedError();

        var (items, totalCount) = await _transactionsService.GetTransactions(
            userId.Value, page, pageSize, type, accountId, categoryId, from, to, search);

        return Ok(new
        {
            data = items,
            page,
            pageSize,
            totalCount
        });
    }

    /// <summary>
    /// Retrieves a single transaction by its unique identifier.
    /// Returns 403 if the transaction belongs to a different user.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetTransactionById(Guid id)
    {
        var userId = GetUserId();
        if (userId == null)
            return UnauthorizedError();

        var transaction = await _transactionsService.GetTransactionById(id);

        if (transaction == null)
            return NotFoundError("Transaction not found.");

        if (transaction.UserId != userId.Value)
            return ForbiddenError("You do not have access to this transaction.");

        return Ok(transaction);
    }

    /// <summary>
    /// Creates a new transaction for the authenticated user and adjusts the
    /// linked account's balance accordingly.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateTransaction([FromBody] TransactionsDTO dto)
    {
        var userId = GetUserId();
        if (userId == null)
            return UnauthorizedError();

        if (!ModelState.IsValid)
            return ValidationError();

        try
        {
            var transaction = await _transactionsService.CreateTransaction(dto, userId.Value);

            if (transaction == null)
            {
                return NotFoundError(
                    "Referenced account or category not found or does not belong to you.");
            }

            return CreatedAtAction(
                nameof(GetTransactionById), new { id = transaction.Id }, transaction);
        }
        catch (ArgumentException ex)
        {
            return ValidationError(ex.Message, "type");
        }
    }

    /// <summary>
    /// Fully replaces an existing transaction with the provided values.
    /// The transaction date is never modified. Returns 403 if the transaction
    /// belongs to a different user.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateTransaction(Guid id, [FromBody] TransactionsDTO dto)
    {
        var userId = GetUserId();
        if (userId == null)
            return UnauthorizedError();

        if (!ModelState.IsValid)
            return ValidationError();

        try
        {
            var transaction = await _transactionsService.UpdateTransaction(id, dto, userId.Value);

            if (transaction == null)
            {
                var exists = await _transactionsService.GetTransactionById(id);
                if (exists != null && exists.UserId != userId.Value)
                    return ForbiddenError("You do not have access to this transaction.");

                return NotFoundError(
                    "Transaction not found, or referenced account/category not found or does not belong to you.");
            }

            return Ok(transaction);
        }
        catch (ArgumentException ex)
        {
            return ValidationError(ex.Message, "type");
        }
    }

    /// <summary>
    /// Permanently deletes a transaction and reverses its balance effect on the
    /// linked account. Returns 403 if the transaction belongs to a different user.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteTransaction(Guid id)
    {
        var userId = GetUserId();
        if (userId == null)
            return UnauthorizedError();

        var deleted = await _transactionsService.DeleteTransaction(id, userId.Value);

        if (!deleted)
        {
            var exists = await _transactionsService.GetTransactionById(id);
            if (exists != null && exists.UserId != userId.Value)
                return ForbiddenError("You do not have access to this transaction.");

            return NotFoundError("Transaction not found.");
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
