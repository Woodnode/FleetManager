using FleetManager.Domain.Entities;
using Microsoft.EntityFrameworkCore;
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
}
