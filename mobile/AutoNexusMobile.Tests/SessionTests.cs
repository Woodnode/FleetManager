using System.Net;
using AutoNexusMobile.Core.Api;
using AutoNexusMobile.Core.Auth;
using FluentAssertions;

namespace AutoNexusMobile.Tests;

public class SessionTests
{
    private readonly MemoryTokenStore _store = new();

    [Fact]
    public async Task EtantDonneIdentifiantsValides_QuandConnexion_AlorsEnTeteMobileEtSessionEnregistree()
    {
        var expires = DateTimeOffset.UtcNow.AddMinutes(15);
        var http = new FakeHttp((_, _) => FakeHttp.Json(FakeHttp.Session(FakeHttp.Jwt(expires), "r1")));
        var sessions = new SessionManager(FakeHttp.Client(http), _store);

        var session = await sessions.LoginAsync(" tech1.paris@fleetmanager.fr ", "secret");

        http.Requests.Should().ContainSingle();
        http.Requests[0].Path.Should().Be("/api/v1/auth/login");
        http.Requests[0].Client.Should().Be("mobile");
        http.Requests[0].Body.Should().Contain("\"email\":\"tech1.paris@fleetmanager.fr\"");
        session.Role.Should().Be(UserRole.Technician);
        session.FullName.Should().Be("Lucas Moreau");
        session.Initials.Should().Be("LM");
        session.AccessTokenExpiresAt.ToUnixTimeSeconds().Should().Be(expires.ToUnixTimeSeconds());
        _store.Saved.Should().Be(session);
    }

    [Fact]
    public async Task EtantDonneMauvaisMotDePasse_QuandConnexion_AlorsMessageDuWeb()
    {
        var http = new FakeHttp((_, _) => FakeHttp.Status(HttpStatusCode.Unauthorized, "{\"error\":\"Email ou mot de passe incorrect.\"}"));
        var sessions = new SessionManager(FakeHttp.Client(http), _store);

        var act = () => sessions.LoginAsync("x@y.fr", "faux");

        (await act.Should().ThrowAsync<ApiException>()).Which.Message.Should().Be("Email ou mot de passe incorrect.");
        sessions.Current.Should().BeNull();
    }

    [Fact]
    public async Task EtantDonneJetonValide_QuandAppel_AlorsBearerSansRenouvellement()
    {
        var token = FakeHttp.Jwt(DateTimeOffset.UtcNow.AddMinutes(10));
        var sessions = await Opened(token, "r1", _ => FakeHttp.Status(HttpStatusCode.InternalServerError));
        var api = new FakeHttp((_, _) => FakeHttp.Json(Array.Empty<StoreDto>()));
        var client = new HttpClient(new AuthHeaderHandler(sessions) { InnerHandler = api }) { BaseAddress = new Uri("http://api.test/api/v1/") };

        await new FleetApi(client).GetStoresAsync();

        api.Requests.Should().ContainSingle().Which.Authorization.Should().Be(token);
    }

    [Fact]
    public async Task EtantDonneJetonExpirant_QuandAppel_AlorsRenouveleAvantEnvoiAvecLeRefreshDansLeCorps()
    {
        var old = FakeHttp.Jwt(DateTimeOffset.UtcNow.AddSeconds(20), "old");
        var fresh = FakeHttp.Jwt(DateTimeOffset.UtcNow.AddMinutes(15), "new");
        var auth = new List<string?>();
        var sessions = await Opened(old, "r1", body =>
        {
            auth.Add(body);
            return FakeHttp.Json(FakeHttp.Session(fresh, "r2"));
        });
        var api = new FakeHttp((_, _) => FakeHttp.Json(Array.Empty<StoreDto>()));
        var client = new HttpClient(new AuthHeaderHandler(sessions) { InnerHandler = api }) { BaseAddress = new Uri("http://api.test/api/v1/") };

        await new FleetApi(client).GetStoresAsync();

        auth.Should().ContainSingle().Which.Should().Contain("\"refreshToken\":\"r1\"");
        api.Requests.Single().Authorization.Should().Be(fresh);
        _store.Saved!.RefreshToken.Should().Be("r2");
    }

