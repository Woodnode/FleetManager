using FleetManager.Application.Common;
using FleetManager.Application.Interfaces;
using FleetManager.Domain.Enums;
using FleetManager.Domain.Interfaces;
using MediatR;

namespace FleetManager.Application.Users.Queries;

public record GetTechniciansByStoreQuery(Guid StoreId) : IRequest<Result<IReadOnlyList<TechnicianDto>>>;

public class GetTechniciansByStoreQueryHandler
    : IRequestHandler<GetTechniciansByStoreQuery, Result<IReadOnlyList<TechnicianDto>>>
{
    private readonly IUserRepository _userRepository;
    private readonly IStoreAuthorizationService _authorizationService;
    private readonly ICurrentUserService _currentUser;

    public GetTechniciansByStoreQueryHandler(
        IUserRepository userRepository,
        IStoreAuthorizationService authorizationService,
        ICurrentUserService currentUser)
    {
        _userRepository       = userRepository;
        _authorizationService = authorizationService;
        _currentUser          = currentUser;
    }

    public async Task<Result<IReadOnlyList<TechnicianDto>>> Handle(
        GetTechniciansByStoreQuery request,
        CancellationToken cancellationToken)
    {
        if (!_authorizationService.CanAccessStore(_currentUser.Role, _currentUser.StoreId, request.StoreId))
            return Result.Failure<IReadOnlyList<TechnicianDto>>(Error.NotFound("Enseigne introuvable."));

        var users = await _userRepository.GetByStoreIdAsync(request.StoreId, cancellationToken);

        var technicians = users
            .Where(u => u.Role == UserRole.Technician)
            .Select(TechnicianDto.FromEntity)
            .ToList()
            .AsReadOnly();

        return Result.Success<IReadOnlyList<TechnicianDto>>(technicians);
    }
}
