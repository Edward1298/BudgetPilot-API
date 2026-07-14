using BudgetPilot_API.Tests.Fixtures;
using System.Net.Http.Headers;
using System.Text.Json;

namespace BudgetPilot_API.Tests.Integration;

public class EndToEndFlowTests
{
    [Fact]
    public async Task Full_User_Journey_Works()
    {
        using var factory = new TestWebAppFactory();
        var client = factory.CreateClient();

        var user = await TestUserFixture.RegisterAndLoginAsync(client);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);

        var accountResponse = await client.PostAsJsonAsync("api/v1/accounts", new { name = "Cash", type = "cash", balance = 100 });
        accountResponse.EnsureSuccessStatusCode();
        var accountId = (await accountResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var categoryResponse = await client.PostAsJsonAsync("api/v1/categories", new { name = "Food", type = "expense" });
        categoryResponse.EnsureSuccessStatusCode();
        var categoryId = (await categoryResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var txResponse = await client.PostAsJsonAsync("api/v1/transactions", new { accountId, categoryId, amount = 25, type = "expense", description = "Lunch" });
        txResponse.EnsureSuccessStatusCode();
        var txId = (await txResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var list = await client.GetAsync("api/v1/transactions");
        list.EnsureSuccessStatusCode();
        var listJson = await list.Content.ReadFromJsonAsync<JsonElement>();
        listJson.GetProperty("totalCount").GetInt32().Should().Be(1);

        var account = await client.GetAsync($"api/v1/accounts/{accountId}");
        account.EnsureSuccessStatusCode();
        var balance = (await account.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("balance").GetDecimal();
        balance.Should().Be(75);

        var me = await client.GetAsync("api/v1/users/me");
        me.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
