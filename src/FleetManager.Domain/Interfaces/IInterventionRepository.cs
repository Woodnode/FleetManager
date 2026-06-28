using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;

namespace FleetManager.Domain.Interfaces;

public record InterventionSummaryCounts(
    Dictionary<InterventionStatus, int> StatusCounts,
    Dictionary<InterventionType,   int> TypeCounts
);

public interface IInterventionRepository
{
    Task<Intervention?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<Intervention> Items, int TotalCount)> GetPagedAsync(Guid? storeId, int skip, int take, string? status = null, string? type = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Intervention>> GetByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Intervention>> GetByStoreIdAsync(Guid storeId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Intervention>> GetRecentAsync(Guid? storeId, int count, CancellationToken cancellationToken = default);
    Task<InterventionSummaryCounts> GetSummaryCountsAsync(Guid? storeId, CancellationToken cancellationToken = default);
    Task<bool> ExistsByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken = default);
    Task AddAsync(Intervention intervention, CancellationToken cancellationToken = default);
    void Update(Intervention intervention);
    void Remove(Intervention intervention);
}
