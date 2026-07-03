using BudgetPilot_API.Dtos;
using BudgetPilot_API.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace BudgetPilot_API.Tests.Services;

public class AccountsServiceTests
{
    private static (AppDbContext Db, AccountsService Service, Guid UserId) CreateService()
    {
        var factory = new Integration.TestWebAppFactory();
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<AccountsService>();

        var userId = Guid.NewGuid();
        db.Users.Add(new UsersOBJ
        {
            Id = userId,
            Name = "Test",
            Email = $"test-{userId}@example.com",
            PasswordHash = "$2a$10$abcdefghijklmnopqrstuvwxycdefghijklmnopqrstuv",
            CreatedAt = DateTime.UtcNow
        });

        return (db, service, userId);
    }

    [Fact]
    public async Task CreateAccount_Cash_With_Negative_Balance_Throws_ArgumentException()
    {
        var (db, service, userId) = CreateService();

        var dto = new AccountsDTO
        {
            Name = "Cash",
            Type = "cash",
            Balance = -10
        };

        var action = async () => await service.CreateAccount(dto, userId);

        await action.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task CreateAccount_CreditCard_With_Negative_Balance_Succeeds()
    {
        var (db, service, userId) = CreateService();

        var dto = new AccountsDTO
        {
            Name = "Credit Card",
            Type = "creditCard",
            Balance = -500
        };

        var account = await service.CreateAccount(dto, userId);

        account.Balance.Should().Be(-500);
    }

    [Fact]
    public async Task DeleteAccount_With_Linked_Transactions_Returns_Conflict()
    {
        var (db, service, userId) = CreateService();

        var account = new AccountsOBJ
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Account",
            Type = "cash",
            Balance = 100,
            CreatedAt = DateTime.UtcNow
        };
        db.Accounts.Add(account);

        var category = new CategoriesOBJ
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Food",
            Type = "expense"
        };
        db.Categories.Add(category);

        db.Transactions.Add(new TransactionsOBJ
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountId = account.Id,
            CategoryId = category.Id,
            Amount = 10,
            Type = "expense",
            Date = DateOnly.FromDateTime(DateTime.UtcNow)
        });

        await db.SaveChangesAsync();

        var (_, hasConflict) = await service.DeleteAccount(account.Id, userId);

        hasConflict.Should().BeTrue();
    }
}
