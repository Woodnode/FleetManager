namespace FleetManager.Api.DTOs.Requests;

/// <summary>Nouvel état d'un compte : actif ou désactivé.</summary>
public record SetUserActiveRequest(bool IsActive);
