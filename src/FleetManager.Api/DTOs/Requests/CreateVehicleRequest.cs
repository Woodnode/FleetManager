namespace FleetManager.Api.DTOs.Requests;

public record CreateVehicleRequest(
    string Vin,
    string Brand,
    string Model,
    int Year,
    int Mileage,
    Guid StoreId);
