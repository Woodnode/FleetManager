using FleetManager.Domain.Enums;

namespace FleetManager.Api.DTOs.Requests;

/// <param name="Mileage">Kilométrage relevé à la clôture (Completed uniquement), facultatif.</param>
public record ChangeInterventionStatusRequest(InterventionStatus NewStatus, string? Comment, int? Mileage = null);
