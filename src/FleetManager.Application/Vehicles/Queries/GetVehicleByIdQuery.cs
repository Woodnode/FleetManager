using FleetManager.Application.Common;
using FleetManager.Application.Interfaces;
using FleetManager.Domain.Interfaces;
using MediatR;

namespace FleetManager.Application.Vehicles.Queries;

public record GetVehicleByIdQuery(Guid VehicleId) : IRequest<Result<VehicleDto>>;

public class GetVehicleByIdQueryHandler : IRequestHandler<GetVehicleByIdQuery, Result<VehicleDto>>
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IStoreAuthorizationService _authorizationService;
    private readonly ICurrentUserService _currentUser;

    public GetVehicleByIdQueryHandler(
        IVehicleRepository vehicleRepository,
        IStoreAuthorizationService authorizationService,
        ICurrentUserService currentUser)
    {
        _vehicleRepository    = vehicleRepository;
        _authorizationService = authorizationService;
        _currentUser          = currentUser;
    }

    public async Task<Result<VehicleDto>> Handle(GetVehicleByIdQuery request, CancellationToken cancellationToken)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(request.VehicleId, cancellationToken);

        if (vehicle is null)
            return Result.Failure<VehicleDto>(Error.NotFound($"Vehicle '{request.VehicleId}' not found."));

        // Return 404 instead of 403 to avoid confirming the resource exists to unauthorized callers.
        if (!_authorizationService.CanAccessStore(_currentUser.Role, _currentUser.StoreId, vehicle.StoreId))
            return Result.Failure<VehicleDto>(Error.NotFound($"Vehicle '{request.VehicleId}' not found."));

        return Result.Success(VehicleDto.FromEntity(vehicle));
    }
}
