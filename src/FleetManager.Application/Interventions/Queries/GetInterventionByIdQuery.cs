using FleetManager.Application.Common;
using FleetManager.Application.Interfaces;
using FleetManager.Domain.Interfaces;
using MediatR;

namespace FleetManager.Application.Interventions.Queries;

public record GetInterventionByIdQuery(Guid InterventionId) : IRequest<Result<InterventionDto>>;

public class GetInterventionByIdQueryHandler : IRequestHandler<GetInterventionByIdQuery, Result<InterventionDto>>
{
    private readonly IInterventionRepository _interventionRepository;
    private readonly IStoreAuthorizationService _authorizationService;
    private readonly ICurrentUserService _currentUser;

    public GetInterventionByIdQueryHandler(
        IInterventionRepository interventionRepository,
        IStoreAuthorizationService authorizationService,
        ICurrentUserService currentUser)
    {
        _interventionRepository = interventionRepository;
        _authorizationService   = authorizationService;
        _currentUser            = currentUser;
    }

    public async Task<Result<InterventionDto>> Handle(GetInterventionByIdQuery request, CancellationToken cancellationToken)
    {
        var intervention = await _interventionRepository.GetByIdAsync(request.InterventionId, cancellationToken);

        if (intervention is null)
            return Result.Failure<InterventionDto>(Error.NotFound($"Intervention '{request.InterventionId}' not found."));

        // Return 404 instead of 403 to avoid confirming the resource exists to unauthorized callers.
        if (!_authorizationService.CanAccessStore(_currentUser.Role, _currentUser.StoreId, intervention.StoreId))
            return Result.Failure<InterventionDto>(Error.NotFound($"Intervention '{request.InterventionId}' not found."));

        return Result.Success(InterventionDto.FromEntity(intervention));
    }
}
