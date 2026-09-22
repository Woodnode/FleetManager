namespace FleetManager.Application.Common;

// Details : données structurées optionnelles exposées au client (extensions ProblemDetails),
// par exemple l'identifiant du véhicule archivé qui bloque une création.
public sealed record Error(string Code, string Message, IReadOnlyDictionary<string, object?>? Details = null)
{
    public static readonly Error None = new(string.Empty, string.Empty);

    public static Error NotFound(string msg)   => new("NOT_FOUND",  msg);
    public static Error Forbidden(string msg)  => new("FORBIDDEN",  msg);
    public static Error Conflict(string msg)   => new("CONFLICT",   msg);
    public static Error Conflict(string msg, IReadOnlyDictionary<string, object?> details) => new("CONFLICT", msg, details);
    public static Error Validation(string msg) => new("VALIDATION", msg);
    public static Error Unauthorized(string msg) => new("UNAUTHORIZED", msg);
}
