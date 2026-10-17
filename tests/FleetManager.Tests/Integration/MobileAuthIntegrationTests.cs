using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using FleetManager.Application.Interfaces;
using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;
using FleetManager.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace FleetManager.Tests.Integration;

/// <summary>
/// Session de l'application mobile : jetons dans le corps (en-tête X-Client: mobile), cookies
/// réservés au navigateur. Chaque classe a sa propre application, donc sa propre limite de
/// 5 connexions par minute : aucune classe n'en consomme plus de trois.
/// </summary>
[Collection(ApiHostCollection.Name)]
public sealed class MobileAuthIntegrationTests : IClassFixture<FleetManagerWebAppFactory>
{
    internal const string Password = "Fleet@2024";

    private readonly FleetManagerWebAppFactory _factory;
    private readonly HttpClient _client;

    public MobileAuthIntegrationTests(FleetManagerWebAppFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        AuthTestData.EnsureSeeded(factory);
    }

    [Fact]
    public async Task EtantDonneClientMobile_QuandConnexion_AlorsJetonsDansLeCorpsEtAucunCookie()
    {
        var response = await AuthTestData.LoginAsync(_client, AuthTestData.TechnicianEmail, mobile: true);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.Contains("Set-Cookie").Should().BeFalse();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("refreshToken").GetString().Should().NotBeNullOrWhiteSpace();
        body.GetProperty("role").GetString().Should().Be("Technician");
    }

    [Fact]
    public async Task EtantDonneRefreshTokenDansLeCorps_QuandRafraichir_AlorsRotationEtAncienJetonRefuse()
    {
        var login = await (await AuthTestData.LoginAsync(_client, AuthTestData.TechnicianEmail, mobile: true))
            .Content.ReadFromJsonAsync<JsonElement>();
        var firstRefresh = login.GetProperty("refreshToken").GetString();

        var refreshed = await AuthTestData.MobileRefreshAsync(_client, firstRefresh);
        var replay    = await AuthTestData.MobileRefreshAsync(_client, firstRefresh);

        refreshed.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await refreshed.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("refreshToken").GetString().Should().NotBe(firstRefresh);
        body.GetProperty("accessToken").GetString().Should().NotBeNullOrWhiteSpace();
        replay.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EtantDonneClientMobileSansCorps_QuandRafraichirAvecCookie_AlorsCookieIgnore()
    {
        // Une page tierce qui imiterait le mobile ne doit pas obtenir de jetons lisibles à partir
        // du cookie d'un navigateur.
        var webLogin = await AuthTestData.LoginAsync(_client, AuthTestData.TechnicianEmail, mobile: false);
        var refreshCookie = AuthTestData.CookieValue(webLogin, "refresh_token");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("X-Client", "mobile");
        request.Headers.Add("Cookie", $"refresh_token={refreshCookie}");
        var response = await _client.SendAsync(request);

        refreshCookie.Should().NotBeNullOrWhiteSpace();
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task EtantDonneJetonDeTechnicien_QuandMesInterventions_AlorsAccepteEnBearer()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/interventions/mine");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AuthTestData.TokenFor(_factory, AuthTestData.TechnicianEmail));

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<JsonElement>()).ValueKind.Should().Be(JsonValueKind.Array);
    }

    [Fact]
    public async Task EtantDonneJetonDeGerant_QuandMesInterventions_AlorsInterdit()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/v1/interventions/mine");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AuthTestData.TokenFor(_factory, AuthTestData.ManagerEmail));

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task EtantDonnePeriodeTropLongue_QuandMesInterventions_AlorsRequeteInvalide()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            "/api/v1/interventions/mine?from=2026-10-01T04:00:00Z&to=2026-10-20T04:00:00Z");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", AuthTestData.TokenFor(_factory, AuthTestData.TechnicianEmail));

        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}

/// <summary>Le navigateur garde exactement le comportement d'avant : cookies, rien dans le corps.</summary>
[Collection(ApiHostCollection.Name)]
public sealed class WebAuthIntegrationTests : IClassFixture<FleetManagerWebAppFactory>
{
    private readonly HttpClient _client;

