using FleetManager.Domain.Entities;

namespace FleetManager.Domain.Interfaces;

public interface IStoreRepository
{
    Task<Store?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Store>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(Store store, CancellationToken cancellationToken = default);
    void Update(Store store);
    void Remove(Store store);
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default);
}
