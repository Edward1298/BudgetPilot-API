using BudgetPilot_API.Tests.Fixtures;
using System.Net.Http.Headers;
using System.Text.Json;

namespace BudgetPilot_API.Tests.Integration;

public class CardsApiTests
{
    private static async Task<AuthenticatedUser> AuthenticateClientAsync(HttpClient client)
    {
        var user = await TestUserFixture.RegisterAndLoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
        return user;
    }

    private static async Task<Guid> CreateAccountAsync(HttpClient client, string name = "Account", string type = "cash", decimal balance = 1000)
    {
        var response = await client.PostAsJsonAsync("api/v1/accounts", new { name, type, balance });
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Create_Debit_Card_Returns_201()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        var accountId = await CreateAccountAsync(client);

        var dto = new
        {
            accountId,
            type = "debit",
            cardNumber = "4111111111111111",
            expirationDate = "2027-12-31",
            cvc = "123",
            nameOnCard = "Test User"
        };

        var response = await client.PostAsJsonAsync("api/v1/cards", dto);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("id").GetGuid().Should().NotBe(Guid.Empty);
        json.GetProperty("type").GetString().Should().Be("debit");
        json.GetProperty("cardNumber").GetString().Should().Be("4111111111111111");
        json.GetProperty("cvc").GetString().Should().Be("123");
    }

    [Fact]
    public async Task Create_Credit_Card_With_CreditLimit_Returns_201()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        var accountId = await CreateAccountAsync(client);

        var dto = new
        {
            accountId,
            type = "credit",
            cardNumber = "5500000000000004",
            expirationDate = "2028-06-30",
            cvc = "456",
            nameOnCard = "Test User",
            creditLimit = 5000,
            apr = 18.5
        };

