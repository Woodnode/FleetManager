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
        if (existing is null)
            return Invalid();

        // Un jeton déjà consommé qui revient : soit il a été volé, soit c'est le voleur qui a
        // servi le premier. Dans les deux cas, toute la famille (la session) est révoquée.
        if (existing.IsRevoked)
        {
            await _refreshTokenRepository.RevokeFamilyAsync(existing.FamilyId, cancellationToken);
            return Invalid();
        }

        if (existing.IsExpired)
            return Invalid();

        var user = await _userRepository.GetByIdAsync(existing.UserId, cancellationToken);
        if (user is null || !user.IsActive)
            return Invalid();

        // Consommation atomique : de deux requêtes simultanées avec le même jeton, une seule passe.
        if (!await _refreshTokenRepository.TryConsumeAsync(existing.Id, cancellationToken))
            return Invalid();

        var newAccessToken = _jwtTokenGenerator.GenerateToken(user);
        var (newRefreshToken, newRefreshTokenValue) = RefreshToken.Create(user.Id, existing.FamilyId);

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
            newRefreshTokenValue));
    }

    private static Result<AuthResultDto> Invalid() =>
        Result.Failure<AuthResultDto>(Error.Unauthorized("Refresh token invalide ou expiré."));
}
