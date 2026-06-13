using BudgetPilot_API.Entities;
using BudgetPilot_API.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

/// <summary>
/// Provides business logic for user operations including registration, authentication,
/// JWT token generation, and user retrieval with optional filtering.
/// </summary>
public class UserService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="UserService"/> class
    /// with the specified database context and application configuration.
    /// </summary>
    /// <param name="context">The application database context for accessing the users table.</param>
    /// <param name="configuration">The application configuration for reading JWT settings.</param>
    public UserService(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    /// <summary>
    /// Registers a new user by validating the request, checking for duplicate emails,
    /// hashing the password with BCrypt, and persisting the new user record to the database.
    /// </summary>
    /// <param name="dto">The registration data provided by the client.</param>
    /// <returns>The newly created user entity.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the email address is already registered.
    /// </exception>
    public async Task<UsersOBJ> Register(RegisterDTO dto)
    {
        var emailExists = await _context.Users
            .AnyAsync(u => u.Email == dto.Email);

        if (emailExists)
            throw new InvalidOperationException("EmailExists");

        var user = new UsersOBJ
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Email = dto.Email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return user;
    }

    /// <summary>
    /// Authenticates a user by verifying their email and password against the database.
    /// Returns a JWT access token on successful authentication.
    /// </summary>
    /// <param name="dto">The login credentials provided by the client.</param>
    /// <returns>A tuple containing the JWT token string, token type, and expiration date.</returns>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown when the email is not found or the password does not match.
    /// </exception>
    public async Task<(string Token, string TokenType, DateTime ExpiresAt)> Login(LoginDTO dto)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == dto.Email);

        if (user == null || !BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid email or password.");

        var expiresAt = DateTime.UtcNow.AddMinutes(
            double.Parse(_configuration["Jwt:ExpirationMinutes"]!));

        var token = GenerateJwtToken(user, expiresAt);

        return (token, "Bearer", expiresAt);
    }

    /// <summary>
    /// Retrieves a list of users with optional filters for name and email.
    /// Both filters use partial matching and are combined with AND logic when both are provided.
    /// </summary>
    /// <param name="name">Optional partial match on the user's display name.</param>
    /// <param name="email">Optional partial match on the user's email address.</param>
    /// <returns>A list of matching user entities with PasswordHash excluded via <see cref="System.Text.Json.Serialization.JsonIgnoreAttribute"/>.</returns>
    public async Task<List<UsersOBJ>> GetUsers(string? name, string? email)
    {
        var query = _context.Users.AsQueryable();

        if (!string.IsNullOrWhiteSpace(name))
            query = query.Where(u => u.Name.Contains(name));

        if (!string.IsNullOrWhiteSpace(email))
            query = query.Where(u => u.Email.Contains(email));

        return await query.OrderBy(u => u.Name).ToListAsync();
    }

    /// <summary>
    /// Retrieves a single user by their unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the user to retrieve.</param>
    /// <returns>
    /// The matching user entity if found; otherwise, <see langword="null" />.
    /// </returns>
    public async Task<UsersOBJ?> GetUserById(Guid id)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Id == id);
    }

    /// <summary>
    /// Generates a JWT access token for the specified user with claims for
    /// user identifier and email address.
    /// </summary>
    /// <param name="user">The authenticated user entity.</param>
    /// <param name="expiresAt">The token expiration timestamp.</param>
    /// <returns>A signed JWT token string.</returns>
    private string GenerateJwtToken(UsersOBJ user, DateTime expiresAt)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Email, user.Email)
        };

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt,
            Issuer = _configuration["Jwt:Issuer"],
            Audience = _configuration["Jwt:Audience"],
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);

        return tokenHandler.WriteToken(token);
    }
}
