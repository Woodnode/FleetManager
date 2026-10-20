using FleetManager.Application.Auth.Queries;
using FleetManager.Application.Common;
using FleetManager.Application.Interfaces;
using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;
using FleetManager.Domain.Exceptions;
using FleetManager.Domain.Interfaces;
using MediatR;

namespace FleetManager.Application.Auth.Commands;

public record RegisterCommand(
    string FirstName,
    string LastName,
    string Email,
    string Password,
    UserRole Role,
    Guid? StoreId) : IRequest<Result<RegisteredUserDto>>;

/// <summary>
/// Compte créé. Aucun jeton : l'admin qui crée un compte ne doit pas pouvoir agir sous
/// l'identité du nouvel utilisateur.
/// </summary>
public record RegisteredUserDto(Guid UserId, string FirstName, string LastName, string Email, UserRole Role, Guid? StoreId);

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result<RegisteredUserDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IStoreRepository _storeRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterCommandHandler(
        IUserRepository userRepository,
        IStoreRepository storeRepository,
        IPasswordHasher passwordHasher,
        IUnitOfWork unitOfWork)
    {
        _userRepository    = userRepository;
        _storeRepository   = storeRepository;
        _passwordHasher    = passwordHasher;
        _unitOfWork        = unitOfWork;
    }

    public async Task<Result<RegisteredUserDto>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        if (await _userRepository.ExistsByEmailAsync(request.Email, cancellationToken))
            return Result.Failure<RegisteredUserDto>(Error.Conflict($"L'email '{request.Email}' est déjà utilisé."));

        if (request.StoreId.HasValue && !await _storeRepository.ExistsAsync(request.StoreId.Value, cancellationToken))
            return Result.Failure<RegisteredUserDto>(Error.NotFound($"Le magasin '{request.StoreId}' n'existe pas."));

        try
        {
            var passwordHash = _passwordHasher.Hash(request.Password);
            var user = User.Create(request.FirstName, request.LastName, request.Email, passwordHash, request.Role, request.StoreId);

            await _userRepository.AddAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(new RegisteredUserDto(
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email.Value,
                user.Role,
                user.StoreId));
        }
        catch (DomainException ex)
        {
            return Result.Failure<RegisteredUserDto>(Error.Validation(ex.Message));
        }
    }
}
