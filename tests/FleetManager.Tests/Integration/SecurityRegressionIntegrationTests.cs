using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using FleetManager.Api.Infrastructure;
using FleetManager.Application.Interfaces;
using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;
using FleetManager.Infrastructure.Persistence;
using FleetManager.Infrastructure.Services;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace FleetManager.Tests.Integration;

/// <summary>
/// Non-régression des failles relevées par l'audit de sécurité, vérifiées à travers l'API réelle
/// (pipeline HTTP, autorisations par rôle, limiteur, liste noire des JTI).
/// </summary>
[Collection(ApiHostCollection.Name)]
public sealed class InterventionCreationRoleTests : IClassFixture<FleetManagerWebAppFactory>
{
    private readonly FleetManagerWebAppFactory _factory;
    private readonly HttpClient _client;

    public InterventionCreationRoleTests(FleetManagerWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        AuthTestData.EnsureSeeded(factory);
    }

    [Fact]
    public async Task EtantDonneTechnicien_QuandCreerIntervention_AlorsInterdit()
    {
        // La création est réservée aux gérants : le technicien est refusé avant même la validation du corps.
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/interventions")
        {
            Content = JsonContent.Create(new
            {
                vehicleId = Guid.NewGuid(), storeId = Guid.NewGuid(), technicianId = Guid.NewGuid(),
                type = "Repair", plannedStartDate = DateTime.UtcNow.AddDays(1), plannedEndDate = DateTime.UtcNow.AddDays(2),
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AuthTestData.TokenFor(_factory, AuthTestData.TechnicianEmail));

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task EtantDonneGerant_QuandCreerInterventionInvalide_AlorsPasInterdit()
    {
        // Le gérant franchit l'autorisation : la requête échoue ensuite sur le véhicule inexistant, pas sur le rôle.
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/interventions")
        {
            Content = JsonContent.Create(new
            {
                vehicleId = Guid.NewGuid(), storeId = Guid.NewGuid(), technicianId = Guid.NewGuid(),
                type = "Repair", plannedStartDate = DateTime.UtcNow.AddDays(1), plannedEndDate = DateTime.UtcNow.AddDays(2),
            }),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AuthTestData.TokenFor(_factory, AuthTestData.ManagerEmail));

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().NotBe(HttpStatusCode.Forbidden);
        response.StatusCode.Should().NotBe(HttpStatusCode.Created);
    }
}

/// <summary>Utilisateur dont l'enseigne a disparu : il ne doit voir aucune donnée, pas tout le parc.</summary>
[Collection(ApiHostCollection.Name)]
public sealed class OrphanStoreScopeTests : IClassFixture<FleetManagerWebAppFactory>
{
    private readonly FleetManagerWebAppFactory _factory;
    private readonly HttpClient _client;

    public OrphanStoreScopeTests(FleetManagerWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string url, string token)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await _client.SendAsync(request);
    }

    private static string Email(string prefix) => $"{prefix}.{Guid.NewGuid():N}@test.fr";

    [Fact]
    public async Task EtantDonneEnseigneAvecUtilisateurs_QuandSupprimer_AlorsConflitEtUtilisateurRattache()
    {
        string adminEmail = Email("admin"), managerEmail = Email("gerante");
        var store = Store.Create("Enseigne Vide", "3 rue C", "13001", "Marseille");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FleetManagerDbContext>();
            db.AddRange(
                store,
                User.Create("Ada", "Admin", adminEmail, "hash", UserRole.Admin, null),
                User.Create("Gina", "Gerante", managerEmail, "hash", UserRole.StoreManager, store.Id));
            db.SaveChanges();
        }

        var response = await SendAsync(HttpMethod.Delete, $"/api/v1/stores/{store.Id}", AuthTestData.TokenFor(_factory, adminEmail));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        using var verify = _factory.Services.CreateScope();
        var users = verify.ServiceProvider.GetRequiredService<FleetManagerDbContext>().Users.AsNoTracking().AsEnumerable();
        users.Single(u => u.Email.Value == managerEmail).StoreId.Should().Be(store.Id);
    }

    [Fact]
    public async Task EtantDonneGerantSansEnseigne_QuandTableauDeBordEtEnseignes_AlorsRienNEstVisible()
    {
        // Données héritées d'avant le correctif : l'enseigne a été supprimée, la clé étrangère SetNull a vidé StoreId.
        string managerEmail = Email("orpheline");
        var visible = Store.Create("Paris Factice", "1 rue A", "75001", "Paris");
        var deleted = Store.Create("Enseigne Supprimée", "3 rue C", "13001", "Marseille");
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FleetManagerDbContext>();
            var manager = User.Create("Gina", "Gerante", managerEmail, "hash", UserRole.StoreManager, deleted.Id);
            db.AddRange(visible, deleted, manager,
                Vehicle.Create("1HGBH41JXMN109186", "Peugeot", "308", 2021, 1000, visible.Id));
            db.SaveChanges();
            db.Remove(deleted);
            db.SaveChanges();
            manager.StoreId.Should().BeNull();
        }
        var token = AuthTestData.TokenFor(_factory, managerEmail);

        var dashboard = await SendAsync(HttpMethod.Get, "/api/v1/dashboard/summary", token);
        var stores    = await SendAsync(HttpMethod.Get, "/api/v1/stores", token);

        dashboard.StatusCode.Should().Be(HttpStatusCode.OK);
        var summary = await dashboard.Content.ReadFromJsonAsync<JsonElement>();
        summary.GetProperty("vehicles").GetProperty("total").GetInt32().Should().Be(0);
        summary.GetProperty("recentInterventions").GetArrayLength().Should().Be(0);
        stores.StatusCode.Should().Be(HttpStatusCode.OK);
        (await stores.Content.ReadFromJsonAsync<JsonElement>()).GetArrayLength().Should().Be(0);
    }
}

