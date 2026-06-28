using FleetManager.Domain.Entities;

namespace FleetManager.Application.Stores.Queries;

public record StoreDto(Guid Id, string Name, string Address, string PostalCode, string City)
{
    public static StoreDto FromEntity(Store store) => new(
        store.Id, store.Name, store.Address, store.PostalCode, store.City);
}
