using FleetManager.Application.Extensions;
using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;

namespace FleetManager.Application.Interventions.Queries;

public record InterventionDto(
    Guid Id,
    Guid VehicleId,
    string? VehicleBrand,
    string? VehicleModel,
    string? VehicleVin,
    Guid StoreId,
    string? StoreName,
    Guid TechnicianId,
    string? TechnicianFullName,
    InterventionType Type,
    string TypeLabel,
    InterventionStatus Status,
    string StatusLabel,
    DateTime PlannedStartDate,
    DateTime PlannedEndDate,
    DateTime? ActualEndDate,
    string? Comment)
{
    public static InterventionDto FromEntity(Intervention i) => new(
        i.Id,
        i.VehicleId,
        i.Vehicle?.Brand,
        i.Vehicle?.Model,
        i.Vehicle?.Vin.Value,
        i.StoreId,
        i.Store?.Name,
        i.TechnicianId,
        i.Technician?.FullName,
        i.Type,
        i.Type.ToLabel(),
        i.Status,
        i.Status.ToLabel(),
        i.PlannedStartDate,
        i.PlannedEndDate,
        i.ActualEndDate,
        i.Comment);

}
