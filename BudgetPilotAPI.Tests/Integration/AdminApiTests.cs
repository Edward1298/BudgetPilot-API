using BudgetPilot_API.Tests.Fixtures;
using System.Net.Http.Headers;
using System.Text.Json;

namespace BudgetPilot_API.Tests.Integration;

public class AdminApiTests
{
    private static async Task<AuthenticatedUser> AuthenticateClientAsync(HttpClient client)
    {
        var user = await TestUserFixture.RegisterAndLoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
        return user;
    }

    [Fact]
    public async Task ApplyMonthlyInterest_Unauthenticated_Returns_401()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();

        var response = await client.PostAsync("api/v1/admin/apply-monthly-interest", null);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ApplyMonthlyInterest_NonAdmin_Returns_403()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        var response = await client.PostAsync("api/v1/admin/apply-monthly-interest", null);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }
}
