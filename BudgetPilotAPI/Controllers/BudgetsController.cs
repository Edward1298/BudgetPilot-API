using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BudgetPilot_API.Dtos;
using System.Text.Json;

[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class BudgetsController : ControllerBase
{
    private readonly BudgetsService _budgetsService;

    public BudgetsController(BudgetsService budgetsService)
    {
        _budgetsService = budgetsService;
    }

    [HttpGet]
    public async Task<IActionResult> GetBudgets(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] int? month = null,
        [FromQuery] int? year = null,
        [FromQuery] Guid? categoryId = null)
    {
        if (page < 1)
            return BadRequest(new { statusCode = 400, message = "Page must be 1 or greater.", errors = Array.Empty<object>() });

        var userId = GetUserId();
        if (userId == null)
            return UnauthorizedError();

        var (items, totalCount) = await _budgetsService.GetBudgets(
            userId.Value, page, pageSize, month, year, categoryId);

        return Ok(new
        {
            data = items,
            page,
            pageSize,
            totalCount
        });
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetBudgetById(Guid id)
    {
        var userId = GetUserId();
        if (userId == null)
            return UnauthorizedError();

        var budget = await _budgetsService.GetBudgetById(id);

        if (budget == null)
            return NotFoundError("Budget not found.");

        if (budget.UserId != userId.Value)
            return ForbiddenError("You do not have access to this budget.");

        return Ok(budget);
    }

    [HttpPost]
    public async Task<IActionResult> CreateBudget([FromBody] BudgetsDTO dto)
    {
        var userId = GetUserId();
        if (userId == null)
            return UnauthorizedError();

        if (!ModelState.IsValid)
            return ValidationError();

        var (budget, isConflict) = await _budgetsService.CreateBudget(dto, userId.Value);

        if (isConflict)
        {
            return Conflict(new
            {
                statusCode = 409,
                message = "A budget for this category, month, and year already exists.",
                errors = Array.Empty<object>()
            });
        }

        if (budget == null)
            return NotFoundError("Category not found or does not belong to you.");

        return CreatedAtAction(nameof(GetBudgetById), new { id = budget.Id }, budget);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateBudget(Guid id, [FromBody] BudgetUpdateDTO dto)
    {
        var userId = GetUserId();
        if (userId == null)
            return UnauthorizedError();

        if (!ModelState.IsValid)
            return ValidationError();

        var (budget, isConflict) = await _budgetsService.UpdateBudget(id, dto, userId.Value);

        if (isConflict)
        {
            return Conflict(new
            {
                statusCode = 409,
                message = "A budget for this category, month, and year already exists.",
                errors = Array.Empty<object>()
            });
        }

        if (budget == null)
        {
            var exists = await _budgetsService.GetBudgetById(id);
            if (exists != null && exists.UserId != userId.Value)
                return ForbiddenError("You do not have access to this budget.");

            return NotFoundError("Budget not found, or category not found or does not belong to you.");
        }

        return Ok(budget);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteBudget(Guid id)
    {
        var userId = GetUserId();
        if (userId == null)
            return UnauthorizedError();

        var isAdmin = IsAdmin();
        var deleted = await _budgetsService.DeleteBudget(id, userId.Value, isAdmin);

        if (!deleted)
        {
            var exists = await _budgetsService.GetBudgetById(id);
            if (exists != null && exists.UserId != userId.Value)
                return ForbiddenError("You do not have access to this budget.");

            return NotFoundError("Budget not found.");
        }

        return NoContent();
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(claim) || !Guid.TryParse(claim, out var userId))
            return null;

        return userId;
    }

    private bool IsAdmin()
    {
        return User.IsInRole("Admin");
    }

    private IActionResult UnauthorizedError()
    {
        return Unauthorized(new
        {
            statusCode = 401,
            message = "Authentication required.",
            errors = Array.Empty<object>()
        });
    }

    private IActionResult ForbiddenError(string message)
    {
        return StatusCode(403, new
        {
            statusCode = 403,
            message,
            errors = Array.Empty<object>()
        });
    }

    private IActionResult NotFoundError(string message)
    {
        return NotFound(new
        {
            statusCode = 404,
            message,
            errors = Array.Empty<object>()
        });
    }

    private IActionResult ValidationError()
    {
        var errors = ModelState
            .SelectMany(entry => entry.Value!.Errors
                .Select(error => new
                {
                    field = JsonNamingPolicy.CamelCase.ConvertName(entry.Key),
                    message = error.ErrorMessage
                }))
            .ToArray();

        return BadRequest(new
        {
            statusCode = 400,
            message = "Validation failed.",
            errors
        });
    }
}
