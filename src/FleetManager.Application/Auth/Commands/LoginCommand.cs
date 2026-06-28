using FleetManager.Application.Auth.Queries;
using FleetManager.Application.Common;
using FleetManager.Application.Interfaces;
using FleetManager.Domain.Entities;
using FleetManager.Domain.Interfaces;
using MediatR;

namespace FleetManager.Application.Auth.Commands;

public record LoginCommand(string Email, string Password) : IRequest<Result<AuthResultDto>>;

public class LoginCommandHandler : IRequestHandler<LoginCommand, Result<AuthResultDto>>
{
    // Pre-computed BCrypt hash used as a timing guard when the email is not found.
    // Calling Verify with this hash (instead of short-circuiting) prevents attackers
    // from distinguishing "unknown email" from "wrong password" via response time.
    private const string TimingGuardHash =
        "$2b$12$LQv3c1yqBWVHxkd0LHAkCOYz6TtxMQJqhN8LfN1iWXxY0K7c21ZmC";

    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IUnitOfWork _unitOfWork;

    public LoginCommandHandler(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IUnitOfWork unitOfWork)
    {
        _userRepository        = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _passwordHasher        = passwordHasher;
        _jwtTokenGenerator     = jwtTokenGenerator;
        _unitOfWork            = unitOfWork;
    }

    public async Task<Result<AuthResultDto>> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);

        // Always call Verify even when user is null — prevents timing-based
        // enumeration of valid email addresses via response-time differences.
        var storedHash = user?.PasswordHash ?? TimingGuardHash;
        var passwordCorrect = _passwordHasher.Verify(request.Password, storedHash);

        if (user is null || !passwordCorrect)
            return Result.Failure<AuthResultDto>(Error.Unauthorized("Email ou mot de passe incorrect."));

        var accessToken  = _jwtTokenGenerator.GenerateToken(user);
        var refreshToken = RefreshToken.Create(user.Id);

        await _refreshTokenRepository.AddAsync(refreshToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success(new AuthResultDto(
            user.Id,
            user.FirstName,
            user.LastName,
            user.Email.Value,
            user.Role,
            user.StoreId,
            accessToken,
            refreshToken.Token));
    }
}
