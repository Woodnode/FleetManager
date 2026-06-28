using FleetManager.Application.Common;
using FleetManager.Application.Interfaces;
using FleetManager.Domain.Interfaces;
using MediatR;

namespace FleetManager.Application.Interventions.Queries;

public record GetInterventionsByVehicleQuery(Guid VehicleId) : IRequest<Result<IReadOnlyList<InterventionDto>>>;

public class GetInterventionsByVehicleQueryHandler : IRequestHandler<GetInterventionsByVehicleQuery, Result<IReadOnlyList<InterventionDto>>>
{
    private readonly IInterventionRepository _interventionRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IStoreAuthorizationService _authorizationService;
    private readonly ICurrentUserService _currentUser;

    public GetInterventionsByVehicleQueryHandler(
        IInterventionRepository interventionRepository,
        IVehicleRepository vehicleRepository,
        IStoreAuthorizationService authorizationService,
        ICurrentUserService currentUser)
    {
        _interventionRepository = interventionRepository;
        _vehicleRepository      = vehicleRepository;
        _authorizationService   = authorizationService;
        _currentUser            = currentUser;
    }

    public async Task<Result<IReadOnlyList<InterventionDto>>> Handle(GetInterventionsByVehicleQuery request, CancellationToken cancellationToken)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(request.VehicleId, cancellationToken);
        if (vehicle is null)
            return Result.Failure<IReadOnlyList<InterventionDto>>(Error.NotFound($"Vehicle '{request.VehicleId}' not found."));

        // Return 404 instead of 403 — do not reveal the vehicle exists in another store.
        if (!_authorizationService.CanAccessStore(_currentUser.Role, _currentUser.StoreId, vehicle.StoreId))
            return Result.Failure<IReadOnlyList<InterventionDto>>(Error.NotFound($"Vehicle '{request.VehicleId}' not found."));

        var interventions = await _interventionRepository.GetByVehicleIdAsync(request.VehicleId, cancellationToken);
        return Result.Success<IReadOnlyList<InterventionDto>>(interventions.Select(InterventionDto.FromEntity).ToList().AsReadOnly());
    }
}
