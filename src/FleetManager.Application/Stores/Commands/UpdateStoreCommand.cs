using FleetManager.Application.Common;
using FleetManager.Application.Interfaces;
using FleetManager.Application.Stores.Queries;
using FleetManager.Domain.Exceptions;
using FleetManager.Domain.Interfaces;
using MediatR;

namespace FleetManager.Application.Stores.Commands;

public record UpdateStoreCommand(
    Guid   Id,
    string Name,
    string Address,
    string PostalCode,
    string City) : IRequest<Result<StoreDto>>;

public class UpdateStoreCommandHandler : IRequestHandler<UpdateStoreCommand, Result<StoreDto>>
{
    private readonly IStoreRepository    _storeRepository;
    private readonly IUnitOfWork         _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public UpdateStoreCommandHandler(
        IStoreRepository    storeRepository,
        IUnitOfWork         unitOfWork,
        ICurrentUserService currentUser)
    {
        _storeRepository = storeRepository;
        _unitOfWork      = unitOfWork;
        _currentUser     = currentUser;
    }

    public async Task<Result<StoreDto>> Handle(UpdateStoreCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return Result.Failure<StoreDto>(Error.Forbidden("Seuls les administrateurs peuvent modifier des enseignes."));

        var store = await _storeRepository.GetByIdAsync(request.Id, cancellationToken);
        if (store is null)
            return Result.Failure<StoreDto>(Error.NotFound($"Enseigne {request.Id} introuvable."));

        try
        {
            store.Update(request.Name, request.Address, request.PostalCode, request.City);
            _storeRepository.Update(store);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(StoreDto.FromEntity(store));
        }
        catch (DomainException ex)
        {
            return Result.Failure<StoreDto>(Error.Validation(ex.Message));
        }
    }
}
