using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

[ApiController]
[Authorize]
[Route("api/v1/[controller]")]
public class ReportsController : ControllerBase
{
    private readonly ReportsService _reportsService;

    public ReportsController(ReportsService reportsService)
    {
        _reportsService = reportsService;
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(
        [FromQuery] DateOnly from,
        [FromQuery] DateOnly to,
        [FromQuery] string? type = null)
    {
        if (from > to)
            return BadRequest(new { statusCode = 400, message = "'from' must be before or equal to 'to'.", errors = Array.Empty<object>() });

        var userId = GetUserId();
        if (userId == null)
            return UnauthorizedError();

        var report = await _reportsService.GetSummary(userId.Value, from, to, type);
        return Ok(report);
    }

    private Guid? GetUserId()
    {
        var claim = User.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(claim) || !Guid.TryParse(claim, out var userId))
            return null;

        return userId;
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
}
