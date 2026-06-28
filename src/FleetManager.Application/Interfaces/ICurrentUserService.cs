using FleetManager.Domain.Enums;

namespace FleetManager.Application.Interfaces;

public interface ICurrentUserService
{
    Guid?     UserId    { get; }
    Guid?     StoreId   { get; }
    UserRole? Role      { get; }
    bool      IsAdmin   { get; }
    string?   FirstName { get; }
    string?   LastName  { get; }
    string?   Jti       { get; }
}
