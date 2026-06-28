using FleetManager.Domain.Enums;

namespace FleetManager.Api.DTOs.Requests;

public record ChangeInterventionStatusRequest(InterventionStatus NewStatus, string? Comment);
