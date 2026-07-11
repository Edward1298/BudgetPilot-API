using Microsoft.EntityFrameworkCore;
using System.Security.Principal;
using BudgetPilot_API.Entities;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<RolesOBJ> Roles { get; set; }
    public DbSet<UsersOBJ> Users { get; set; }
    public DbSet<AccountsOBJ> Accounts { get; set; }
    public DbSet<CategoriesOBJ> Categories { get; set; }
    public DbSet<TransactionsOBJ> Transactions { get; set; }
    public DbSet<CardsOBJ> Cards { get; set; }
    public DbSet<BudgetsOBJ> Budgets { get; set; }
    public DbSet<RefreshTokensOBJ> RefreshTokens { get; set; }
}