    public WebAuthIntegrationTests(FleetManagerWebAppFactory factory)
    {
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        AuthTestData.EnsureSeeded(factory);
    }

    [Fact]
    public async Task EtantDonneNavigateur_QuandConnexion_AlorsCookiesEtAucunJetonDansLeCorps()
    {
        var response = await AuthTestData.LoginAsync(_client, AuthTestData.ManagerEmail, mobile: false);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AuthTestData.CookieValue(response, "access_token").Should().NotBeNullOrWhiteSpace();
        AuthTestData.CookieValue(response, "refresh_token").Should().NotBeNullOrWhiteSpace();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("accessToken", out _).Should().BeFalse();
        body.TryGetProperty("refreshToken", out _).Should().BeFalse();
    }

    [Fact]
    public async Task EtantDonneCookieSansCorps_QuandRafraichir_AlorsNouveauxCookies()
    {
        var login = await AuthTestData.LoginAsync(_client, AuthTestData.ManagerEmail, mobile: false);
        var refreshCookie = AuthTestData.CookieValue(login, "refresh_token");

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh");
        request.Headers.Add("Cookie", $"refresh_token={refreshCookie}");
        var response = await _client.SendAsync(request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        AuthTestData.CookieValue(response, "refresh_token").Should().NotBe(refreshCookie);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.TryGetProperty("refreshToken", out _).Should().BeFalse();
    }

    [Fact]
    public async Task EtantDonneNavigateurAvecRefreshTokenDansLeCorps_QuandRafraichir_AlorsRefuse()
    {
        // Le corps n'est lu que pour le mobile : le navigateur passe toujours par le cookie.
        var response = await _client.PostAsJsonAsync("/api/v1/auth/refresh", new { refreshToken = "quelconque" });

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}

internal static class AuthTestData
{
    public const string TechnicianEmail = "tech1.paris@test.fr";
    public const string ManagerEmail    = "directeur.paris@test.fr";

    private static readonly object Gate = new();

    public static void EnsureSeeded(FleetManagerWebAppFactory factory)
    {
        lock (Gate)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<FleetManagerDbContext>();
            if (db.Users.Any())
                return;

            var hash  = BCrypt.Net.BCrypt.HashPassword(MobileAuthIntegrationTests.Password, workFactor: 4);
            var store = Store.Create("Paris Centre", "1 rue de Rivoli", "75001", "Paris");
            db.AddRange(
                store,
                User.Create("Lucas", "Moreau", TechnicianEmail, hash, UserRole.Technician, store.Id),
                User.Create("Thomas", "Dupont", ManagerEmail, hash, UserRole.StoreManager, store.Id));
            db.SaveChanges();
        }
    }

    public static string TokenFor(FleetManagerWebAppFactory factory, string email)
    {
        using var scope = factory.Services.CreateScope();
        var db   = scope.ServiceProvider.GetRequiredService<FleetManagerDbContext>();
        var user = db.Users.AsEnumerable().Single(u => u.Email.Value == email);
        return scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>().GenerateToken(user);
    }

    public static Task<HttpResponseMessage> LoginAsync(HttpClient client, string email, bool mobile)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/login")
        {
            Content = JsonContent.Create(new { email, password = MobileAuthIntegrationTests.Password }),
        };
        if (mobile)
            request.Headers.Add("X-Client", "mobile");
        return client.SendAsync(request);
    }

    public static Task<HttpResponseMessage> MobileRefreshAsync(HttpClient client, string? refreshToken)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/refresh")
        {
            Content = JsonContent.Create(new { refreshToken }),
        };
        request.Headers.Add("X-Client", "mobile");
        return client.SendAsync(request);
    }

    public static string? CookieValue(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues("Set-Cookie", out var cookies)
            ? cookies.Select(c => c.Split(';')[0])
                     .Where(c => c.StartsWith(name + "=", StringComparison.Ordinal))
                     .Select(c => Uri.UnescapeDataString(c[(name.Length + 1)..]))
                     .FirstOrDefault()
            : null;
}
