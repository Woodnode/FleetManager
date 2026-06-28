using FleetManager.Application.Auth.Queries;
using FleetManager.Application.Common;
using FleetManager.Application.Interfaces;
using FleetManager.Domain.Entities;
using FleetManager.Domain.Interfaces;
using MediatR;

namespace FleetManager.Application.Auth.Commands;

public record RefreshTokenCommand(string RefreshToken) : IRequest<Result<AuthResultDto>>;

public class RefreshTokenCommandHandler : IRequestHandler<RefreshTokenCommand, Result<AuthResultDto>>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IUnitOfWork _unitOfWork;

    public RefreshTokenCommandHandler(
        IRefreshTokenRepository refreshTokenRepository,
        IUserRepository userRepository,
        IJwtTokenGenerator jwtTokenGenerator,
        IUnitOfWork unitOfWork)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _userRepository         = userRepository;
        _jwtTokenGenerator      = jwtTokenGenerator;
        _unitOfWork             = unitOfWork;
    }

    public async Task<Result<AuthResultDto>> Handle(RefreshTokenCommand request, CancellationToken cancellationToken)
    {
        var existing = await _refreshTokenRepository.GetByTokenAsync(request.RefreshToken, cancellationToken);

        if (existing is null || !existing.IsActive)
            return Result.Failure<AuthResultDto>(Error.Unauthorized("Refresh token invalide ou expiré."));

        var user = await _userRepository.GetByIdAsync(existing.UserId, cancellationToken);
        if (user is null)
            return Result.Failure<AuthResultDto>(Error.NotFound("Utilisateur introuvable."));

        // Rotate: revoke old token, issue new pair
        existing.Revoke();

        var newAccessToken  = _jwtTokenGenerator.GenerateToken(user);
        var newRefreshToken = RefreshToken.Create(user.Id);

        await _refreshTokenRepository.AddAsync(newRefreshToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new AuthResultDto(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email.Value,
            user.Role,
            user.StoreId,
            newAccessToken,
            newRefreshToken.Token));
    }
}
