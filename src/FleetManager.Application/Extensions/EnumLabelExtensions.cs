using FleetManager.Domain.Enums;

namespace FleetManager.Application.Extensions;

public static class EnumLabelExtensions
{
    public static string ToLabel(this VehicleStatus status) => status switch
    {
        VehicleStatus.Available      => "Disponible",
        VehicleStatus.InIntervention => "En intervention",
        VehicleStatus.Sold           => "Vendu",
        VehicleStatus.OutOfService   => "Hors service",
        _                            => status.ToString(),
    };

    public static string ToLabel(this InterventionStatus status) => status switch
    {
        InterventionStatus.Planned    => "Planifiée",
        InterventionStatus.InProgress => "En cours",
        InterventionStatus.Completed  => "Terminée",
        InterventionStatus.Cancelled  => "Annulée",
        _                             => status.ToString(),
    };

    public static string ToLabel(this InterventionType type) => type switch
    {
        InterventionType.Maintenance => "Maintenance",
        InterventionType.Repair      => "Réparation",
        InterventionType.Inspection  => "Inspection",
        InterventionType.Other       => "Autre",
        _                            => type.ToString(),
    };
}
