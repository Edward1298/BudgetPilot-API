using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace BudgetPilot_API.Tests.Integration;

    /// <summary>
    /// Factory that bootstraps the API with an in-memory SQLite database for isolated integration tests.
    /// Each factory instance owns a single SQLite in-memory connection that is disposed with the factory.
    /// The production SQL Server registration is replaced so tests do not require a real SQL Server instance.
    /// </summary>
public class TestWebAppFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;

    /// <summary>
    /// Initializes a new instance of the <see cref="TestWebAppFactory"/> class
    /// and opens the shared SQLite in-memory connection.
    /// </summary>
    public TestWebAppFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
    }

    /// <summary>
    /// Replaces the production SQL Server DbContext registration with a SQLite in-memory
    /// registration and ensures the schema is created from the entity model.
    /// </summary>
    /// <param name="builder">The web host builder used to configure the test server.</param>
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var providerNamespaces = new[]
            {
                "Microsoft.EntityFrameworkCore",
                "Microsoft.EntityFrameworkCore.SqlServer"
            };

            bool IsEfDescriptor(ServiceDescriptor d)
            {
                var types = new[]
                {
                    d.ServiceType,
                    d.ImplementationType,
                    d.ImplementationInstance?.GetType(),
                    d.ImplementationFactory?.GetType()
                };

                return types.Any(t =>
                    t != null &&
                    t.Namespace != null &&
                    providerNamespaces.Any(ns => t.Namespace.StartsWith(ns)));
            }

            var descriptors = services.Where(IsEfDescriptor).ToList();
            foreach (var descriptor in descriptors)
                services.Remove(descriptor);

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlite(_connection));

            using var scope = services.BuildServiceProvider().CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            db.Database.EnsureCreated();
        });
    }

    /// <summary>
    /// Disposes the factory resources including the shared SQLite connection.
    /// </summary>
    /// <param name="disposing">
    /// <see langword="true" /> to release both managed and unmanaged resources;
    /// <see langword="false" /> to release only unmanaged resources.
    /// </param>
    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        _connection.Dispose();
    }
}
