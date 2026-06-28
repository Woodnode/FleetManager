using FleetManager.Domain.Enums;

namespace FleetManager.Api.DTOs.Requests;

public record ChangeVehicleStatusRequest(VehicleStatus NewStatus);
