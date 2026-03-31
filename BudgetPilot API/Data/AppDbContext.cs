using Microsoft.EntityFrameworkCore;
using System.Security.Principal;
using BudgetPilot_API.Entities;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<UsersOBJ> Users { get; set; }
    //public DbSet<Account> Accounts { get; set; }
    //public DbSet<Transaction> Transactions { get; set; }
    //public DbSet<Category> Categories { get; set; }
    //public DbSet<Budget> Budgets { get; set; }
}