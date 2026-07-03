using BudgetPilot_API.Dtos;
using BudgetPilot_API.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace BudgetPilot_API.Tests.Services;

public class CategoriesServiceTests
{
    private static (AppDbContext Db, CategoriesService Service, Guid UserId) CreateService()
    {
        var factory = new Integration.TestWebAppFactory();
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<CategoriesService>();

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
    public async Task CreateCategory_Duplicate_Name_And_Type_Returns_Conflict()
    {
        var (db, service, userId) = CreateService();

        await service.CreateCategory(new CategoriesDTO { Name = "Food", Type = "expense" }, userId);

        var (_, isConflict) = await service.CreateCategory(new CategoriesDTO { Name = "Food", Type = "expense" }, userId);

        isConflict.Should().BeTrue();
    }

    [Fact]
    public async Task UpdateCategory_To_Duplicate_Name_And_Type_Returns_Conflict()
    {
        var (db, service, userId) = CreateService();

        await service.CreateCategory(new CategoriesDTO { Name = "Food", Type = "expense" }, userId);
        var (groceries, _) = await service.CreateCategory(new CategoriesDTO { Name = "Groceries", Type = "expense" }, userId);

        var (_, isConflict) = await service.UpdateCategory(groceries!.Id, new CategoriesDTO { Name = "Food", Type = "expense" }, userId);

        isConflict.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteCategory_With_Linked_Transactions_Returns_Conflict()
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

        var (_, hasConflict) = await service.DeleteCategory(category.Id, userId);

        hasConflict.Should().BeTrue();
    }
}
