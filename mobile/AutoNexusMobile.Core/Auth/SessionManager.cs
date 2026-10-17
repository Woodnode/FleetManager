using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using AutoNexusMobile.Core.Api;

namespace AutoNexusMobile.Core.Auth;

/// <summary>
/// Ouvre, renouvelle et ferme la session. Les appels d'authentification portent l'en-tête
/// <c>X-Client: mobile</c> : l'API répond alors avec les jetons dans le corps au lieu de cookies.
/// Le renouvellement est sérialisé : plusieurs requêtes qui reçoivent un 401 en même temps ne
/// consomment qu'un seul refresh token (la rotation révoque l'ancien).
/// </summary>
public sealed class SessionManager
{
    public const string ClientHeader = "X-Client";
    public const string ClientValue = "mobile";

    // Marge avant expiration : un jeton qui expire dans la minute est renouvelé avant l'envoi.
    private static readonly TimeSpan RefreshMargin = TimeSpan.FromSeconds(60);

    private readonly HttpClient _authClient;
    private readonly ITokenStore _store;
    private readonly TimeProvider _clock;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    public SessionManager(HttpClient authClient, ITokenStore store, TimeProvider? clock = null)
    {
        _authClient = authClient;
        _store = store;
        _clock = clock ?? TimeProvider.System;
    }

    public AuthSession? Current { get; private set; }

    /// <summary>La session a changé (connexion, renouvellement, déconnexion).</summary>
    public event EventHandler<AuthSession?>? SessionChanged;

    /// <summary>Le refresh token a été refusé : il faut se reconnecter.</summary>
    public event EventHandler? SessionExpired;

    public async Task<AuthSession?> RestoreAsync()
    {
        Current = await _store.LoadAsync();
        return Current;
    }

    public async Task<AuthSession> LoginAsync(string email, string password, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "auth/login")
        {
            Content = JsonContent.Create(new LoginRequest(email.Trim(), password), options: ApiJson.Options),
        };
        request.Headers.Add(ClientHeader, ClientValue);

        using var response = await _authClient.SendAsync(request, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw response.StatusCode == HttpStatusCode.Unauthorized
                ? new ApiException(HttpStatusCode.Unauthorized, "Email ou mot de passe incorrect.")
                : ApiException.FromResponse(response.StatusCode, body);
        }

        var session = AuthSession.From(await ReadSessionAsync(response, ct));
        await SetAsync(session);
        return session;
    }

    /// <summary>
    /// Jeton d'accès à joindre à une requête, renouvelé s'il expire bientôt. Null si aucune
    /// session n'est ouverte ou si le renouvellement a échoué.
    /// </summary>
    public async Task<string?> GetAccessTokenAsync(CancellationToken ct = default)
    {
        var session = Current;
        if (session is null)
            return null;

        if (session.AccessTokenExpiresAt - _clock.GetUtcNow() > RefreshMargin)
            return session.AccessToken;

        return (await RefreshAsync(session.AccessToken, ct))?.AccessToken;
    }

    /// <summary>
    /// Renouvelle la session. <paramref name="rejectedAccessToken"/> est le jeton qui vient d'être
    /// refusé ou qui expire : si une autre requête l'a déjà remplacé, on réutilise le nouveau.
    /// </summary>
    public async Task<AuthSession?> RefreshAsync(string? rejectedAccessToken, CancellationToken ct = default)
    {
        await _refreshGate.WaitAsync(ct);
        try
        {
            var session = Current;
            if (session is null)
                return null;

            if (rejectedAccessToken is not null && session.AccessToken != rejectedAccessToken)
                return session;

            using var request = new HttpRequestMessage(HttpMethod.Post, "auth/refresh")
            {
                Content = JsonContent.Create(new RefreshRequest(session.RefreshToken), options: ApiJson.Options),
            };
            request.Headers.Add(ClientHeader, ClientValue);

            HttpResponseMessage response;
            try
            {
                response = await _authClient.SendAsync(request, ct);
            }
            catch (HttpRequestException)
            {
                // Réseau coupé : la session reste, la requête appelante échouera et pourra être relancée.
                return null;
            }

            using (response)
            {
                if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.NotFound)
                {
                    await EndAsync();
                    SessionExpired?.Invoke(this, EventArgs.Empty);
                    return null;
                }

                if (!response.IsSuccessStatusCode)
                    return null;

                var refreshed = AuthSession.From(await ReadSessionAsync(response, ct));
                await SetAsync(refreshed);
                return refreshed;
            }
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    /// <summary>Révoque la session côté serveur (au mieux) puis l'efface de l'appareil.</summary>
    public async Task LogoutAsync()
    {
        var session = Current;
        if (session is not null)
        {
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "auth/logout");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", session.AccessToken);
                request.Headers.Add(ClientHeader, ClientValue);
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                using var _ = await _authClient.SendAsync(request, timeout.Token);
            }
            catch (Exception ex) when (ex is HttpRequestException or OperationCanceledException)
            {
                // Hors ligne : les jetons expireront d'eux-mêmes, la session locale est effacée quand même.
            }
        }

        await EndAsync();
    }

    private async Task SetAsync(AuthSession session)
    {
        Current = session;
        await _store.SaveAsync(session);
        SessionChanged?.Invoke(this, session);
    }

    private async Task EndAsync()
    {
        Current = null;
        await _store.ClearAsync();
        SessionChanged?.Invoke(this, null);
    }

    private static async Task<SessionResponse> ReadSessionAsync(HttpResponseMessage response, CancellationToken ct) =>
        await response.Content.ReadFromJsonAsync<SessionResponse>(ApiJson.Options, ct)
        ?? throw new ApiException(response.StatusCode, "Réponse de connexion illisible.");
}
