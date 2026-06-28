using FleetManager.Domain.Entities;

namespace FleetManager.Application.Users.Queries;

public record TechnicianDto(Guid Id, string FullName, string Email, Guid? StoreId)
{
    public static TechnicianDto FromEntity(User user) => new(
        user.Id,
        user.FullName,
        user.Email.Value,
        user.StoreId);
}
