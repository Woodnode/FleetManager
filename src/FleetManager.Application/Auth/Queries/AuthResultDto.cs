using FleetManager.Domain.Enums;

namespace FleetManager.Application.Auth.Queries;

public record AuthResultDto(
    Guid UserId,
    string FirstName,
    string LastName,
    string Email,
    UserRole Role,
    Guid? StoreId,
    string AccessToken,
    string? RefreshToken = null);
