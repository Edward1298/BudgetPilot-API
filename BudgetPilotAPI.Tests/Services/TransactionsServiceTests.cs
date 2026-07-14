using BudgetPilot_API.Dtos;
using BudgetPilot_API.Entities;
using Microsoft.Extensions.DependencyInjection;

namespace BudgetPilot_API.Tests.Services;

public class TransactionsServiceTests
{
    private static (AppDbContext Db, TransactionsService Service, Guid UserId) CreateService()
    {
        var factory = new Integration.TestWebAppFactory();
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<TransactionsService>();

        var userId = Guid.NewGuid();
        db.Users.Add(new UsersOBJ
        {
            Id = userId,
            Name = "Test",
            Email = $"test-{userId}@example.com",
            PasswordHash = "$2a$10$abcdefghijklmnopqrstuvwxycdefghijklmnopqrstuv",
            RoleId = Integration.TestWebAppFactory.UserRoleId,
            CreatedAt = DateTime.UtcNow
        });

        return (db, service, userId);
    }

    private static async Task<AccountsOBJ> CreateAccountAsync(AppDbContext db, Guid userId, string type, decimal balance)
    {
        var account = new AccountsOBJ
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Test Account",
            Type = type,
            Balance = balance,
            CreatedAt = DateTime.UtcNow
        };

        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        return account;
    }

    private static async Task<CategoriesOBJ> CreateCategoryAsync(AppDbContext db, Guid userId, string type)
    {
        var category = new CategoriesOBJ
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Test Category",
            Type = type
        };

        db.Categories.Add(category);
        await db.SaveChangesAsync();
        return category;
    }

    [Fact]
    public async Task Create_Expense_Decreases_Balance_By_Amount()
    {
        var (db, service, userId) = CreateService();
        var account = await CreateAccountAsync(db, userId, "cash", 100.50m);
        var category = await CreateCategoryAsync(db, userId, "expense");

        var dto = new TransactionsDTO
        {
            AccountId = account.Id,
            CategoryId = category.Id,
            Amount = 25.25m
        };

        await service.CreateTransaction(dto, userId);

        account.Balance.Should().Be(75.25m);
    }

    [Fact]
    public async Task Create_Income_Increases_Balance_By_Amount()
    {
        var (db, service, userId) = CreateService();
        var account = await CreateAccountAsync(db, userId, "cash", 50m);
        var category = await CreateCategoryAsync(db, userId, "income");

        var dto = new TransactionsDTO
        {
            AccountId = account.Id,
            CategoryId = category.Id,
            Amount = 123.45m
        };

        await service.CreateTransaction(dto, userId);

        account.Balance.Should().Be(173.45m);
    }

    [Fact]
    public async Task Update_Changing_Amount_Recomputes_Balance_Correctly()
    {
        var (db, service, userId) = CreateService();
        var account = await CreateAccountAsync(db, userId, "cash", 100m);
        var category = await CreateCategoryAsync(db, userId, "expense");

        var createDto = new TransactionsDTO
        {
            AccountId = account.Id,
            CategoryId = category.Id,
            Amount = 10m
        };

        var (transaction, _, _) = await service.CreateTransaction(createDto, userId);
        account.Balance.Should().Be(90m);

        var updateDto = new TransactionUpdateDTO
        {
            Amount = 35m
        };

        await service.UpdateTransaction(transaction!.Id, updateDto, userId);

        account.Balance.Should().Be(65m);
    }

    [Fact]
    public async Task Update_Changing_Account_Reverses_Old_And_Applies_New()
    {
        var (db, service, userId) = CreateService();
        var accountA = await CreateAccountAsync(db, userId, "cash", 100m);
        var accountB = await CreateAccountAsync(db, userId, "cash", 200m);
        var category = await CreateCategoryAsync(db, userId, "expense");

        var createDto = new TransactionsDTO
        {
            AccountId = accountA.Id,
            CategoryId = category.Id,
            Amount = 50m
        };

        var (transaction, _, _) = await service.CreateTransaction(createDto, userId);
        accountA.Balance.Should().Be(50m);
        accountB.Balance.Should().Be(200m);

        var updateDto = new TransactionUpdateDTO
        {
            AccountId = accountB.Id,
            Amount = 30m
        };

        await service.UpdateTransaction(transaction!.Id, updateDto, userId);

        accountA.Balance.Should().Be(100m);
        accountB.Balance.Should().Be(170m);
    }

    [Fact]
    public async Task Delete_Reverses_Balance_Effect()
    {
        var (db, service, userId) = CreateService();
        var account = await CreateAccountAsync(db, userId, "cash", 100m);
        var category = await CreateCategoryAsync(db, userId, "expense");

        var dto = new TransactionsDTO
        {
            AccountId = account.Id,
            CategoryId = category.Id,
            Amount = 42.17m
        };

        var (transaction, _, _) = await service.CreateTransaction(dto, userId);
        account.Balance.Should().Be(57.83m);

        await service.DeleteTransaction(transaction!.Id, userId, true);

        account.Balance.Should().Be(100m);
    }
}
