using FleetManager.Domain.Entities;
using FleetManager.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FleetManager.Infrastructure.Persistence.Repositories;

public class StoreRepository : IStoreRepository
{
    private readonly FleetManagerDbContext _context;

    public StoreRepository(FleetManagerDbContext context)
    {
        _context = context;
    }

    public async Task<Store?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Stores.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Store>> GetAllAsync(CancellationToken cancellationToken = default)
        => await _context.Stores.ToListAsync(cancellationToken);

    public async Task AddAsync(Store store, CancellationToken cancellationToken = default)
        => await _context.Stores.AddAsync(store, cancellationToken);

    public void Update(Store store)
        => _context.Stores.Update(store);

    public void Remove(Store store)
        => _context.Stores.Remove(store);

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.Stores.AnyAsync(s => s.Id == id, cancellationToken);
}
