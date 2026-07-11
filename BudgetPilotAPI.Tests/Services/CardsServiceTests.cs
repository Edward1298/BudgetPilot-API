using BudgetPilot_API.Dtos;
using BudgetPilot_API.Entities;
using BudgetPilot_API.Services;
using Microsoft.Extensions.DependencyInjection;

namespace BudgetPilot_API.Tests.Services;

public class CardsServiceTests
{
    private static (AppDbContext Db, CardsService Service, Guid UserId) CreateService()
    {
        var factory = new Integration.TestWebAppFactory();
        var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var service = scope.ServiceProvider.GetRequiredService<CardsService>();

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

    private static async Task<AccountsOBJ> CreateAccountAsync(AppDbContext db, Guid userId)
    {
        var account = new AccountsOBJ
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Test Account",
            Type = "cash",
            Balance = 1000,
            CreatedAt = DateTime.UtcNow
        };

        db.Accounts.Add(account);
        await db.SaveChangesAsync();
        return account;
    }

    [Fact]
    public async Task CreateCard_Debit_Succeeds()
    {
        var (db, service, userId) = CreateService();
        var account = await CreateAccountAsync(db, userId);

        var dto = new CardsDTO
        {
            AccountId = account.Id,
            Type = "debit",
            CardNumber = "4111111111111111",
            ExpirationDate = new DateOnly(2027, 12, 31),
            Cvc = "123",
            NameOnCard = "Test User"
        };

        var card = await service.CreateCard(dto, userId);

        card.Should().NotBeNull();
        card.Type.Should().Be("debit");
        card.CardNumber.Should().Be("4111111111111111");
        card.Cvc.Should().Be("123");
    }

    [Fact]
    public async Task CreateCard_Credit_With_CreditLimit_Succeeds()
    {
        var (db, service, userId) = CreateService();
        var account = await CreateAccountAsync(db, userId);

        var dto = new CardsDTO
        {
            AccountId = account.Id,
            Type = "credit",
            CardNumber = "5500000000000004",
            ExpirationDate = new DateOnly(2028, 6, 30),
            Cvc = "456",
            NameOnCard = "Test User",
            CreditLimit = 5000,
            Apr = 18.5m
        };

        var card = await service.CreateCard(dto, userId);

        card.Should().NotBeNull();
        card.Type.Should().Be("credit");
        card.CreditLimit.Should().Be(5000);
        card.Apr.Should().Be(18.5m);
    }

    [Fact]
    public async Task CreateCard_Account_Not_Owned_Throws()
    {
        var (db, service, userId) = CreateService();
        var otherUserId = Guid.NewGuid();

        db.Users.Add(new UsersOBJ
        {
            Id = otherUserId,
            Name = "Other",
            Email = $"other-{otherUserId}@example.com",
            PasswordHash = "hash",
            RoleId = Integration.TestWebAppFactory.UserRoleId,
            CreatedAt = DateTime.UtcNow
        });

        var account = await CreateAccountAsync(db, otherUserId);

        var dto = new CardsDTO
        {
            AccountId = account.Id,
            Type = "debit",
            CardNumber = "4111111111111111",
            ExpirationDate = new DateOnly(2027, 12, 31),
            Cvc = "123",
            NameOnCard = "Test User"
        };

        var action = async () => await service.CreateCard(dto, userId);

        await action.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UpdateCard_Changes_NameOnCard()
    {
        var (db, service, userId) = CreateService();
        var account = await CreateAccountAsync(db, userId);

        var createDto = new CardsDTO
        {
            AccountId = account.Id,
            Type = "debit",
            CardNumber = "4111111111111111",
            ExpirationDate = new DateOnly(2027, 12, 31),
            Cvc = "123",
            NameOnCard = "Old Name"
        };

        var card = await service.CreateCard(createDto, userId);

        var updateDto = new CardUpdateDTO
        {
            NameOnCard = "New Name"
        };

        var updated = await service.UpdateCard(card.Id, updateDto, userId);

        updated.Should().NotBeNull();
        updated!.NameOnCard.Should().Be("New Name");
    }

    [Fact]
    public async Task DeleteCard_Soft_Delete_Sets_Inactive()
    {
        var (db, service, userId) = CreateService();
        var account = await CreateAccountAsync(db, userId);

        var createDto = new CardsDTO
        {
            AccountId = account.Id,
            Type = "debit",
            CardNumber = "4111111111111111",
            ExpirationDate = new DateOnly(2027, 12, 31),
            Cvc = "123",
            NameOnCard = "Test User"
        };

        var card = await service.CreateCard(createDto, userId);

        var deleted = await service.DeleteCard(card.Id, userId, false);

        deleted.Should().BeTrue();

        var dbCard = await db.Cards.FindAsync(card.Id);
        dbCard!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task GetCardById_Decrypts_CardNumber_And_Cvc()
    {
        var (db, service, userId) = CreateService();
        var account = await CreateAccountAsync(db, userId);

        var createDto = new CardsDTO
        {
            AccountId = account.Id,
            Type = "debit",
            CardNumber = "4111111111111111",
            ExpirationDate = new DateOnly(2027, 12, 31),
            Cvc = "123",
            NameOnCard = "Test User"
        };

        var created = await service.CreateCard(createDto, userId);

        var retrieved = await service.GetCardById(created.Id);

        retrieved.Should().NotBeNull();
        retrieved!.CardNumber.Should().Be("4111111111111111");
        retrieved.Cvc.Should().Be("123");
    }
}
