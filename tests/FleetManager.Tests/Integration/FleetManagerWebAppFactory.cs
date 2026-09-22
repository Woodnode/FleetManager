using FleetManager.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
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

        // UseSetting et non ConfigureAppConfiguration : avec le minimal hosting, Program.cs lit la
        // configuration JWT pendant la construction du builder, avant que ConfigureAppConfiguration
        // ne s'applique. Les tests signaient donc leurs jetons avec la valeur d'exemple d'appsettings.json.
        builder.UseSetting("JwtSettings:Secret",   "test-secret-key-that-is-at-least-32-characters-long!");
        builder.UseSetting("JwtSettings:Issuer",   "FleetManagerTest");
        builder.UseSetting("JwtSettings:Audience", "FleetManagerTestClient");

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
