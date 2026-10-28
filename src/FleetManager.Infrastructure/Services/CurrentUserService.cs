using FleetManager.Application.Interfaces;
using FleetManager.Domain.Enums;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace FleetManager.Infrastructure.Services;

public class CurrentUserService : ICurrentUserService
{
    public CurrentUserService(IHttpContextAccessor accessor, IOptions<JwtSettings> jwtSettings)
    {
        var user = accessor.HttpContext?.User;
        UserId    = Guid.TryParse(user?.FindFirstValue(ClaimTypes.NameIdentifier), out var id)  ? id  : null;
        StoreId   = Guid.TryParse(user?.FindFirstValue("storeId"),                  out var sid) ? sid : null;
        Role      = Enum.TryParse<UserRole>(user?.FindFirstValue(ClaimTypes.Role),   out var r)  ? r   : null;
        FirstName = user?.FindFirstValue(ClaimTypes.GivenName);
        LastName  = user?.FindFirstValue(ClaimTypes.Surname);
        Jti       = user?.FindFirstValue(JwtRegisteredClaimNames.Jti);

        // Sans claim exp lisible, on retient la durée de vie maximale qu'un jeton émis peut avoir.
        if (Jti is not null)
            TokenExpiresAt = long.TryParse(user?.FindFirstValue(JwtRegisteredClaimNames.Exp), out var exp)
                ? DateTimeOffset.FromUnixTimeSeconds(exp)
                : DateTimeOffset.UtcNow.AddMinutes(jwtSettings.Value.ExpiryMinutes);
    }

    public Guid?     UserId    { get; }
    public Guid?     StoreId   { get; }
    public UserRole? Role      { get; }
    public bool      IsAdmin   => Role == UserRole.Admin;
    public string?   FirstName { get; }
    public string?   LastName  { get; }
    public string?   Jti       { get; }
    public DateTimeOffset? TokenExpiresAt { get; }
}
