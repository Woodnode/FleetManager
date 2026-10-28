using FleetManager.Application.Interfaces;
using FleetManager.Application.Interventions.Commands;
using FleetManager.Application.Services;
using FleetManager.Application.Vehicles.Commands;
using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;
using FleetManager.Infrastructure.Persistence;
using FleetManager.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using MediatR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace FleetManager.Tests.Integration;

/// <summary>
/// Transfert d'un véhicule entre enseignes pendant une intervention active.
///
/// FAILLE CORRIGÉE : l'Admin pouvait transférer un véhicule vers l'enseigne B alors qu'une intervention
/// de l'enseigne A était planifiée ; le technicien de A clôturait ensuite l'intervention et écrivait
/// le kilométrage et le statut d'un véhicule de B. Testé sur SQLite réel.
/// </summary>
public sealed class CrossStoreTransferTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");

    private readonly Store _storeA = Store.Create("Enseigne A", "1 rue A", "75001", "Paris");
    private readonly Store _storeB = Store.Create("Enseigne B", "1 rue B", "69001", "Lyon");
    private readonly User _techA;
    private readonly Vehicle _vehicle;
    private readonly Intervention _intervention;

    public CrossStoreTransferTests()
    {
        _connection.Open();
        _techA = User.Create("Tech", "A", "techa@test.fr", "hash", UserRole.Technician, _storeA.Id);
        _vehicle = Vehicle.Create("1HGBH41JXMN109186", "Renault", "Clio", 2021, 10_000, _storeA.Id);
        _intervention = Intervention.Create(_vehicle.Id, _storeA.Id, _techA, InterventionType.Repair,
            DateTime.UtcNow.AddDays(1), DateTime.UtcNow.AddDays(2));
        _vehicle.ChangeStatus(VehicleStatus.InIntervention);

        using var context = NewContext();
        context.Database.EnsureCreated();
        context.AddRange(_storeA, _storeB, _techA, _vehicle, _intervention);
        context.SaveChanges();
    }

    private FleetManagerDbContext NewContext()
        => new(new DbContextOptionsBuilder<FleetManagerDbContext>().UseSqlite(_connection).Options);

    private static ICurrentUserService As(UserRole role, Guid? storeId)
    {
        var mock = new Mock<ICurrentUserService>();
        mock.Setup(u => u.Role).Returns(role);
        mock.Setup(u => u.StoreId).Returns(storeId);
        mock.Setup(u => u.IsAdmin).Returns(role == UserRole.Admin);
        return mock.Object;
    }

    [Fact]
    public async Task TransfertAdmin_AvecInterventionPlanifiee_EstRefuse()
    {
        //Étant donné — une intervention de A est planifiée sur le véhicule
        using var context = NewContext();
        var handler = new UpdateVehicleCommandHandler(new VehicleRepository(context), new StoreRepository(context),
            new InterventionRepository(context), new StoreAuthorizationService(), As(UserRole.Admin, null), new UnitOfWork(context));

        //Quand — l'Admin tente de transférer le véhicule vers B
        var result = await handler.Handle(
            new UpdateVehicleCommand(_vehicle.Id, "Renault", "Clio", 2021, 10_000, _storeB.Id), default);

        //Alors
        result.IsFailure.Should().BeTrue();
        result.Error!.Code.Should().Be("CONFLICT");
        using var verify = NewContext();
        (await verify.Vehicles.AsNoTracking().SingleAsync(v => v.Id == _vehicle.Id)).StoreId.Should().Be(_storeA.Id);
    }

    [Fact]
    public async Task TechnicienEnseigneOrigine_NePeutPasEcrireSurVehiculeDejaTransfere()
    {
        //Étant donné — données héritées d'avant le correctif : le véhicule est déjà dans B, l'intervention reste à A
        using (var context = NewContext())
        {
            var vehicle = await context.Vehicles.SingleAsync(v => v.Id == _vehicle.Id);
            vehicle.Update("Renault", "Clio", 2021, 10_000, _storeB.Id);
            await context.SaveChangesAsync();
        }

        using (var context = NewContext())
        {
            var handler = new ChangeInterventionStatusCommandHandler(new InterventionRepository(context),
                new VehicleRepository(context), new StoreAuthorizationService(), As(UserRole.Technician, _storeA.Id),
                new UnitOfWork(context), Mock.Of<IPublisher>());

            //Quand — le technicien de A clôture avec un relevé aberrant
            var result = await handler.Handle(
                new ChangeInterventionStatusCommand(_intervention.Id, InterventionStatus.Completed, "x", 999_999), default);

            //Alors — refusé avant toute écriture
            result.IsFailure.Should().BeTrue();
            result.Error!.Code.Should().Be("FORBIDDEN");
        }

        using var verify = NewContext();
        var after = await verify.Vehicles.AsNoTracking().SingleAsync(v => v.Id == _vehicle.Id);
        after.Mileage.Should().Be(10_000);
        after.Status.Should().Be(VehicleStatus.InIntervention);
        (await verify.Interventions.AsNoTracking().SingleAsync(i => i.Id == _intervention.Id))
            .Status.Should().Be(InterventionStatus.Planned);
    }

    public void Dispose() => _connection.Dispose();
}
