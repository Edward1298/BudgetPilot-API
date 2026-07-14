using BudgetPilot_API.Dtos;
using Microsoft.EntityFrameworkCore;

public class ReportsService
{
    private readonly AppDbContext _context;

    public ReportsService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<ReportResponseDTO> GetSummary(Guid userId, DateOnly from, DateOnly to, string? type)
    {
        var effectiveType = string.IsNullOrWhiteSpace(type) ? "expense" : type;

        var transactions = await _context.Transactions
            .Where(t => t.UserId == userId
                && t.Type == effectiveType
                && t.Date >= from
                && t.Date <= to)
            .Join(_context.Categories,
                t => t.CategoryId,
                c => c.Id,
                (t, c) => new { t.Amount, t.CategoryId, CategoryName = c.Name })
            .GroupBy(x => new { x.CategoryId, x.CategoryName })
            .Select(g => new
            {
                CategoryId = g.Key.CategoryId,
                CategoryName = g.Key.CategoryName,
                TotalAmount = g.Sum(x => x.Amount),
                TransactionCount = g.Count()
            })
            .OrderByDescending(x => x.TotalAmount)
            .ToListAsync();

        var totalAmount = transactions.Sum(x => x.TotalAmount);
        var totalCount = transactions.Sum(x => x.TransactionCount);

        return new ReportResponseDTO
        {
            From = from,
            To = to,
            Type = effectiveType,
            TotalAmount = totalAmount,
            TotalTransactions = totalCount,
            Categories = transactions.Select(x => new CategoryReportDTO
            {
                CategoryId = x.CategoryId,
                CategoryName = x.CategoryName,
                TotalAmount = x.TotalAmount,
                Percentage = totalAmount > 0 ? Math.Round(x.TotalAmount / totalAmount * 100, 1) : 0,
                TransactionCount = x.TransactionCount
            }).ToList()
        };
    }
}
