namespace FleetManager.Api.DTOs.Requests;

/// <summary>Refresh token envoyé dans le corps par l'application mobile.</summary>
public record RefreshTokenRequest(string? RefreshToken);