/// <summary>
/// Le limiteur de connexion compte par adresse IP, et l'en-tête d'IP client (voir ClientIpForwarding)
/// n'est cru que s'il arrive d'une adresse privée (Caddy, réseau Docker).
/// </summary>
[Collection(ApiHostCollection.Name)]
public sealed class LoginRateLimiterTests : IClassFixture<FleetManagerWebAppFactory>
{
    private readonly FleetManagerWebAppFactory _factory;

    public LoginRateLimiterTests(FleetManagerWebAppFactory factory)
    {
        _factory = factory;
        AuthTestData.EnsureSeeded(factory);
    }

    private async Task<HttpStatusCode> LoginFromAsync(string remoteIp, string email, string password, string? clientIpHeader = null)
    {
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { email, password }));
        var context = await _factory.Server.SendAsync(c =>
        {
            c.Connection.RemoteIpAddress = IPAddress.Parse(remoteIp);
            c.Request.Method = HttpMethods.Post;
            c.Request.Path = "/api/v1/auth/login";
            c.Request.ContentType = "application/json";
            c.Request.ContentLength = body.Length;
            c.Request.Body = new MemoryStream(body);
            c.Request.Headers["X-Client"] = "mobile";
            if (clientIpHeader is not null)
                c.Request.Headers[ClientIpForwarding.HeaderName] = clientIpHeader;
        });
        return (HttpStatusCode)context.Response.StatusCode;
    }

    [Fact]
    public async Task EtantDonneCinqEchecsDUnAttaquant_QuandUnAutreClientSeConnecte_AlorsIlNEstPasBloque()
    {
        for (var i = 0; i < 5; i++)
            await LoginFromAsync("203.0.113.7", $"personne{i}@test.fr", "x");

        var attacker = await LoginFromAsync("203.0.113.7", "personne@test.fr", "x");
        var victim   = await LoginFromAsync("198.51.100.4", AuthTestData.ManagerEmail, MobileAuthIntegrationTests.Password);

        attacker.Should().Be(HttpStatusCode.TooManyRequests);
        victim.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task EtantDonneClientNonFiable_QuandIlChangeDEnTeteDIp_AlorsIlResteLimite()
    {
        // L'en-tête vient directement d'Internet : il est ignoré, l'adresse réelle de la connexion compte.
        var statuses = new List<HttpStatusCode>();
        for (var i = 0; i < 6; i++)
            statuses.Add(await LoginFromAsync("203.0.113.50", $"personne{i}@test.fr", "x", clientIpHeader: $"192.0.2.{i + 1}"));

        statuses[5].Should().Be(HttpStatusCode.TooManyRequests);
    }

    [Fact]
    public async Task EtantDonneProxyDeConfiance_QuandEnTeteDIp_AlorsChaqueClientASaPropreLimite()
    {
        // Derrière le proxy local (loopback), l'adresse du visiteur sert de clé de partition.
        for (var i = 0; i < 6; i++)
            await LoginFromAsync("127.0.0.1", $"personne{i}@test.fr", "x", clientIpHeader: "203.0.113.99");

        var victim = await LoginFromAsync("127.0.0.1", AuthTestData.ManagerEmail, MobileAuthIntegrationTests.Password, clientIpHeader: "198.51.100.77");

        victim.Should().Be(HttpStatusCode.OK);
    }
}

