using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using BudgetPilot_API.Dtos;

/// <summary>
/// Handles HTTP requests for user management including registration, login,
/// and user retrieval with optional filtering.
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
    /// <param name="userService">The service that provides user business logic and JWT generation.</param>
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
            return CreatedAtAction(nameof(GetUserById), new { id = user.Id }, user);
        }
        catch (InvalidOperationException ex) when (ex.Message == "EmailExists")
        {
            return ConflictError("A user with this email already exists.");
        }
    }

    /// <summary>
    /// Authenticates a user with email and password and returns a JWT access token.
    /// Returns 401 if the credentials are invalid.
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginDTO dto)
    {
        if (!ModelState.IsValid)
            return ValidationError();

        try
        {
            var (token, tokenType, expiresAt) = await _userService.Login(dto);

            return Ok(new
            {
                token,
                tokenType,
                expiresAt
            });
        }
        catch (UnauthorizedAccessException)
        {
            return Unauthorized(new
            {
                statusCode = 401,
                message = "Invalid email or password.",
                errors = Array.Empty<object>()
            });
        }
    }

    /// <summary>
    /// Returns a list of users with optional name and email filters.
    /// Requires JWT authentication.
    /// </summary>
    [HttpGet]
    [Authorize]
    public async Task<IActionResult> GetUsers(
        [FromQuery] string? name = null,
        [FromQuery] string? email = null)
    {
        var users = await _userService.GetUsers(name, email);

        return Ok(new
        {
            data = users
        });
    }

    /// <summary>
    /// Retrieves a single user by their unique identifier.
    /// Requires JWT authentication.
    /// </summary>
    [HttpGet("{id:guid}")]
    [Authorize]
    public async Task<IActionResult> GetUserById(Guid id)
    {
        var user = await _userService.GetUserById(id);

        if (user == null)
            return NotFound(new
            {
                statusCode = 404,
                message = "User not found.",
                errors = Array.Empty<object>()
            });

        return Ok(user);
    }

    /// <summary>
    /// Returns the profile of the currently authenticated user.
    /// The user identifier is extracted from the JWT token.
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userIdClaim = User.FindFirst(
            System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;

        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
            return Unauthorized(new
            {
                statusCode = 401,
                message = "Authentication required.",
                errors = Array.Empty<object>()
            });

        var user = await _userService.GetUserById(userId);

        if (user == null)
            return NotFound(new
            {
                statusCode = 404,
                message = "User not found.",
                errors = Array.Empty<object>()
            });

        return Ok(user);
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
    /// the ModelState, formatted in the standard error envelope.
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
                    field = entry.Key,
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
