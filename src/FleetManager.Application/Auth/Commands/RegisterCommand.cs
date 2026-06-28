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
    Guid? StoreId) : IRequest<Result<AuthResultDto>>;

public class RegisterCommandHandler : IRequestHandler<RegisterCommand, Result<AuthResultDto>>
{
    private readonly IUserRepository _userRepository;
    private readonly IStoreRepository _storeRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IUnitOfWork _unitOfWork;

    public RegisterCommandHandler(
        IUserRepository userRepository,
        IStoreRepository storeRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IUnitOfWork unitOfWork)
    {
        _userRepository    = userRepository;
        _storeRepository   = storeRepository;
        _passwordHasher    = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _unitOfWork        = unitOfWork;
    }

    public async Task<Result<AuthResultDto>> Handle(RegisterCommand request, CancellationToken cancellationToken)
    {
        if (await _userRepository.ExistsByEmailAsync(request.Email, cancellationToken))
            return Result.Failure<AuthResultDto>(Error.Conflict($"L'email '{request.Email}' est déjà utilisé."));

        if (request.StoreId.HasValue && !await _storeRepository.ExistsAsync(request.StoreId.Value, cancellationToken))
            return Result.Failure<AuthResultDto>(Error.NotFound($"Le magasin '{request.StoreId}' n'existe pas."));

        try
        {
            var passwordHash = _passwordHasher.Hash(request.Password);
            var user = User.Create(request.FirstName, request.LastName, request.Email, passwordHash, request.Role, request.StoreId);

            await _userRepository.AddAsync(user, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var accessToken = _jwtTokenGenerator.GenerateToken(user);

            return Result.Success(new AuthResultDto(
                user.Id,
                user.FirstName,
                user.LastName,
                user.Email.Value,
                user.Role,
                user.StoreId,
                accessToken));
        }
        catch (DomainException ex)
        {
            return Result.Failure<AuthResultDto>(Error.Validation(ex.Message));
        }
    }
}
