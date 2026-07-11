using BudgetPilot_API.Dtos;
using BudgetPilot_API.Entities;
using Microsoft.EntityFrameworkCore;

public class BudgetsService
{
    private readonly AppDbContext _context;

    public BudgetsService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<(List<BudgetsOBJ> Items, int TotalCount)> GetBudgets(
        Guid userId, int page, int pageSize, int? month = null, int? year = null, Guid? categoryId = null)
    {
        var query = _context.Budgets
            .Where(b => b.UserId == userId && b.IsActive);

        if (month.HasValue)
            query = query.Where(b => b.Month == month.Value);

        if (year.HasValue)
            query = query.Where(b => b.Year == year.Value);

        if (categoryId.HasValue)
            query = query.Where(b => b.CategoryId == categoryId.Value);

        var totalCount = await query.CountAsync();

        var items = await query
            .OrderByDescending(b => b.Year)
            .ThenByDescending(b => b.Month)
            .ThenBy(b => b.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalCount);
    }

    public async Task<BudgetsOBJ?> GetBudgetById(Guid id)
    {
        return await _context.Budgets
            .FirstOrDefaultAsync(b => b.Id == id && b.IsActive);
    }

    public async Task<(BudgetsOBJ? Budget, bool IsConflict)> CreateBudget(BudgetsDTO dto, Guid userId)
    {
        var categoryExists = await _context.Categories
            .AnyAsync(c => c.Id == dto.CategoryId && c.UserId == userId && c.IsActive);

        if (!categoryExists)
            return (null, false);

        var exists = await _context.Budgets
            .AnyAsync(b => b.UserId == userId
                && b.CategoryId == dto.CategoryId
                && b.Month == dto.Month
                && b.Year == dto.Year
                && b.IsActive);

        if (exists)
            return (null, true);

        var budget = new BudgetsOBJ
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CategoryId = dto.CategoryId,
            Amount = dto.Amount,
            Month = dto.Month,
            Year = dto.Year,
            IsActive = true,
            CreatedAt = DateTime.UtcNow
        };

        _context.Budgets.Add(budget);
        await _context.SaveChangesAsync();

        return (budget, false);
    }

    public async Task<(BudgetsOBJ? Budget, bool IsConflict)> UpdateBudget(Guid id, BudgetUpdateDTO dto, Guid userId)
    {
        var budget = await _context.Budgets
            .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId && b.IsActive);

        if (budget == null)
            return (null, false);

        var newCategoryId = dto.CategoryId ?? budget.CategoryId;
        var newMonth = dto.Month ?? budget.Month;
        var newYear = dto.Year ?? budget.Year;

        if (dto.CategoryId.HasValue || dto.Month.HasValue || dto.Year.HasValue)
        {
            var duplicateExists = await _context.Budgets
                .AnyAsync(b => b.UserId == userId
                    && b.Id != id
                    && b.CategoryId == newCategoryId
                    && b.Month == newMonth
                    && b.Year == newYear
                    && b.IsActive);

            if (duplicateExists)
                return (null, true);

            if (dto.CategoryId.HasValue)
            {
                var categoryExists = await _context.Categories
                    .AnyAsync(c => c.Id == dto.CategoryId.Value && c.UserId == userId && c.IsActive);

                if (!categoryExists)
                    return (null, false);
            }
        }

        if (dto.CategoryId.HasValue)
            budget.CategoryId = dto.CategoryId.Value;

        if (dto.Amount.HasValue)
            budget.Amount = dto.Amount.Value;

        if (dto.Month.HasValue)
            budget.Month = dto.Month.Value;

        if (dto.Year.HasValue)
            budget.Year = dto.Year.Value;

        await _context.SaveChangesAsync();

        return (budget, false);
    }

    public async Task<bool> DeleteBudget(Guid id, Guid userId, bool isAdmin)
    {
        var budget = await _context.Budgets
            .FirstOrDefaultAsync(b => b.Id == id && b.UserId == userId);

        if (budget == null)
            return false;

        if (isAdmin)
        {
            _context.Budgets.Remove(budget);
        }
        else
        {
            budget.IsActive = false;
        }

        await _context.SaveChangesAsync();

        return true;
    }
}
