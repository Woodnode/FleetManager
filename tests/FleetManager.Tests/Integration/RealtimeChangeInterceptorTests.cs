using FleetManager.Application.Interfaces;
using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;
using FleetManager.Infrastructure.Persistence;
using FleetManager.Infrastructure.Realtime;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace FleetManager.Tests.Integration;

/// <summary>
/// Le signal temps réel doit partir après chaque enregistrement réussi, vers les seules enseignes
/// concernées, et jamais après un échec. Vérifié sur une vraie base (SQLite), sans mocks d'EF.
/// </summary>
public sealed class RealtimeChangeInterceptorTests : IDisposable
{
    private sealed class RecordingNotifier : IRealtimeNotifier
    {
        public List<RealtimeChange> Changes { get; } = [];
        public bool Throw { get; set; }

        public Task NotifyAsync(RealtimeChange change, CancellationToken cancellationToken = default)
        {
            if (Throw) throw new InvalidOperationException("Hub indisponible");
            Changes.Add(change);
            return Task.CompletedTask;
        }
    }

    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly RecordingNotifier _notifier = new();
    private readonly FleetManagerDbContext _context;

    private readonly Store _paris = Store.Create("Paris Centre", "1 rue de Rivoli", "75001", "Paris");
    private readonly Store _lyon  = Store.Create("Lyon Part-Dieu", "5 place Béraudier", "69003", "Lyon");

    public RealtimeChangeInterceptorTests()
    {
        _connection.Open();
        var options = new DbContextOptionsBuilder<FleetManagerDbContext>()
            .UseSqlite(_connection)
            .AddInterceptors(new RealtimeChangeInterceptor(_notifier, NullLogger<RealtimeChangeInterceptor>.Instance))
            .Options;
        _context = new FleetManagerDbContext(options);
        _context.Database.EnsureCreated();
        _context.AddRange(_paris, _lyon);
        _context.SaveChanges();
        _notifier.Changes.Clear();
    }

    private async Task<Vehicle> AddVehicleAsync(Guid storeId, string vin = "1HGBH41JXMN109186")
    {
        var vehicle = Vehicle.Create(vin, "Citroën", "C5 X", 2022, 14200, storeId);
        _context.Add(vehicle);
        await _context.SaveChangesAsync();
        _notifier.Changes.Clear();
        return vehicle;
    }

    [Fact]
    public async Task CreationVehicule_PrevientSeulementSonEnseigne()
    {
        _context.Add(Vehicle.Create("1HGBH41JXMN109186", "Citroën", "C5 X", 2022, 14200, _paris.Id));
        await _context.SaveChangesAsync();

        var change = _notifier.Changes.Should().ContainSingle().Subject;
        change.Entities.Should().BeEquivalentTo(new[] { RealtimeEntity.Vehicles });
        change.StoreIds.Should().BeEquivalentTo(new[] { _paris.Id });
        change.Global.Should().BeFalse();
    }

    [Fact]
    public async Task TransfertVehicule_PrevientLAncienneEtLaNouvelleEnseigne()
    {
        var vehicle = await AddVehicleAsync(_paris.Id);

        vehicle.Update(vehicle.Brand, vehicle.Model, vehicle.Year, vehicle.Mileage, _lyon.Id);
        await _context.SaveChangesAsync();

        _notifier.Changes.Should().ContainSingle()
            .Which.StoreIds.Should().BeEquivalentTo(new[] { _paris.Id, _lyon.Id });
    }

    [Fact]
    public async Task SuppressionLogique_EstSignalee()
    {
        var vehicle = await AddVehicleAsync(_paris.Id);

        vehicle.SoftDelete();
        await _context.SaveChangesAsync();

        _notifier.Changes.Should().ContainSingle()
            .Which.Entities.Should().Contain(RealtimeEntity.Vehicles);
    }

    [Fact]
    public async Task ChangementStatutIntervention_SignaleInterventionsEtVehicules()
    {
        var technician = User.Create("Jean", "Dupont", "tech@test.fr", "hash", UserRole.Technician, _paris.Id);
        var vehicle = Vehicle.Create("1HGBH41JXMN109186", "Citroën", "C5 X", 2022, 14200, _paris.Id);
        var intervention = Intervention.Create(vehicle.Id, _paris.Id, technician, InterventionType.Repair,
            new DateTime(2026, 10, 1, 9, 0, 0), new DateTime(2026, 10, 1, 17, 0, 0));
        _context.AddRange(technician, vehicle, intervention);
        await _context.SaveChangesAsync();
        _notifier.Changes.Clear();

        // Comme ChangeInterventionStatusCommand : l'intervention démarre, le véhicule passe en intervention.
        intervention.Start();
        vehicle.ChangeStatus(VehicleStatus.InIntervention);
        await _context.SaveChangesAsync();

        var change = _notifier.Changes.Should().ContainSingle().Subject;
        change.Entities.Should().BeEquivalentTo(new[] { RealtimeEntity.Interventions, RealtimeEntity.Vehicles });
        change.StoreIds.Should().BeEquivalentTo(new[] { _paris.Id });
    }

    [Fact]
    public async Task ModificationEnseigne_EstDiffuseeATous()
    {
        _context.Add(Store.Create("Bordeaux", "8 cours de la Marne", "33000", "Bordeaux"));
        await _context.SaveChangesAsync();

        var change = _notifier.Changes.Should().ContainSingle().Subject;
        change.Global.Should().BeTrue();
        change.Entities.Should().Contain(RealtimeEntity.Stores);
    }

    [Fact]
    public async Task EnregistrementEchoue_NeNotifieRien()
    {
        await AddVehicleAsync(_paris.Id, "1HGBH41JXMN109186");

        // Même VIN : l'index unique fait échouer l'enregistrement.
        _context.Add(Vehicle.Create("1HGBH41JXMN109186", "Peugeot", "308", 2018, 120000, _lyon.Id));
        var save = () => _context.SaveChangesAsync();

        await save.Should().ThrowAsync<DbUpdateException>();
        _notifier.Changes.Should().BeEmpty();
    }

    [Fact]
    public async Task NotificateurEnPanne_NeFaitPasEchouerLEnregistrement()
    {
        _notifier.Throw = true;

        _context.Add(Vehicle.Create("1HGBH41JXMN109186", "Citroën", "C5 X", 2022, 14200, _paris.Id));
        var saved = await _context.SaveChangesAsync();

        saved.Should().Be(1);
        _context.ChangeTracker.Clear();
        (await _context.Vehicles.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task AucuneModification_AucunSignal()
    {
        await _context.SaveChangesAsync();

        _notifier.Changes.Should().BeEmpty();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
