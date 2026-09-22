using FleetManager.Application.Interfaces;
using FleetManager.Application.Services;
using FleetManager.Application.Stores.Commands;
using FleetManager.Application.Vehicles.Commands;
using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;
using FleetManager.Infrastructure.Persistence;
using FleetManager.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace FleetManager.Tests.Integration;

/// <summary>
/// Cas où un véhicule archivé (suppression logique) reste présent en base : index unique sur le VIN
/// et clés étrangères Restrict. Testé sur SQLite réel : les mocks ne voient pas ces contraintes.
/// </summary>
public sealed class ArchiveIntegrityTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly FleetManagerDbContext _context;
    private readonly Mock<ICurrentUserService> _currentUser = new();

    private readonly Store _paris = Store.Create("Paris Centre", "1 rue de Rivoli", "75001", "Paris");
    private readonly Store _lyon  = Store.Create("Lyon Part-Dieu", "5 place Béraudier", "69003", "Lyon");
    private const string ArchivedVin = "1HGBH41JXMN109186";

    public ArchiveIntegrityTests()
    {
        _connection.Open();
        _context = new FleetManagerDbContext(
            new DbContextOptionsBuilder<FleetManagerDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
        _context.AddRange(_paris, _lyon);
        _context.SaveChanges();
        ActAs(UserRole.Admin, null);
    }

    private void ActAs(UserRole role, Guid? storeId)
    {
        _currentUser.Setup(u => u.Role).Returns(role);
        _currentUser.Setup(u => u.StoreId).Returns(storeId);
        _currentUser.Setup(u => u.IsAdmin).Returns(role == UserRole.Admin);
    }

    private async Task<Vehicle> ArchiveVehicleAsync(Guid storeId, string vin = ArchivedVin)
    {
        var vehicle = Vehicle.Create(vin, "Citroën", "C5 X", 2022, 14200, storeId);
        _context.Add(vehicle);
        await _context.SaveChangesAsync();
        vehicle.SoftDelete();
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        return vehicle;
    }

    private CreateVehicleCommandHandler CreateVehicleHandler() => new(
        new VehicleRepository(_context), new StoreRepository(_context),
        new StoreAuthorizationService(), _currentUser.Object, new UnitOfWork(_context));

    private DeleteStoreCommandHandler DeleteStoreHandler() => new(
        new StoreRepository(_context), new VehicleRepository(_context), new InterventionRepository(_context),
        new UnitOfWork(_context), _currentUser.Object);

    private RestoreVehicleCommandHandler RestoreHandler() => new(
        new VehicleRepository(_context), new StoreAuthorizationService(), _currentUser.Object, new UnitOfWork(_context));

    // ── Création avec le VIN d'un véhicule archivé ────────────────────────────

    [Fact]
    public async Task CreerVehicule_AvecVinArchive_RetourneUnConflitEtNonUneErreurServeur()
    {
        var archived = await ArchiveVehicleAsync(_paris.Id);

        var result = await CreateVehicleHandler().Handle(
            new CreateVehicleCommand(ArchivedVin, "Citroën", "C5 X", 2022, 14200, _paris.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("CONFLICT");
        result.Error.Details.Should().NotBeNull();
        result.Error.Details!["archivedVehicleId"].Should().Be(archived.Id);
        result.Error.Details["storeName"].Should().Be("Paris Centre");
    }

    [Fact]
    public async Task CreerVehicule_AvecVinArchiveDUneAutreEnseigne_NeDivulguePasLeVehicule()
    {
        await ArchiveVehicleAsync(_lyon.Id);
        ActAs(UserRole.StoreManager, _paris.Id);

        var result = await CreateVehicleHandler().Handle(
            new CreateVehicleCommand(ArchivedVin, "Citroën", "C5 X", 2022, 14200, _paris.Id), default);

        result.Error!.Code.Should().Be("CONFLICT");
        result.Error.Details.Should().BeNull();
        result.Error.Message.Should().NotContain("Citroën").And.NotContain("Lyon");
    }

    [Fact]
    public async Task CreerVehicule_VinArchiveEnMinuscules_EstAussiDetecte()
    {
        await ArchiveVehicleAsync(_paris.Id);

        var result = await CreateVehicleHandler().Handle(
            new CreateVehicleCommand(ArchivedVin.ToLowerInvariant(), "Citroën", "C5 X", 2022, 14200, _paris.Id), default);

        result.Error!.Code.Should().Be("CONFLICT");
    }

    // ── Restauration ──────────────────────────────────────────────────────────

    [Fact]
    public async Task Restaurer_RemetLeVehiculeEtSonHistoriqueDansLeParc()
    {
        var technician = User.Create("Jean", "Dupont", "tech@test.fr", "hash", UserRole.Technician, _paris.Id);
        var vehicle = Vehicle.Create(ArchivedVin, "Citroën", "C5 X", 2022, 14200, _paris.Id);
        var repair = Intervention.Create(vehicle.Id, _paris.Id, technician, InterventionType.Repair,
            new DateTime(2026, 3, 2, 9, 0, 0), new DateTime(2026, 3, 2, 17, 0, 0));
        repair.Start();
        repair.Complete();
        _context.AddRange(technician, vehicle, repair);
        await _context.SaveChangesAsync();
        vehicle.SoftDelete();
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();
        ActAs(UserRole.StoreManager, _paris.Id);

        var result = await RestoreHandler().Handle(new RestoreVehicleCommand(vehicle.Id), default);

        result.IsSuccess.Should().BeTrue();
        result.Value!.StoreName.Should().Be("Paris Centre");
        _context.ChangeTracker.Clear();
        (await new VehicleRepository(_context).GetByIdAsync(vehicle.Id)).Should().NotBeNull();
        (await new VehicleRepository(_context).GetArchivedByIdAsync(vehicle.Id)).Should().BeNull();
        (await new InterventionRepository(_context).GetByVehicleIdAsync(vehicle.Id)).Should().ContainSingle();
    }

    [Fact]
    public async Task Restaurer_VehiculeDUneAutreEnseigne_RetourneNotFound()
    {
        var vehicle = await ArchiveVehicleAsync(_lyon.Id);
        ActAs(UserRole.StoreManager, _paris.Id);

        var result = await RestoreHandler().Handle(new RestoreVehicleCommand(vehicle.Id), default);

        result.Error!.Code.Should().Be("NOT_FOUND");
        (await new VehicleRepository(_context).GetArchivedByIdAsync(vehicle.Id)).Should().NotBeNull("rien n'a été modifié");
    }

    [Fact]
    public async Task Restaurer_VehiculeActif_RetourneNotFound()
    {
        var vehicle = Vehicle.Create(ArchivedVin, "Citroën", "C5 X", 2022, 14200, _paris.Id);
        _context.Add(vehicle);
        await _context.SaveChangesAsync();

        var result = await RestoreHandler().Handle(new RestoreVehicleCommand(vehicle.Id), default);

        result.Error!.Code.Should().Be("NOT_FOUND");
    }

    [Fact]
    public void Restore_SurUnVehiculeNonArchive_LeveUneDomainException()
    {
        var vehicle = Vehicle.Create(ArchivedVin, "Citroën", "C5 X", 2022, 14200, _paris.Id);

        vehicle.Invoking(v => v.Restore()).Should().Throw<FleetManager.Domain.Exceptions.DomainException>();
    }

    // ── Suppression d'une enseigne ────────────────────────────────────────────

    [Fact]
    public async Task SupprimerEnseigne_AvecSeulementDesVehiculesArchives_RetourneUnConflit()
    {
        await ArchiveVehicleAsync(_lyon.Id);

        var result = await DeleteStoreHandler().Handle(new DeleteStoreCommand(_lyon.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("CONFLICT");
    }

    [Fact]
    public async Task SupprimerEnseigne_AvecInterventionsDUnVehiculeTransfere_RetourneUnConflit()
    {
        // Un véhicule transféré garde ses anciennes interventions rattachées à l'enseigne d'origine.
        var technician = User.Create("Jean", "Dupont", "tech@test.fr", "hash", UserRole.Technician, _lyon.Id);
        var vehicle = Vehicle.Create("2HGBH41JXMN109187", "Peugeot", "308", 2018, 120000, _lyon.Id);
        var old = Intervention.Create(vehicle.Id, _lyon.Id, technician, InterventionType.Repair,
            new DateTime(2026, 3, 2, 9, 0, 0), new DateTime(2026, 3, 2, 17, 0, 0));
        old.Start();
        old.Complete();
        _context.AddRange(technician, vehicle, old);
        await _context.SaveChangesAsync();
        vehicle.Update(vehicle.Brand, vehicle.Model, vehicle.Year, vehicle.Mileage, _paris.Id);
        await _context.SaveChangesAsync();
        _context.ChangeTracker.Clear();

        var result = await DeleteStoreHandler().Handle(new DeleteStoreCommand(_lyon.Id), default);

        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("CONFLICT");
    }

    [Fact]
    public async Task SupprimerEnseigne_Vide_Reussit()
    {
        var result = await DeleteStoreHandler().Handle(new DeleteStoreCommand(_lyon.Id), default);

        result.IsSuccess.Should().BeTrue();
        (await _context.Stores.AnyAsync(s => s.Id == _lyon.Id)).Should().BeFalse();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
