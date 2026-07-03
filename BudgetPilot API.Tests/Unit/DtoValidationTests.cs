using BudgetPilot_API.Dtos;
using System.ComponentModel.DataAnnotations;

namespace BudgetPilot_API.Tests.Unit;

public class DtoValidationTests
{
    private static IList<ValidationResult> Validate(object dto)
    {
        var context = new ValidationContext(dto);
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(dto, context, results, validateAllProperties: true);
        return results;
    }

    [Fact]
    public void AccountsDTO_Invalid_Type_Fails()
    {
        var dto = new AccountsDTO { Name = "Valid", Type = "invalid", Balance = 0 };
        var results = Validate(dto);

        results.Should().Contain(r => r.MemberNames.Contains("Type"));
    }

    [Fact]
    public void AccountsDTO_Name_Whitespace_Only_Fails()
    {
        var dto = new AccountsDTO { Name = "   ", Type = "cash", Balance = 0 };
        var results = Validate(dto);

        results.Should().Contain(r => r.MemberNames.Contains("Name"));
    }

    [Fact]
    public void CategoriesDTO_Invalid_Type_Fails()
    {
        var dto = new CategoriesDTO { Name = "Valid", Type = "savings" };
        var results = Validate(dto);

        results.Should().Contain(r => r.MemberNames.Contains("Type"));
    }

    [Fact]
    public void CategoriesDTO_Whitespace_Name_Fails()
    {
        var dto = new CategoriesDTO { Name = "   ", Type = "expense" };
        var results = Validate(dto);

        results.Should().Contain(r => r.MemberNames.Contains("Name"));
    }

    [Fact]
    public void TransactionsDTO_Amount_Zero_Fails()
    {
        var dto = new TransactionsDTO
        {
            AccountId = Guid.NewGuid(),
            CategoryId = Guid.NewGuid(),
            Amount = 0,
            Type = "expense"
        };
        var results = Validate(dto);

        results.Should().Contain(r => r.MemberNames.Contains("Amount"));
    }

    [Fact]
    public void TransactionsDTO_Invalid_Type_Fails()
    {
        var dto = new TransactionsDTO
        {
            AccountId = Guid.NewGuid(),
            CategoryId = Guid.NewGuid(),
            Amount = 10,
            Type = "withdrawal"
        };
        var results = Validate(dto);

        results.Should().Contain(r => r.MemberNames.Contains("Type"));
    }

    [Fact]
    public void TransactionsDTO_Description_Too_Long_Fails()
    {
        var dto = new TransactionsDTO
        {
            AccountId = Guid.NewGuid(),
            CategoryId = Guid.NewGuid(),
            Amount = 10,
            Type = "expense",
            Description = new string('x', 501)
        };
        var results = Validate(dto);

        results.Should().Contain(r => r.MemberNames.Contains("Description"));
    }

    [Fact]
    public void RegisterDTO_Invalid_Email_Fails()
    {
        var dto = new RegisterDTO { Name = "Test", Email = "not-an-email", Password = "Password123!" };
        var results = Validate(dto);

        results.Should().Contain(r => r.MemberNames.Contains("Email"));
    }

    [Fact]
    public void RegisterDTO_Short_Password_Fails()
    {
        var dto = new RegisterDTO { Name = "Test", Email = "test@example.com", Password = "short" };
        var results = Validate(dto);

        results.Should().Contain(r => r.MemberNames.Contains("Password"));
    }

    [Fact]
    public void LoginDTO_Missing_Email_Fails()
    {
        var dto = new LoginDTO { Email = "", Password = "Password123!" };
        var results = Validate(dto);

        results.Should().Contain(r => r.MemberNames.Contains("Email"));
    }
}
