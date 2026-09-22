using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;
using FleetManager.Infrastructure.Persistence;
using FleetManager.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FleetManager.Tests.Integration;

/// <summary>
/// Un véhicule supprimé (suppression logique) est filtré par EF Core. La liste des interventions
/// charge le véhicule (INNER JOIN filtré) tandis que les compteurs du tableau de bord ne le font pas :
/// sans filtre équivalent sur Intervention, les deux vues divergeaient.
/// </summary>
public sealed class InterventionSoftDeleteConsistencyTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly FleetManagerDbContext _context;

    public InterventionSoftDeleteConsistencyTests()
    {
        _connection.Open();
        _context = new FleetManagerDbContext(
            new DbContextOptionsBuilder<FleetManagerDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task CountsAndList_AfterVehicleWithCompletedInterventionIsDeleted_ShouldMatch()
    {
        // Arrange : une intervention terminée (le véhicule redevient disponible, donc supprimable)
        var store = Store.Create("Paris Centre", "1 rue de Rivoli", "75001", "Paris");
        var technician = User.Create("Jean", "Dupont", "tech@test.fr", "hash", UserRole.Technician, store.Id);
        var deletedVehicle = Vehicle.Create("1HGBH41JXMN109186", "Toyota", "Corolla", 2022, 0, store.Id);
        var keptVehicle = Vehicle.Create("2HGBH41JXMN109187", "Renault", "Clio", 2021, 0, store.Id);

        var historic = Intervention.Create(deletedVehicle.Id, store.Id, technician, InterventionType.Repair,
            new DateTime(2026, 5, 4, 9, 0, 0), new DateTime(2026, 5, 4, 17, 0, 0));
        historic.Start();
        historic.Complete();

        var active = Intervention.Create(keptVehicle.Id, store.Id, technician, InterventionType.Maintenance,
            new DateTime(2026, 10, 1, 9, 0, 0), new DateTime(2026, 10, 1, 17, 0, 0));

        _context.AddRange(store, technician, deletedVehicle, keptVehicle, historic, active);
        await _context.SaveChangesAsync();

        deletedVehicle.SoftDelete();
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var repository = new InterventionRepository(_context);

        // Act
        var (items, totalCount) = await repository.GetPagedAsync(store.Id, 0, 50);
        var counts = await repository.GetSummaryCountsAsync(store.Id);

        // Assert : même total partout, l'intervention du véhicule supprimé n'apparaît nulle part
        counts.StatusCounts.Values.Sum().Should().Be(totalCount);
        totalCount.Should().Be(1);
        items.Should().ContainSingle(i => i.Id == active.Id);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
