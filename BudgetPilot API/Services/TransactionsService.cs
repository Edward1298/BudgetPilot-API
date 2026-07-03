using BudgetPilot_API.Dtos;
using BudgetPilot_API.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Provides business logic for transaction operations including CRUD, pagination,
/// ownership filtering, foreign-key ownership verification, type matching,
/// and automatic account balance adjustment.
/// </summary>
public class TransactionsService
{
    private readonly AppDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionsService"/> class
    /// with the specified database context.
    /// </summary>
    /// <param name="context">The application database context for accessing the transactions table.</param>
    public TransactionsService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Retrieves a paginated list of transactions belonging to the specified user,
    /// optionally filtered by type, account, category, date range, and/or description.
    /// Results are ordered by date descending (newest first).
    /// </summary>
    /// <param name="userId">The unique identifier of the authenticated user.</param>
    /// <param name="page">The one-based page number to retrieve.</param>
    /// <param name="pageSize">The number of items per page (1–100).</param>
    /// <param name="type">Optional filter by transaction type.</param>
    /// <param name="accountId">Optional filter by linked account identifier.</param>
    /// <param name="categoryId">Optional filter by linked category identifier.</param>
    /// <param name="from">Optional inclusive lower bound on the transaction date.</param>
    /// <param name="to">Optional inclusive upper bound on the transaction date.</param>
    /// <param name="search">Optional partial match on the transaction description.</param>
    /// <returns>A tuple containing the list of transactions and the total matching count.</returns>
    public async Task<(List<TransactionsOBJ> Items, int TotalCount)> GetTransactions(
        Guid userId, int page, int pageSize, string? type,
        Guid? accountId, Guid? categoryId, DateOnly? from, DateOnly? to, string? search)
    {
        var query = _context.Transactions
            .Where(t => t.UserId == userId);

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(t => t.Type == type);

        if (accountId.HasValue)
            query = query.Where(t => t.AccountId == accountId.Value);

        if (categoryId.HasValue)
            query = query.Where(t => t.CategoryId == categoryId.Value);

        if (from.HasValue)
            query = query.Where(t => t.Date >= from.Value);

        if (to.HasValue)
            query = query.Where(t => t.Date <= to.Value);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(t => t.Description != null && t.Description.Contains(search));

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(t => t.Date)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    /// <summary>
    /// Retrieves a single transaction by its unique identifier without applying the
    /// user ownership filter. Ownership is verified separately by the caller
    /// to distinguish between 403 and 404 responses.
    /// </summary>
    /// <param name="id">The unique identifier of the transaction to retrieve.</param>
    /// <returns>
    /// The matching transaction if found; otherwise, <see langword="null" />.
    /// </returns>
    public async Task<TransactionsOBJ?> GetTransactionById(Guid id)
    {
        return await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    /// <summary>
    /// Creates a new transaction for the specified user, verifies that the referenced
    /// account and category belong to the user, ensures the transaction type matches
    /// the category type, adjusts the account balance, and persists both atomically.
    /// </summary>
    /// <param name="dto">The transaction data provided by the client.</param>
    /// <param name="userId">The unique identifier of the authenticated user who will own the transaction.</param>
    /// <returns>
    /// The newly created transaction entity if the referenced account and category
    /// are valid; otherwise, <see langword="null" />.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the transaction type does not match the referenced category's type.
    /// </exception>
    public async Task<TransactionsOBJ?> CreateTransaction(TransactionsDTO dto, Guid userId)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == dto.AccountId && a.UserId == userId);

        if (account == null)
            return null;

        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == dto.CategoryId && c.UserId == userId);

        if (category == null)
            return null;

        if (dto.Type != category.Type)
            throw new ArgumentException("Transaction type must match the referenced category's type.");

        var transaction = new TransactionsOBJ
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountId = dto.AccountId,
            CategoryId = dto.CategoryId,
            Amount = dto.Amount,
            Type = dto.Type,
            Description = dto.Description,
            Date = DateOnly.FromDateTime(DateTime.UtcNow)
        };

        ApplyBalanceEffect(account, dto.Amount, dto.Type, reverse: false);

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        return transaction;
    }

    /// <summary>
    /// Updates an existing transaction after verifying ownership, reversing the old
    /// balance effect, applying the new balance effect, and ensuring the new
    /// account, category, and type combination is valid. The transaction date is
    /// never modified.
    /// </summary>
    /// <param name="id">The unique identifier of the transaction to update.</param>
    /// <param name="dto">The updated transaction data provided by the client.</param>
    /// <param name="userId">The unique identifier of the authenticated user.</param>
    /// <returns>
    /// The updated transaction entity if found and all referenced entities are valid;
    /// otherwise, <see langword="null" />.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the transaction type does not match the referenced category's type.
    /// </exception>
    public async Task<TransactionsOBJ?> UpdateTransaction(Guid id, TransactionsDTO dto, Guid userId)
    {
        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

        if (transaction == null)
            return null;

        var oldAccount = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == transaction.AccountId && a.UserId == userId);

        if (oldAccount == null)
            return null;

        var newAccount = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == dto.AccountId && a.UserId == userId);

        if (newAccount == null)
            return null;

        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == dto.CategoryId && c.UserId == userId);

        if (category == null)
            return null;

        if (dto.Type != category.Type)
            throw new ArgumentException("Transaction type must match the referenced category's type.");

        ApplyBalanceEffect(oldAccount, transaction.Amount, transaction.Type, reverse: true);
        ApplyBalanceEffect(newAccount, dto.Amount, dto.Type, reverse: false);

        transaction.AccountId = dto.AccountId;
        transaction.CategoryId = dto.CategoryId;
        transaction.Amount = dto.Amount;
        transaction.Type = dto.Type;
        transaction.Description = dto.Description;

        await _context.SaveChangesAsync();

        return transaction;
    }

    /// <summary>
    /// Permanently deletes a transaction after verifying ownership and reverses
    /// its balance effect on the linked account in a single atomic operation.
    /// </summary>
    /// <param name="id">The unique identifier of the transaction to delete.</param>
    /// <param name="userId">The unique identifier of the authenticated user.</param>
    /// <returns>
    /// <see langword="true" /> if the transaction was found and deleted;
    /// otherwise, <see langword="false" />.
    /// </returns>
    public async Task<bool> DeleteTransaction(Guid id, Guid userId)
    {
        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

        if (transaction == null)
            return false;

        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == transaction.AccountId && a.UserId == userId);

        if (account == null)
            return false;

        ApplyBalanceEffect(account, transaction.Amount, transaction.Type, reverse: true);

        _context.Transactions.Remove(transaction);
        await _context.SaveChangesAsync();

        return true;
    }

    /// <summary>
    /// Applies or reverses a transaction's balance effect on the specified account.
    /// Income increases the balance; expense decreases it. Reversing inverts the effect.
    /// </summary>
    /// <param name="account">The account whose balance will be modified.</param>
    /// <param name="amount">The transaction amount.</param>
    /// <param name="type">The transaction type, either "income" or "expense".</param>
    /// <param name="reverse">
    /// <see langword="true" /> to reverse the effect; <see langword="false" /> to apply it normally.
    /// </param>
    private static void ApplyBalanceEffect(AccountsOBJ account, decimal amount, string type, bool reverse)
    {
        var multiplier = reverse ? -1 : 1;

        if (type == "income")
            account.Balance += amount * multiplier;
        else
            account.Balance -= amount * multiplier;
    }
}
