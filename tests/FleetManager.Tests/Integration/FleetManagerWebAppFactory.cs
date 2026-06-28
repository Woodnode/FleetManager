using FleetManager.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FleetManager.Tests.Integration;

public class FleetManagerWebAppFactory : WebApplicationFactory<Program>
{
    // Keep the connection open for the lifetime of the factory so the in-memory SQLite DB persists.
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["JwtSettings:Secret"]   = "test-secret-key-that-is-at-least-32-characters-long!",
                ["JwtSettings:Issuer"]   = "FleetManagerTest",
                ["JwtSettings:Audience"] = "FleetManagerTestClient",
            });
        });

        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<FleetManagerDbContext>));
            if (descriptor is not null)
                services.Remove(descriptor);

            // SQLite in-memory: supports FK constraints, transactions, and relational queries —
            // far more representative than EF InMemory while still running without SQL Server.
            services.AddDbContext<FleetManagerDbContext>(opt =>
                opt.UseSqlite(_connection));
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _connection.Dispose();
        base.Dispose(disposing);
    }
}
