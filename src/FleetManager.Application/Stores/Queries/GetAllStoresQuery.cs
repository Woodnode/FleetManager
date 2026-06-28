using FleetManager.Application.Common;
using FleetManager.Application.Interfaces;
using FleetManager.Domain.Interfaces;
using MediatR;

namespace FleetManager.Application.Stores.Queries;

public record GetAllStoresQuery : IRequest<Result<IReadOnlyList<StoreDto>>>;

public class GetAllStoresQueryHandler : IRequestHandler<GetAllStoresQuery, Result<IReadOnlyList<StoreDto>>>
{
    private readonly IStoreRepository    _storeRepository;
    private readonly ICurrentUserService _currentUser;

    public GetAllStoresQueryHandler(IStoreRepository storeRepository, ICurrentUserService currentUser)
    {
        _storeRepository = storeRepository;
        _currentUser     = currentUser;
    }

    public async Task<Result<IReadOnlyList<StoreDto>>> Handle(GetAllStoresQuery request, CancellationToken cancellationToken)
    {
        var stores = await _storeRepository.GetAllAsync(cancellationToken);

        // Non-admin users can only see their own store
        if (!_currentUser.IsAdmin && _currentUser.StoreId.HasValue)
            stores = stores.Where(s => s.Id == _currentUser.StoreId.Value).ToList();

        var dtos = stores.Select(StoreDto.FromEntity).ToList().AsReadOnly();
        return Result.Success<IReadOnlyList<StoreDto>>(dtos);
    }
}
