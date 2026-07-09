using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    /// <summary>
    /// Initializes a new instance of the <see cref="AdminController"/> class
    /// with the specified stored procedure service.
    /// </summary>
    /// <param name="storedProcedureService">The service that executes stored procedures.</param>
    public AdminController(StoredProcedureService storedProcedureService)
    {
        _storedProcedureService = storedProcedureService;
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
