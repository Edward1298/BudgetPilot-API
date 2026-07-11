using BudgetPilot_API.Tests.Fixtures;
using System.Net.Http.Headers;
using System.Text.Json;

namespace BudgetPilot_API.Tests.Integration;

public class CategoriesApiTests
{
    private static async Task<AuthenticatedUser> AuthenticateClientAsync(HttpClient client)
    {
        var user = await TestUserFixture.RegisterAndLoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
        return user;
    }

    private static async Task<JsonElement> CreateCategoryAsync(HttpClient client, string name, string type)
    {
        var dto = new { name, type };
        var response = await client.PostAsJsonAsync("api/v1/categories", dto);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    [Fact]
    public async Task Create_Income_And_Expense_Returns_201()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        var income = await client.PostAsJsonAsync("api/v1/categories", new { name = "Salary", type = "income" });
        income.StatusCode.Should().Be(HttpStatusCode.Created);

        var expense = await client.PostAsJsonAsync("api/v1/categories", new { name = "Food", type = "expense" });
        expense.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_Duplicate_Name_And_Type_Returns_409()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        var dto = new { name = "Food", type = "expense" };
        var first = await client.PostAsJsonAsync("api/v1/categories", dto);
        first.StatusCode.Should().Be(HttpStatusCode.Created);

        var second = await client.PostAsJsonAsync("api/v1/categories", dto);
        second.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await second.Content.ReadFromJsonAsync<JsonElement>();
        error.GetProperty("statusCode").GetInt32().Should().Be(409);
        error.GetProperty("errors").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Create_Same_Name_Different_Type_Returns_201()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        var expense = await client.PostAsJsonAsync("api/v1/categories", new { name = "Transfer", type = "expense" });
        expense.StatusCode.Should().Be(HttpStatusCode.Created);

        var income = await client.PostAsJsonAsync("api/v1/categories", new { name = "Transfer", type = "income" });
        income.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Create_Bad_Type_Returns_400()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        var response = await client.PostAsJsonAsync("api/v1/categories", new { name = "Food", type = "savings" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        var fields = error.GetProperty("errors").EnumerateArray()
            .Select(e => e.GetProperty("field").GetString())
            .ToList();

        fields.Should().Contain("type");
    }

    [Fact]
    public async Task List_Filters_By_Type_And_Search()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        await CreateCategoryAsync(client, "Groceries", "expense");
        await CreateCategoryAsync(client, "Salary", "income");
        await CreateCategoryAsync(client, "Transport", "expense");

        var response = await client.GetAsync("api/v1/categories?type=expense&search=Gro");
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        json.GetProperty("totalCount").GetInt32().Should().Be(1);

        var item = json.GetProperty("data").EnumerateArray().Single();
        item.GetProperty("name").GetString().Should().Be("Groceries");
    }

    [Fact]
    public async Task Put_Rename_To_Duplicate_Name_And_Type_Returns_409()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        await CreateCategoryAsync(client, "Food", "expense");
        var groceries = await CreateCategoryAsync(client, "Groceries", "expense");
        var categoryId = groceries.GetProperty("id").GetGuid();

        var response = await client.PutAsJsonAsync($"api/v1/categories/{categoryId}", new { name = "Food", type = "expense" });
        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Put_Not_Owned_Returns_403()
    {
        using var factory = new TestWebAppFactory();
        var clientA = factory.CreateClient();
        await AuthenticateClientAsync(clientA);
        var category = await CreateCategoryAsync(clientA, "Food", "expense");
        var categoryId = category.GetProperty("id").GetGuid();

        var clientB = factory.CreateClient();
        var userB = await TestUserFixture.RegisterAndLoginAsync(clientB);
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userB.Token);

        var response = await clientB.PutAsJsonAsync($"api/v1/categories/{categoryId}", new { name = "Food", type = "expense" });
        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Delete_Not_Owned_Returns_403_And_Not_Found_Returns_404()
    {
        using var factory = new TestWebAppFactory();
        var clientA = factory.CreateClient();
        await AuthenticateClientAsync(clientA);
        var category = await CreateCategoryAsync(clientA, "Food", "expense");
        var categoryId = category.GetProperty("id").GetGuid();

        var clientB = factory.CreateClient();
        var userB = await TestUserFixture.RegisterAndLoginAsync(clientB);
        clientB.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", userB.Token);

        var forbidden = await clientB.DeleteAsync($"api/v1/categories/{categoryId}");
        forbidden.StatusCode.Should().Be(HttpStatusCode.Forbidden);

        var notFound = await clientA.DeleteAsync($"api/v1/categories/{Guid.NewGuid()}");
        notFound.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Delete_Category_With_Linked_Transactions_Returns_409()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        var account = await client.PostAsJsonAsync("api/v1/accounts", new { name = "Cash", type = "cash", balance = 100 });
        account.EnsureSuccessStatusCode();
        var accountId = (await account.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var category = await CreateCategoryAsync(client, "Food", "expense");
        var categoryId = category.GetProperty("id").GetGuid();

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

        var deleteResponse = await client.DeleteAsync($"api/v1/categories/{categoryId}");
        deleteResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var error = await deleteResponse.Content.ReadFromJsonAsync<JsonElement>();
        error.GetProperty("statusCode").GetInt32().Should().Be(409);
        error.GetProperty("errors").GetArrayLength().Should().Be(0);
    }

    [Fact]
    public async Task Type_Lowercase_Strictly_Enforced()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();
        await AuthenticateClientAsync(client);

        var response = await client.PostAsJsonAsync("api/v1/categories", new { name = "Food", type = "Expense" });
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        var fields = error.GetProperty("errors").EnumerateArray()
            .Select(e => e.GetProperty("field").GetString())
            .ToList();

        fields.Should().Contain("type");
    }
}
