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
    /// Retrieves a paginated list of active transactions belonging to the specified user,
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
    /// Retrieves a single active transaction by its unique identifier without applying the
    /// user ownership filter. Ownership is verified separately by the caller
    /// to distinguish between 403 and 404 responses.
    /// </summary>
    /// <param name="id">The unique identifier of the transaction to retrieve.</param>
    /// <returns>
    /// The matching active transaction if found; otherwise, <see langword="null" />.
    /// </returns>
    public async Task<TransactionsOBJ?> GetTransactionById(Guid id)
    {
        return await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    /// <summary>
    /// Creates a new transaction for the specified user, verifies that the referenced
    /// account and category belong to the user, derives the transaction type from the
    /// category, adjusts the account balance, and persists both atomically.
    /// </summary>
    /// <param name="dto">The transaction data provided by the client.</param>
    /// <param name="userId">The unique identifier of the authenticated user who will own the transaction.</param>
    /// <returns>
    /// A tuple containing the newly created transaction entity, the previous balance,
    /// and the new balance after applying the transaction effect. Returns null if the
    /// referenced account or category is not found or not owned by the user.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the category type is neither income nor expense.
    /// </exception>
    public async Task<(TransactionsOBJ? Transaction, decimal PreviousBalance, decimal NewBalance)> CreateTransaction(TransactionsDTO dto, Guid userId)
    {
        var account = await _context.Accounts
            .FirstOrDefaultAsync(a => a.Id == dto.AccountId && a.UserId == userId);

        if (account == null)
            return (null, 0, 0);

        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == dto.CategoryId && c.UserId == userId);

        if (category == null)
            return (null, 0, 0);

        if (category.Type != "income" && category.Type != "expense")
            throw new ArgumentException("Category type must be either income or expense.");

        if (category.Type == "expense")
        {
            var cards = await _context.Cards
                .Where(c => c.AccountId == dto.AccountId && c.IsActive)
                .ToListAsync();

            if (cards.Any(c => c.Type == "debit") && account.Balance < dto.Amount)
                throw new InvalidOperationException("Insufficient funds.");

            var creditCard = cards.FirstOrDefault(c => c.Type == "credit");
            if (creditCard != null && creditCard.CurrentBalance + dto.Amount > creditCard.CreditLimit)
                throw new InvalidOperationException("Credit limit exceeded.");

            var now = DateTime.UtcNow;
            var budget = await _context.Budgets
                .FirstOrDefaultAsync(b => b.UserId == userId
                    && b.CategoryId == dto.CategoryId
                    && b.Year == now.Year
                    && b.Month == now.Month
                    && b.IsActive);

            if (budget != null)
            {
                var spentThisMonth = await _context.Transactions
                    .Where(t => t.UserId == userId
                        && t.CategoryId == dto.CategoryId
                        && t.Type == "expense"
                        && t.Date.Year == now.Year
                        && t.Date.Month == now.Month)
                    .SumAsync(t => t.Amount);

                if (spentThisMonth + dto.Amount > budget.Amount)
                    throw new InvalidOperationException("Budget limit exceeded for this category.");
            }
        }

        var previousBalance = account.Balance;

        var transaction = new TransactionsOBJ
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AccountId = dto.AccountId,
            CategoryId = dto.CategoryId,
            Amount = dto.Amount,
            Type = category.Type,
            Description = dto.Description,
            Date = DateOnly.FromDateTime(DateTime.UtcNow)
        };

        ApplyBalanceEffect(account, dto.Amount, category.Type, reverse: false);

        var newBalance = account.Balance;

        _context.Transactions.Add(transaction);
        await _context.SaveChangesAsync();

        return (transaction, previousBalance, newBalance);
    }

    /// <summary>
    /// Updates an existing transaction after verifying ownership, reversing the old
    /// balance effect if needed, applying the new balance effect, and ensuring the new
    /// account, category, and type combination is valid. The transaction date is
    /// never modified. Only non-null fields in the DTO are applied. The transaction
    /// type is derived from the referenced category.
    /// </summary>
    /// <param name="id">The unique identifier of the transaction to update.</param>
    /// <param name="dto">The partial update data provided by the client.</param>
    /// <param name="userId">The unique identifier of the authenticated user.</param>
    /// <returns>
    /// A tuple containing the updated transaction entity, the previous balance,
    /// and the new balance after applying the transaction effect. Returns null if
    /// the transaction or referenced entities are not found or not owned by the user.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the category type is neither income nor expense.
    /// </exception>
    public async Task<(TransactionsOBJ? Transaction, decimal PreviousBalance, decimal NewBalance)> UpdateTransaction(Guid id, TransactionUpdateDTO dto, Guid userId)
    {
        var transaction = await _context.Transactions
            .FirstOrDefaultAsync(t => t.Id == id && t.UserId == userId);

        if (transaction == null)
            return (null, 0, 0);

        var newAccountId = dto.AccountId ?? transaction.AccountId;
        var newCategoryId = dto.CategoryId ?? transaction.CategoryId;
        var newAmount = dto.Amount ?? transaction.Amount;

        var accountChanged = dto.AccountId.HasValue && dto.AccountId.Value != transaction.AccountId;
        var categoryChanged = dto.CategoryId.HasValue && dto.CategoryId.Value != transaction.CategoryId;
        var amountChanged = dto.Amount.HasValue && dto.Amount.Value != transaction.Amount;

        decimal previousBalance = 0;
        decimal newBalance = 0;

        if (accountChanged || categoryChanged || amountChanged)
        {
            var oldAccount = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Id == transaction.AccountId && a.UserId == userId);

            if (oldAccount == null)
                return (null, 0, 0);

            var category = await _context.Categories
                .FirstOrDefaultAsync(c => c.Id == newCategoryId && c.UserId == userId && c.IsActive);

            if (category == null)
                return (null, 0, 0);

            if (category.Type != "income" && category.Type != "expense")
                throw new ArgumentException("Category type must be either income or expense.");

            var newType = category.Type;

            var newAccount = accountChanged
                ? await _context.Accounts.FirstOrDefaultAsync(a => a.Id == newAccountId && a.UserId == userId && a.IsActive)
                : oldAccount;

            if (newAccount == null)
                return (null, 0, 0);

            previousBalance = newAccount.Balance;

            ApplyBalanceEffect(oldAccount, transaction.Amount, transaction.Type, reverse: true);
            ApplyBalanceEffect(newAccount, newAmount, newType, reverse: false);

            newBalance = newAccount.Balance;

            transaction.AccountId = newAccountId;
            transaction.CategoryId = newCategoryId;
            transaction.Amount = newAmount;
            transaction.Type = newType;
        }
        else
        {
            var account = await _context.Accounts
                .FirstOrDefaultAsync(a => a.Id == transaction.AccountId && a.UserId == userId);

            if (account == null)
                return (null, 0, 0);

            previousBalance = account.Balance;
            newBalance = account.Balance;
        }

        if (dto.Description != null)
            transaction.Description = dto.Description;

        await _context.SaveChangesAsync();

        return (transaction, previousBalance, newBalance);
    }

    /// <summary>
    /// Deletes a transaction after verifying ownership. Only administrators can delete transactions.
    /// Performs a hard delete and reverses the balance effect on the linked account.
    /// </summary>
    /// <param name="id">The unique identifier of the transaction to delete.</param>
    /// <param name="userId">The unique identifier of the authenticated user.</param>
    /// <param name="isAdmin">Whether the caller has admin privileges.</param>
    /// <returns>
    /// <see langword="true" /> if the transaction was found and deleted;
    /// otherwise, <see langword="false" />.
    /// </returns>
    /// <exception cref="UnauthorizedAccessException">
    /// Thrown when a non-admin user attempts to delete a transaction.
    /// </exception>
    public async Task<bool> DeleteTransaction(Guid id, Guid userId, bool isAdmin)
    {
        if (!isAdmin)
            throw new UnauthorizedAccessException("Only administrators can delete transactions.");

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
    /// Retrieves all transactions for a specific account, optionally filtered by month,
    /// type, and date range. Used by admin endpoints.
    /// </summary>
    public async Task<List<TransactionsOBJ>> GetTransactionsByAccountId(
        Guid accountId, int? month = null, string? type = null,
        DateOnly? from = null, DateOnly? to = null)
    {
        var query = _context.Transactions
            .Where(t => t.AccountId == accountId).AsQueryable();

        if (month.HasValue)
            query = query.Where(t => t.Date.Month == month.Value);

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(t => t.Type == type);

        if (from.HasValue)
            query = query.Where(t => t.Date >= from.Value);

        if (to.HasValue)
            query = query.Where(t => t.Date <= to.Value);

        return await query
            .OrderByDescending(t => t.Date)
            .ToListAsync();
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
