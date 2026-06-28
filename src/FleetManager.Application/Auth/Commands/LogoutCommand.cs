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
        // Blacklist the current access token's JTI so it cannot be reused
        if (_currentUser.Jti is not null)
            _blacklist.Revoke(_currentUser.Jti, TimeSpan.FromMinutes(15));

        // Revoke all refresh tokens for this user
        if (_currentUser.UserId.HasValue)
        {
            await _refreshTokens.RevokeAllForUserAsync(_currentUser.UserId.Value, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return Result.Success();
    }
}
