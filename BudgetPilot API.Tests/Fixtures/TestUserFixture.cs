using System.Text.Json;

namespace BudgetPilot_API.Tests.Fixtures;

/// <summary>
/// Represents an authenticated test user with the JWT token returned by the login endpoint.
/// </summary>
/// <param name="Id">The unique identifier assigned to the user during registration.</param>
/// <param name="Email">The normalized email address used for registration and login.</param>
/// <param name="Token">The JWT bearer token returned by the login endpoint.</param>
public record AuthenticatedUser(Guid Id, string Email, string Token);

/// <summary>
/// Helper class that registers a new test user, logs in, and returns the resulting token.
/// Each call creates a distinct user so tests remain isolated.
/// </summary>
public static class TestUserFixture
{
    /// <summary>
    /// Registers a new user with a unique email, logs in, and returns the authenticated user details.
    /// </summary>
    /// <param name="client">The HTTP client used to call the registration and login endpoints.</param>
    /// <returns>An <see cref="AuthenticatedUser"/> containing the user id, email, and bearer token.</returns>
    public static async Task<AuthenticatedUser> RegisterAndLoginAsync(HttpClient client)
    {
        var email = $"test-{Guid.NewGuid()}@example.com";
        const string password = "Password123!";

        var registerDto = new { name = "Test User", email, password };
        var registerResponse = await client.PostAsJsonAsync("api/v1/users/register", registerDto);
        registerResponse.EnsureSuccessStatusCode();

        var registerJson = await registerResponse.Content.ReadFromJsonAsync<JsonElement>();
        var userId = registerJson.GetProperty("id").GetGuid();

        var loginDto = new { email, password };
        var loginResponse = await client.PostAsJsonAsync("api/v1/users/login", loginDto);
        loginResponse.EnsureSuccessStatusCode();

        var loginJson = await loginResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = loginJson.GetProperty("token").GetString()!;

        return new AuthenticatedUser(userId, email, token);
    }
}
