using BudgetPilot_API.Entities;
using BudgetPilot_API.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

/// <summary>
/// Provides business logic for user operations including registration,
/// authentication, profile retrieval, and user listing with pagination.
/// </summary>
public class UserService
{
    private readonly AppDbContext _context;
    private readonly JwtOptions _jwtOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserService"/> class
    /// with the specified database context and JWT configuration options.
    /// </summary>
    /// <param name="context">The application database context for accessing the users table.</param>
    /// <param name="jwtOptions">The strongly-typed JWT configuration bound from appsettings.</param>
    public UserService(AppDbContext context, IOptions<JwtOptions> jwtOptions)
    {
        _context = context;
        _jwtOptions = jwtOptions.Value;
    }

    /// <summary>
    /// Registers a new user by normalizing the email, checking for duplicates,
    /// hashing the password with BCrypt, and persisting the record to the database.
    /// </summary>
    /// <param name="dto">The registration data provided by the client.</param>
    /// <returns>The newly created user entity.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the name is empty or whitespace-only after trimming.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when a user with the same normalized email already exists.
    /// </exception>
    public async Task<UsersOBJ> Register(RegisterDTO dto)
    {
        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
        var trimmedName = dto.Name.Trim();

        if (string.IsNullOrWhiteSpace(trimmedName))
            throw new ArgumentException("Name must not be empty or whitespace-only.");

        var existingUser = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        if (existingUser != null)
            throw new InvalidOperationException("A user with this email already exists.");

        var user = new UsersOBJ
        {
            Id = Guid.NewGuid(),
            Name = trimmedName,
            Email = normalizedEmail,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return user;
    }

    /// <summary>
    /// Authenticates a user by email and password, returning JWT token information
    /// on success. Returns <see langword="null" /> when the email is not found
    /// or the password does not match.
    /// </summary>
    /// <param name="dto">The login credentials provided by the client.</param>
    /// <returns>
    /// A tuple containing the JWT token string, token type, and expiration timestamp
    /// if authentication succeeds; otherwise, <see langword="null" />.
    /// </returns>
    public async Task<(string Token, string TokenType, DateTime ExpiresAt)?> Login(LoginDTO dto)
    {
        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail);

        if (user == null)
            return null;

        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return null;

        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpirationMinutes);
        var token = GenerateJwtToken(user.Id, expiresAt);

        return (Token: token, TokenType: "Bearer", ExpiresAt: expiresAt);
    }

    /// <summary>
    /// Retrieves a paginated list of users, optionally filtered by partial
    /// name and/or email matches (AND logic), ordered by creation date descending.
    /// </summary>
    /// <param name="name">Optional partial match on the user's display name.</param>
    /// <param name="email">Optional partial match on the user's email address.</param>
    /// <param name="page">The one-based page number to retrieve.</param>
    /// <param name="pageSize">The number of items per page.</param>
    /// <returns>A tuple containing the list of users and the total matching count.</returns>
    public async Task<(List<UsersOBJ> Items, int TotalCount)> GetUsers(
        string? name, string? email, int page, int pageSize)
    {
        var query = _context.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(name))
            query = query.Where(u => u.Name.Contains(name));

        if (!string.IsNullOrWhiteSpace(email))
            query = query.Where(u => u.Email.Contains(email));

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    /// <summary>
    /// Retrieves a single user by their unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the user to retrieve.</param>
    /// <returns>
    /// The matching user if found; otherwise, <see langword="null" />.
    /// </returns>
    public async Task<UsersOBJ?> GetUserById(Guid id)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
    }

    /// <summary>
    /// Generates a JWT access token for the specified user with the configured
    /// issuer, audience, signing key, and expiration time.
    /// </summary>
    /// <param name="userId">The unique identifier of the authenticated user.</param>
    /// <param name="expiresAt">The UTC timestamp at which the token expires.</param>
    /// <returns>The encoded JWT token string.</returns>
    private string GenerateJwtToken(Guid userId, DateTime expiresAt)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _jwtOptions.Issuer,
            audience: _jwtOptions.Audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
