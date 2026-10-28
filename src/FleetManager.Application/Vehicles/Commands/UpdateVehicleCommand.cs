using FleetManager.Application.Common;
using FleetManager.Application.Interfaces;
using FleetManager.Application.Vehicles.Queries;
using FleetManager.Domain.Enums;
using FleetManager.Domain.Exceptions;
using FleetManager.Domain.Interfaces;
using MediatR;

namespace FleetManager.Application.Vehicles.Commands;

public record UpdateVehicleCommand(
    Guid Id,
    string Brand,
    string Model,
    int Year,
    int Mileage,
    Guid StoreId) : IRequest<Result<VehicleDto>>;

public class UpdateVehicleCommandHandler : IRequestHandler<UpdateVehicleCommand, Result<VehicleDto>>
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IStoreRepository _storeRepository;
    private readonly IInterventionRepository _interventionRepository;
    private readonly IStoreAuthorizationService _authorizationService;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public UpdateVehicleCommandHandler(
        IVehicleRepository vehicleRepository,
        IStoreRepository storeRepository,
        IInterventionRepository interventionRepository,
        IStoreAuthorizationService authorizationService,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _vehicleRepository    = vehicleRepository;
        _storeRepository      = storeRepository;
        _interventionRepository = interventionRepository;
        _authorizationService = authorizationService;
        _currentUser          = currentUser;
        _unitOfWork           = unitOfWork;
    }

    public async Task<Result<VehicleDto>> Handle(UpdateVehicleCommand request, CancellationToken cancellationToken)
    {
        var vehicle = await _vehicleRepository.GetByIdAsync(request.Id, cancellationToken);
        if (vehicle is null)
            return Result.Failure<VehicleDto>(Error.NotFound($"Vehicle '{request.Id}' not found."));

        if (!_authorizationService.CanAccessStore(_currentUser.Role, _currentUser.StoreId, vehicle.StoreId))
            return Result.Failure<VehicleDto>(Error.Forbidden("Vous ne pouvez pas modifier un véhicule d'une autre enseigne."));

        // Non-Admin users cannot reassign a vehicle to a different store.
        var targetStoreId = _currentUser.IsAdmin ? request.StoreId : vehicle.StoreId;

        if (!await _storeRepository.ExistsAsync(targetStoreId, cancellationToken))
            return Result.Failure<VehicleDto>(Error.NotFound($"Store '{targetStoreId}' not found."));

        // Une intervention planifiée ou en cours reste rattachée à l'enseigne d'origine : transférer le véhicule
        // laisserait le technicien de cette enseigne modifier un véhicule qui ne lui appartient plus.
        if (targetStoreId != vehicle.StoreId
            && await _interventionRepository.HasActiveForVehicleAsync(vehicle.Id, cancellationToken))
            return Result.Failure<VehicleDto>(Error.Conflict("Impossible de transférer un véhicule avec une intervention planifiée ou en cours."));

        try
        {
            vehicle.Update(request.Brand, request.Model, request.Year, request.Mileage, targetStoreId);
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
