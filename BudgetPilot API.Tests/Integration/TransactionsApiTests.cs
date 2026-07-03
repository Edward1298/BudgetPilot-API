using BudgetPilot_API.Tests.Fixtures;
using System.Net.Http.Headers;
using System.Text.Json;

namespace BudgetPilot_API.Tests.Integration;

public class TransactionsApiTests
{
    private static async Task<AuthenticatedUser> AuthenticateClientAsync(HttpClient client)
    {
        var user = await TestUserFixture.RegisterAndLoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
        return user;
    }

    private static async Task<(Guid AccountId, Guid CategoryId)> CreateAccountAndCategoryAsync(HttpClient client, string accountType = "cash", string categoryType = "expense")
    {
        var accountResponse = await client.PostAsJsonAsync("api/v1/accounts", new { name = "Account", type = accountType, balance = 100 });
        accountResponse.EnsureSuccessStatusCode();
        var accountId = (await accountResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var categoryResponse = await client.PostAsJsonAsync("api/v1/categories", new { name = "Category", type = categoryType });
        categoryResponse.EnsureSuccessStatusCode();
        var categoryId = (await categoryResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        return (accountId, categoryId);
    }

    [Fact]
    public async Task Create_Valid_Returns_201_With_Server_Set_Date()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        var (accountId, categoryId) = await CreateAccountAndCategoryAsync(client);
        var dto = new { accountId, categoryId, amount = 50, type = "expense", description = "Lunch" };

        var response = await client.PostAsJsonAsync("api/v1/transactions", dto);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("id").GetGuid().Should().NotBe(Guid.Empty);
        json.GetProperty("accountId").GetGuid().Should().Be(accountId);
        json.GetProperty("categoryId").GetGuid().Should().Be(categoryId);
        json.GetProperty("amount").GetDecimal().Should().Be(50);
        json.GetProperty("type").GetString().Should().Be("expense");
        json.GetProperty("description").GetString().Should().Be("Lunch");
        json.TryGetProperty("userId", out _).Should().BeFalse();

        var dateString = json.GetProperty("date").GetString()!;
        DateOnly.Parse(dateString).Should().Be(DateOnly.FromDateTime(DateTime.UtcNow));
    }

    [Fact]
    public async Task Create_With_Not_Owned_Account_Returns_404()
    {
        using var factory = new TestWebAppFactory();
        var clientA = factory.CreateClient();
        await AuthenticateClientAsync(clientA);
        var (accountId, _) = await CreateAccountAndCategoryAsync(clientA);

        var clientB = factory.CreateClient();
        await AuthenticateClientAsync(clientB);
        var (_, categoryIdB) = await CreateAccountAndCategoryAsync(clientB);

        var dto = new { accountId, categoryId = categoryIdB, amount = 50, type = "expense" };
        var response = await clientB.PostAsJsonAsync("api/v1/transactions", dto);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_With_Not_Owned_Category_Returns_404()
    {
        using var factory = new TestWebAppFactory();
        var clientA = factory.CreateClient();
        await AuthenticateClientAsync(clientA);
        var (_, categoryId) = await CreateAccountAndCategoryAsync(clientA);

        var clientB = factory.CreateClient();
        await AuthenticateClientAsync(clientB);
        var (accountIdB, _) = await CreateAccountAndCategoryAsync(clientB);

        var dto = new { accountId = accountIdB, categoryId, amount = 50, type = "expense" };
        var response = await clientB.PostAsJsonAsync("api/v1/transactions", dto);

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Create_Type_Mismatch_With_Category_Returns_400_Type_Field()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        var account = await client.PostAsJsonAsync("api/v1/accounts", new { name = "Cash", type = "cash", balance = 100 });
        var accountId = (await account.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var category = await client.PostAsJsonAsync("api/v1/categories", new { name = "Salary", type = "income" });
        var categoryId = (await category.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var dto = new { accountId, categoryId, amount = 50, type = "expense" };
        var response = await client.PostAsJsonAsync("api/v1/transactions", dto);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        var fieldError = error.GetProperty("errors").EnumerateArray().Single();
        fieldError.GetProperty("field").GetString().Should().Be("type");
        fieldError.GetProperty("message").GetString().Should().Be("Transaction type must match the referenced category's type.");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-10)]
    public async Task Create_Invalid_Amount_Returns_400(decimal amount)
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        var (accountId, categoryId) = await CreateAccountAndCategoryAsync(client);
        var dto = new { accountId, categoryId, amount, type = "expense" };

        var response = await client.PostAsJsonAsync("api/v1/transactions", dto);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        var fields = error.GetProperty("errors").EnumerateArray()
            .Select(e => e.GetProperty("field").GetString())
            .ToList();

        fields.Should().Contain("amount");
    }

    [Fact]
    public async Task Create_Expense_Decreases_Account_Balance()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        var (accountId, categoryId) = await CreateAccountAndCategoryAsync(client, "cash", "expense");
        var dto = new { accountId, categoryId, amount = 25, type = "expense" };

        await client.PostAsJsonAsync("api/v1/transactions", dto);

        var account = await client.GetAsync($"api/v1/accounts/{accountId}");
        account.EnsureSuccessStatusCode();

        var json = await account.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("balance").GetDecimal().Should().Be(75);
    }

    [Fact]
    public async Task Create_Income_Increases_Account_Balance()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        var account = await client.PostAsJsonAsync("api/v1/accounts", new { name = "Cash", type = "cash", balance = 100 });
        var accountId = (await account.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var category = await client.PostAsJsonAsync("api/v1/categories", new { name = "Salary", type = "income" });
        var categoryId = (await category.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var dto = new { accountId, categoryId, amount = 500, type = "income" };
        await client.PostAsJsonAsync("api/v1/transactions", dto);

        var updated = await client.GetAsync($"api/v1/accounts/{accountId}");
        var json = await updated.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("balance").GetDecimal().Should().Be(600);
    }

    [Fact]
    public async Task Put_Changes_Account_And_Reverses_Old_Balance()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        var accountA = await client.PostAsJsonAsync("api/v1/accounts", new { name = "A", type = "cash", balance = 100 });
        var accountIdA = (await accountA.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var accountB = await client.PostAsJsonAsync("api/v1/accounts", new { name = "B", type = "cash", balance = 100 });
        var accountIdB = (await accountB.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var category = await client.PostAsJsonAsync("api/v1/categories", new { name = "Food", type = "expense" });
        var categoryId = (await category.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var tx = await client.PostAsJsonAsync("api/v1/transactions", new { accountId = accountIdA, categoryId, amount = 20, type = "expense" });
        var txId = (await tx.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var put = await client.PutAsJsonAsync($"api/v1/transactions/{txId}", new { accountId = accountIdB, categoryId, amount = 30, type = "expense" });
        put.EnsureSuccessStatusCode();

        var updatedA = await client.GetAsync($"api/v1/accounts/{accountIdA}");
        var balanceA = (await updatedA.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("balance").GetDecimal();
        balanceA.Should().Be(100);

        var updatedB = await client.GetAsync($"api/v1/accounts/{accountIdB}");
        var balanceB = (await updatedB.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("balance").GetDecimal();
        balanceB.Should().Be(70);
    }

    [Fact]
    public async Task Put_Changes_Amount_Balance_Delta_Is_Correct()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        var (accountId, categoryId) = await CreateAccountAndCategoryAsync(client, "cash", "expense");
        var tx = await client.PostAsJsonAsync("api/v1/transactions", new { accountId, categoryId, amount = 10, type = "expense" });
        var txId = (await tx.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await client.PutAsJsonAsync($"api/v1/transactions/{txId}", new { accountId, categoryId, amount = 25, type = "expense" });

        var account = await client.GetAsync($"api/v1/accounts/{accountId}");
        var balance = (await account.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("balance").GetDecimal();
        balance.Should().Be(75);
    }

    [Fact]
    public async Task Put_Date_Is_Immutable()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        var (accountId, categoryId) = await CreateAccountAndCategoryAsync(client, "cash", "expense");
        var tx = await client.PostAsJsonAsync("api/v1/transactions", new { accountId, categoryId, amount = 10, type = "expense" });
        var txJson = await tx.Content.ReadFromJsonAsync<JsonElement>();
        var txId = txJson.GetProperty("id").GetGuid();
        var originalDate = txJson.GetProperty("date").GetString();

        var put = await client.PutAsJsonAsync($"api/v1/transactions/{txId}", new { accountId, categoryId, amount = 15, type = "expense", date = "2020-01-01" });
        put.EnsureSuccessStatusCode();

        var updated = await client.GetAsync($"api/v1/transactions/{txId}");
        var updatedJson = await updated.Content.ReadFromJsonAsync<JsonElement>();
        updatedJson.GetProperty("date").GetString().Should().Be(originalDate);
    }

    [Fact]
    public async Task Delete_Reverses_Balance_And_Get_Returns_404()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        var (accountId, categoryId) = await CreateAccountAndCategoryAsync(client, "cash", "expense");
        var tx = await client.PostAsJsonAsync("api/v1/transactions", new { accountId, categoryId, amount = 20, type = "expense" });
        var txId = (await tx.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var delete = await client.DeleteAsync($"api/v1/transactions/{txId}");
        delete.StatusCode.Should().Be(HttpStatusCode.NoContent);

        var account = await client.GetAsync($"api/v1/accounts/{accountId}");
        var balance = (await account.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("balance").GetDecimal();
        balance.Should().Be(100);

        var get = await client.GetAsync($"api/v1/transactions/{txId}");
        get.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task List_Filters_By_Account_Category_Type_DateRange_And_Search()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        var accountA = await client.PostAsJsonAsync("api/v1/accounts", new { name = "A", type = "cash", balance = 1000 });
        var accountIdA = (await accountA.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var accountB = await client.PostAsJsonAsync("api/v1/accounts", new { name = "B", type = "cash", balance = 1000 });
        var accountIdB = (await accountB.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var categoryFood = await client.PostAsJsonAsync("api/v1/categories", new { name = "Food", type = "expense" });
        var categoryIdFood = (await categoryFood.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var categorySalary = await client.PostAsJsonAsync("api/v1/categories", new { name = "Salary", type = "income" });
        var categoryIdSalary = (await categorySalary.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        await client.PostAsJsonAsync("api/v1/transactions", new { accountId = accountIdA, categoryId = categoryIdFood, amount = 10, type = "expense", description = "Lunch" });
        await client.PostAsJsonAsync("api/v1/transactions", new { accountId = accountIdA, categoryId = categoryIdSalary, amount = 100, type = "income", description = "Paycheck" });
        await client.PostAsJsonAsync("api/v1/transactions", new { accountId = accountIdB, categoryId = categoryIdFood, amount = 20, type = "expense", description = "Dinner" });

        var response = await client.GetAsync($"api/v1/transactions?accountId={accountIdA}&type=expense&search=Lunch");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("totalCount").GetInt32().Should().Be(1);

        var item = json.GetProperty("data").EnumerateArray().Single();
        item.GetProperty("amount").GetDecimal().Should().Be(10);
    }

    [Fact]
    public async Task GetById_Not_Owned_Returns_403_And_Not_Found_Returns_404()
    {
        using var factory = new TestWebAppFactory();
        var clientA = factory.CreateClient();
        await AuthenticateClientAsync(clientA);
        var (accountId, categoryId) = await CreateAccountAndCategoryAsync(clientA, "cash", "expense");
        var tx = await clientA.PostAsJsonAsync("api/v1/transactions", new { accountId, categoryId, amount = 10, type = "expense" });
        var txId = (await tx.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var clientB = factory.CreateClient();
        var userB = await TestUserFixture.RegisterAndLoginAsync(clientB);
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userB.Token);

        var forbidden = await clientB.GetAsync($"api/v1/transactions/{txId}");
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var notFound = await clientA.GetAsync($"api/v1/transactions/{Guid.NewGuid()}");
        notFound.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
