using FleetManager.Application.Extensions;
using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;

namespace FleetManager.Application.Vehicles.Queries;

public record VehicleDto(
    Guid Id,
    string Vin,
    string Brand,
    string Model,
    int Year,
    int Mileage,
    VehicleStatus Status,
    string StatusLabel,
    Guid StoreId,
    string? StoreName)
{
    public static VehicleDto FromEntity(Vehicle vehicle) => new(
        vehicle.Id,
        vehicle.Vin.Value,
        vehicle.Brand,
        vehicle.Model,
        vehicle.Year,
        vehicle.Mileage,
        vehicle.Status,
        vehicle.Status.ToLabel(),
        vehicle.StoreId,
        vehicle.Store?.Name);
}
