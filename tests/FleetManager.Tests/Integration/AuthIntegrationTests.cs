using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace FleetManager.Tests.Integration;

public class AuthIntegrationTests : IClassFixture<FleetManagerWebAppFactory>
{
    private readonly HttpClient _client;

    public AuthIntegrationTests(FleetManagerWebAppFactory factory)
    {
        _client = factory.CreateClient();
    }

    // ── GET /api/auth/me — sans cookie ────────────────────────────────────────

    [Fact]
    public async Task Me_SansCookie_Retourne401()
    {
        var response = await _client.GetAsync("/api/v1/auth/me");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── GET /api/v1/vehicles — sans cookie ───────────────────────────────────

    [Fact]
    public async Task Vehicles_SansCookie_Retourne401()
    {
        var response = await _client.GetAsync("/api/v1/vehicles");
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── POST /api/v1/auth/login — identifiants incorrects ────────────────────

    [Fact]
    public async Task Login_IdentifiantsIncorrects_Retourne401()
    {
        var payload = new { email = "inexistant@test.fr", password = "WrongPass" };
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", payload);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── POST /api/v1/auth/login — payload invalide (400) ─────────────────────

    [Fact]
    public async Task Login_PayloadVide_Retourne400OuUnauthorized()
    {
        var payload = new { email = "", password = "" };
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", payload);
        // Empty credentials may produce 400 (validation) or 401 (auth failure)
        response.StatusCode.Should().BeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized);
    }

    // ── POST /api/v1/auth/register — sans token ──────────────────────────────

    [Fact]
    public async Task Register_SansToken_Retourne401()
    {
        var payload = new { firstName = "Test", lastName = "User", email = "t@t.fr", password = "Pass1!", role = "Admin" };
        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", payload);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Endpoints protégés — sans token ──────────────────────────────────────

    [Theory]
    [InlineData("/api/v1/interventions")]
    [InlineData("/api/v1/stores")]
    public async Task EndpointProtege_SansCookie_Retourne401(string url)
    {
        var response = await _client.GetAsync(url);
        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
