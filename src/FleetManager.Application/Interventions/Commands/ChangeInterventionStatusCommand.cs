using FleetManager.Application.Common;
using FleetManager.Application.Interfaces;
using FleetManager.Application.Interventions.Events;
using FleetManager.Application.Interventions.Queries;
using FleetManager.Domain.Enums;
using FleetManager.Domain.Exceptions;
using FleetManager.Domain.Interfaces;
using MediatR;

namespace FleetManager.Application.Interventions.Commands;

public record ChangeInterventionStatusCommand(
    Guid InterventionId,
    InterventionStatus NewStatus,
    string? Comment = null) : IRequest<Result<InterventionDto>>;

public class ChangeInterventionStatusCommandHandler : IRequestHandler<ChangeInterventionStatusCommand, Result<InterventionDto>>
{
    private readonly IInterventionRepository _interventionRepository;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IStoreAuthorizationService _authorizationService;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPublisher _publisher;

    public ChangeInterventionStatusCommandHandler(
        IInterventionRepository interventionRepository,
        IVehicleRepository vehicleRepository,
        IStoreAuthorizationService authorizationService,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork,
        IPublisher publisher)
    {
        _interventionRepository = interventionRepository;
        _vehicleRepository      = vehicleRepository;
        _authorizationService   = authorizationService;
        _currentUser            = currentUser;
        _unitOfWork             = unitOfWork;
        _publisher              = publisher;
    }

    public async Task<Result<InterventionDto>> Handle(ChangeInterventionStatusCommand request, CancellationToken cancellationToken)
    {
        var intervention = await _interventionRepository.GetByIdAsync(request.InterventionId, cancellationToken);
        if (intervention is null)
            return Result.Failure<InterventionDto>(Error.NotFound($"Intervention '{request.InterventionId}' introuvable."));

        // Return the same "introuvable" message as a non-existent intervention — prevents
        // cross-store enumeration by distinguishing "not found" from "forbidden".
        if (!_authorizationService.CanAccessStore(_currentUser.Role, _currentUser.StoreId, intervention.StoreId))
            return Result.Failure<InterventionDto>(Error.NotFound($"Intervention '{request.InterventionId}' introuvable."));

        try
        {
            switch (request.NewStatus)
            {
                case InterventionStatus.InProgress:
                    intervention.Start();
                    break;
                case InterventionStatus.Completed:
                    intervention.Complete(request.Comment);
                    break;
                case InterventionStatus.Cancelled:
                    intervention.Cancel(request.Comment ?? string.Empty);
                    break;
                default:
                    return Result.Failure<InterventionDto>(Error.Validation($"Status transition to '{request.NewStatus}' is not supported."));
            }

            _interventionRepository.Update(intervention);

            // Free the vehicle in the same transaction to guarantee atomicity:
            // if the save fails, both intervention and vehicle statuses are rolled back together.
            if (request.NewStatus is InterventionStatus.Completed or InterventionStatus.Cancelled)
            {
                var vehicle = await _vehicleRepository.GetByIdAsync(intervention.VehicleId, cancellationToken);
                if (vehicle is not null)
                {
                    vehicle.ChangeStatus(VehicleStatus.Available);
                    _vehicleRepository.Update(vehicle);
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            // Publish events after commit — available for downstream consumers (audit, notifications, etc.)
            if (request.NewStatus == InterventionStatus.Completed)
                await _publisher.Publish(new InterventionCompletedEvent(intervention.VehicleId), cancellationToken);
            else if (request.NewStatus == InterventionStatus.Cancelled)
                await _publisher.Publish(new InterventionCancelledEvent(intervention.VehicleId), cancellationToken);

            return Result.Success(InterventionDto.FromEntity(intervention));
        }
        catch (DomainException ex)
        {
            return Result.Failure<InterventionDto>(Error.Validation(ex.Message));
        }
    }
}
