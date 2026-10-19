using System.Net;
using System.Text.Json;

namespace AutoNexusMobile.Core.Api;

/// <summary>
/// Erreur renvoyée par l'API. <see cref="Message"/> est le texte à montrer : le champ
/// <c>detail</c> des ProblemDetails (message métier), sinon la première erreur de validation,
/// sinon un message générique selon le code HTTP.
/// </summary>
public sealed class ApiException : Exception
{
    public HttpStatusCode StatusCode { get; }

    /// <summary>Véhicule archivé qui porte déjà le NIV saisi (409 à la création), s'il est restaurable.</summary>
    public ArchivedVinConflict? ArchivedVinConflict { get; }

    public ApiException(HttpStatusCode statusCode, string message, ArchivedVinConflict? conflict = null)
        : base(message)
    {
        StatusCode = statusCode;
        ArchivedVinConflict = conflict;
    }

    public static ApiException FromResponse(HttpStatusCode status, string? body)
    {
        string? detail = null;
        ArchivedVinConflict? conflict = null;

        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var document = JsonDocument.Parse(body);
                var root = document.RootElement;
                if (root.ValueKind == JsonValueKind.Object)
                {
                    detail = ReadString(root, "detail") ?? ReadString(root, "error") ?? FirstValidationError(root);
                    conflict = ReadConflict(status, root, detail);
                }
            }
            catch (JsonException)
            {
                // Corps non JSON (page d'erreur d'un relais) : le message générique suffit.
            }
        }

        return new ApiException(status, string.IsNullOrWhiteSpace(detail) ? Fallback(status) : detail, conflict);
    }

    public static string Fallback(HttpStatusCode status) => status switch
    {
        HttpStatusCode.Unauthorized => "Votre session a expiré. Reconnectez-vous.",
        HttpStatusCode.Forbidden => "Cette action n'est pas autorisée pour votre rôle.",
        HttpStatusCode.NotFound => "Élément introuvable. Il a peut-être été supprimé.",
        HttpStatusCode.Conflict => "Cette action entre en conflit avec l'état actuel des données.",
        HttpStatusCode.TooManyRequests => "Trop de tentatives. Réessayez dans une minute.",
        >= HttpStatusCode.InternalServerError => "Le serveur a rencontré une erreur. Réessayez dans un instant.",
        _ => "La demande a été refusée.",
    };

    private static string? ReadString(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()
            : null;

    private static string? FirstValidationError(JsonElement root)
    {
        if (!root.TryGetProperty("errors", out var errors) || errors.ValueKind != JsonValueKind.Object)
            return null;

        foreach (var field in errors.EnumerateObject())
        {
            if (field.Value.ValueKind == JsonValueKind.Array)
            {
                foreach (var message in field.Value.EnumerateArray())
                {
                    if (message.ValueKind == JsonValueKind.String)
                        return message.GetString();
                }
            }
        }

        return null;
    }

    private static ArchivedVinConflict? ReadConflict(HttpStatusCode status, JsonElement root, string? detail)
    {
        if (status != HttpStatusCode.Conflict || ReadString(root, "archivedVehicleId") is not { } id || !Guid.TryParse(id, out var vehicleId))
            return null;

        var year = root.TryGetProperty("year", out var y) && y.TryGetInt32(out var parsed) ? parsed : 0;
        return new ArchivedVinConflict(vehicleId, ReadString(root, "brand") ?? "", ReadString(root, "model") ?? "", year,
            ReadString(root, "storeName"), detail ?? "");
    }
}

public sealed record ArchivedVinConflict(Guid ArchivedVehicleId, string Brand, string Model, int Year, string? StoreName, string Message);
