namespace FleetManager.Api.Infrastructure;

/// <summary>
/// Repère l'application mobile. Un client natif ne garde pas de cookies httpOnly (et les cookies
/// <c>Secure</c> ne passent pas sur l'HTTP local de l'émulateur) : il reçoit ses jetons dans le
/// corps des réponses et renvoie son refresh token dans le corps. Le navigateur garde les cookies.
/// </summary>
public static class NativeClient
{
    public const string HeaderName  = "X-Client";
    public const string MobileValue = "mobile";

    public static bool IsRequestedBy(HttpRequest request) =>
        string.Equals(request.Headers[HeaderName].ToString(), MobileValue, StringComparison.OrdinalIgnoreCase);
}
