using FleetManager.Domain.Enums;

namespace FleetManager.Api.DTOs.Requests;

public record RegisterRequest(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    UserRole Role,
    Guid? StoreId);
