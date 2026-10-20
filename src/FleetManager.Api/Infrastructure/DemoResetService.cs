using FleetManager.Infrastructure.Persistence;

namespace FleetManager.Api.Infrastructure;

/// <summary>
/// Remise à zéro quotidienne des données de démonstration. Les comptes de démo sont publics,
/// dont un administrateur : sans cette remise à zéro, un visiteur pourrait vider le parc ou
/// laisser des comptes à lui. Activée par <c>Demo:ResetEnabled</c>, à l'heure UTC <c>Demo:ResetHourUtc</c>.
/// </summary>
public sealed class DemoResetService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<DemoResetService> _logger;
    private readonly int _hourUtc;

    public DemoResetService(IServiceScopeFactory scopes, IConfiguration configuration, ILogger<DemoResetService> logger)
    {
        _scopes = scopes;
        _logger = logger;
        _hourUtc = Math.Clamp(configuration.GetValue("Demo:ResetHourUtc", 4), 0, 23);
    }

    /// <summary>Délai jusqu'à la prochaine remise à zéro, toujours dans les 24 heures.</summary>
    public static TimeSpan DelayUntilNext(DateTimeOffset now, int hourUtc)
    {
        var next = new DateTimeOffset(now.UtcDateTime.Date.AddHours(hourUtc), TimeSpan.Zero);
        if (next <= now)
            next = next.AddDays(1);
        return next - now;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(DelayUntilNext(DateTimeOffset.UtcNow, _hourUtc), stoppingToken);

            try
            {
                using var scope = _scopes.CreateScope();
                await scope.ServiceProvider.GetRequiredService<DatabaseSeeder>().ResetAsync(stoppingToken);
                _logger.LogInformation("Données de démonstration remises à zéro.");
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Échec de la remise à zéro des données de démonstration.");
            }
        }
    }
}
