using FleetManager.Application.Common;
using FleetManager.Application.Interfaces;
using FleetManager.Domain.Interfaces;
using MediatR;

namespace FleetManager.Application.Users.Commands;

/// <summary>Active ou désactive un compte. Réservé aux administrateurs.</summary>
public record SetUserActiveCommand(Guid UserId, bool IsActive) : IRequest<Result>;

public class SetUserActiveCommandHandler : IRequestHandler<SetUserActiveCommand, Result>
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokens;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDemoAccountPolicy _demoAccounts;

    public SetUserActiveCommandHandler(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokens,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        IDemoAccountPolicy demoAccounts)
    {
        _userRepository = userRepository;
        _refreshTokens  = refreshTokens;
        _currentUser    = currentUser;
        _unitOfWork     = unitOfWork;
        _demoAccounts   = demoAccounts;
    }

    public async Task<Result> Handle(SetUserActiveCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return Result.Failure(Error.Forbidden("Seuls les administrateurs peuvent activer ou désactiver un compte."));

        // Un admin qui se désactiverait lui-même pourrait laisser l'application sans administrateur.
        if (request.UserId == _currentUser.UserId && !request.IsActive)
            return Result.Failure(Error.Validation("Vous ne pouvez pas désactiver votre propre compte."));

        var user = await _userRepository.GetByIdAsync(request.UserId, cancellationToken);
        if (user is null)
            return Result.Failure(Error.NotFound($"Utilisateur '{request.UserId}' introuvable."));

        // Comptes de démonstration publics : les désactiver fermerait la démo à tous les visiteurs.
        if (!request.IsActive && _demoAccounts.IsProtected(user.Email.Value))
            return Result.Failure(Error.Validation("Les comptes de démonstration ne peuvent pas être désactivés."));

        if (request.IsActive)
        {
            user.Activate();
        }
        else
        {
            user.Deactivate();
            // Les sessions ouvertes s'arrêtent au prochain rafraîchissement (15 minutes au plus).
            await _refreshTokens.RevokeAllForUserAsync(user.Id, cancellationToken);
        }

        _userRepository.Update(user);
        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success();
    }
}
