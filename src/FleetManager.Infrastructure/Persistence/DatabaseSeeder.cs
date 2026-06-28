using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FleetManager.Infrastructure.Persistence;

public class DatabaseSeeder
{
    private readonly FleetManagerDbContext _context;
    private readonly ILogger<DatabaseSeeder> _logger;

    public DatabaseSeeder(FleetManagerDbContext context, ILogger<DatabaseSeeder> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task SeedAsync()
    {
        if (await _context.Stores.AnyAsync())
        {
            _logger.LogInformation("Database already seeded — skipping.");
            return;
        }

        _logger.LogInformation("Seeding database...");

        var stores = SeedStores();
        await _context.Stores.AddRangeAsync(stores);
        await _context.SaveChangesAsync();

        var users = SeedUsers(stores);
        await _context.Users.AddRangeAsync(users);
        await _context.SaveChangesAsync();

        var vehicles = SeedVehicles(stores);
        await _context.Vehicles.AddRangeAsync(vehicles);
        await _context.SaveChangesAsync();

        var technicians = users.Where(u => u.Role == UserRole.Technician).ToList();
        var interventions = SeedInterventions(vehicles, stores, technicians);
        await _context.Interventions.AddRangeAsync(interventions);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Database seeded successfully.");
    }

    private static List<Store> SeedStores() =>
    [
        Store.Create("AutoGroup Paris Nord",  "12 avenue de la République", "75010", "Paris"),
        Store.Create("AutoGroup Lyon Sud",    "45 rue Garibaldi",           "69007", "Lyon"),
        Store.Create("AutoGroup Bordeaux",    "8 cours de la Marne",        "33000", "Bordeaux"),
    ];

    private static List<User> SeedUsers(List<Store> stores)
    {
        // Mot de passe de démo : Fleet@2024 (hash BCrypt work factor 12)
        var demoHash = BCrypt.Net.BCrypt.HashPassword("Fleet@2024", workFactor: 12);

        return
        [
            User.Create("Sophie",  "Martin",   "admin@fleetmanager.fr",         demoHash, UserRole.Admin),
            User.Create("Thomas",  "Dupont",   "directeur.paris@fleetmanager.fr",  demoHash, UserRole.StoreManager, stores[0].Id),
            User.Create("Camille", "Bernard",  "directeur.lyon@fleetmanager.fr",   demoHash, UserRole.StoreManager, stores[1].Id),
            User.Create("Lucas",   "Moreau",   "tech1.paris@fleetmanager.fr",      demoHash, UserRole.Technician,   stores[0].Id),
            User.Create("Emma",    "Leroy",    "tech2.paris@fleetmanager.fr",      demoHash, UserRole.Technician,   stores[0].Id),
            User.Create("Hugo",    "Petit",    "tech1.lyon@fleetmanager.fr",       demoHash, UserRole.Technician,   stores[1].Id),
            User.Create("Léa",     "Roux",     "tech1.bordeaux@fleetmanager.fr",   demoHash, UserRole.Technician,   stores[2].Id),
        ];
    }

    private static List<Vehicle> SeedVehicles(List<Store> stores) =>
    [
        // Paris Nord
        Vehicle.Create("VF1RFD00X56789001", "Renault",  "Clio V",      2021, 32000,  stores[0].Id),
        Vehicle.Create("VF7NC5FVB73456002", "Peugeot",  "308 SW",      2022, 18500,  stores[0].Id),
        Vehicle.Create("WBA3A5G59DNP26001", "BMW",      "Série 3",     2020, 54200,  stores[0].Id),
        Vehicle.Create("VF1LJSRY5KY234003", "Renault",  "Megane IV",   2019, 67000,  stores[0].Id),
        Vehicle.Create("VF7PSCYHXHZ123004", "Citroën",  "C3 Aircross", 2023, 5800,   stores[0].Id),

        // Lyon Sud
        Vehicle.Create("VF1AAA000B1234005", "Renault",  "Zoe",         2022, 22000,  stores[1].Id),
        Vehicle.Create("VF3AAHFV9CY567006", "Peugeot",  "e-208",       2023, 12000,  stores[1].Id),
        Vehicle.Create("WVWZZZ1KZ8W234007", "Volkswagen","Golf VIII",  2021, 38000,  stores[1].Id),
        Vehicle.Create("WDD2050021R123008", "Mercedes", "Classe A",    2020, 49500,  stores[1].Id),

        // Bordeaux
        Vehicle.Create("VF7HBRHY5DZ789009", "Citroën",  "C5 X",        2022, 14200,  stores[2].Id),
        Vehicle.Create("VF1BBBBBB12345010", "Renault",  "Arkana",      2021, 28700,  stores[2].Id),
        Vehicle.Create("VSSZZZ6FZHR123011", "SEAT",     "Leon",        2023, 7300,   stores[2].Id),
    ];

    private static List<Intervention> SeedInterventions(
        List<Vehicle> vehicles,
        List<Store> stores,
        List<User> technicians)
    {
        var now = DateTime.UtcNow;
        var parisVehicles = vehicles.Where(v => v.StoreId == stores[0].Id).ToList();
        var lyonVehicles = vehicles.Where(v => v.StoreId == stores[1].Id).ToList();
        var bordeauxVehicles = vehicles.Where(v => v.StoreId == stores[2].Id).ToList();

        var tech1Paris = technicians.First(t => t.Email.Value.Contains("tech1.paris"));
        var tech2Paris = technicians.First(t => t.Email.Value.Contains("tech2.paris"));
        var tech1Lyon  = technicians.First(t => t.Email.Value.Contains("tech1.lyon"));
        var tech1Bordeaux = technicians.First(t => t.Email.Value.Contains("tech1.bordeaux"));

        var interventions = new List<Intervention>();

        // --- Paris : intervention terminée ---
        var i1 = Intervention.Create(
            parisVehicles[0].Id, stores[0].Id, tech1Paris,
            InterventionType.Maintenance,
            now.AddDays(-10), now.AddDays(-8),
            "Révision 30 000 km");
        i1.Start();
        i1.Complete("Révision effectuée, plaquettes remplacées");
        parisVehicles[0].ChangeStatus(VehicleStatus.Available);
        interventions.Add(i1);

        // --- Paris : intervention en cours ---
        var i2 = Intervention.Create(
            parisVehicles[1].Id, stores[0].Id, tech2Paris,
            InterventionType.Repair,
            now.AddDays(-1), now.AddDays(2),
            "Remplacement amortisseurs avant");
        i2.Start();
        parisVehicles[1].ChangeStatus(VehicleStatus.InIntervention);
        interventions.Add(i2);

        // --- Paris : planifiée ---
        var i3 = Intervention.Create(
            parisVehicles[2].Id, stores[0].Id, tech1Paris,
            InterventionType.Inspection,
            now.AddDays(3), now.AddDays(4),
            "Contrôle technique préparatoire");
        interventions.Add(i3);

        // --- Lyon : terminée ---
        var i4 = Intervention.Create(
            lyonVehicles[0].Id, stores[1].Id, tech1Lyon,
            InterventionType.Maintenance,
            now.AddDays(-5), now.AddDays(-4),
            "Vidange + filtres");
        i4.Start();
        i4.Complete("RAS");
        lyonVehicles[0].ChangeStatus(VehicleStatus.Available);
        interventions.Add(i4);

        // --- Lyon : planifiée ---
        var i5 = Intervention.Create(
            lyonVehicles[1].Id, stores[1].Id, tech1Lyon,
            InterventionType.Repair,
            now.AddDays(5), now.AddDays(7),
            "Diagnostic batterie haute tension");
        interventions.Add(i5);

        // --- Bordeaux : planifiée ---
        var i6 = Intervention.Create(
            bordeauxVehicles[0].Id, stores[2].Id, tech1Bordeaux,
            InterventionType.Maintenance,
            now.AddDays(1), now.AddDays(2),
            "Révision annuelle");
        interventions.Add(i6);

        return interventions;
    }
}
