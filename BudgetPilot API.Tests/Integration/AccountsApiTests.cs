using BudgetPilot_API.Tests.Fixtures;
using System.Net.Http.Headers;
using System.Text.Json;

namespace BudgetPilot_API.Tests.Integration;

public class AccountsApiTests
{
    private static async Task<AuthenticatedUser> AuthenticateClientAsync(HttpClient client)
    {
        var user = await TestUserFixture.RegisterAndLoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
        return user;
    }

    private static async Task<JsonElement> CreateAccountAsync(HttpClient client, string name, string type, decimal balance = 0)
    {
        var dto = new { name, type, balance };
        var response = await client.PostAsJsonAsync("api/v1/accounts", dto);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Create_Returns_201_Location_And_Defaults_Balance_To_Zero()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        var dto = new { name = "Cash Wallet", type = "cash" };
        var response = await client.PostAsJsonAsync("api/v1/accounts", dto);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("id").GetGuid().Should().NotBe(Guid.Empty);
        json.GetProperty("name").GetString().Should().Be("Cash Wallet");
        json.GetProperty("type").GetString().Should().Be("cash");
        json.GetProperty("balance").GetDecimal().Should().Be(0);
    }

    [Fact]
    public async Task Create_Negative_Balance_On_Cash_Returns_400()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        var dto = new { name = "Cash Wallet", type = "cash", balance = -50 };
        var response = await client.PostAsJsonAsync("api/v1/accounts", dto);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        var fields = error.GetProperty("errors").EnumerateArray()
            .Select(e => e.GetProperty("field").GetString())
            .ToList();

        fields.Should().Contain("balance");
    }

    [Fact]
    public async Task Create_Negative_Balance_On_BankAccount_Returns_400()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        var dto = new { name = "Bank", type = "bankAccount", balance = -50 };
        var response = await client.PostAsJsonAsync("api/v1/accounts", dto);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Create_Negative_Balance_On_CreditCard_Returns_201()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        var dto = new { name = "Credit Card", type = "creditCard", balance = -500 };
        var response = await client.PostAsJsonAsync("api/v1/accounts", dto);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("balance").GetDecimal().Should().Be(-500);
    }

    [Fact]
    public async Task List_Filters_By_Type_And_Search()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        await CreateAccountAsync(client, "Personal Cash", "cash", 100);
        await CreateAccountAsync(client, "Credit Card", "creditCard", -100);
        await CreateAccountAsync(client, "Savings Bank", "bankAccount", 1000);

        var response = await client.GetAsync("api/v1/accounts?type=cash&search=Personal");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("totalCount").GetInt32().Should().Be(1);

        var item = json.GetProperty("data").EnumerateArray().Single();
        item.GetProperty("name").GetString().Should().Be("Personal Cash");
    }

    [Fact]
    public async Task GetById_Not_Owned_Returns_403()
    {
        using var factory = new TestWebAppFactory();
        var clientA = factory.CreateClient();
        var userA = await AuthenticateClientAsync(clientA);
        var account = await CreateAccountAsync(clientA, "Account A", "cash", 100);
        var accountId = account.GetProperty("id").GetGuid();

        var clientB = factory.CreateClient();
        var userB = await TestUserFixture.RegisterAndLoginAsync(clientB);
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userB.Token);

        var response = await clientB.GetAsync($"api/v1/accounts/{accountId}");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetById_Not_Found_Returns_404()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        var response = await client.GetAsync($"api/v1/accounts/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Put_Full_Replace_Updates_Balance()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        var account = await CreateAccountAsync(client, "Cash", "cash", 100);
        var accountId = account.GetProperty("id").GetGuid();

        var dto = new { name = "Updated Cash", type = "cash", balance = 250 };
        var response = await client.PutAsJsonAsync($"api/v1/accounts/{accountId}", dto);

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("name").GetString().Should().Be("Updated Cash");
        json.GetProperty("balance").GetDecimal().Should().Be(250);
    }

    [Fact]
    public async Task Delete_Returns_204_And_Subsequent_Get_Returns_404()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        var account = await CreateAccountAsync(client, "To Delete", "cash", 0);
        var accountId = account.GetProperty("id").GetGuid();

        var deleteResponse = await client.DeleteAsync($"api/v1/accounts/{accountId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await client.GetAsync($"api/v1/accounts/{accountId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_Account_With_Linked_Transactions_Returns_409()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        var user = await AuthenticateClientAsync(client);

        var account = await CreateAccountAsync(client, "Linked Account", "cash", 100);
        var accountId = account.GetProperty("id").GetGuid();

        var category = await client.PostAsJsonAsync("api/v1/categories", new { name = "Food", type = "expense" });
        category.EnsureSuccessStatusCode();
        var categoryId = (await category.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var transaction = new
        {
            accountId,
            categoryId,
            amount = 10,
            type = "expense",
            description = "Lunch"
        };
        var txResponse = await client.PostAsJsonAsync("api/v1/transactions", transaction);
        txResponse.EnsureSuccessStatusCode();

        var deleteResponse = await client.DeleteAsync($"api/v1/accounts/{accountId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await deleteResponse.Content.ReadFromJsonAsync<JsonElement>();
        error.GetProperty("statusCode").GetInt32().Should().Be(409);
        error.GetProperty("errors").GetArrayLength().Should().Be(0);
    }
}
