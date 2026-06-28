namespace FleetManager.Api.DTOs.Requests;

public record UpdateStoreRequest(
    string Name,
    string Address,
    string PostalCode,
    string City);
