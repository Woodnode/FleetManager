using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;
using FleetManager.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FleetManager.Infrastructure.Persistence.Repositories;

public class VehicleRepository : IVehicleRepository
{
    private readonly FleetManagerDbContext _context;

    public VehicleRepository(FleetManagerDbContext context)
    {
        _context = context;
    }

    public async Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Vehicles
            .Include(v => v.Store)
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

    public async Task<Vehicle?> GetByVinAsync(string vin, CancellationToken cancellationToken = default)
        => await _context.Vehicles
            .Include(v => v.Store)
            .FirstOrDefaultAsync(v => v.Vin.Value == vin.ToUpperInvariant(), cancellationToken);

    public async Task<(IReadOnlyList<Vehicle> Items, int TotalCount)> GetPagedAsync(
        Guid? storeId, int skip, int take, string? search = null, string? status = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Vehicles.Include(v => v.Store).AsQueryable();
        if (storeId.HasValue)
            query = query.Where(v => v.StoreId == storeId.Value);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var q = search.ToLowerInvariant();
            query = query.Where(v =>
                v.Vin.Value.ToLower().Contains(q) ||
                v.Brand.ToLower().Contains(q) ||
                v.Model.ToLower().Contains(q));
        }
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<VehicleStatus>(status, out var parsedStatus))
            query = query.Where(v => v.Status == parsedStatus);
        var total = await query.CountAsync(cancellationToken);
        var items = await query.OrderBy(v => v.Brand).ThenBy(v => v.Id).Skip(skip).Take(take).ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<IReadOnlyList<Vehicle>> GetByStoreIdAsync(Guid storeId, CancellationToken cancellationToken = default)
        => await _context.Vehicles
            .Include(v => v.Store)
            .Where(v => v.StoreId == storeId)
            .ToListAsync(cancellationToken);

    public async Task<Dictionary<VehicleStatus, int>> GetStatusCountsAsync(Guid? storeId, CancellationToken cancellationToken = default)
    {
        var query = _context.Vehicles.AsQueryable();
        if (storeId.HasValue)
            query = query.Where(v => v.StoreId == storeId.Value);

        return await query
            .GroupBy(v => v.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Status, x => x.Count, cancellationToken);
    }

    public async Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken = default)
        => await _context.Vehicles.AddAsync(vehicle, cancellationToken);

    public void Update(Vehicle vehicle)
        => _context.Vehicles.Update(vehicle);

    public void Remove(Vehicle vehicle)
        => _context.Vehicles.Remove(vehicle);

    public async Task<bool> ExistsByVinAsync(string vin, CancellationToken cancellationToken = default)
        => await _context.Vehicles.AnyAsync(v => v.Vin.Value == vin.ToUpperInvariant(), cancellationToken);
}
