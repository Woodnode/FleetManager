using FleetManager.Application.Common;
using FleetManager.Application.Interfaces;
using FleetManager.Domain.Interfaces;
using MediatR;

namespace FleetManager.Application.Stores.Commands;

public record DeleteStoreCommand(Guid Id) : IRequest<Result>;

public class DeleteStoreCommandHandler : IRequestHandler<DeleteStoreCommand, Result>
{
    private readonly IStoreRepository    _storeRepository;
    private readonly IVehicleRepository  _vehicleRepository;
    private readonly IUnitOfWork         _unitOfWork;
    private readonly ICurrentUserService _currentUser;

    public DeleteStoreCommandHandler(
        IStoreRepository    storeRepository,
        IVehicleRepository  vehicleRepository,
        IUnitOfWork         unitOfWork,
        ICurrentUserService currentUser)
    {
        _storeRepository   = storeRepository;
        _vehicleRepository = vehicleRepository;
        _unitOfWork        = unitOfWork;
        _currentUser       = currentUser;
    }

    public async Task<Result> Handle(DeleteStoreCommand request, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAdmin)
            return Result.Failure(Error.Forbidden("Seuls les administrateurs peuvent supprimer des enseignes."));

        var store = await _storeRepository.GetByIdAsync(request.Id, cancellationToken);
        if (store is null)
            return Result.Failure(Error.NotFound($"Enseigne {request.Id} introuvable."));

        var vehicles = await _vehicleRepository.GetByStoreIdAsync(request.Id, cancellationToken);
        if (vehicles.Count > 0)
            return Result.Failure(Error.Conflict(
                "Impossible de supprimer cette enseigne : elle contient des véhicules. Supprimez ou transférez les véhicules d'abord."));

        _storeRepository.Remove(store);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
