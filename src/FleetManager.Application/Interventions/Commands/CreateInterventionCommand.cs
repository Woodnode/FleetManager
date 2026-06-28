using FleetManager.Application.Common;
using FleetManager.Application.Interfaces;
using FleetManager.Application.Interventions.Queries;
using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;
using FleetManager.Domain.Exceptions;
using FleetManager.Domain.Interfaces;
using MediatR;

namespace FleetManager.Application.Interventions.Commands;

public record CreateInterventionCommand(
    Guid VehicleId,
    Guid StoreId,
    Guid TechnicianId,
    InterventionType Type,
    DateTime PlannedStartDate,
    DateTime PlannedEndDate,
    string? Comment) : IRequest<Result<InterventionDto>>;

public class CreateInterventionCommandHandler : IRequestHandler<CreateInterventionCommand, Result<InterventionDto>>
{
    private readonly IInterventionRepository _interventionRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IStoreRepository _storeRepository;
    private readonly IUserRepository _userRepository;
    private readonly IStoreAuthorizationService _authorizationService;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public CreateInterventionCommandHandler(
        IInterventionRepository interventionRepository,
        IVehicleRepository vehicleRepository,
        IStoreRepository storeRepository,
        IUserRepository userRepository,
        IStoreAuthorizationService authorizationService,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _interventionRepository = interventionRepository;
        _vehicleRepository      = vehicleRepository;
        _storeRepository        = storeRepository;
        _userRepository         = userRepository;
        _authorizationService   = authorizationService;
        _currentUser            = currentUser;
        _unitOfWork             = unitOfWork;
    }

    public async Task<Result<InterventionDto>> Handle(CreateInterventionCommand request, CancellationToken cancellationToken)
    {
        if (!_authorizationService.CanAccessStore(_currentUser.Role, _currentUser.StoreId, request.StoreId))
            return Result.Failure<InterventionDto>(Error.Forbidden("Vous ne pouvez pas créer une intervention pour une autre enseigne."));

        var vehicle = await _vehicleRepository.GetByIdAsync(request.VehicleId, cancellationToken);
        if (vehicle is null)
            return Result.Failure<InterventionDto>(Error.NotFound($"Véhicule '{request.VehicleId}' introuvable."));

        // Vehicle must belong to the same store as the intervention — prevents cross-store IDOR.
        // Return the same "introuvable" message to avoid confirming the vehicle exists in another store.
        if (vehicle.StoreId != request.StoreId)
            return Result.Failure<InterventionDto>(Error.NotFound($"Véhicule '{request.VehicleId}' introuvable."));

        if (!await _storeRepository.ExistsAsync(request.StoreId, cancellationToken))
            return Result.Failure<InterventionDto>(Error.NotFound($"Enseigne '{request.StoreId}' introuvable."));

        var technician = await _userRepository.GetByIdAsync(request.TechnicianId, cancellationToken);
        if (technician is null)
            return Result.Failure<InterventionDto>(Error.NotFound($"Technicien '{request.TechnicianId}' introuvable."));

        // Technician must belong to the same store as the intervention.
        if (technician.StoreId != request.StoreId)
            return Result.Failure<InterventionDto>(Error.Validation("Le technicien doit appartenir à l'enseigne de l'intervention."));

        try
        {
            var intervention = Intervention.Create(
                request.VehicleId,
                request.StoreId,
                technician,
                request.Type,
                request.PlannedStartDate,
                request.PlannedEndDate,
                request.Comment);

            vehicle.ChangeStatus(VehicleStatus.InIntervention);
            _vehicleRepository.Update(vehicle);

            await _interventionRepository.AddAsync(intervention, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(InterventionDto.FromEntity(intervention));
        }
        catch (DomainException ex)
        {
            return Result.Failure<InterventionDto>(Error.Validation(ex.Message));
        }
    }
}
