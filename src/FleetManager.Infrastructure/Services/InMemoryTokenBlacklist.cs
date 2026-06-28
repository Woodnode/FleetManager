using FleetManager.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace FleetManager.Infrastructure.Services;

public class InMemoryTokenBlacklist : ITokenBlacklist
{
    private readonly IMemoryCache _cache;

    public InMemoryTokenBlacklist(IMemoryCache cache)
    {
        _cache = cache;
    }

    public void Revoke(string jti, TimeSpan expiresIn) =>
        _cache.Set($"jti:{jti}", true, expiresIn);

    public bool IsRevoked(string jti) =>
        _cache.TryGetValue($"jti:{jti}", out _);
}
