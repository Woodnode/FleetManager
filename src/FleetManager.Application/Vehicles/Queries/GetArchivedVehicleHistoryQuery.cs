using FleetManager.Application.Common;
using FleetManager.Application.Interfaces;
using FleetManager.Application.Interventions.Queries;
using FleetManager.Domain.Interfaces;
using MediatR;

namespace FleetManager.Application.Vehicles.Queries;

public record GetArchivedVehicleHistoryQuery(Guid VehicleId) : IRequest<Result<ArchivedVehicleHistoryDto>>;

public class GetArchivedVehicleHistoryQueryHandler
    : IRequestHandler<GetArchivedVehicleHistoryQuery, Result<ArchivedVehicleHistoryDto>>
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IInterventionRepository _interventionRepository;
    private readonly IStoreAuthorizationService _authorizationService;
    private readonly ICurrentUserService _currentUser;

    public GetArchivedVehicleHistoryQueryHandler(
        IVehicleRepository vehicleRepository,
        IInterventionRepository interventionRepository,
        IStoreAuthorizationService authorizationService,
        ICurrentUserService currentUser)
    {
        _vehicleRepository      = vehicleRepository;
        _interventionRepository = interventionRepository;
        _authorizationService   = authorizationService;
        _currentUser            = currentUser;
    }

    public async Task<Result<ArchivedVehicleHistoryDto>> Handle(GetArchivedVehicleHistoryQuery request, CancellationToken cancellationToken)
    {
        var vehicle = await _vehicleRepository.GetArchivedByIdAsync(request.VehicleId, cancellationToken);
        if (vehicle is null)
            return Result.Failure<ArchivedVehicleHistoryDto>(Error.NotFound($"Archived vehicle '{request.VehicleId}' not found."));

        // 404 plutôt que 403 : ne pas révéler l'existence d'un véhicule d'une autre enseigne.
        if (!_authorizationService.CanAccessStore(_currentUser.Role, _currentUser.StoreId, vehicle.StoreId))
            return Result.Failure<ArchivedVehicleHistoryDto>(Error.NotFound($"Archived vehicle '{request.VehicleId}' not found."));

        var interventions = await _interventionRepository.GetArchivedVehicleHistoryAsync(vehicle.Id, cancellationToken);

        return Result.Success(new ArchivedVehicleHistoryDto(
            ArchivedVehicleDto.FromEntity(vehicle, interventions.Count),
            interventions.Select(InterventionDto.FromEntity).ToList().AsReadOnly()));
    }
}
