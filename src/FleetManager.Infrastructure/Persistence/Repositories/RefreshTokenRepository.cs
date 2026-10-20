using FleetManager.Domain.Entities;
using FleetManager.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace FleetManager.Infrastructure.Persistence.Repositories;

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly FleetManagerDbContext _context;

    public RefreshTokenRepository(FleetManagerDbContext context)
    {
        _context = context;
    }

    public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        var hash = RefreshToken.Hash(token);
        return _context.RefreshTokens.FirstOrDefaultAsync(r => r.TokenHash == hash, cancellationToken);
    }

    public async Task AddAsync(RefreshToken refreshToken, CancellationToken cancellationToken = default) =>
        await _context.RefreshTokens.AddAsync(refreshToken, cancellationToken);

    public async Task<bool> TryConsumeAsync(Guid tokenId, CancellationToken cancellationToken = default)
    {
        var updated = await _context.RefreshTokens
            .Where(r => r.Id == tokenId && !r.IsRevoked)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsRevoked, true), cancellationToken);
        return updated == 1;
    }

    public Task RevokeFamilyAsync(Guid familyId, CancellationToken cancellationToken = default) =>
        _context.RefreshTokens
            .Where(r => r.FamilyId == familyId && !r.IsRevoked)
            .ExecuteUpdateAsync(s => s.SetProperty(r => r.IsRevoked, true), cancellationToken);

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var tokens = await _context.RefreshTokens
            .Where(r => r.UserId == userId && !r.IsRevoked)
            .ToListAsync(cancellationToken);

        foreach (var token in tokens)
            token.Revoke();
    }
}
