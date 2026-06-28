using MediatR;

namespace FleetManager.Application.Interventions.Events;

public record InterventionCompletedEvent(Guid VehicleId) : INotification;
