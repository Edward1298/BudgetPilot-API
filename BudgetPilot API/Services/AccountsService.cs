using BudgetPilot_API.Entities;
using BudgetPilot_API.Dtos;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Provides business logic for account operations including CRUD, pagination,
/// ownership filtering, and balance validation.
/// </summary>
public class AccountsService
{
    private readonly AppDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="AccountsService"/> class
    /// with the specified database context.
    /// </summary>
    /// <param name="context">The application database context for accessing the accounts table.</param>
    public AccountsService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Retrieves a paginated list of active accounts belonging to the specified user,
    /// optionally filtered by account type and/or a partial name match.
    /// </summary>
    /// <param name="userId">The unique identifier of the authenticated user.</param>
    /// <param name="page">The one-based page number to retrieve.</param>
    /// <param name="pageSize">The number of items per page (1–100).</param>
    /// <param name="type">Optional filter by account type.</param>
    /// <param name="search">Optional partial match on the account name.</param>
    /// <returns>A tuple containing the list of accounts and the total matching count.</returns>
    public async Task<(List<AccountsOBJ> Items, int TotalCount)> GetAccounts(
        Guid userId, int page, int pageSize, string? type, string? search)
    {
        var query = _context.Accounts
            .Where(a => a.UserId == userId && a.IsActive);

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(a => a.Type == type);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(a => a.Name.Contains(search));

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(a => a.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    /// <summary>
    /// Retrieves a single active account by its unique identifier without applying the
    /// user ownership filter. Ownership is verified separately by the caller
    /// to distinguish between 403 and 404 responses.
    /// </summary>
    /// <param name="id">The unique identifier of the account to retrieve.</param>
    /// <returns>
    /// The matching active account if found; otherwise, <see langword="null" />.
    /// </returns>
    public async Task<AccountsOBJ?> GetAccountById(Guid id)
    {
        return await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == id && a.IsActive);
    }

    /// <summary>
    /// Creates a new account for the specified user after validating that
    /// the balance and interest rate are valid for the account type.
    /// </summary>
    /// <param name="dto">The account data provided by the client.</param>
    /// <param name="userId">The unique identifier of the authenticated user who will own the account.</param>
    /// <returns>The newly created account entity.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the balance or interest rate is invalid for the account type.
    /// </exception>
    public async Task<AccountsOBJ> CreateAccount(AccountsDTO dto, Guid userId)
    {
        ValidateBalanceForType(dto.Balance, dto.Type, dto.InterestRate);

        var account = new AccountsOBJ
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Type = dto.Type,
            Balance = dto.Balance,
            InterestRate = dto.InterestRate,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();

        return account;
    }

    /// <summary>
    /// Updates an existing account with new values after verifying ownership and
    /// validating balance constraints for the account type. Only non-null fields
    /// in the DTO are applied to the account entity.
    /// </summary>
    /// <param name="id">The unique identifier of the account to update.</param>
    /// <param name="dto">The partial update data provided by the client.</param>
    /// <param name="userId">The unique identifier of the authenticated user.</param>
    /// <returns>
    /// The updated account entity if found and owned by the user;
    /// otherwise, <see langword="null" />.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the balance is invalid for the account type, or when the
    /// interest rate is invalid for a savings account.
    /// </exception>
    public async Task<AccountsOBJ?> UpdateAccount(Guid id, AccountUpdateDTO dto, Guid userId)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId && a.IsActive);

        if (account == null)
            return null;

        var newType = dto.Type ?? account.Type;
        var newBalance = dto.Balance ?? account.Balance;
        var newInterestRate = dto.InterestRate ?? account.InterestRate;

        ValidateBalanceForType(newBalance, newType, newInterestRate);

        if (dto.Name != null)
            account.Name = dto.Name;

        if (dto.Type != null)
            account.Type = dto.Type;

        if (dto.Balance.HasValue)
            account.Balance = dto.Balance.Value;

        if (dto.InterestRate.HasValue)
            account.InterestRate = dto.InterestRate;

        await _context.SaveChangesAsync();

        return account;
    }

    /// <summary>
    /// Deletes an account from the database after verifying ownership and checking for
    /// linked transactions. If the caller is an admin, performs a hard delete.
    /// Otherwise, performs a soft delete by setting IsActive to false.
    /// </summary>
    /// <param name="id">The unique identifier of the account to delete.</param>
    /// <param name="userId">The unique identifier of the authenticated user.</param>
    /// <param name="isAdmin">Whether the caller has admin privileges.</param>
    /// <returns>
    /// A tuple indicating whether the account was deleted and whether it has
    /// linked transactions that prevent deletion.
    /// </returns>
    public async Task<(bool Deleted, bool HasConflict)> DeleteAccount(Guid id, Guid userId, bool isAdmin)
    {
        var hasTransactions = await _context.Transactions
            .AnyAsync(t => t.AccountId == id && t.IsActive);

        if (hasTransactions)
            return (false, true);

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

        if (account == null)
            return (false, false);

        if (isAdmin)
        {
            _context.Accounts.Remove(account);
        }
        else
        {
            account.IsActive = false;
        }

        await _context.SaveChangesAsync();

        return (true, false);
    }

    /// <summary>
    /// Validates that the balance and interest rate are valid for the given account type.
    /// Cash accounts require balance >= 0. Bank accounts require balance > 0.
    /// Savings accounts require balance >= 0 and interest rate > 0.
    /// </summary>
    /// <param name="balance">The balance value to validate.</param>
    /// <param name="type">The account type that determines the validation rule.</param>
    /// <param name="interestRate">The optional interest rate for savings accounts.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when the balance or interest rate is invalid for the account type.
    /// </exception>
    private static void ValidateBalanceForType(decimal balance, string type, decimal? interestRate = null)
    {
        switch (type)
        {
            case "cash":
                if (balance < 0)
                    throw new ArgumentException("Balance must not be negative for Cash accounts.");
                break;
            case "bankAccount":
                if (balance <= 0)
                    throw new ArgumentException("Balance must be greater than zero for Bank Account accounts.");
                break;
            case "savingsAccount":
                if (balance < 0)
                    throw new ArgumentException("Balance must not be negative for Savings accounts.");
                if (interestRate == null || interestRate <= 0)
                    throw new ArgumentException("Interest rate is required and must be greater than zero for Savings accounts.");
                break;
            default:
                throw new ArgumentException($"Unknown account type: {type}");
        }
    }
}
