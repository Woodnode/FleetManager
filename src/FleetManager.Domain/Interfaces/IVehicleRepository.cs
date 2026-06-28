using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;

namespace FleetManager.Domain.Interfaces;

public interface IVehicleRepository
{
    Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Vehicle?> GetByVinAsync(string vin, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Vehicle> Items, int TotalCount)> GetPagedAsync(Guid? storeId, int skip, int take, string? search = null, string? status = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Vehicle>> GetByStoreIdAsync(Guid storeId, CancellationToken cancellationToken = default);
    Task<Dictionary<VehicleStatus, int>> GetStatusCountsAsync(Guid? storeId, CancellationToken cancellationToken = default);
    Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken = default);
    void Update(Vehicle vehicle);
    void Remove(Vehicle vehicle);
    Task<bool> ExistsByVinAsync(string vin, CancellationToken cancellationToken = default);
}
