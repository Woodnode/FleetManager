using FleetManager.Application.Common;
using FleetManager.Application.Interfaces;
using FleetManager.Domain.Exceptions;
using FleetManager.Domain.Interfaces;
using MediatR;

namespace FleetManager.Application.Vehicles.Commands;

public record DeleteVehicleCommand(Guid VehicleId) : IRequest<Result>;

public class DeleteVehicleCommandHandler : IRequestHandler<DeleteVehicleCommand, Result>
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IInterventionRepository _interventionRepository;
    private readonly IStoreAuthorizationService _authorizationService;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteVehicleCommandHandler(
        IVehicleRepository vehicleRepository,
        IInterventionRepository interventionRepository,
        IStoreAuthorizationService authorizationService,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _vehicleRepository      = vehicleRepository;
        _interventionRepository = interventionRepository;
        _authorizationService   = authorizationService;
        _currentUser            = currentUser;
        _unitOfWork             = unitOfWork;
    }

    public async Task<Result> Handle(DeleteVehicleCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(request.VehicleId, cancellationToken);
        if (vehicle is null)
            return Result.Failure(Error.NotFound($"Vehicle '{request.VehicleId}' not found."));

        if (!_authorizationService.CanAccessStore(_currentUser.Role, _currentUser.StoreId, vehicle.StoreId))
            return Result.Failure(Error.Forbidden("Vous ne pouvez pas supprimer un véhicule d'une autre enseigne."));

        // Source de vérité : les interventions elles-mêmes, pas seulement le statut du véhicule
        // (des données importées ou de démo peuvent avoir une intervention planifiée sur un véhicule "Disponible").
        if (await _interventionRepository.HasActiveForVehicleAsync(vehicle.Id, cancellationToken))
            return Result.Failure(Error.Conflict("Impossible de supprimer un véhicule avec une intervention planifiée ou en cours."));

        try
        {
            vehicle.SoftDelete();
        }
        catch (DomainException ex)
        {
            return Result.Failure(Error.Conflict(ex.Message));
        }

        _vehicleRepository.Update(vehicle);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
