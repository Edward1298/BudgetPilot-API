using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BudgetPilot_API.Dtos;
using System.Text.Json;

/// <summary>
/// Handles HTTP requests for role management including listing, retrieval,
/// creation, update, and deletion of user roles.
/// </summary>
[ApiController]
[Authorize(Roles = "Admin")]
[Route("api/v1/[controller]")]
public class RolesController : ControllerBase
{
    private readonly RolesService _rolesService;

    /// <summary>
    /// Initializes a new instance of the <see cref="RolesController"/> class
    /// with the specified roles service.
    /// </summary>
    /// <param name="rolesService">The service that provides role business logic.</param>
    public RolesController(RolesService rolesService)
    {
        _rolesService = rolesService;
    }

    /// <summary>
    /// Returns a paginated list of roles, optionally filtered by name.
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> GetRoles(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null)
    {
        var (items, totalCount) = await _rolesService.GetRoles(page, pageSize, search);

        return Ok(new
        {
            data = items,
            page,
            pageSize,
            totalCount
        });
    }

    /// <summary>
    /// Retrieves a single role by its unique identifier.
    /// </summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetRoleById(Guid id)
    {
        var role = await _rolesService.GetRoleById(id);

        if (role == null)
            return NotFoundError("Role not found.");

        return Ok(role);
    }

    /// <summary>
    /// Creates a new role.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CreateRole([FromBody] RolesDTO dto)
    {
        if (!ModelState.IsValid)
            return ValidationError();

        var (role, isConflict) = await _rolesService.CreateRole(dto);

        if (isConflict)
        {
            return Conflict(new
            {
                statusCode = 409,
                message = "A role with this name already exists.",
                errors = Array.Empty<object>()
            });
        }

        return CreatedAtAction(nameof(GetRoleById), new { id = role!.Id }, role);
    }

    /// <summary>
    /// Updates an existing role with the provided values.
    /// </summary>
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateRole(Guid id, [FromBody] RolesDTO dto)
    {
        if (!ModelState.IsValid)
            return ValidationError();

        var (role, isConflict) = await _rolesService.UpdateRole(id, dto);

        if (isConflict)
        {
            return Conflict(new
            {
                statusCode = 409,
                message = "A role with this name already exists.",
                errors = Array.Empty<object>()
            });
        }

        if (role == null)
            return NotFoundError("Role not found.");

        return Ok(role);
    }

    /// <summary>
    /// Permanently deletes a role from the database.
    /// Returns 409 if the role has linked users.
    /// </summary>
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteRole(Guid id)
    {
        var (deleted, hasConflict) = await _rolesService.DeleteRole(id);

        if (hasConflict)
            return ConflictError("Role has linked users and cannot be deleted.");

        if (!deleted)
            return NotFoundError("Role not found.");

        return NoContent();
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
