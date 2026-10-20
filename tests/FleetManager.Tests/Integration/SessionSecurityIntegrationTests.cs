using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FleetManager.Application.Auth.Commands;
using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;
using FleetManager.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace FleetManager.Tests.Integration;

/// <summary>
/// Protections ajoutées après l'audit de sécurité : jetons hachés, révocation de la famille en cas
/// de réutilisation, comptes désactivés, droits du technicien. Application propre à la classe :
/// trois connexions au plus, sous la limite de cinq par minute.
/// </summary>
[Collection(ApiHostCollection.Name)]
public sealed class SessionSecurityIntegrationTests : IClassFixture<FleetManagerWebAppFactory>
{
    private const string AdminEmail = "admin@test.fr";
    private const string InactiveEmail = "ancien.paris@test.fr";

    private readonly FleetManagerWebAppFactory _factory;
    private readonly HttpClient _client;

    public SessionSecurityIntegrationTests(FleetManagerWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        AuthTestData.EnsureSeeded(factory);
        SeedExtraUsers();
    }

    [Fact]
    public async Task EtantDonneJetonDejaConsomme_QuandRejoue_AlorsLaSessionEntiereEstRevoquee()
    {
        var login = await (await AuthTestData.LoginAsync(_client, AuthTestData.TechnicianEmail, mobile: true))
            .Content.ReadFromJsonAsync<JsonElement>();
        var stolen = login.GetProperty("refreshToken").GetString();

        var legit = await (await AuthTestData.MobileRefreshAsync(_client, stolen)).Content.ReadFromJsonAsync<JsonElement>();
        var replay = await AuthTestData.MobileRefreshAsync(_client, stolen);
        var afterReplay = await AuthTestData.MobileRefreshAsync(_client, legit.GetProperty("refreshToken").GetString());

        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        afterReplay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EtantDonneConnexion_QuandLireLaBase_AlorsSeuleLEmpreinteDuJetonEstStockee()
    {
        var login = await (await AuthTestData.LoginAsync(_client, AuthTestData.ManagerEmail, mobile: true))
            .Content.ReadFromJsonAsync<JsonElement>();
        var token = login.GetProperty("refreshToken").GetString()!;

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetManagerDbContext>();
        var hashes = await db.RefreshTokens.Select(r => r.TokenHash).ToListAsync();

        hashes.Should().Contain(RefreshToken.Hash(token));
        hashes.Should().NotContain(token);
    }

    [Fact]
    public async Task EtantDonneCompteDesactive_QuandConnexion_AlorsRefuseCommeUnMauvaisMotDePasse()
    {
        var response = await AuthTestData.LoginAsync(_client, InactiveEmail, mobile: true);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EtantDonneAdmin_QuandDesactiverUnCompte_AlorsSesJetonsSontRevoques()
    {
        Guid managerId;
        string refreshValue;
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FleetManagerDbContext>();
            var manager = db.Users.AsEnumerable().Single(u => u.Email.Value == AuthTestData.ManagerEmail);
            managerId = manager.Id;
            var (entity, value) = RefreshToken.Create(manager.Id);
            db.RefreshTokens.Add(entity);
            await db.SaveChangesAsync();
            refreshValue = value;
        }

        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/users/{managerId}/active")
        {
            Content = JsonContent.Create(new { isActive = false }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AuthTestData.TokenFor(_factory, AdminEmail));
        var response = await _client.SendAsync(request);
        var refresh = await AuthTestData.MobileRefreshAsync(_client, refreshValue);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        refresh.StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // Remise en état pour les autres tests de la classe.
        using var restore = _factory.Services.CreateScope();
        var restoreDb = restore.ServiceProvider.GetRequiredService<FleetManagerDbContext>();
        restoreDb.Users.Single(u => u.Id == managerId).Activate();
        await restoreDb.SaveChangesAsync();
    }

    [Fact]
    public async Task EtantDonneGerant_QuandDesactiverUnCompte_AlorsInterdit()
    {
        var target = Guid.NewGuid();
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/users/{target}/active")
        {
            Content = JsonContent.Create(new { isActive = false }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AuthTestData.TokenFor(_factory, AuthTestData.ManagerEmail));

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task EtantDonneTechnicien_QuandChangerStatutVehicule_AlorsInterdit()
    {
        using var request = new HttpRequestMessage(HttpMethod.Patch, $"/api/v1/vehicles/{Guid.NewGuid()}/status")
        {
            Content = JsonContent.Create(new { newStatus = "Sold" }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AuthTestData.TokenFor(_factory, AuthTestData.TechnicianEmail));

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task EtantDonneTechnicien_QuandPlanifierUneIntervention_AlorsInterdit()
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/interventions")
        {
            Content = JsonContent.Create(new
            {
                vehicleId = Guid.NewGuid(),
                storeId = Guid.NewGuid(),
                technicianId = Guid.NewGuid(),
                type = "Maintenance",
                plannedStartDate = DateTime.UtcNow.AddDays(1),
                plannedEndDate = DateTime.UtcNow.AddDays(1).AddHours(2),
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AuthTestData.TokenFor(_factory, AuthTestData.TechnicianEmail));

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    private void SeedExtraUsers()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<FleetManagerDbContext>();
        if (db.Users.AsEnumerable().Any(u => u.Email.Value == AdminEmail))
            return;

        var hash = BCrypt.Net.BCrypt.HashPassword(MobileAuthIntegrationTests.Password, workFactor: 4);
        var storeId = db.Stores.Select(s => s.Id).First();
        var inactive = User.Create("Paul", "Ancien", InactiveEmail, hash, UserRole.Technician, storeId);
        inactive.Deactivate();
        db.AddRange(User.Create("Sophie", "Martin", AdminEmail, hash, UserRole.Admin), inactive);
        db.SaveChanges();
    }
}

public class LogoutRemainingLifetimeTests
{
    [Fact]
    public void EtantDonneJetonValideUneHeure_QuandDeconnexion_AlorsListeNoireJusquALExpiration()
    {
        var ttl = LogoutCommandHandler.RemainingLifetime(DateTimeOffset.UtcNow.AddMinutes(60));

        ttl.Should().BeGreaterThan(TimeSpan.FromMinutes(60));
        ttl.Should().BeLessThan(TimeSpan.FromMinutes(62));
    }

    [Fact]
    public void EtantDonneExpirationInconnue_QuandDeconnexion_AlorsUneJournee()
    {
        LogoutCommandHandler.RemainingLifetime(null).Should().Be(TimeSpan.FromDays(1));
    }
}
