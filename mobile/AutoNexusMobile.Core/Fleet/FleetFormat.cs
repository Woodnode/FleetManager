using System.Globalization;
using AutoNexusMobile.Core.Api;

namespace AutoNexusMobile.Core.Fleet;

/// <summary>
/// Libellés et mises en forme partagés avec le front web : français de France
/// (toLocaleString('fr-FR')), mêmes intitulés de statuts et de types.
/// </summary>
public static class FleetFormat
{
    public static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("fr-FR");

    public static string Number(int value) => value.ToString("N0", Culture);

    public static string Kilometers(int value) => $"{Number(value)} km";

    /// <summary>« 1 véhicule », « 12 véhicules ».</summary>
    public static string Count(int value, string singular, string plural) =>
        $"{Number(value)} {(value > 1 ? plural : singular)}";

    /// <summary>« 17/10/2026 », comme toLocaleDateString('fr-FR').</summary>
    public static string Date(DateTime value) => value.ToString("dd/MM/yyyy", Culture);

    public static string Date(DateTime? value) => value is null ? "-" : Date(value.Value);

    /// <summary>« 08:30 ».</summary>
    public static string Time(DateTime value) => value.ToString("HH:mm", Culture);

    /// <summary>« 17 oct. 08:30 - 17:00 » ou « 17 oct. 08:30 - 19 oct. 17:00 ».</summary>
    public static string Window(DateTime start, DateTime end) =>
        start.Date == end.Date
            ? $"{start.ToString("d MMM", Culture)} {Time(start)} - {Time(end)}"
            : $"{start.ToString("d MMM", Culture)} {Time(start)} - {end.ToString("d MMM", Culture)} {Time(end)}";

    /// <summary>« vendredi 17 octobre ».</summary>
    public static string LongDay(DateTime value) => value.ToString("dddd d MMMM", Culture);

    public static string Label(VehicleStatus status) => status switch
    {
        VehicleStatus.Available => "Disponible",
        VehicleStatus.InIntervention => "En intervention",
        VehicleStatus.Sold => "Vendu",
        VehicleStatus.OutOfService => "Hors service",
        _ => status.ToString(),
    };

    /// <summary>Filtres de la liste des véhicules (pilules du web, au pluriel).</summary>
    public static string FilterLabel(VehicleStatus? status) => status switch
    {
        null => "Tous",
        VehicleStatus.Available => "Disponibles",
        VehicleStatus.InIntervention => "En intervention",
        VehicleStatus.Sold => "Vendus",
        VehicleStatus.OutOfService => "Hors service",
        _ => status.ToString()!,
    };

    public static string Label(InterventionStatus status) => status switch
    {
        InterventionStatus.Planned => "Planifiée",
        InterventionStatus.InProgress => "En cours",
        InterventionStatus.Completed => "Terminée",
        InterventionStatus.Cancelled => "Annulée",
        _ => status.ToString(),
    };

    public static string Label(InterventionType type) => type switch
    {
        InterventionType.Maintenance => "Maintenance",
        InterventionType.Repair => "Réparation",
        InterventionType.Inspection => "Inspection",
        InterventionType.Other => "Autre",
        _ => type.ToString(),
    };

    /// <summary>Verbe du bouton qui mène à un statut (« Commencer », « Terminer », « Annuler »).</summary>
    public static string ActionLabel(InterventionStatus target) => target switch
    {
        InterventionStatus.InProgress => "Commencer",
        InterventionStatus.Completed => "Terminer",
        InterventionStatus.Cancelled => "Annuler",
        _ => Label(target),
    };

    public static string Vehicle(string? brand, string? model) =>
        string.Join(' ', new[] { brand, model }.Where(s => !string.IsNullOrWhiteSpace(s))) is { Length: > 0 } name ? name : "Véhicule";
}
