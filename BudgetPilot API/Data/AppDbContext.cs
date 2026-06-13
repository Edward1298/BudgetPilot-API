using Microsoft.EntityFrameworkCore;
using System.Security.Principal;
using BudgetPilot_API.Entities;

public class AppDbContext : DbContext // This class is a DbContext that represents the database context for the application. It is responsible for managing the database connection and providing access to the database tables. The AppDbContext class inherits from the DbContext class provided by Entity Framework Core, which is an Object-Relational Mapping (ORM) framework for .NET applications. The AppDbContext class defines DbSet properties for each entity in the application, which represent the corresponding tables in the database. In this case, it defines a DbSet for the UsersOBJ entity, which corresponds to the "users" table in the database.
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) // This is the constructor for the AppDbContext class. It takes a DbContextOptions<AppDbContext> object as a parameter, which contains the configuration options for the database context. The constructor calls the base constructor of the DbContext class, passing the options parameter to it. This allows the AppDbContext to be configured with the necessary options for connecting to the database, such as the connection string and other settings.
    {
    }
    // Define DbSet properties for each entity in the application. These properties represent the corresponding tables in the database and allow you to perform CRUD operations on those tables using Entity Framework Core.
    public DbSet<UsersOBJ> Users { get; set; }
    public DbSet<AccountsOBJ> Accounts { get; set; }
    //public DbSet<Transaction> Transactions { get; set; }
    //public DbSet<Category> Categories { get; set; }
    //public DbSet<Budget> Budgets { get; set; }
}