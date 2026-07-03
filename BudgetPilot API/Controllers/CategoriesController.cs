using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BudgetPilot_API.Dtos;
using System.Text.Json;

/// <summary>
/// Handles HTTP requests for category management including listing, retrieval,
/// creation, update, and deletion of income/expense categories.
/// </summary>
[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class CategoriesController : ControllerBase
{
    private readonly CategoriesService _categoriesService;

    /// <summary>
    /// Initializes a new instance of the <see cref="CategoriesController"/> class
    /// with the specified categories service.
    /// </summary>
    /// <param name="categoriesService">The service that provides category business logic.</param>
    public CategoriesController(CategoriesService categoriesService)
    {
        _categoriesService = categoriesService;
    }

    /// <summary>
    /// Returns a paginated list of categories belonging to the authenticated user,
    /// optionally filtered by type and/or name.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetCategories(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? type = null,
        [FromQuery] string? search = null)
    {
        var userId = GetUserId();
        if (userId == null)
            return UnauthorizedError();

        var (items, totalCount) = await _categoriesService.GetCategories(
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
    /// Retrieves a single category by its unique identifier.
    /// Returns 403 if the category belongs to a different user.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetCategoryById(Guid id)
    {
        var userId = GetUserId();
        if (userId == null)
            return UnauthorizedError();

        var category = await _categoriesService.GetCategoryById(id);

        if (category == null)
            return NotFoundError("Category not found.");

        if (category.UserId != userId.Value)
            return ForbiddenError("You do not have access to this category.");

        return Ok(category);
    }

    /// <summary>
    /// Creates a new income/expense category for the authenticated user.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateCategory([FromBody] CategoriesDTO dto)
    {
        var userId = GetUserId();
        if (userId == null)
            return UnauthorizedError();

        if (!ModelState.IsValid)
            return ValidationError();

        var (category, isConflict) = await _categoriesService.CreateCategory(dto, userId.Value);

        if (isConflict)
        {
            return Conflict(new
            {
                statusCode = 409,
                message = "A category with this name and type already exists.",
                errors = Array.Empty<object>()
            });
        }

        return CreatedAtAction(nameof(GetCategoryById), new { id = category!.Id }, category);
    }

    /// <summary>
    /// Fully replaces an existing category with the provided values.
    /// Returns 403 if the category belongs to a different user.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] CategoriesDTO dto)
    {
        var userId = GetUserId();
        if (userId == null)
            return UnauthorizedError();

        if (!ModelState.IsValid)
            return ValidationError();

        var (category, isConflict) = await _categoriesService.UpdateCategory(id, dto, userId.Value);

        if (isConflict)
        {
            return Conflict(new
            {
                statusCode = 409,
                message = "A category with this name and type already exists.",
                errors = Array.Empty<object>()
            });
        }

        if (category == null)
        {
            var exists = await _categoriesService.GetCategoryById(id);
            if (exists != null && exists.UserId != userId.Value)
                return ForbiddenError("You do not have access to this category.");

            return NotFoundError("Category not found.");
        }

        return Ok(category);
    }

    /// <summary>
    /// Permanently deletes a category from the database.
    /// Returns 403 if the category belongs to a different user.
    /// Returns 409 if the category has linked transactions.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteCategory(Guid id)
    {
        var userId = GetUserId();
        if (userId == null)
            return UnauthorizedError();

        var (deleted, hasConflict) = await _categoriesService.DeleteCategory(id, userId.Value);

        if (hasConflict)
        {
            return ConflictError("Category has linked transactions and cannot be deleted.");
        }

        if (!deleted)
        {
            var exists = await _categoriesService.GetCategoryById(id);
            if (exists != null && exists.UserId != userId.Value)
                return ForbiddenError("You do not have access to this category.");

            return NotFoundError("Category not found.");
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
    /// the ModelState, formatted in the standard error envelope with camelCase field names.
    /// </summary>
    private IActionResult ValidationError()
    {
        var errors = new List<object>();

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

        return BadRequest(new
        {
            statusCode = 400,
            message = "Validation failed.",
            errors = errors.ToArray()
        });
    }
}
