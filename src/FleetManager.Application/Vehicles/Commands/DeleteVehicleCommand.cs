using FleetManager.Application.Common;
using FleetManager.Application.Interfaces;
using FleetManager.Domain.Interfaces;
using MediatR;

namespace FleetManager.Application.Vehicles.Commands;

public record DeleteVehicleCommand(Guid VehicleId) : IRequest<Result>;

public class DeleteVehicleCommandHandler : IRequestHandler<DeleteVehicleCommand, Result>
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IStoreAuthorizationService _authorizationService;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteVehicleCommandHandler(
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

    public async Task<Result> Handle(DeleteVehicleCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(request.VehicleId, cancellationToken);
        if (vehicle is null)
            return Result.Failure(Error.NotFound($"Vehicle '{request.VehicleId}' not found."));

        if (!_authorizationService.CanAccessStore(_currentUser.Role, _currentUser.StoreId, vehicle.StoreId))
            return Result.Failure(Error.Forbidden("Vous ne pouvez pas supprimer un véhicule d'une autre enseigne."));

        vehicle.SoftDelete();
        _vehicleRepository.Update(vehicle);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
