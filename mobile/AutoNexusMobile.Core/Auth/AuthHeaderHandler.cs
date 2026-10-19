using System.Net;
using System.Net.Http.Headers;

namespace AutoNexusMobile.Core.Auth;

/// <summary>
/// Joint le jeton d'accès à chaque appel de l'API. Sur un 401, renouvelle la session une fois
/// et rejoue la requête ; si le renouvellement échoue, le 401 remonte à l'appelant.
/// </summary>
public sealed class AuthHeaderHandler : DelegatingHandler
{
    private readonly SessionManager _session;

    public AuthHeaderHandler(SessionManager session) => _session = session;

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        // Le corps est mis en mémoire avant le premier envoi pour pouvoir rejouer la requête.
        var body = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);
        var contentHeaders = request.Content?.Headers.ToList();

        var token = await _session.GetAccessTokenAsync(cancellationToken);
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await base.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.Unauthorized || token is null)
            return response;

        var refreshed = await _session.RefreshAsync(token, cancellationToken);
        if (refreshed is null)
            return response;

        response.Dispose();
        using var retry = Clone(request, body, contentHeaders);
        retry.Headers.Authorization = new AuthenticationHeaderValue("Bearer", refreshed.AccessToken);
        return await base.SendAsync(retry, cancellationToken);
    }

    private static HttpRequestMessage Clone(
        HttpRequestMessage original, byte[]? body, List<KeyValuePair<string, IEnumerable<string>>>? contentHeaders)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri) { Version = original.Version };
        foreach (var header in original.Headers.Where(h => h.Key != "Authorization"))
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        if (body is not null)
        {
            clone.Content = new ByteArrayContent(body);
            foreach (var header in contentHeaders ?? [])
                clone.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }

        return clone;
    }
}
