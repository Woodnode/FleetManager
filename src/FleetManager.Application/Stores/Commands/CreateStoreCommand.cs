using FleetManager.Application.Common;
using FleetManager.Application.Interfaces;
using FleetManager.Application.Stores.Queries;
using FleetManager.Domain.Entities;
using FleetManager.Domain.Exceptions;
using FleetManager.Domain.Interfaces;
using MediatR;

namespace FleetManager.Application.Stores.Commands;

public record CreateStoreCommand(
    string Name,
    string Address,
    string PostalCode,
    string City) : IRequest<Result<StoreDto>>;

public class CreateStoreCommandHandler : IRequestHandler<CreateStoreCommand, Result<StoreDto>>
{
    private readonly IStoreRepository   _storeRepository;
    private readonly IUnitOfWork        _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public CreateStoreCommandHandler(
        IStoreRepository   storeRepository,
        IUnitOfWork        unitOfWork,
        ICurrentUserService currentUser)
    {
        _storeRepository = storeRepository;
        _unitOfWork      = unitOfWork;
        _currentUser     = currentUser;
    }

    public async Task<Result<StoreDto>> Handle(CreateStoreCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return Result.Failure<StoreDto>(Error.Forbidden("Seuls les administrateurs peuvent créer des enseignes."));

        try
        {
            var store = Store.Create(request.Name, request.Address, request.PostalCode, request.City);
            await _storeRepository.AddAsync(store, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(StoreDto.FromEntity(store));
        }
        catch (DomainException ex)
        {
            return Result.Failure<StoreDto>(Error.Validation(ex.Message));
        }
    }
}
