using FleetManager.Domain.Interfaces;

namespace FleetManager.Infrastructure.Persistence;

public class UnitOfWork : IUnitOfWork
{
    private readonly FleetManagerDbContext _context;

    public UnitOfWork(FleetManagerDbContext context)
    {
        _context = context;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _context.SaveChangesAsync(cancellationToken);
}
