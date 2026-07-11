using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BudgetPilot_API.Dtos;
using System.Text.Json;

/// <summary>
/// Handles HTTP requests for user management including registration,
/// authentication, and profile retrieval.
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class UsersController : ControllerBase
{
    private readonly UserService _userService;

    /// <summary>
    /// Initializes a new instance of the <see cref="UsersController"/> class
    /// with the specified user service.
    /// </summary>
    /// <param name="userService">The service that provides user business logic.</param>
    public UsersController(UserService userService)
    {
        _userService = userService;
    }

    /// <summary>
    /// Registers a new user account. The password is hashed with BCrypt before storage.
    /// Returns 409 if the email is already registered.
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterDTO dto)
    {
        if (!ModelState.IsValid)
            return ValidationError();

        try
        {
            var user = await _userService.Register(dto);
            return CreatedAtAction(
                nameof(GetUserById),
                new { id = user.Id },
                user);
        }
        catch (InvalidOperationException)
        {
            return Conflict(new
            {
                statusCode = 409,
                message = "A user with this email already exists.",
                errors = Array.Empty<object>()
            });
        }
        catch (ArgumentException ex)
        {
            var field = ex.Message.Contains("role", StringComparison.OrdinalIgnoreCase) ? "roleId" : "name";
            return BadRequest(new
            {
                statusCode = 400,
                message = "Validation failed.",
                errors = new[]
                {
                    new { field, message = ex.Message }
                }
            });
        }
    }

    /// <summary>
    /// Authenticates a user with email and password.
    /// Returns a JWT access token on success, or 401 if credentials are invalid.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDTO dto)
    {
        if (!ModelState.IsValid)
            return ValidationError();

        var result = await _userService.Login(dto);

        if (result == null)
        {
            return Unauthorized(new
            {
                statusCode = 401,
                message = "Invalid email or password.",
                errors = Array.Empty<object>()
            });
        }

        return Ok(new
        {
            token = result.Token,
            tokenType = result.TokenType,
            expiresAt = result.ExpiresAt,
            refreshToken = result.RefreshToken,
            refreshExpiresAt = result.RefreshExpiresAt
        });
    }

    /// <summary>
    /// Refreshes an expired or soon-to-expire JWT access token using a valid refresh token.
    /// The old refresh token is revoked and a new token pair is returned.
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> Refresh([FromBody] RefreshTokenRequestDTO dto)
    {
        if (!ModelState.IsValid)
            return ValidationError();

        var result = await _userService.RefreshToken(dto.RefreshToken);

        if (result == null)
        {
            return Unauthorized(new
            {
                statusCode = 401,
                message = "Invalid or expired refresh token.",
                errors = Array.Empty<object>()
            });
        }

        return Ok(result);
    }

    /// <summary>
    /// Revokes the specified refresh token, effectively logging out the user session
    /// associated with that token.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout([FromBody] LogoutDTO dto)
    {
        if (!ModelState.IsValid)
            return ValidationError();

        await _userService.RevokeRefreshToken(dto.RefreshToken);
        return Ok(new { statusCode = 200, message = "Logged out successfully.", errors = Array.Empty<object>() });
    }

    /// <summary>
    /// Returns a paginated list of users, optionally filtered by partial
    /// name and/or email matches. Requires Admin role.
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? name = null,
        [FromQuery] string? email = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20)
    {
        if (page < 1)
            return BadRequest(new { statusCode = 400, message = "Page must be 1 or greater.", errors = Array.Empty<object>() });

        var (items, totalCount) = await _userService.GetUsers(
            name, email, page, pageSize);

        return Ok(new
        {
            data = items,
            page,
            pageSize,
            totalCount
        });
    }

    /// <summary>
    /// Retrieves a single user by their unique identifier.
    /// Requires Admin role. Returns 404 if the user is not found.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> GetUserById(Guid id)
    {
        var user = await _userService.GetUserById(id);

        if (user == null)
        {
            return NotFound(new
            {
                statusCode = 404,
                message = "User not found.",
                errors = Array.Empty<object>()
            });
        }

        return Ok(user);
    }

    /// <summary>
    /// Returns the profile of the currently authenticated user.
    /// The user identifier is extracted from the JWT token claims.
    /// Returns 404 if the user no longer exists in the database.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetMe()
    {
        var userId = GetUserId();
        if (userId == null)
        {
            return Unauthorized(new
            {
                statusCode = 401,
                message = "Authentication required.",
                errors = Array.Empty<object>()
            });
        }

        var user = await _userService.GetUserById(userId.Value);

        if (user == null)
        {
            return NotFound(new
            {
                statusCode = 404,
                message = "User not found.",
                errors = Array.Empty<object>()
            });
        }

        return Ok(user);
    }

    /// <summary>
    /// Partially updates a user's profile with the provided non-null fields.
    /// Admin users can update any user; regular users can only update their own profile.
    /// Requires JWT authentication.
    /// </summary>
    [HttpPut("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> UpdateUser(Guid id, [FromBody] UserUpdateDTO dto)
    {
        var userId = GetUserId();
        if (userId == null)
            return UnauthorizedError();

        if (!ModelState.IsValid)
            return ValidationError();

        var isAdmin = IsAdmin();

        try
        {
            var user = await _userService.UpdateUser(id, dto, userId.Value, isAdmin);

            if (user == null)
                return NotFoundError("User not found.");

            return Ok(user);
        }
        catch (UnauthorizedAccessException)
        {
            return ForbiddenError("You can only update your own profile.");
        }
        catch (InvalidOperationException)
        {
            return Conflict(new
            {
                statusCode = 409,
                message = "A user with this email already exists.",
                errors = Array.Empty<object>()
            });
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new
            {
                statusCode = 400,
                message = "Validation failed.",
                errors = new[]
                {
                    new { field = "name", message = ex.Message }
                }
            });
        }
    }

    /// <summary>
    /// Deletes a user from the database. Admin users perform a hard delete;
    /// regular users perform a soft delete (IsActive = false).
    /// Requires JWT authentication.
    /// </summary>
    [HttpDelete("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> DeleteUser(Guid id)
    {
        var userId = GetUserId();
        if (userId == null)
            return UnauthorizedError();

        var isAdmin = IsAdmin();

        if (!isAdmin && userId.Value != id)
            return ForbiddenError("You can only delete your own account.");

        var deleted = await _userService.DeleteUser(id, isAdmin);

        if (!deleted)
            return NotFoundError("User not found.");

        return NoContent();
    }

    /// <summary>
    /// Extracts the authenticated user's identifier from the JWT claims.
    /// </summary>
    /// <returns>
    /// The user's GUID if the claim is present and valid; otherwise, <see langword="null" />.
    /// </returns>
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
    /// Returns a 400 Bad Request response with validation errors extracted from
    /// the ModelState, formatted in the standard error envelope with camelCase field names.
    /// </summary>
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
