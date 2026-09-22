using FleetManager.Application.Common;
using FleetManager.Application.Interfaces;
using FleetManager.Application.Vehicles.Queries;
using FleetManager.Domain.Exceptions;
using FleetManager.Domain.Interfaces;
using MediatR;

namespace FleetManager.Application.Vehicles.Commands;

public record RestoreVehicleCommand(Guid VehicleId) : IRequest<Result<VehicleDto>>;

public class RestoreVehicleCommandHandler : IRequestHandler<RestoreVehicleCommand, Result<VehicleDto>>
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IStoreAuthorizationService _authorizationService;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public RestoreVehicleCommandHandler(
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

    public async Task<Result<VehicleDto>> Handle(RestoreVehicleCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await _vehicleRepository.GetArchivedByIdAsync(request.VehicleId, cancellationToken);

        // Hors périmètre : même réponse qu'un véhicule inexistant (pas de fuite d'existence).
        if (vehicle is null || !_authorizationService.CanAccessStore(_currentUser.Role, _currentUser.StoreId, vehicle.StoreId))
            return Result.Failure<VehicleDto>(Error.NotFound($"Véhicule archivé {request.VehicleId} introuvable."));

        try
        {
            vehicle.Restore();
        }
        catch (DomainException ex)
        {
            return Result.Failure<VehicleDto>(Error.Conflict(ex.Message));
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        return Result.Success(VehicleDto.FromEntity(vehicle));
    }
}
