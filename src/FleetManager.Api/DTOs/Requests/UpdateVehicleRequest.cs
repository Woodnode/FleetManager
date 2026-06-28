namespace FleetManager.Api.DTOs.Requests;

public record UpdateVehicleRequest(
    string Brand,
    string Model,
    int Year,
    int Mileage,
    Guid StoreId);
