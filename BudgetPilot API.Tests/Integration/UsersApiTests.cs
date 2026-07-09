using BudgetPilot_API.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace BudgetPilot_API.Tests.Integration;

public class UsersApiTests
{
    [Fact]
    public async Task Register_Returns_201_Location_And_Persists_BCrypt_Hash()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();

        var email = $"register-{Guid.NewGuid()}@example.com";
        var dto = new { name = "Alice", email, password = "Password123!", roleId = TestWebAppFactory.UserRoleId };

        var response = await client.PostAsJsonAsync("api/v1/users/register", dto);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("id").GetGuid().Should().NotBe(Guid.Empty);
        json.GetProperty("name").GetString().Should().Be("Alice");
        json.GetProperty("email").GetString().Should().Be(email.ToLowerInvariant());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email.ToLowerInvariant());
        user.PasswordHash.Should().StartWith("$2");
        user.PasswordHash.Length.Should().Be(60);
    }

    [Fact]
    public async Task Register_Duplicate_Email_Returns_409()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();

        var email = $"dup-{Guid.NewGuid()}@example.com";
        var dto = new { name = "Alice", email, password = "Password123!", roleId = TestWebAppFactory.UserRoleId };

        var first = await client.PostAsJsonAsync("api/v1/users/register", dto);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync("api/v1/users/register", dto);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await second.Content.ReadFromJsonAsync<JsonElement>();
        error.GetProperty("statusCode").GetInt32().Should().Be(409);
        error.GetProperty("errors").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Register_Invalid_Input_Returns_400_Field_Errors()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();

        var dto = new { name = "", email = "not-an-email", password = "short", roleId = TestWebAppFactory.UserRoleId };
        var response = await client.PostAsJsonAsync("api/v1/users/register", dto);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        error.GetProperty("statusCode").GetInt32().Should().Be(400);
        error.GetProperty("message").GetString().Should().Be("Validation failed.");

        var fields = error.GetProperty("errors").EnumerateArray()
            .Select(e => e.GetProperty("field").GetString())
            .ToList();

        fields.Should().Contain("name");
        fields.Should().Contain("email");
        fields.Should().Contain("password");
    }

    [Fact]
    public async Task Login_With_Valid_Credentials_Returns_Jwt()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();

        var user = await TestUserFixture.RegisterAndLoginAsync(client);

        user.Token.Should().NotBeNullOrWhiteSpace();
        user.Token.Split('.').Should().HaveCount(3);
    }

    [Fact]
    public async Task Login_With_Wrong_Password_Returns_401()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();

        var email = $"login-{Guid.NewGuid()}@example.com";
        await client.PostAsJsonAsync("api/v1/users/register", new { name = "Alice", email, password = "Password123!", roleId = TestWebAppFactory.UserRoleId });

        var response = await client.PostAsJsonAsync("api/v1/users/login", new { email, password = "WrongPassword123!" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_With_Valid_Jwt_Returns_Current_User()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();

        var user = await TestUserFixture.RegisterAndLoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", user.Token);

        var response = await client.GetAsync("api/v1/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("id").GetGuid().Should().Be(user.Id);
        json.GetProperty("email").GetString().Should().Be(user.Email);
    }

    [Fact]
    public async Task GetMe_Without_Jwt_Returns_401()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();

        var response = await client.GetAsync("api/v1/users/me");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_Is_Case_Insensitive_For_Email()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();

        var email = "John@Example.com";
        await client.PostAsJsonAsync("api/v1/users/register", new { name = "John", email, password = "Password123!", roleId = TestWebAppFactory.UserRoleId });

        var response = await client.PostAsJsonAsync("api/v1/users/login", new { email = "john@example.com", password = "Password123!" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("token").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GetUsers_Returns_Pagination_Metadata()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();

        var user = await TestUserFixture.RegisterAndLoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", user.Token);

        var response = await client.GetAsync("api/v1/users?page=1&pageSize=10");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("data").EnumerateArray().Should().NotBeNull();
        json.GetProperty("page").GetInt32().Should().Be(1);
        json.GetProperty("pageSize").GetInt32().Should().Be(10);
        json.GetProperty("totalCount").GetInt32().Should().BeGreaterThanOrEqualTo(1);
    }
}
