using FleetManager.Api.Infrastructure;
using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;
using FleetManager.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FleetManager.Tests.Integration;

/// <summary>
/// La remise à zéro du site vitrine efface ce que les visiteurs ont fait avec les comptes de
/// démonstration publics, y compris les comptes qu'ils auraient créés.
/// </summary>
public sealed class DemoResetTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly FleetManagerDbContext _context;
    private readonly DatabaseSeeder _seeder;

    public DemoResetTests()
    {
        _connection.Open();
        _context = new FleetManagerDbContext(
            new DbContextOptionsBuilder<FleetManagerDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
        _seeder = new DatabaseSeeder(_context, NullLogger<DatabaseSeeder>.Instance);
    }

    [Fact]
    public async Task EtantDonneDemoModifieeParUnVisiteur_QuandRemiseAZero_AlorsJeuDeDemonstrationRestaure()
    {
        await _seeder.SeedAsync();
        var stores = await _context.Stores.CountAsync();
        var users = await _context.Users.CountAsync();
        var vehicles = await _context.Vehicles.IgnoreQueryFilters().CountAsync();

        _context.Users.Add(User.Create("Porte", "Derobee", "intrus@exemple.fr", "$2b$12$FakeHash", UserRole.Admin));
        _context.Vehicles.IgnoreQueryFilters().First().SoftDelete();
        await _context.SaveChangesAsync();

        await _seeder.ResetAsync();

        _context.ChangeTracker.Clear();
        (await _context.Stores.CountAsync()).Should().Be(stores);
        (await _context.Users.CountAsync()).Should().Be(users);
        (await _context.Vehicles.IgnoreQueryFilters().CountAsync()).Should().Be(vehicles);
        (await _context.Users.AnyAsync(u => u.LastName == "Derobee")).Should().BeFalse();
    }

    [Theory]
    [InlineData("2026-10-20T02:00:00Z", 4, 2)]
    [InlineData("2026-10-20T04:00:00Z", 4, 24)]
    [InlineData("2026-10-20T23:00:00Z", 4, 5)]
    public void EtantDonneHeureCourante_QuandCalculerProchaineRemise_AlorsDansLes24Heures(string now, int hour, int expectedHours)
    {
        DemoResetService.DelayUntilNext(DateTimeOffset.Parse(now), hour).Should().Be(TimeSpan.FromHours(expectedHours));
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
