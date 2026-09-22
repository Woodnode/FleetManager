using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;
using FleetManager.Infrastructure.Persistence;
using FleetManager.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FleetManager.Tests.Integration;

/// <summary>
/// Les archives lèvent les filtres de suppression logique (véhicules et interventions) :
/// vérifié sur une vraie base relationnelle, pas avec des mocks.
/// </summary>
public sealed class ArchivedVehicleRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly FleetManagerDbContext _context;

    private readonly Store _storeA = Store.Create("Paris Centre", "1 rue de Rivoli", "75001", "Paris");
    private readonly Store _storeB = Store.Create("Lyon Part-Dieu", "5 place Béraudier", "69003", "Lyon");
    private Vehicle _archivedA = null!;
    private Vehicle _archivedB = null!;
    private Vehicle _active = null!;

    public ArchivedVehicleRepositoryTests()
    {
        _connection.Open();
        _context = new FleetManagerDbContext(
            new DbContextOptionsBuilder<FleetManagerDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
    }

    private async Task SeedAsync()
    {
        var technician = User.Create("Jean", "Dupont", "tech@test.fr", "hash", UserRole.Technician, _storeA.Id);
        _archivedA = Vehicle.Create("1HGBH41JXMN109186", "Toyota", "Corolla", 2019, 84000, _storeA.Id);
        _archivedB = Vehicle.Create("2HGBH41JXMN109187", "Peugeot", "308", 2018, 120000, _storeB.Id);
        _active    = Vehicle.Create("3HGBH41JXMN109188", "Renault", "Clio", 2022, 12000, _storeA.Id);

        var repair = Intervention.Create(_archivedA.Id, _storeA.Id, technician, InterventionType.Repair,
            new DateTime(2026, 3, 2, 9, 0, 0), new DateTime(2026, 3, 2, 17, 0, 0), "Embrayage");
        repair.Start();
        repair.Complete("Remplacé");
        var inspection = Intervention.Create(_archivedA.Id, _storeA.Id, technician, InterventionType.Inspection,
            new DateTime(2026, 5, 11, 9, 0, 0), new DateTime(2026, 5, 11, 12, 0, 0));
        inspection.Cancel("Véhicule vendu");

        _context.AddRange(_storeA, _storeB, technician, _archivedA, _archivedB, _active, repair, inspection);
        await _context.SaveChangesAsync();

        _archivedA.SoftDelete();
        _archivedB.SoftDelete();
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
    }

    [Fact]
    public async Task GetArchivedPaged_RetourneSeulementLesVehiculesSupprimes()
    {
        await SeedAsync();
        var repository = new VehicleRepository(_context);

        var (items, total) = await repository.GetArchivedPagedAsync(null, 0, 20);

        total.Should().Be(2);
        items.Select(v => v.Id).Should().BeEquivalentTo(new[] { _archivedA.Id, _archivedB.Id });
        items.Should().OnlyContain(v => v.IsDeleted && v.Store != null);
    }

    [Fact]
    public async Task GetArchivedPaged_AvecEnseigne_NeRetournePasCellesDesAutres()
    {
        await SeedAsync();
        var repository = new VehicleRepository(_context);

        var (items, total) = await repository.GetArchivedPagedAsync(_storeA.Id, 0, 20);

        total.Should().Be(1);
        items.Should().ContainSingle(v => v.Id == _archivedA.Id);
    }

    [Fact]
    public async Task GetArchivedById_VehiculeActif_RetourneNull()
    {
        await SeedAsync();
        var repository = new VehicleRepository(_context);

        (await repository.GetArchivedByIdAsync(_active.Id)).Should().BeNull();
        (await repository.GetArchivedByIdAsync(_archivedA.Id)).Should().NotBeNull();
    }

    [Fact]
    public async Task Historique_RetourneLesInterventionsMasqueesAilleurs()
    {
        await SeedAsync();
        var repository = new InterventionRepository(_context);

        var history = await repository.GetArchivedVehicleHistoryAsync(_archivedA.Id);
        var counts  = await repository.CountByArchivedVehicleIdsAsync(new[] { _archivedA.Id, _archivedB.Id });
        var (visibleElsewhere, _) = await repository.GetPagedAsync(_storeA.Id, 0, 50);

        history.Should().HaveCount(2);
        history.First().Type.Should().Be(InterventionType.Inspection, "l'historique est trié du plus récent au plus ancien");
        history.Should().OnlyContain(i => i.Technician != null && i.Store != null);
        counts.Should().ContainKey(_archivedA.Id).WhoseValue.Should().Be(2);
        counts.Should().NotContainKey(_archivedB.Id);
        visibleElsewhere.Should().BeEmpty("les vues courantes continuent de masquer ces interventions");
    }

    [Fact]
    public async Task HasActiveForVehicle_IgnoreLesInterventionsTermineesOuAnnulees()
    {
        await SeedAsync();
        var technician = await _context.Users.FirstAsync();
        var planned = Intervention.Create(_active.Id, _storeA.Id, technician, InterventionType.Maintenance,
            new DateTime(2026, 10, 1, 9, 0, 0), new DateTime(2026, 10, 1, 17, 0, 0));
        _context.Add(planned);
        await _context.SaveChangesAsync();
        var repository = new InterventionRepository(_context);

        (await repository.HasActiveForVehicleAsync(_active.Id)).Should().BeTrue();
        (await repository.HasActiveForVehicleAsync(_archivedA.Id)).Should().BeFalse("ses interventions sont terminée et annulée");
    }

    [Fact]
    public async Task Seeder_ChaqueVehiculeAvecInterventionActiveEstEnIntervention()
    {
        // Les données de démo doivent suivre le même flux que l'API (CreateInterventionCommand),
        // sinon la règle de suppression s'appuie sur un statut incohérent.
        await new DatabaseSeeder(_context, Microsoft.Extensions.Logging.Abstractions.NullLogger<DatabaseSeeder>.Instance).SeedAsync();

        var activeVehicleIds = await _context.Interventions
            .Where(i => i.Status == InterventionStatus.Planned || i.Status == InterventionStatus.InProgress)
            .Select(i => i.VehicleId)
            .Distinct()
            .ToListAsync();
        var statuses = await _context.Vehicles
            .Where(v => activeVehicleIds.Contains(v.Id))
            .Select(v => v.Status)
            .ToListAsync();

        activeVehicleIds.Should().NotBeEmpty();
        statuses.Should().OnlyContain(s => s == VehicleStatus.InIntervention);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
