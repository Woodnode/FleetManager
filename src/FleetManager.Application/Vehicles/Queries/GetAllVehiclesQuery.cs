using FleetManager.Application.Common;
using FleetManager.Application.Interfaces;
using FleetManager.Domain.Interfaces;
using MediatR;

namespace FleetManager.Application.Vehicles.Queries;

public record GetAllVehiclesQuery(int Page = 1, int PageSize = 20, string? Search = null, string? Status = null) : IRequest<Result<PagedResult<VehicleDto>>>;

public class GetAllVehiclesQueryHandler : IRequestHandler<GetAllVehiclesQuery, Result<PagedResult<VehicleDto>>>
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly ICurrentUserService _currentUser;

    public GetAllVehiclesQueryHandler(IVehicleRepository vehicleRepository, ICurrentUserService currentUser)
    {
        _vehicleRepository = vehicleRepository;
        _currentUser       = currentUser;
    }

    public async Task<Result<PagedResult<VehicleDto>>> Handle(GetAllVehiclesQuery request, CancellationToken cancellationToken)
    {
        var page     = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        if (!_currentUser.IsAdmin && (!_currentUser.Role.HasValue || !_currentUser.StoreId.HasValue))
            return Result.Success(new PagedResult<VehicleDto>([], 0, page, pageSize));

        var storeId = _currentUser.IsAdmin ? (Guid?)null : _currentUser.StoreId!.Value;
        var (entities, total) = await _vehicleRepository.GetPagedAsync(storeId, (page - 1) * pageSize, pageSize, request.Search, request.Status, cancellationToken);
        var dtos = entities.Select(VehicleDto.FromEntity).ToList().AsReadOnly();

        return Result.Success(new PagedResult<VehicleDto>(dtos, total, page, pageSize));
    }
}
