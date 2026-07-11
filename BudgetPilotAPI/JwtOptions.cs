/// <summary>
/// Strongly-typed configuration class for JWT authentication settings.
/// Values are bound from the "Jwt" section of appsettings.
/// </summary>
public class JwtOptions
{
    /// <summary>
    /// Gets or sets the secret key used for signing JWT tokens.
    /// Must be at least 32 characters.
    /// </summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the issuer claim value included in generated tokens.
    /// </summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the audience claim value included in generated tokens.
    /// </summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the token expiration time in minutes from issuance.
    /// Default is 1440 minutes (24 hours).
    /// </summary>
    public int ExpirationMinutes { get; set; } = 1440;

    /// <summary>
    /// Gets or sets the refresh token expiration time in days from issuance.
    /// Default is 7 days.
    /// </summary>
    public int RefreshTokenExpirationDays { get; set; } = 7;
}
