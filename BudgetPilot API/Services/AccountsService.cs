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
    /// Retrieves a paginated list of accounts belonging to the specified user,
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
            .Where(a => a.UserId == userId);

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
    /// Retrieves a single account by its unique identifier without applying the
    /// user ownership filter. Ownership is verified separately by the caller
    /// to distinguish between 403 and 404 responses.
    /// </summary>
    /// <param name="id">The unique identifier of the account to retrieve.</param>
    /// <returns>
    /// The matching account if found; otherwise, <see langword="null" />.
    /// </returns>
    public async Task<AccountsOBJ?> GetAccountById(Guid id)
    {
        return await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == id);
    }

    /// <summary>
    /// Creates a new account for the specified user after validating that
    /// the balance is non-negative for non-credit card account types.
    /// </summary>
    /// <param name="dto">The account data provided by the client.</param>
    /// <param name="userId">The unique identifier of the authenticated user who will own the account.</param>
    /// <returns>The newly created account entity.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the balance is negative for a Cash or Bank Account type.
    /// </exception>
    public async Task<AccountsOBJ> CreateAccount(AccountsDTO dto, Guid userId)
    {
        ValidateBalanceForType(dto.Balance, dto.Type);

        var account = new AccountsOBJ
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Type = dto.Type,
            Balance = dto.Balance,
            UserId = userId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Accounts.Add(account);
        await _context.SaveChangesAsync();

        return account;
    }

    /// <summary>
    /// Updates an existing account with new values after verifying ownership and
    /// validating balance constraints for the account type.
    /// </summary>
    /// <param name="id">The unique identifier of the account to update.</param>
    /// <param name="dto">The updated account data provided by the client.</param>
    /// <param name="userId">The unique identifier of the authenticated user.</param>
    /// <returns>
    /// The updated account entity if found and owned by the user;
    /// otherwise, <see langword="null" />.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the balance is negative for a Cash or Bank Account type.
    /// </exception>
    public async Task<AccountsOBJ?> UpdateAccount(Guid id, AccountsDTO dto, Guid userId)
    {
        ValidateBalanceForType(dto.Balance, dto.Type);

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

        if (account == null)
            return null;

        account.Name = dto.Name;
        account.Type = dto.Type;
        account.Balance = dto.Balance;

        await _context.SaveChangesAsync();

        return account;
    }

    /// <summary>
    /// Permanently deletes an account from the database after verifying ownership.
    /// </summary>
    /// <param name="id">The unique identifier of the account to delete.</param>
    /// <param name="userId">The unique identifier of the authenticated user.</param>
    /// <returns>
    /// <see langword="true" /> if the account was found and deleted;
    /// otherwise, <see langword="false" />.
    /// </returns>
    public async Task<bool> DeleteAccount(Guid id, Guid userId)
    {
        // TODO: When Transactions module is implemented, check for linked transactions
        // and return a conflict indicator if any exist. The controller should return 409.
        //   bool hasTransactions = await _context.Transactions
        //       .AnyAsync(t => t.AccountId == id);
        //   if (hasTransactions) return false;

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == id && a.UserId == userId);

        if (account == null)
            return false;

        _context.Accounts.Remove(account);
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// Validates that the balance is non-negative for Cash and Bank Account types.
    /// Credit Card accounts are allowed to have a negative balance to represent debt.
    /// </summary>
    /// <param name="balance">The balance value to validate.</param>
    /// <param name="type">The account type that determines the validation rule.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when the balance is negative and the account type is not Credit Card.
    /// </exception>
    private static void ValidateBalanceForType(decimal balance, string type)
    {
        if (type != "creditCard" && balance < 0)
            throw new ArgumentException("Balance must not be negative for Cash and Bank Account types.");
    }
}
