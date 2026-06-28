using FleetManager.Application.Common;
using FleetManager.Application.Interfaces;
using FleetManager.Application.Vehicles.Queries;
using FleetManager.Domain.Enums;
using FleetManager.Domain.Exceptions;
using FleetManager.Domain.Interfaces;
using MediatR;

namespace FleetManager.Application.Vehicles.Commands;

public record ChangeVehicleStatusCommand(
    Guid VehicleId,
    VehicleStatus NewStatus) : IRequest<Result<VehicleDto>>;

public class ChangeVehicleStatusCommandHandler : IRequestHandler<ChangeVehicleStatusCommand, Result<VehicleDto>>
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IStoreAuthorizationService _authorizationService;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public ChangeVehicleStatusCommandHandler(
        IVehicleRepository vehicleRepository,
        IStoreAuthorizationService authorizationService,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _vehicleRepository    = vehicleRepository;
        _authorizationService = authorizationService;
        _currentUser          = currentUser;
        _unitOfWork           = unitOfWork;
    }

    public async Task<Result<VehicleDto>> Handle(ChangeVehicleStatusCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(request.VehicleId, cancellationToken);
        if (vehicle is null)
            return Result.Failure<VehicleDto>(Error.NotFound($"Vehicle '{request.VehicleId}' not found."));

        if (!_authorizationService.CanAccessStore(_currentUser.Role, _currentUser.StoreId, vehicle.StoreId))
            return Result.Failure<VehicleDto>(Error.Forbidden("Vous ne pouvez pas modifier le statut d'un véhicule d'une autre enseigne."));

        try
        {
            vehicle.ChangeStatus(request.NewStatus);
            _vehicleRepository.Update(vehicle);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(VehicleDto.FromEntity(vehicle));
        }
        catch (DomainException ex)
        {
            return Result.Failure<VehicleDto>(Error.Validation(ex.Message));
        }
    }
}