    [Fact]
    public async Task EtantDonne401_QuandAppel_AlorsRenouvelleUneFoisEtRejoueAvecLeMemeCorps()
    {
        var old = FakeHttp.Jwt(DateTimeOffset.UtcNow.AddMinutes(10), "old");
        var fresh = FakeHttp.Jwt(DateTimeOffset.UtcNow.AddMinutes(15), "new");
        var sessions = await Opened(old, "r1", _ => FakeHttp.Json(FakeHttp.Session(fresh, "r2")));
        var api = new FakeHttp((request, _) => request.Headers.Authorization!.Parameter == old
            ? FakeHttp.Status(HttpStatusCode.Unauthorized)
            : FakeHttp.Json(new { id = Guid.NewGuid(), name = "Paris", address = "1 rue", postalCode = "75001", city = "Paris" }));
        var client = new HttpClient(new AuthHeaderHandler(sessions) { InnerHandler = api }) { BaseAddress = new Uri("http://api.test/api/v1/") };

        await new FleetApi(client).CreateStoreAsync(new StoreRequest("Paris", "1 rue", "75001", "Paris"));

        api.Requests.Should().HaveCount(2);
        api.Requests[1].Authorization.Should().Be(fresh);
        api.Requests[1].Body.Should().Be(api.Requests[0].Body).And.Contain("\"postalCode\":\"75001\"");
    }

    [Fact]
    public async Task EtantDonneRequetesSimultaneesRefusees_QuandRenouveler_AlorsUnSeulRefreshConsomme()
    {
        var old = FakeHttp.Jwt(DateTimeOffset.UtcNow.AddMinutes(10), "old");
        var fresh = FakeHttp.Jwt(DateTimeOffset.UtcNow.AddMinutes(15), "new");
        var refreshCalls = 0;
        var sessions = await Opened(old, "r1", _ =>
        {
            Interlocked.Increment(ref refreshCalls);
            Thread.Sleep(50);
            return FakeHttp.Json(FakeHttp.Session(fresh, "r2"));
        });

        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => sessions.RefreshAsync(old)));

        refreshCalls.Should().Be(1);
        results.Should().OnlyContain(s => s!.AccessToken == fresh);
    }

    [Fact]
    public async Task EtantDonneRefreshRefuse_QuandRenouveler_AlorsSessionEffaceeEtExpirationSignalee()
    {
        var old = FakeHttp.Jwt(DateTimeOffset.UtcNow.AddMinutes(10));
        var sessions = await Opened(old, "r1", _ => FakeHttp.Status(HttpStatusCode.Unauthorized));
        var expired = false;
        sessions.SessionExpired += (_, _) => expired = true;

        var result = await sessions.RefreshAsync(old);

        result.Should().BeNull();
        expired.Should().BeTrue();
        sessions.Current.Should().BeNull();
        _store.Saved.Should().BeNull();
    }

    [Fact]
    public async Task EtantDonneSessionOuverte_QuandDeconnexion_AlorsServeurPrevenuEtSessionEffacee()
    {
        var token = FakeHttp.Jwt(DateTimeOffset.UtcNow.AddMinutes(10));
        FakeHttp? http = null;
        http = new FakeHttp((request, _) => request.RequestUri!.AbsolutePath.EndsWith("login")
            ? FakeHttp.Json(FakeHttp.Session(token, "r1"))
            : FakeHttp.Status(HttpStatusCode.NoContent));
        var sessions = new SessionManager(FakeHttp.Client(http), _store);
        await sessions.LoginAsync("a@b.fr", "x");

        await sessions.LogoutAsync();

        http.Requests.Last().Path.Should().Be("/api/v1/auth/logout");
        http.Requests.Last().Authorization.Should().Be(token);
        sessions.Current.Should().BeNull();
        _store.Saved.Should().BeNull();
    }

    [Theory]
    [InlineData("pas-un-jwt")]
    [InlineData("a.@@@.c")]
    public void EtantDonneJetonIllisible_QuandLireExpiration_AlorsNull(string token)
    {
        JwtExpiry.Read(token).Should().BeNull();
    }

    private async Task<SessionManager> Opened(string accessToken, string refreshToken, Func<string?, HttpResponseMessage> onRefresh)
    {
        var http = new FakeHttp((request, body) => request.RequestUri!.AbsolutePath.EndsWith("login")
            ? FakeHttp.Json(FakeHttp.Session(accessToken, refreshToken))
            : onRefresh(body));
        var sessions = new SessionManager(FakeHttp.Client(http), _store);
        await sessions.LoginAsync("tech1.paris@fleetmanager.fr", "x");
        return sessions;
    }
}