        var response = await client.PostAsJsonAsync("api/v1/cards", dto);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("creditLimit").GetDecimal().Should().Be(5000);
        json.GetProperty("apr").GetDecimal().Should().Be(18.5m);
    }

    [Fact]
    public async Task Create_Card_With_Invalid_Account_Returns_400()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        var dto = new
        {
            accountId = Guid.NewGuid(),
            type = "debit",
            cardNumber = "4111111111111111",
            expirationDate = "2027-12-31",
            cvc = "123",
            nameOnCard = "Test User"
        };

        var response = await client.PostAsJsonAsync("api/v1/cards", dto);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetCardById_Returns_Decrypted_Data()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        var accountId = await CreateAccountAsync(client);

        var createDto = new
        {
            accountId,
            type = "debit",
            cardNumber = "4111111111111111",
            expirationDate = "2027-12-31",
            cvc = "123",
            nameOnCard = "Test User"
        };

        var createResponse = await client.PostAsJsonAsync("api/v1/cards", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var cardId = created.GetProperty("id").GetGuid();

        var getResponse = await client.GetAsync($"api/v1/cards/{cardId}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("cardNumber").GetString().Should().Be("4111111111111111");
        json.GetProperty("cvc").GetString().Should().Be("123");
    }

    [Fact]
    public async Task GetCardById_Not_Owned_Returns_403()
    {
        using var factory = new TestWebAppFactory();
        var clientA = factory.CreateClient();
        var userA = await AuthenticateClientAsync(clientA);

        var accountId = await CreateAccountAsync(clientA);

        var createDto = new
        {
            accountId,
            type = "debit",
            cardNumber = "4111111111111111",
            expirationDate = "2027-12-31",
            cvc = "123",
            nameOnCard = "Test User"
        };

        var createResponse = await clientA.PostAsJsonAsync("api/v1/cards", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var cardId = created.GetProperty("id").GetGuid();

        var clientB = factory.CreateClient();
        var userB = await TestUserFixture.RegisterAndLoginAsync(clientB);
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userB.Token);

        var getResponse = await clientB.GetAsync($"api/v1/cards/{cardId}");

        getResponse.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task UpdateCard_Changes_NameOnCard()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        var accountId = await CreateAccountAsync(client);

        var createDto = new
        {
            accountId,
            type = "debit",
            cardNumber = "4111111111111111",
            expirationDate = "2027-12-31",
            cvc = "123",
            nameOnCard = "Old Name"
        };

        var createResponse = await client.PostAsJsonAsync("api/v1/cards", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var cardId = created.GetProperty("id").GetGuid();

        var updateDto = new { nameOnCard = "New Name" };
        var updateResponse = await client.PutAsJsonAsync($"api/v1/cards/{cardId}", updateDto);

        updateResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await updateResponse.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("nameOnCard").GetString().Should().Be("New Name");
    }

    [Fact]
    public async Task DeleteCard_Returns_204_And_Subsequent_Get_Returns_404()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        var accountId = await CreateAccountAsync(client);

        var createDto = new
        {
            accountId,
            type = "debit",
            cardNumber = "4111111111111111",
            expirationDate = "2027-12-31",
            cvc = "123",
            nameOnCard = "Test User"
        };

        var createResponse = await client.PostAsJsonAsync("api/v1/cards", createDto);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var cardId = created.GetProperty("id").GetGuid();

        var deleteResponse = await client.DeleteAsync($"api/v1/cards/{cardId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var getResponse = await client.GetAsync($"api/v1/cards/{cardId}");
        getResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task ListCards_Returns_Paginated_Results()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        var accountId = await CreateAccountAsync(client);

        for (int i = 0; i < 3; i++)
        {
            var dto = new
            {
                accountId,
                type = "debit",
                cardNumber = $"411111111111111{i}",
                expirationDate = "2027-12-31",
                cvc = "123",
                nameOnCard = $"Card {i}"
            };

            await client.PostAsJsonAsync("api/v1/cards", dto);
        }

        var response = await client.GetAsync("api/v1/cards?page=1&pageSize=2");

        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("totalCount").GetInt32().Should().Be(3);
        json.GetProperty("data").GetArrayLength().Should().Be(2);
    }

    [Fact]
    public async Task Create_Expense_With_Debit_Card_And_Insufficient_Funds_Returns_400()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        var accountId = await CreateAccountAsync(client, balance: 50);

        var cardDto = new
        {
            accountId,
            type = "debit",
            cardNumber = "4111111111111111",
            expirationDate = "2027-12-31",
            cvc = "123",
            nameOnCard = "Test User"
        };

        await client.PostAsJsonAsync("api/v1/cards", cardDto);

        var categoryResponse = await client.PostAsJsonAsync("api/v1/categories", new { name = "Food", type = "expense" });
        var categoryId = (await categoryResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var txDto = new
        {
            accountId,
            categoryId,
            amount = 100
        };

        var txResponse = await client.PostAsJsonAsync("api/v1/transactions", txDto);

        txResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await txResponse.Content.ReadFromJsonAsync<JsonElement>();
        error.GetProperty("message").GetString().Should().Be("Insufficient funds.");
    }

    [Fact]
    public async Task Create_Expense_With_Credit_Card_Exceeding_Limit_Returns_400()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        var accountId = await CreateAccountAsync(client, balance: 1000);

        var cardDto = new
        {
            accountId,
            type = "credit",
            cardNumber = "5500000000000004",
            expirationDate = "2028-06-30",
            cvc = "456",
            nameOnCard = "Test User",
            creditLimit = 100
        };

        await client.PostAsJsonAsync("api/v1/cards", cardDto);

        var categoryResponse = await client.PostAsJsonAsync("api/v1/categories", new { name = "Food", type = "expense" });
        var categoryId = (await categoryResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var txDto = new
        {
            accountId,
            categoryId,
            amount = 150
        };

        var txResponse = await client.PostAsJsonAsync("api/v1/transactions", txDto);

        txResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await txResponse.Content.ReadFromJsonAsync<JsonElement>();
        error.GetProperty("message").GetString().Should().Be("Credit limit exceeded.");
    }

    [Fact]
    public async Task Create_Credit_Card_Without_Apr_Uses_DefaultApr()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        var accountId = await CreateAccountAsync(client);

        var dto = new
        {
            accountId,
            type = "credit",
            cardNumber = "5500000000000004",
            expirationDate = "2028-06-30",
            cvc = "456",
            nameOnCard = "Test User",
            creditLimit = 5000
        };

        var response = await client.PostAsJsonAsync("api/v1/cards", dto);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("creditLimit").GetDecimal().Should().Be(5000);
        json.GetProperty("apr").GetDecimal().Should().Be(24.99m);
    }

    [Fact]
    public async Task Create_Card_Without_NameOnCard_Uses_User_Name()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        var user = await TestUserFixture.RegisterAndLoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);

        var accountId = await CreateAccountAsync(client);

        var dto = new
        {
            accountId,
            type = "debit",
            cardNumber = "4111111111111111",
            expirationDate = "2027-12-31",
            cvc = "123"
        };

        var response = await client.PostAsJsonAsync("api/v1/cards", dto);

        response.StatusCode.Should().Be(HttpStatusCode.Created);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("nameOnCard").GetString().Should().Be("Test User");
    }
}
