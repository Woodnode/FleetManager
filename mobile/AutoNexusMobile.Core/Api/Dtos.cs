namespace AutoNexusMobile.Core.Api;

// Contrats de l'API AutoNexus (src/FleetManager.Api), désérialisés en camelCase avec les
// énumérations en texte, comme les renvoie le serveur (JsonStringEnumConverter).

public enum VehicleStatus { Available = 1, InIntervention = 2, Sold = 3, OutOfService = 4 }

public enum InterventionStatus { Planned = 1, InProgress = 2, Completed = 3, Cancelled = 4 }

public enum InterventionType { Maintenance = 1, Repair = 2, Inspection = 3, Other = 4 }

public enum UserRole { Admin = 1, StoreManager = 2, Technician = 3 }

public sealed record VehicleDto(
    Guid Id,
    string Vin,
    string Brand,
    string Model,
    int Year,
    int Mileage,
    VehicleStatus Status,
    string StatusLabel,
    Guid StoreId,
    string? StoreName);

public sealed record InterventionDto(
    Guid Id,
    Guid VehicleId,
    string? VehicleBrand,
    string? VehicleModel,
    string? VehicleVin,
    Guid StoreId,
    string? StoreName,
    Guid TechnicianId,
    string? TechnicianFullName,
    InterventionType Type,
    string TypeLabel,
    InterventionStatus Status,
    string StatusLabel,
    DateTime PlannedStartDate,
    DateTime PlannedEndDate,
    DateTime? ActualEndDate,
    string? Comment);

public sealed record StoreDto(Guid Id, string Name, string Address, string PostalCode, string City);

public sealed record TechnicianDto(Guid Id, string FullName, string Email, Guid? StoreId);

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int Page,
    int PageSize,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage);

public sealed record VehicleSummary(int Total, int Available, int InIntervention, int Sold, int OutOfService);

public sealed record InterventionSummary(
    int Total,
    int Planned,
    int InProgress,
    int Completed,
    int Cancelled,
    int Maintenance,
    int Repair,
    int Inspection,
    int Other);

public sealed record RecentInterventionDto(
    Guid Id,
    string VehicleBrand,
    string VehicleModel,
    string VehicleVin,
    string StoreName,
    string? TechnicianFullName,
    InterventionType Type,
    string TypeLabel,
    InterventionStatus Status,
    string StatusLabel,
    DateTime PlannedStartDate);

public sealed record DashboardSummaryDto(
    VehicleSummary Vehicles,
    InterventionSummary Interventions,
    IReadOnlyList<RecentInterventionDto> RecentInterventions);

public sealed record ArchivedVehicleDto(
    Guid Id,
    string Vin,
    string Brand,
    string Model,
    int Year,
    int Mileage,
    Guid StoreId,
    string? StoreName,
    DateTime? DeletedAt,
    int InterventionCount);

public sealed record ArchivedVehicleHistoryDto(ArchivedVehicleDto Vehicle, IReadOnlyList<InterventionDto> Interventions);

/// <summary>Réponse de connexion et de rafraîchissement pour le client mobile (jetons dans le corps).</summary>
public sealed record SessionResponse(
    Guid UserId,
    string FirstName,
    string LastName,
    string? Email,
    UserRole Role,
    Guid? StoreId,
    string AccessToken,
    string RefreshToken);

public sealed record LoginRequest(string Email, string Password);

public sealed record RefreshRequest(string RefreshToken);

public sealed record CreateVehicleRequest(string Vin, string Brand, string Model, int Year, int Mileage, Guid StoreId);

public sealed record UpdateVehicleRequest(string Brand, string Model, int Year, int Mileage, Guid StoreId);

public sealed record ChangeVehicleStatusRequest(VehicleStatus NewStatus);

public sealed record CreateInterventionRequest(
    Guid VehicleId,
    Guid StoreId,
    Guid TechnicianId,
    InterventionType Type,
    DateTime PlannedStartDate,
    DateTime PlannedEndDate,
    string? Comment);

public sealed record ChangeInterventionStatusRequest(InterventionStatus NewStatus, string? Comment, int? Mileage);

public sealed record StoreRequest(string Name, string Address, string PostalCode, string City);
