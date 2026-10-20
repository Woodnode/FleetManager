using FleetManager.Application.Common;
using FleetManager.Application.Interfaces;
using FleetManager.Domain.Interfaces;
using MediatR;

namespace FleetManager.Application.Auth.Commands;

public record LogoutCommand : IRequest<Result>;

public class LogoutCommandHandler : IRequestHandler<LogoutCommand, Result>
{
    private readonly ICurrentUserService _currentUser;
    private readonly ITokenBlacklist _blacklist;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly IUnitOfWork _unitOfWork;

    public LogoutCommandHandler(
        ICurrentUserService currentUser,
        ITokenBlacklist blacklist,
        IRefreshTokenRepository refreshTokens,
        IUnitOfWork unitOfWork)
    {
        _currentUser   = currentUser;
        _blacklist     = blacklist;
        _refreshTokens = refreshTokens;
        _unitOfWork    = unitOfWork;
    }

    public async Task<Result> Handle(LogoutCommand request, CancellationToken cancellationToken)
    {
        // Le JTI reste en liste noire jusqu'à l'expiration réelle du jeton (et non une durée fixe,
        // plus courte que la durée de vie configurée) : un jeton volé ne survit pas à la déconnexion.
        if (_currentUser.Jti is not null)
            _blacklist.Revoke(_currentUser.Jti, RemainingLifetime(_currentUser.AccessTokenExpiresAt));

        // Revoke all refresh tokens for this user
        if (_currentUser.UserId.HasValue)
        {
            await _refreshTokens.RevokeAllForUserAsync(_currentUser.UserId.Value, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }

    /// <summary>Durée de vie restante du jeton, avec une marge pour la tolérance d'horloge.</summary>
    public static TimeSpan RemainingLifetime(DateTimeOffset? expiresAt)
    {
        // Sans claim exp lisible, on garde le JTI une journée : bien plus que toute durée configurée.
        if (expiresAt is null)
            return TimeSpan.FromDays(1);

        var remaining = expiresAt.Value - DateTimeOffset.UtcNow + TimeSpan.FromMinutes(1);
        return remaining > TimeSpan.Zero ? remaining : TimeSpan.FromMinutes(1);
    }
}
