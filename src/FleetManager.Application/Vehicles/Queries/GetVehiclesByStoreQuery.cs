using FleetManager.Application.Common;
using FleetManager.Application.Interfaces;
using FleetManager.Domain.Interfaces;
using MediatR;

namespace FleetManager.Application.Vehicles.Queries;

public record GetVehiclesByStoreQuery(Guid StoreId) : IRequest<Result<IReadOnlyList<VehicleDto>>>;

public class GetVehiclesByStoreQueryHandler : IRequestHandler<GetVehiclesByStoreQuery, Result<IReadOnlyList<VehicleDto>>>
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IStoreRepository _storeRepository;
    private readonly IStoreAuthorizationService _authorizationService;
    private readonly ICurrentUserService _currentUser;

    public GetVehiclesByStoreQueryHandler(
        IVehicleRepository vehicleRepository,
        IStoreRepository storeRepository,
        IStoreAuthorizationService authorizationService,
        ICurrentUserService currentUser)
    {
        _vehicleRepository    = vehicleRepository;
        _storeRepository      = storeRepository;
        _authorizationService = authorizationService;
        _currentUser          = currentUser;
    }

    public async Task<Result<IReadOnlyList<VehicleDto>>> Handle(GetVehiclesByStoreQuery request, CancellationToken cancellationToken)
    {
        // Return the same "not found" response as a non-existent store to avoid information disclosure.
        if (!_authorizationService.CanAccessStore(_currentUser.Role, _currentUser.StoreId, request.StoreId))
            return Result.Failure<IReadOnlyList<VehicleDto>>(Error.NotFound($"Store '{request.StoreId}' not found."));

        if (!await _storeRepository.ExistsAsync(request.StoreId, cancellationToken))
            return Result.Failure<IReadOnlyList<VehicleDto>>(Error.NotFound($"Store '{request.StoreId}' not found."));

        var vehicles = await _vehicleRepository.GetByStoreIdAsync(request.StoreId, cancellationToken);
        return Result.Success<IReadOnlyList<VehicleDto>>(vehicles.Select(VehicleDto.FromEntity).ToList().AsReadOnly());
    }
}
