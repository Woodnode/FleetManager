using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FleetManager.Infrastructure.Persistence;

/// <summary>
/// Jeu de démonstration : cinq enseignes, leurs équipes, une quarantaine de véhicules et deux mois
/// d'interventions.
///
/// Le premier jeu (douze véhicules, six interventions) laissait le tableau de bord et les listes à
/// moitié vides : un seul technicien par enseigne, aucune intervention en retard ni annulée,
/// aucun véhicule vendu ou archivé. Ici chaque statut et chaque badge de l'interface a un exemple.
///
/// Les choix aléatoires viennent d'un générateur à graine fixe : la démonstration est identique à
/// chaque régénération. Rien n'est fait si la base contient déjà des enseignes.
/// </summary>
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
        var random = new Random(2024);

        var stores = SeedStores();
        await _context.Stores.AddRangeAsync(stores);
        await _context.SaveChangesAsync();

        var users = SeedUsers(stores);
        await _context.Users.AddRangeAsync(users);
        await _context.SaveChangesAsync();

        var vehicles = SeedVehicles(stores, random);
        await _context.Vehicles.AddRangeAsync(vehicles);
        await _context.SaveChangesAsync();

        var technicians = users.Where(u => u.Role == UserRole.Technician).ToList();
        var interventions = SeedInterventions(vehicles, technicians, random);
        await _context.Interventions.AddRangeAsync(interventions);

        // Une intervention terminée prend la date du jour à la clôture : on la ramène à sa fin
        // prévue, sinon tout l'historique semblerait s'être terminé le jour de la génération.
        foreach (var done in interventions.Where(i => i.Status == InterventionStatus.Completed))
            _context.Entry(done).Property(nameof(Intervention.ActualEndDate)).CurrentValue = done.PlannedEndDate;

        await _context.SaveChangesAsync();

        // Archivés en dernier : un véhicule archivé garde son historique d'interventions.
        foreach (var archived in vehicles.Where(v => v.Status != VehicleStatus.InIntervention).Take(3))
            archived.SoftDelete();
        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Database seeded: {Stores} stores, {Users} users, {Vehicles} vehicles, {Interventions} interventions.",
            stores.Count, users.Count, vehicles.Count, interventions.Count);
    }

    private static List<Store> SeedStores() =>
    [
        Store.Create("AutoGroup Paris Nord",     "12 avenue de la République",  "75010", "Paris"),
        Store.Create("AutoGroup Lyon Sud",       "45 rue Garibaldi",            "69007", "Lyon"),
        Store.Create("AutoGroup Bordeaux",       "8 cours de la Marne",         "33000", "Bordeaux"),
        Store.Create("AutoGroup Lille Centre",   "27 rue Nationale",            "59000", "Lille"),
        Store.Create("AutoGroup Marseille Prado","140 avenue du Prado",         "13008", "Marseille"),
    ];

    private static List<User> SeedUsers(List<Store> stores)
    {
        // Mot de passe de démo : Fleet@2024 (hash BCrypt work factor 12)
        var demoHash = BCrypt.Net.BCrypt.HashPassword("Fleet@2024", workFactor: 12);

        return
        [
            User.Create("Sophie",  "Martin",   "admin@fleetmanager.fr",               demoHash, UserRole.Admin),
            User.Create("Thomas",  "Dupont",   "directeur.paris@fleetmanager.fr",     demoHash, UserRole.StoreManager, stores[0].Id),
            User.Create("Camille", "Bernard",  "directeur.lyon@fleetmanager.fr",      demoHash, UserRole.StoreManager, stores[1].Id),
            User.Create("Julien",  "Fabre",    "directeur.lille@fleetmanager.fr",     demoHash, UserRole.StoreManager, stores[3].Id),
            User.Create("Nadia",   "Benali",   "directeur.marseille@fleetmanager.fr", demoHash, UserRole.StoreManager, stores[4].Id),
            User.Create("Lucas",   "Moreau",   "tech1.paris@fleetmanager.fr",         demoHash, UserRole.Technician,   stores[0].Id),
            User.Create("Emma",    "Leroy",    "tech2.paris@fleetmanager.fr",         demoHash, UserRole.Technician,   stores[0].Id),
            User.Create("Hugo",    "Petit",    "tech1.lyon@fleetmanager.fr",          demoHash, UserRole.Technician,   stores[1].Id),
            User.Create("Inès",    "Garnier",  "tech2.lyon@fleetmanager.fr",          demoHash, UserRole.Technician,   stores[1].Id),
            User.Create("Léa",     "Roux",     "tech1.bordeaux@fleetmanager.fr",      demoHash, UserRole.Technician,   stores[2].Id),
            User.Create("Mathis",  "Lambert",  "tech2.bordeaux@fleetmanager.fr",      demoHash, UserRole.Technician,   stores[2].Id),
            User.Create("Chloé",   "Vasseur",  "tech1.lille@fleetmanager.fr",         demoHash, UserRole.Technician,   stores[3].Id),
            User.Create("Yanis",   "Haddad",   "tech1.marseille@fleetmanager.fr",     demoHash, UserRole.Technician,   stores[4].Id),
            User.Create("Manon",   "Giraud",   "tech2.marseille@fleetmanager.fr",     demoHash, UserRole.Technician,   stores[4].Id),
        ];
    }

    private static List<Vehicle> SeedVehicles(List<Store> stores, Random random)
    {
        // (marque, modèle, année, kilométrage, préfixe VIN du constructeur)
        var models = new (string Brand, string Model, int Year, int Mileage, string Wmi)[]
        {
            ("Renault",    "Clio V",       2021, 32000, "VF1"), ("Peugeot",    "308 SW",      2022, 18500, "VF3"),
            ("BMW",        "Série 3",      2020, 54200, "WBA"), ("Renault",    "Megane IV",   2019, 67000, "VF1"),
            ("Citroën",    "C3 Aircross",  2023,  5800, "VF7"), ("Renault",    "Zoe",         2022, 22000, "VF1"),
            ("Peugeot",    "e-208",        2023, 12000, "VF3"), ("Volkswagen", "Golf VIII",   2021, 38000, "WVW"),
            ("Mercedes",   "Classe A",     2020, 49500, "WDD"), ("Citroën",    "C5 X",        2022, 14200, "VF7"),
            ("Renault",    "Arkana",       2021, 28700, "VF1"), ("SEAT",       "Leon",        2023,  7300, "VSS"),
            ("Toyota",     "Yaris Cross",  2023,  9100, "JTD"), ("Dacia",      "Duster",      2022, 31500, "UU1"),
            ("Tesla",      "Model 3",      2022, 41200, "5YJ"), ("Peugeot",    "3008",        2021, 46800, "VF3"),
            ("Volkswagen", "ID.3",         2023, 15300, "WVW"), ("Audi",       "A3 Sportback",2020, 58900, "WAU"),
            ("Kia",        "Niro EV",      2022, 26400, "KNA"), ("Hyundai",    "Tucson",      2021, 39700, "KMH"),
            ("Renault",    "Austral",      2023,  8600, "VF1"), ("Ford",       "Puma",        2022, 21900, "WF0"),
            ("Skoda",      "Octavia",      2020, 72300, "TMB"), ("Fiat",       "500e",        2023,  6400, "ZFA"),
            ("Citroën",    "Berlingo",     2021, 52100, "VF7"), ("Opel",       "Corsa",       2022, 17800, "W0V"),
            ("Mini",       "Cooper SE",    2021, 24600, "WMW"), ("Volvo",      "XC40",        2022, 33100, "YV1"),
            ("Peugeot",    "Partner",      2020, 88400, "VF3"), ("Renault",    "Kangoo",      2021, 61200, "VF1"),
            ("Toyota",     "Corolla",      2021, 44500, "SB1"), ("Nissan",     "Leaf",        2020, 51700, "SJN"),
            ("BMW",        "iX1",          2023, 11200, "WBA"), ("Dacia",      "Sandero",     2022, 19900, "UU1"),
            ("Mercedes",   "Vito",         2020, 97800, "WDF"), ("Volkswagen", "Polo",        2022, 23300, "WVW"),
            ("Renault",    "Captur",       2022, 27600, "VF1"), ("Peugeot",    "2008",        2023, 10400, "VF3"),
            ("Cupra",      "Born",         2023, 13800, "VSS"), ("Kia",        "Sportage",    2022, 29400, "KNA"),
        };

        var vehicles = new List<Vehicle>(models.Length);
        for (var i = 0; i < models.Length; i++)
        {
            var model = models[i];
            var vin = $"{model.Wmi}{RandomVinBody(random)}{i + 1:000}";
            vehicles.Add(Vehicle.Create(vin, model.Brand, model.Model, model.Year, model.Mileage, stores[i % stores.Count].Id));
        }

        // Quelques véhicules hors du parc disponible : vendus, ou immobilisés sans intervention.
        vehicles[22].ChangeStatus(VehicleStatus.OutOfService);
        vehicles[34].ChangeStatus(VehicleStatus.OutOfService);
        vehicles[28].ChangeStatus(VehicleStatus.Sold);
        vehicles[29].ChangeStatus(VehicleStatus.Sold);
        vehicles[35].ChangeStatus(VehicleStatus.Sold);

        return vehicles;
    }

    /// <summary>Onze caractères alphanumériques sans I, O ni Q, comme dans un vrai VIN.</summary>
    private static string RandomVinBody(Random random)
    {
        const string alphabet = "ABCDEFGHJKLMNPRSTUVWXYZ0123456789";
        return new string(Enumerable.Range(0, 11).Select(_ => alphabet[random.Next(alphabet.Length)]).ToArray());
    }

    private static List<Intervention> SeedInterventions(List<Vehicle> vehicles, List<User> technicians, Random random)
    {
        var today = DateTime.UtcNow.Date;
        var interventions = new List<Intervention>();

        var comments = new Dictionary<InterventionType, string[]>
        {
            [InterventionType.Maintenance] = ["Révision 30 000 km", "Vidange et filtres", "Révision annuelle", "Remplacement des plaquettes avant", "Contrôle de la climatisation"],
            [InterventionType.Repair]      = ["Remplacement des amortisseurs avant", "Diagnostic batterie haute tension", "Réparation du rétroviseur", "Changement de l'embrayage", "Remplacement du capteur ABS"],
            [InterventionType.Inspection]  = ["Contrôle technique préparatoire", "Inspection avant revente", "Vérification de l'état des pneus", "Expertise après sinistre"],
            [InterventionType.Other]       = ["Préparation esthétique avant livraison", "Pose d'attelage", "Mise à jour du logiciel embarqué"],
        };
        var types = new[] { InterventionType.Maintenance, InterventionType.Maintenance, InterventionType.Repair, InterventionType.Inspection, InterventionType.Other };

        Intervention Plan(Vehicle vehicle, DateTime start, int durationDays)
        {
            var storeTechnicians = technicians.Where(t => t.StoreId == vehicle.StoreId).ToList();
            var technician = storeTechnicians[random.Next(storeTechnicians.Count)];
            var type = types[random.Next(types.Length)];
            var comment = comments[type][random.Next(comments[type].Length)];
            return Intervention.Create(vehicle.Id, vehicle.StoreId, technician, type,
                start.AddHours(8 + random.Next(0, 4)), start.AddDays(durationDays).AddHours(17), comment);
        }

        var fleet = vehicles.Where(v => v.Status == VehicleStatus.Available).ToList();

        // Historique terminé, réparti sur deux mois : plusieurs passages par véhicule.
        for (var n = 0; n < 26; n++)
        {
            var vehicle = fleet[random.Next(fleet.Count)];
            var intervention = Plan(vehicle, today.AddDays(-random.Next(6, 62)), random.Next(1, 4));
            intervention.Start();
            intervention.Complete(random.Next(3) == 0 ? "RAS, véhicule restitué" : null);
            interventions.Add(intervention);
        }

        // Interventions annulées.
        string[] cancelReasons = ["Pièce indisponible, reportée", "Véhicule vendu entre-temps", "Doublon de planification"];
        for (var n = 0; n < 3; n++)
        {
            var vehicle = fleet[random.Next(fleet.Count)];
            var intervention = Plan(vehicle, today.AddDays(-random.Next(3, 30)), 1);
            intervention.Cancel(cancelReasons[n]);
            interventions.Add(intervention);
        }

        // Interventions actives : une seule par véhicule, qui passe alors « en intervention ».
        var active = fleet.OrderBy(_ => random.Next()).Take(10).ToList();
        for (var n = 0; n < active.Count; n++)
        {
            var vehicle = active[n];
            Intervention intervention;
            if (n < 4)
            {
                // En cours depuis un ou deux jours.
                intervention = Plan(vehicle, today.AddDays(-random.Next(1, 3)), random.Next(2, 5));
                intervention.Start();
            }
            else if (n < 6)
            {
                // En retard : la date de début est passée et l'atelier n'a pas commencé.
                intervention = Plan(vehicle, today.AddDays(-random.Next(2, 9)), 1);
            }
            else
            {
                // Planifiées dans les deux semaines à venir.
                intervention = Plan(vehicle, today.AddDays(random.Next(1, 15)), random.Next(1, 3));
            }

            vehicle.ChangeStatus(VehicleStatus.InIntervention);
            interventions.Add(intervention);
        }

        return interventions;
    }
}
