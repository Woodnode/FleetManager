namespace FleetManager.Application.Dashboard.Queries;

public record DashboardSummaryDto(
    VehicleSummary   Vehicles,
    InterventionSummary Interventions,
    IReadOnlyList<RecentInterventionDto> RecentInterventions
);

public record VehicleSummary(
    int Total,
    int Available,
    int InIntervention,
    int Sold,
    int OutOfService
);

public record InterventionSummary(
    int Total,
    int Planned,
    int InProgress,
    int Completed,
    int Cancelled,
    int Maintenance,
    int Repair,
    int Inspection,
    int Other
);

public record RecentInterventionDto(
    Guid   Id,
    string VehicleBrand,
    string VehicleModel,
    string VehicleVin,
    string StoreName,
    string? TechnicianFullName,
    string Type,
    string TypeLabel,
    string Status,
    string StatusLabel,
    DateTime PlannedStartDate
);
