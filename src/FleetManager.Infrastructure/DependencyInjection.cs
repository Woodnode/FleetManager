using Microsoft.Extensions.DependencyInjection.Extensions;
using FleetManager.Infrastructure.Realtime;
using FleetManager.Application.Interfaces;
using FleetManager.Domain.Interfaces;
using FleetManager.Infrastructure.Persistence;
using FleetManager.Infrastructure.Persistence.Repositories;
using FleetManager.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FleetManager.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtSettings>(configuration.GetSection("JwtSettings"));

        // Temps réel : l'API remplace ce notificateur vide par SignalR.
        services.TryAddSingleton<IRealtimeNotifier, NoOpRealtimeNotifier>();
        services.AddScoped<RealtimeChangeInterceptor>();

        services.AddDbContext<FleetManagerDbContext>((sp, options) =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                b => b.MigrationsAssembly(typeof(FleetManagerDbContext).Assembly.FullName))
            .AddInterceptors(sp.GetRequiredService<RealtimeChangeInterceptor>()));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IVehicleRepository, VehicleRepository>();
        services.AddScoped<IInterventionRepository, InterventionRepository>();
        services.AddScoped<IStoreRepository, StoreRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRefreshTokenRepository, RefreshTokenRepository>();
        services.AddScoped<DatabaseSeeder>();
        services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
        services.AddScoped<IPasswordHasher, PasswordHasher>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddMemoryCache();
        services.AddSingleton<ITokenBlacklist, InMemoryTokenBlacklist>();

        return services;
    }
}
