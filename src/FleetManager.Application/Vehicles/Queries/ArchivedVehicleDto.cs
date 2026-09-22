using FleetManager.Application.Interventions.Queries;
using FleetManager.Domain.Entities;

namespace FleetManager.Application.Vehicles.Queries;

/// <summary>Véhicule supprimé logiquement, consultable dans les archives.</summary>
public record ArchivedVehicleDto(
    Guid Id,
    string Vin,
    string Brand,
    string Model,
    int Year,
    int Mileage,
    Guid StoreId,
    string? StoreName,
    DateTime? DeletedAt,
    int InterventionCount)
{
    public static ArchivedVehicleDto FromEntity(Vehicle vehicle, int interventionCount) => new(
        vehicle.Id,
        vehicle.Vin.Value,
        vehicle.Brand,
        vehicle.Model,
        vehicle.Year,
        vehicle.Mileage,
        vehicle.StoreId,
        vehicle.Store?.Name,
        vehicle.DeletedAt,
        interventionCount);
}

/// <summary>Véhicule archivé et son historique complet d'interventions.</summary>
public record ArchivedVehicleHistoryDto(
    ArchivedVehicleDto Vehicle,
    IReadOnlyList<InterventionDto> Interventions);
