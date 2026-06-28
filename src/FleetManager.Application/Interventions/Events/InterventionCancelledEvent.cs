using MediatR;

namespace FleetManager.Application.Interventions.Events;

public record InterventionCancelledEvent(Guid VehicleId) : INotification;
