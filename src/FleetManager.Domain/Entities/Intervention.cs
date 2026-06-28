using FleetManager.Domain.Enums;
using FleetManager.Domain.Exceptions;

namespace FleetManager.Domain.Entities;

public class Intervention
{
    public Guid Id { get; private set; }
    public Guid VehicleId { get; private set; }
    public Vehicle Vehicle { get; private set; } = null!;
    public Guid StoreId { get; private set; }
    public Store Store { get; private set; } = null!;
    public Guid TechnicianId { get; private set; }
    public User Technician { get; private set; } = null!;
    public InterventionType Type { get; private set; }
    public InterventionStatus Status { get; private set; }
    public DateTime PlannedStartDate { get; private set; }
    public DateTime PlannedEndDate { get; private set; }
    public DateTime? ActualEndDate { get; private set; }
    public string? Comment { get; private set; }

    private Intervention() { }

    public static Intervention Create(
        Guid vehicleId,
        Guid storeId,
        User technician,
        InterventionType type,
        DateTime plannedStartDate,
        DateTime plannedEndDate,
        string? comment = null)
    {
        if (technician.Role != UserRole.Technician)
            throw new DomainException("L'utilisateur sélectionné n'est pas un technicien.");

        if (plannedEndDate <= plannedStartDate)
            throw new DomainException("Planned end date must be after planned start date.");

        if (comment?.Length > 1000)
            throw new DomainException("Comment cannot exceed 1000 characters.");

        return new Intervention
        {
            Id = Guid.NewGuid(),
            VehicleId = vehicleId,
            StoreId = storeId,
            TechnicianId = technician.Id,
            Type = type,
            Status = InterventionStatus.Planned,
            PlannedStartDate = plannedStartDate,
            PlannedEndDate = plannedEndDate,
            Comment = comment
        };
    }

    public void Start()
    {
        if (Status != InterventionStatus.Planned)
            throw new DomainException("Only a planned intervention can be started.");

        Status = InterventionStatus.InProgress;
    }

    public void Complete(string? finalComment = null)
    {
        if (Status != InterventionStatus.InProgress)
            throw new DomainException("Only an in-progress intervention can be completed.");

        if (finalComment?.Length > 1000)
            throw new DomainException("Comment cannot exceed 1000 characters.");

        Status = InterventionStatus.Completed;
        ActualEndDate = DateTime.UtcNow;

        if (finalComment is not null)
            Comment = finalComment;
    }

    public void Cancel(string reason)
    {
        if (Status == InterventionStatus.Completed)
            throw new DomainException("A completed intervention cannot be cancelled.");

        if (string.IsNullOrWhiteSpace(reason))
            throw new DomainException("A cancellation reason is required.");

        if (reason.Length > 1000)
            throw new DomainException("Comment cannot exceed 1000 characters.");

        Status = InterventionStatus.Cancelled;
        Comment = reason;
    }

    public void UpdateDetails(InterventionType type, DateTime plannedStartDate, DateTime plannedEndDate, string? comment)
    {
        if (Status != InterventionStatus.Planned)
            throw new DomainException("Only a planned intervention can be updated.");

        if (plannedEndDate <= plannedStartDate)
            throw new DomainException("Planned end date must be after planned start date.");

        Type = type;
        PlannedStartDate = plannedStartDate;
        PlannedEndDate = plannedEndDate;
        Comment = comment;
    }
}
