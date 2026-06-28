using FleetManager.Application.Common;
using FleetManager.Application.Interfaces;
using FleetManager.Domain.Interfaces;
using MediatR;

namespace FleetManager.Application.Interventions.Queries;

public record GetAllInterventionsQuery(int Page = 1, int PageSize = 20, string? Status = null, string? Type = null) : IRequest<Result<PagedResult<InterventionDto>>>;

public class GetAllInterventionsQueryHandler : IRequestHandler<GetAllInterventionsQuery, Result<PagedResult<InterventionDto>>>
{
    private readonly IInterventionRepository _interventionRepository;
    private readonly ICurrentUserService _currentUser;

    public GetAllInterventionsQueryHandler(IInterventionRepository interventionRepository, ICurrentUserService currentUser)
    {
        _interventionRepository = interventionRepository;
        _currentUser            = currentUser;
    }

    public async Task<Result<PagedResult<InterventionDto>>> Handle(GetAllInterventionsQuery request, CancellationToken cancellationToken)
    {
        var page     = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 100);

        if (!_currentUser.IsAdmin && (!_currentUser.Role.HasValue || !_currentUser.StoreId.HasValue))
            return Result.Success(new PagedResult<InterventionDto>([], 0, page, pageSize));

        var storeId = _currentUser.IsAdmin ? (Guid?)null : _currentUser.StoreId!.Value;
        var (entities, total) = await _interventionRepository.GetPagedAsync(storeId, (page - 1) * pageSize, pageSize, request.Status, request.Type, cancellationToken);
        var dtos = entities.Select(InterventionDto.FromEntity).ToList().AsReadOnly();

        return Result.Success(new PagedResult<InterventionDto>(dtos, total, page, pageSize));
    }
}
