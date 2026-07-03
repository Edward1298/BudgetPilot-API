using BudgetPilot_API.Dtos;
using BudgetPilot_API.Entities;
using Microsoft.EntityFrameworkCore;

/// <summary>
/// Provides business logic for category operations including CRUD, pagination,
/// ownership filtering, and uniqueness enforcement per user.
/// </summary>
public class CategoriesService
{
    private readonly AppDbContext _context;

    /// <summary>
    /// Initializes a new instance of the <see cref="CategoriesService"/> class
    /// with the specified database context.
    /// </summary>
    /// <param name="context">The application database context for accessing the categories table.</param>
    public CategoriesService(AppDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Retrieves a paginated list of categories belonging to the specified user,
    /// optionally filtered by category type and/or a partial name match.
    /// </summary>
    /// <param name="userId">The unique identifier of the authenticated user.</param>
    /// <param name="page">The one-based page number to retrieve.</param>
    /// <param name="pageSize">The number of items per page (1–100).</param>
    /// <param name="type">Optional filter by category type.</param>
    /// <param name="search">Optional partial match on the category name.</param>
    /// <returns>A tuple containing the list of categories and the total matching count.</returns>
    public async Task<(List<CategoriesOBJ> Items, int TotalCount)> GetCategories(
        Guid userId, int page, int pageSize, string? type, string? search)
    {
        var query = _context.Categories
            .Where(c => c.UserId == userId);

        if (!string.IsNullOrWhiteSpace(type))
            query = query.Where(c => c.Type == type);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(c => c.Name.Contains(search));

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(c => c.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    /// <summary>
    /// Retrieves a single category by its unique identifier without applying the
    /// user ownership filter. Ownership is verified separately by the caller
    /// to distinguish between 403 and 404 responses.
    /// </summary>
    /// <param name="id">The unique identifier of the category to retrieve.</param>
    /// <returns>
    /// The matching category if found; otherwise, <see langword="null" />.
    /// </returns>
    public async Task<CategoriesOBJ?> GetCategoryById(Guid id)
    {
        return await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == id);
    }

    /// <summary>
    /// Creates a new category for the specified user after verifying that the
    /// user does not already have a category with the same name and type.
    /// </summary>
    /// <param name="dto">The category data provided by the client.</param>
    /// <param name="userId">The unique identifier of the authenticated user who will own the category.</param>
    /// <returns>
    /// A tuple containing the newly created category and a flag indicating whether
    /// a conflict with an existing category was detected.
    /// </returns>
    public async Task<(CategoriesOBJ? Category, bool IsConflict)> CreateCategory(
        CategoriesDTO dto, Guid userId)
    {
        var exists = await _context.Categories
            .AnyAsync(c => c.UserId == userId &&
                           c.Name == dto.Name &&
                           c.Type == dto.Type);

        if (exists)
            return (null, true);

        var category = new CategoriesOBJ
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Type = dto.Type,
            UserId = userId
        };

        _context.Categories.Add(category);
        await _context.SaveChangesAsync();

        return (category, false);
    }

    /// <summary>
    /// Updates an existing category with new values after verifying ownership and
    /// ensuring the new name/type combination does not conflict with another
    /// category belonging to the same user.
    /// </summary>
    /// <param name="id">The unique identifier of the category to update.</param>
    /// <param name="dto">The updated category data provided by the client.</param>
    /// <param name="userId">The unique identifier of the authenticated user.</param>
    /// <returns>
    /// A tuple containing the updated category and a flag indicating whether
    /// a conflict with another category was detected. If the category is not found,
    /// both values signal the not-found condition.
    /// </returns>
    public async Task<(CategoriesOBJ? Category, bool IsConflict)> UpdateCategory(
        Guid id, CategoriesDTO dto, Guid userId)
    {
        var duplicateExists = await _context.Categories
            .AnyAsync(c => c.UserId == userId &&
                           c.Id != id &&
                           c.Name == dto.Name &&
                           c.Type == dto.Type);

        if (duplicateExists)
            return (null, true);

        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

        if (category == null)
            return (null, false);

        category.Name = dto.Name;
        category.Type = dto.Type;

        await _context.SaveChangesAsync();

        return (category, false);
    }

    /// <summary>
    /// Permanently deletes a category from the database after verifying ownership
    /// and checking for linked transactions.
    /// </summary>
    /// <param name="id">The unique identifier of the category to delete.</param>
    /// <param name="userId">The unique identifier of the authenticated user.</param>
    /// <returns>
    /// A tuple indicating whether the category was deleted and whether it has
    /// linked transactions that prevent deletion.
    /// </returns>
    public async Task<(bool Deleted, bool HasConflict)> DeleteCategory(Guid id, Guid userId)
    {
        var hasTransactions = await _context.Transactions
            .AnyAsync(t => t.CategoryId == id);

        if (hasTransactions)
            return (false, true);

        var category = await _context.Categories
            .FirstOrDefaultAsync(c => c.Id == id && c.UserId == userId);

        if (category == null)
            return (false, false);

        _context.Categories.Remove(category);
        await _context.SaveChangesAsync();

        return (true, false);
    }
}
