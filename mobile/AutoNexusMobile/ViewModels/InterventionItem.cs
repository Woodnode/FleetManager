using AutoNexusMobile.Core.Api;
using AutoNexusMobile.Core.Fleet;

namespace AutoNexusMobile.ViewModels;

/// <summary>Intervention mise en forme pour une liste (journée, liste, historique d'un véhicule).</summary>
public sealed class InterventionItem
{
    public InterventionItem(InterventionDto dto, DateTime? localNow = null)
    {
        Dto = dto;
        var now = localNow ?? DateTime.Now;
        IsOverdue = dto.Status is InterventionStatus.Planned or InterventionStatus.InProgress && dto.PlannedEndDate < now;
        NextAction = RolePermissions.NextStatuses(dto.Status).Where(s => s != InterventionStatus.Cancelled).Cast<InterventionStatus?>().FirstOrDefault();
    }

    public InterventionDto Dto { get; }

    public Guid Id => Dto.Id;

    public string Vehicle => FleetFormat.Vehicle(Dto.VehicleBrand, Dto.VehicleModel);

    public string? Vin => Dto.VehicleVin;

    public string Window => FleetFormat.Window(Dto.PlannedStartDate, Dto.PlannedEndDate);

    public string Date => FleetFormat.Date(Dto.PlannedStartDate);

    public string StartTime => FleetFormat.Time(Dto.PlannedStartDate);

    public string People => $"{Dto.TechnicianFullName ?? "Non assigné"} · {Dto.StoreName}";

    public string? Comment => string.IsNullOrWhiteSpace(Dto.Comment) ? null : Dto.Comment;

    public bool IsOverdue { get; }

    /// <summary>Action principale proposée sur la carte (Commencer ou Terminer), sinon aucune.</summary>
    public InterventionStatus? NextAction { get; }

    public bool HasNextAction => NextAction is not null;

    public string NextActionLabel => NextAction is { } action ? FleetFormat.ActionLabel(action) : "";

    public bool NextActionIsStart => NextAction == InterventionStatus.InProgress;

    public bool NextActionIsComplete => NextAction == InterventionStatus.Completed;
}
