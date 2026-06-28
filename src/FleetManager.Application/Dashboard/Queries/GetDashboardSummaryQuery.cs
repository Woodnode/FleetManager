using FleetManager.Application.Common;
using FleetManager.Application.Extensions;
using FleetManager.Application.Interfaces;
using FleetManager.Domain.Enums;
using FleetManager.Domain.Interfaces;
using MediatR;

namespace FleetManager.Application.Dashboard.Queries;

public record GetDashboardSummaryQuery : IRequest<Result<DashboardSummaryDto>>;

public class GetDashboardSummaryQueryHandler : IRequestHandler<GetDashboardSummaryQuery, Result<DashboardSummaryDto>>
{
    private readonly IVehicleRepository      _vehicleRepository;
    private readonly IInterventionRepository _interventionRepository;
    private readonly ICurrentUserService     _currentUser;

    public GetDashboardSummaryQueryHandler(
        IVehicleRepository      vehicleRepository,
        IInterventionRepository interventionRepository,
        ICurrentUserService     currentUser)
    {
        _vehicleRepository      = vehicleRepository;
        _interventionRepository = interventionRepository;
        _currentUser            = currentUser;
    }

    public async Task<Result<DashboardSummaryDto>> Handle(
        GetDashboardSummaryQuery request,
        CancellationToken        cancellationToken)
    {
        var storeId = _currentUser.IsAdmin ? (Guid?)null : _currentUser.StoreId;

        var vehicleCounts      = await _vehicleRepository.GetStatusCountsAsync(storeId, cancellationToken);
        var interventionCounts = await _interventionRepository.GetSummaryCountsAsync(storeId, cancellationToken);
        var recentInterventions = await _interventionRepository.GetRecentAsync(storeId, 6, cancellationToken);

        var vehicleSummary = new VehicleSummary(
            Total:          vehicleCounts.Values.Sum(),
            Available:      vehicleCounts.GetValueOrDefault(VehicleStatus.Available),
            InIntervention: vehicleCounts.GetValueOrDefault(VehicleStatus.InIntervention),
            Sold:           vehicleCounts.GetValueOrDefault(VehicleStatus.Sold),
            OutOfService:   vehicleCounts.GetValueOrDefault(VehicleStatus.OutOfService)
        );

        var interventionSummary = new InterventionSummary(
            Total:       interventionCounts.StatusCounts.Values.Sum(),
            Planned:     interventionCounts.StatusCounts.GetValueOrDefault(InterventionStatus.Planned),
            InProgress:  interventionCounts.StatusCounts.GetValueOrDefault(InterventionStatus.InProgress),
            Completed:   interventionCounts.StatusCounts.GetValueOrDefault(InterventionStatus.Completed),
            Cancelled:   interventionCounts.StatusCounts.GetValueOrDefault(InterventionStatus.Cancelled),
            Maintenance: interventionCounts.TypeCounts.GetValueOrDefault(InterventionType.Maintenance),
            Repair:      interventionCounts.TypeCounts.GetValueOrDefault(InterventionType.Repair),
            Inspection:  interventionCounts.TypeCounts.GetValueOrDefault(InterventionType.Inspection),
            Other:       interventionCounts.TypeCounts.GetValueOrDefault(InterventionType.Other)
        );

        var recent = recentInterventions.Select(i => new RecentInterventionDto(
            Id:                  i.Id,
            VehicleBrand:        i.Vehicle.Brand,
            VehicleModel:        i.Vehicle.Model,
            VehicleVin:          i.Vehicle.Vin.Value,
            StoreName:           i.Store.Name,
            TechnicianFullName:  i.Technician is not null
                                     ? $"{i.Technician.FirstName} {i.Technician.LastName}"
                                     : null,
            Type:                i.Type.ToString(),
            TypeLabel:           i.Type.ToLabel(),
            Status:              i.Status.ToString(),
            StatusLabel:         i.Status.ToLabel(),
            PlannedStartDate:    i.PlannedStartDate
        )).ToList().AsReadOnly();

        return Result.Success(new DashboardSummaryDto(vehicleSummary, interventionSummary, recent));
    }
}
