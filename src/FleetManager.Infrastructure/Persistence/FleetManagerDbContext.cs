using FleetManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Reflection;

namespace FleetManager.Infrastructure.Persistence;

public class FleetManagerDbContext : DbContext
{
    public FleetManagerDbContext(DbContextOptions<FleetManagerDbContext> options) : base(options) { }

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Intervention> Interventions => Set<Intervention>();
    public DbSet<User> Users => Set<User>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Dates stockées comme le faisait SQL Server (datetime2) : sans fuseau horaire.
    /// Npgsql refuse d'écrire un DateTime dont le Kind ne correspond pas au type de colonne
    /// (UTC vers "timestamp without time zone", ou Unspecified vers "timestamptz").
    /// Le code écrit des DateTime.UtcNow (Kind=Utc) et l'API reçoit des dates sans fuseau
    /// (Kind=Unspecified) : on les normalise toutes en Unspecified, sans décaler l'heure,
    /// pour que les réponses JSON restent identiques à la version SQL Server.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        configurationBuilder.Properties<DateTime>()
            .HaveColumnType("timestamp without time zone")
            .HaveConversion<UnspecifiedKindConverter>();

        // Le convertisseur non nullable s'applique aussi aux DateTime? (EF ne l'appelle pas pour null).
        configurationBuilder.Properties<DateTime?>()
            .HaveColumnType("timestamp without time zone")
            .HaveConversion<UnspecifiedKindConverter>();
    }

    private sealed class UnspecifiedKindConverter()
        : ValueConverter<DateTime, DateTime>(
            v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified),
            v => DateTime.SpecifyKind(v, DateTimeKind.Unspecified));
}
