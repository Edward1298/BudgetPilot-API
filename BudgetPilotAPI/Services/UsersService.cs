using BudgetPilot_API.Entities;
using BudgetPilot_API.Dtos;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
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
    /// validating the role exists, hashing the password with BCrypt, and persisting
    /// the record to the database.
    /// </summary>
    /// <param name="dto">The registration data provided by the client.</param>
    /// <returns>The newly created user entity.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the name is empty or whitespace-only after trimming, or when the
    /// specified RoleId does not exist in the roles table.
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

        var roleExists = await _context.Roles.AnyAsync(r => r.Id == dto.RoleId);
        if (!roleExists)
            throw new ArgumentException("The specified role does not exist.");

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
            RoleId = dto.RoleId,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        return user;
    }

    /// <summary>
    /// Authenticates a user by email and password, returning JWT token information
    /// and a refresh token on success. The JWT includes the user's role claim.
    /// Returns <see langword="null" /> when the email is not found or the password does not match.
    /// </summary>
    /// <param name="dto">The login credentials provided by the client.</param>
    /// <returns>
    /// A <see cref="RefreshTokenResponseDTO"/> containing the JWT token, refresh token,
    /// and their expiration timestamps if authentication succeeds; otherwise, <see langword="null" />.
    /// </returns>
    public async Task<RefreshTokenResponseDTO?> Login(LoginDTO dto)
    {
        var normalizedEmail = dto.Email.Trim().ToLowerInvariant();

        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail && u.IsActive);

        if (user == null)
            return null;

        if (!BCrypt.Net.BCrypt.Verify(dto.Password, user.PasswordHash))
            return null;

        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == user.RoleId);
        var roleName = role?.Name ?? "User";

        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpirationMinutes);
        var token = GenerateJwtToken(user.Id, roleName, expiresAt);

        var refreshToken = GenerateRefreshToken();
        var refreshExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpirationDays);

        _context.RefreshTokens.Add(new RefreshTokensOBJ
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = BCrypt.Net.BCrypt.HashPassword(refreshToken),
            ExpiresAt = refreshExpiresAt,
            CreatedAt = DateTime.UtcNow
        });
        await _context.SaveChangesAsync();

        return new RefreshTokenResponseDTO
        {
            Token = token,
            TokenType = "Bearer",
            ExpiresAt = expiresAt,
            RefreshToken = refreshToken,
            RefreshExpiresAt = refreshExpiresAt
        };
    }

    /// <summary>
    /// Retrieves a paginated list of active users, optionally filtered by partial
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
        var query = _context.Users
            .Where(u => u.IsActive)
            .AsQueryable();

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
    /// Retrieves a single active user by their unique identifier.
    /// </summary>
    /// <param name="id">The unique identifier of the user to retrieve.</param>
    /// <returns>
    /// The matching active user if found; otherwise, <see langword="null" />.
    /// </returns>
    public async Task<UsersOBJ?> GetUserById(Guid id)
    {
        return await _context.Users.FirstOrDefaultAsync(u => u.Id == id && u.IsActive);
    }

    /// <summary>
    /// Generates a JWT access token for the specified user with the configured
    /// issuer, audience, signing key, expiration time, and role claim.
    /// </summary>
    /// <param name="userId">The unique identifier of the authenticated user.</param>
    /// <param name="roleName">The name of the user's role (e.g., "Admin" or "User").</param>
    /// <param name="expiresAt">The UTC timestamp at which the token expires.</param>
    /// <returns>The encoded JWT token string.</returns>
    private string GenerateJwtToken(Guid userId, string roleName, DateTime expiresAt)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
            new Claim(ClaimTypes.Role, roleName)
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

    private static string GenerateRefreshToken()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    public async Task<RefreshTokenResponseDTO?> RefreshToken(string refreshToken)
    {
        var tokens = await _context.RefreshTokens
            .Where(t => t.RevokedAt == null && t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();

        RefreshTokensOBJ? storedToken = null;
        foreach (var t in tokens)
        {
            if (BCrypt.Net.BCrypt.Verify(refreshToken, t.Token))
            {
                storedToken = t;
                break;
            }
        }

        if (storedToken == null)
            return null;

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == storedToken.UserId && u.IsActive);
        if (user == null)
            return null;

        storedToken.RevokedAt = DateTime.UtcNow;

        var role = await _context.Roles.FirstOrDefaultAsync(r => r.Id == user.RoleId);
        var roleName = role?.Name ?? "User";

        var expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.ExpirationMinutes);
        var newAccessToken = GenerateJwtToken(user.Id, roleName, expiresAt);

        var newRefreshToken = GenerateRefreshToken();
        var refreshExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenExpirationDays);

        _context.RefreshTokens.Add(new RefreshTokensOBJ
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            Token = BCrypt.Net.BCrypt.HashPassword(newRefreshToken),
            ExpiresAt = refreshExpiresAt,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        return new RefreshTokenResponseDTO
        {
            Token = newAccessToken,
            TokenType = "Bearer",
            ExpiresAt = expiresAt,
            RefreshToken = newRefreshToken,
            RefreshExpiresAt = refreshExpiresAt
        };
    }

    public async Task RevokeRefreshToken(string refreshToken)
    {
        var tokens = await _context.RefreshTokens
            .Where(t => t.RevokedAt == null && t.ExpiresAt > DateTime.UtcNow)
            .ToListAsync();

        foreach (var t in tokens)
        {
            if (BCrypt.Net.BCrypt.Verify(refreshToken, t.Token))
            {
                t.RevokedAt = DateTime.UtcNow;
                break;
            }
        }

        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Updates an existing user's profile with the provided non-null fields.
    /// Admin users can update any user; regular users can only update their own profile.
    /// Validates email uniqueness when the email field is changed.
    /// </summary>
    /// <param name="id">The unique identifier of the user to update.</param>
    /// <param name="dto">The partial update data provided by the client.</param>
    /// <param name="currentUserId">The unique identifier of the authenticated user making the request.</param>
    /// <param name="isAdmin">Whether the caller has admin privileges.</param>
    /// <returns>
    /// The updated user entity if found and the caller has permission;
    /// otherwise, <see langword="null" />.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the new email address is already in use by another user.
    /// </exception>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown when a non-admin user attempts to update another user's profile.
    /// </exception>
    public async Task<UsersOBJ?> UpdateUser(Guid id, UserUpdateDTO dto, Guid currentUserId, bool isAdmin)
    {
        if (!isAdmin && currentUserId != id)
            throw new UnauthorizedAccessException("You can only update your own profile.");

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id && u.IsActive);

        if (user == null)
            return null;

        if (dto.Name != null)
        {
            var trimmedName = dto.Name.Trim();
            if (string.IsNullOrWhiteSpace(trimmedName))
                throw new ArgumentException("Name must not be empty or whitespace-only.");
            user.Name = trimmedName;
        }

        if (dto.Email != null)
        {
            var normalizedEmail = dto.Email.Trim().ToLowerInvariant();
            var existingUser = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == normalizedEmail && u.Id != id);

            if (existingUser != null)
                throw new InvalidOperationException("A user with this email already exists.");

            user.Email = normalizedEmail;
        }

        await _context.SaveChangesAsync();

        return user;
    }

    /// <summary>
    /// Deletes a user from the database. If the caller is an admin, performs a hard delete.
    /// Otherwise, performs a soft delete by setting IsActive to false.
    /// </summary>
    /// <param name="id">The unique identifier of the user to delete.</param>
    /// <param name="isAdmin">Whether the caller has admin privileges.</param>
    /// <returns>
    /// <see langword="true" /> if the user was found and deleted; otherwise, <see langword="false" />.
    /// </returns>
    public async Task<bool> DeleteUser(Guid id, bool isAdmin)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
            return false;

        if (isAdmin)
        {
            _context.Users.Remove(user);
        }
        else
        {
            user.IsActive = false;
        }

        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// Reactivates an inactive user. Returns whether the user was found
    /// and whether it was already active.
    /// </summary>
    public async Task<(bool Found, bool AlreadyActive)> ReactivateUser(Guid id)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user == null)
            return (false, false);

        if (user.IsActive)
            return (true, true);

        user.IsActive = true;
        await _context.SaveChangesAsync();

        return (true, false);
    }
}
