using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;
using FleetManager.Infrastructure.Persistence;
using FleetManager.Infrastructure.Persistence.Repositories;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace FleetManager.Tests.Integration;

/// <summary>
/// Journée d'un technicien (GET /interventions/mine) sur SQLite réel : le filtre combine la
/// période, le technicien, l'enseigne et les interventions en retard encore ouvertes.
/// </summary>
public sealed class TechnicianAgendaRepositoryTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly FleetManagerDbContext _context;

    private readonly Store _paris = Store.Create("Paris Centre", "1 rue de Rivoli", "75001", "Paris");
    private readonly Store _lyon  = Store.Create("Lyon Part-Dieu", "5 place Béraudier", "69003", "Lyon");
    private readonly User _lucas;
    private readonly User _emma;

    private static readonly DateTime DayStart = new(2026, 10, 17, 4, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime DayEnd   = DayStart.AddDays(1);
    private int _vinCounter;

    public TechnicianAgendaRepositoryTests()
    {
        _connection.Open();
        _context = new FleetManagerDbContext(
            new DbContextOptionsBuilder<FleetManagerDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();

        _lucas = User.Create("Lucas", "Moreau", "tech1.paris@test.fr", "$2b$12$FakeHash", UserRole.Technician, _paris.Id);
        _emma  = User.Create("Emma", "Leroy", "tech2.paris@test.fr", "$2b$12$FakeHash", UserRole.Technician, _paris.Id);
        _context.AddRange(_paris, _lyon, _lucas, _emma);
        _context.SaveChanges();
    }

    private Intervention Plan(User technician, Store store, DateTime start, DateTime end, InterventionStatus status = InterventionStatus.Planned)
    {
        var vin = $"VF1AB000000{++_vinCounter:000000}";
        var vehicle = Vehicle.Create(vin, "Renault", "Clio", 2021, 30_000, store.Id);
        var intervention = Intervention.Create(vehicle.Id, store.Id, technician, InterventionType.Maintenance, start, end);
        if (status is InterventionStatus.InProgress or InterventionStatus.Completed) intervention.Start();
        if (status is InterventionStatus.Completed) intervention.Complete();
        if (status is InterventionStatus.Cancelled) intervention.Cancel("Client absent");
        _context.AddRange(vehicle, intervention);
        _context.SaveChanges();
        return intervention;
    }

    private Task<IReadOnlyList<Intervention>> AgendaOf(User technician, Store store) =>
        new InterventionRepository(_context).GetTechnicianAgendaAsync(technician.Id, store.Id, DayStart, DayEnd);

    [Fact]
    public async Task EtantDonneInterventionsDuJour_QuandAgenda_AlorsSeulementCellesDuTechnicienTrieesParHeure()
    {
        var afternoon = Plan(_lucas, _paris, DayStart.AddHours(14), DayStart.AddHours(16));
        var morning   = Plan(_lucas, _paris, DayStart.AddHours(9), DayStart.AddHours(11), InterventionStatus.InProgress);
        Plan(_emma, _paris, DayStart.AddHours(10), DayStart.AddHours(12));

        var agenda = await AgendaOf(_lucas, _paris);

        agenda.Select(i => i.Id).Should().Equal(morning.Id, afternoon.Id);
        agenda.Should().OnlyContain(i => i.Vehicle != null && i.Store != null && i.Technician != null);
    }

    [Fact]
    public async Task EtantDonneInterventionsEnRetard_QuandAgenda_AlorsSeulementCellesEncoreOuvertes()
    {
        var overduePlanned    = Plan(_lucas, _paris, DayStart.AddDays(-3), DayStart.AddDays(-2));
        var overdueInProgress = Plan(_lucas, _paris, DayStart.AddDays(-2), DayStart.AddHours(-5), InterventionStatus.InProgress);
        Plan(_lucas, _paris, DayStart.AddDays(-3), DayStart.AddDays(-2), InterventionStatus.Completed);
        Plan(_lucas, _paris, DayStart.AddDays(-3), DayStart.AddDays(-2), InterventionStatus.Cancelled);

        var agenda = await AgendaOf(_lucas, _paris);

        agenda.Select(i => i.Id).Should().BeEquivalentTo(new[] { overduePlanned.Id, overdueInProgress.Id });
    }

    [Fact]
    public async Task EtantDonneInterventionSurPlusieursJours_QuandAgenda_AlorsIncluse()
    {
        var spanning = Plan(_lucas, _paris, DayStart.AddDays(-1), DayStart.AddDays(2), InterventionStatus.InProgress);

        var agenda = await AgendaOf(_lucas, _paris);

        agenda.Should().ContainSingle().Which.Id.Should().Be(spanning.Id);
    }

    [Fact]
    public async Task EtantDonneInterventionsFuturesOuDuneAutreEnseigne_QuandAgenda_AlorsExclues()
    {
        Plan(_lucas, _paris, DayEnd.AddHours(1), DayEnd.AddHours(3));
        Plan(_lucas, _lyon, DayStart.AddHours(9), DayStart.AddHours(10));

        var agenda = await AgendaOf(_lucas, _paris);

        agenda.Should().BeEmpty();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}
