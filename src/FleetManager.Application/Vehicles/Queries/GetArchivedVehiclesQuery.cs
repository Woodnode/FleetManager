using FleetManager.Application.Common;
using FleetManager.Application.Interfaces;
using FleetManager.Domain.Interfaces;
using MediatR;

namespace FleetManager.Application.Vehicles.Queries;

public record GetArchivedVehiclesQuery(int Page = 1, int PageSize = 20, string? Search = null)
    : IRequest<Result<PagedResult<ArchivedVehicleDto>>>;

/// <summary>
/// Archives paginées. Même cloisonnement que la liste des véhicules :
/// Admin voit toutes les enseignes, les autres rôles uniquement la leur.
/// </summary>
public class GetArchivedVehiclesQueryHandler
    : IRequestHandler<GetArchivedVehiclesQuery, Result<PagedResult<ArchivedVehicleDto>>>
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IInterventionRepository _interventionRepository;
    private readonly ICurrentUserService _currentUser;

    public GetArchivedVehiclesQueryHandler(
        IVehicleRepository vehicleRepository,
        IInterventionRepository interventionRepository,
        ICurrentUserService currentUser)
    {
        _vehicleRepository      = vehicleRepository;
        _interventionRepository = interventionRepository;
        _currentUser            = currentUser;
    }

    public async Task<Result<PagedResult<ArchivedVehicleDto>>> Handle(GetArchivedVehiclesQuery request, CancellationToken cancellationToken)
    {
        var page     = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        if (!_currentUser.IsAdmin && (!_currentUser.Role.HasValue || !_currentUser.StoreId.HasValue))
            return Result.Success(new PagedResult<ArchivedVehicleDto>([], 0, page, pageSize));

        var storeId = _currentUser.IsAdmin ? (Guid?)null : _currentUser.StoreId!.Value;
        var (vehicles, total) = await _vehicleRepository.GetArchivedPagedAsync(
            storeId, (page - 1) * pageSize, pageSize, request.Search, cancellationToken);

        var counts = await _interventionRepository.CountByArchivedVehicleIdsAsync(
            vehicles.Select(v => v.Id).ToList(), cancellationToken);

        var dtos = vehicles
            .Select(v => ArchivedVehicleDto.FromEntity(v, counts.GetValueOrDefault(v.Id)))
            .ToList()
            .AsReadOnly();

        return Result.Success(new PagedResult<ArchivedVehicleDto>(dtos, total, page, pageSize));
    }
}
