using System.Net;
using System.Text;
using System.Text.Json;
using AutoNexusMobile.Core.Api;
using AutoNexusMobile.Core.Auth;

namespace AutoNexusMobile.Tests;

/// <summary>Serveur factice : chaque requête est enregistrée et reçoit la réponse de la fonction donnée.</summary>
internal sealed class FakeHttp : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, string?, HttpResponseMessage> _respond;

    public FakeHttp(Func<HttpRequestMessage, string?, HttpResponseMessage> respond) => _respond = respond;

    public List<(HttpMethod Method, string Path, string? Authorization, string? Client, string? Body)> Requests { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
        lock (Requests)
        {
            Requests.Add((request.Method, request.RequestUri!.PathAndQuery, request.Headers.Authorization?.Parameter,
                request.Headers.TryGetValues(SessionManager.ClientHeader, out var client) ? client.Single() : null, body));
        }

        return _respond(request, body);
    }

    public static HttpResponseMessage Json(object value, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(JsonSerializer.Serialize(value, ApiJson.Options), Encoding.UTF8, "application/json") };

    public static HttpResponseMessage Status(HttpStatusCode status, string? body = null) =>
        new(status) { Content = new StringContent(body ?? "", Encoding.UTF8, "application/problem+json") };

    public static HttpClient Client(HttpMessageHandler handler) =>
        new(handler) { BaseAddress = new Uri("http://api.test/api/v1/") };

    /// <summary>JWT non signé dont seule l'expiration compte pour l'application.</summary>
    public static string Jwt(DateTimeOffset expires, string id = "a")
    {
        static string Encode(string json) =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(json)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        return $"{Encode("{\"alg\":\"none\"}")}.{Encode($"{{\"exp\":{expires.ToUnixTimeSeconds()},\"jti\":\"{id}\"}}")}.sig";
    }

    public static SessionResponse Session(string accessToken, string refreshToken, UserRole role = UserRole.Technician) =>
        new(Guid.Parse("5b8a2f0e-6c1d-4f4e-9a57-3f1c2b7d9e01"), "Lucas", "Moreau", "tech1.paris@fleetmanager.fr",
            role, Guid.Parse("0f3e8d21-9b4c-4a77-8e5f-2d6c1a9b7e33"), accessToken, refreshToken);
}

internal sealed class MemoryTokenStore : ITokenStore
{
    public AuthSession? Saved { get; private set; }

    public Task<AuthSession?> LoadAsync() => Task.FromResult(Saved);

    public Task SaveAsync(AuthSession session)
    {
        Saved = session;
        return Task.CompletedTask;
    }

    public Task ClearAsync()
    {
        Saved = null;
        return Task.CompletedTask;
    }
}
