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
        // Le JTI reste en liste noire jusqu'à l'expiration réelle du jeton : une durée fixe plus courte
        // rendait le jeton de nouveau utilisable après la déconnexion.
        if (_currentUser.Jti is not null && _currentUser.TokenExpiresAt is { } expiresAt)
        {
            var remaining = expiresAt - DateTimeOffset.UtcNow;
            if (remaining > TimeSpan.Zero)
                _blacklist.Revoke(_currentUser.Jti, remaining);
        }

        // Revoke all refresh tokens for this user
        if (_currentUser.UserId.HasValue)
        {
            await _refreshTokens.RevokeAllForUserAsync(_currentUser.UserId.Value, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
