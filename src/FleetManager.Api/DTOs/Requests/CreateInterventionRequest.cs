using FleetManager.Domain.Enums;

namespace FleetManager.Api.DTOs.Requests;

public record CreateInterventionRequest(
    Guid VehicleId,
    Guid StoreId,
    Guid TechnicianId,
    InterventionType Type,
    DateTime PlannedStartDate,
    DateTime PlannedEndDate,
    string? Comment);
