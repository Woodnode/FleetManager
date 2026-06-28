using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;
using FleetManager.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FleetManager.Infrastructure.Persistence.Repositories;

public class InterventionRepository : IInterventionRepository
{
    private readonly FleetManagerDbContext _context;

    public InterventionRepository(FleetManagerDbContext context)
    {
        _context = context;
    }

    public async Task<Intervention?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Interventions
            .Include(i => i.Vehicle)
            .Include(i => i.Store)
            .Include(i => i.Technician)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public async Task<(IReadOnlyList<Intervention> Items, int TotalCount)> GetPagedAsync(
        Guid? storeId, int skip, int take, string? status = null, string? type = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Interventions
            .Include(i => i.Vehicle)
            .Include(i => i.Store)
            .Include(i => i.Technician)
            .AsQueryable();
        if (storeId.HasValue)
            query = query.Where(i => i.StoreId == storeId.Value);
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<InterventionStatus>(status, out var parsedStatus))
            query = query.Where(i => i.Status == parsedStatus);
        if (!string.IsNullOrWhiteSpace(type) && Enum.TryParse<InterventionType>(type, out var parsedType))
            query = query.Where(i => i.Type == parsedType);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderByDescending(i => i.PlannedStartDate).ThenBy(i => i.Id).Skip(skip).Take(take).ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<IReadOnlyList<Intervention>> GetByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken = default)
        => await _context.Interventions
            .Include(i => i.Vehicle)
            .Include(i => i.Store)
            .Include(i => i.Technician)
            .Where(i => i.VehicleId == vehicleId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Intervention>> GetByStoreIdAsync(Guid storeId, CancellationToken cancellationToken = default)
        => await _context.Interventions
            .Include(i => i.Vehicle)
            .Include(i => i.Technician)
            .Where(i => i.StoreId == storeId)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Intervention>> GetRecentAsync(Guid? storeId, int count, CancellationToken cancellationToken = default)
    {
        var query = _context.Interventions
            .Include(i => i.Vehicle)
            .Include(i => i.Store)
            .Include(i => i.Technician)
            .AsQueryable();
        if (storeId.HasValue)
            query = query.Where(i => i.StoreId == storeId.Value);
        return await query
            .OrderByDescending(i => i.PlannedStartDate)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<InterventionSummaryCounts> GetSummaryCountsAsync(Guid? storeId, CancellationToken cancellationToken = default)
    {
        var query = _context.Interventions.AsQueryable();
        if (storeId.HasValue)
            query = query.Where(i => i.StoreId == storeId.Value);

        var statusCounts = await query
            .GroupBy(i => i.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);

        var typeCounts = await query
            .GroupBy(i => i.Type)
            .Select(g => new { Type = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Type, x => x.Count, cancellationToken);

        return new InterventionSummaryCounts(statusCounts, typeCounts);
    }

    public async Task<bool> ExistsByVehicleIdAsync(Guid vehicleId, CancellationToken cancellationToken = default)
        => await _context.Interventions.AnyAsync(i => i.VehicleId == vehicleId, cancellationToken);

    public async Task AddAsync(Intervention intervention, CancellationToken cancellationToken = default)
        => await _context.Interventions.AddAsync(intervention, cancellationToken);

    public void Update(Intervention intervention)
        => _context.Interventions.Update(intervention);

    public void Remove(Intervention intervention)
        => _context.Interventions.Remove(intervention);
}