/// <summary>Liste noire qui garde la durée demandée pour chaque JTI, en plus de la révocation réelle.</summary>
public sealed class RecordingTokenBlacklist : ITokenBlacklist
{
    private readonly InMemoryTokenBlacklist _inner;
    public ConcurrentDictionary<string, TimeSpan> Revocations { get; } = new();

    public RecordingTokenBlacklist(IMemoryCache cache) => _inner = new InMemoryTokenBlacklist(cache);

    public void Revoke(string jti, TimeSpan expiresIn)
    {
        Revocations[jti] = expiresIn;
        _inner.Revoke(jti, expiresIn);
    }

    public bool IsRevoked(string jti) => _inner.IsRevoked(jti);
}

/// <summary>Jetons d'une heure, comme dans docker-compose.yml.</summary>
public sealed class OneHourTokenFactory : FleetManagerWebAppFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.UseSetting("JwtSettings:ExpiryMinutes", "60");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ITokenBlacklist>();
            services.AddSingleton<ITokenBlacklist, RecordingTokenBlacklist>();
        });
    }
}

[Collection(ApiHostCollection.Name)]
public sealed class LogoutRevocationTests : IClassFixture<OneHourTokenFactory>
{
    private readonly OneHourTokenFactory _factory;

    public LogoutRevocationTests(OneHourTokenFactory factory)
    {
        _factory = factory;
        AuthTestData.EnsureSeeded(factory);
    }

    [Fact]
    public async Task EtantDonneJetonDUneHeure_QuandDeconnexion_AlorsJtiRevoqueJusquaSonExpiration()
    {
        var client = _factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var login = await (await AuthTestData.LoginAsync(client, AuthTestData.TechnicianEmail, mobile: true))
            .Content.ReadFromJsonAsync<JsonElement>();
        var access = login.GetProperty("accessToken").GetString()!;
        var jti = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(access).Id;

        using (var logout = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/logout"))
        {
            logout.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
            (await client.SendAsync(logout)).IsSuccessStatusCode.Should().BeTrue();
        }
        using var me = new HttpRequestMessage(HttpMethod.Get, "/api/v1/auth/me");
        me.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access);
        var afterLogout = await client.SendAsync(me);

        afterLogout.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        // Avant le correctif, 15 minutes fixes : le jeton redevenait valide pendant les 45 minutes restantes.
        var blacklist = (RecordingTokenBlacklist)_factory.Services.GetRequiredService<ITokenBlacklist>();
        // La durée inclut la minute de tolérance d'horloge ajoutée par LogoutCommandHandler.RemainingLifetime.
        blacklist.Revocations[jti].Should().BeGreaterThan(TimeSpan.FromMinutes(59)).And.BeLessOrEqualTo(TimeSpan.FromMinutes(61));
    }
}
