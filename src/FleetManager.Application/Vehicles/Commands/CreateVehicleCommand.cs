using FleetManager.Application.Common;
using FleetManager.Application.Interfaces;
using FleetManager.Application.Vehicles.Queries;
using FleetManager.Domain.Entities;
using FleetManager.Domain.Exceptions;
using FleetManager.Domain.Interfaces;
using MediatR;

namespace FleetManager.Application.Vehicles.Commands;

public record CreateVehicleCommand(
    string Vin,
    string Brand,
    string Model,
    int Year,
    int Mileage,
    Guid StoreId) : IRequest<Result<VehicleDto>>;

public class CreateVehicleCommandHandler : IRequestHandler<CreateVehicleCommand, Result<VehicleDto>>
{
    private readonly IVehicleRepository _vehicleRepository;
    private readonly IStoreRepository _storeRepository;
    private readonly IStoreAuthorizationService _authorizationService;
    private readonly ICurrentUserService _currentUser;
    private readonly IUnitOfWork _unitOfWork;

    public CreateVehicleCommandHandler(
        IVehicleRepository vehicleRepository,
        IStoreRepository storeRepository,
        IStoreAuthorizationService authorizationService,
        ICurrentUserService currentUser,
        IUnitOfWork unitOfWork)
    {
        _vehicleRepository    = vehicleRepository;
        _storeRepository      = storeRepository;
        _authorizationService = authorizationService;
        _currentUser          = currentUser;
        _unitOfWork           = unitOfWork;
    }

    public async Task<Result<VehicleDto>> Handle(CreateVehicleCommand request, CancellationToken cancellationToken)
    {
        if (!_authorizationService.CanAccessStore(_currentUser.Role, _currentUser.StoreId, request.StoreId))
            return Result.Failure<VehicleDto>(Error.Forbidden("Vous ne pouvez pas créer un véhicule pour une autre enseigne."));

        if (await _vehicleRepository.ExistsByVinAsync(request.Vin, cancellationToken))
            return Result.Failure<VehicleDto>(Error.Conflict($"A vehicle with VIN '{request.Vin}' already exists."));

        if (!await _storeRepository.ExistsAsync(request.StoreId, cancellationToken))
            return Result.Failure<VehicleDto>(Error.NotFound($"Store '{request.StoreId}' not found."));

        try
        {
            var vehicle = Vehicle.Create(request.Vin, request.Brand, request.Model, request.Year, request.Mileage, request.StoreId);
            await _vehicleRepository.AddAsync(vehicle, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return Result.Success(VehicleDto.FromEntity(vehicle));
        }
        catch (DomainException ex)
        {
            return Result.Failure<VehicleDto>(Error.Validation(ex.Message));
        }
    }
}
