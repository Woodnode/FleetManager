using FleetManager.Api.DTOs.Requests;
using FleetManager.Application.Vehicles.Commands;
using FleetManager.Application.Vehicles.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetManager.Api.Controllers;

[Authorize]
public class VehiclesController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public VehiclesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Récupère les véhicules paginés. Admin : tous ; autres : enseigne uniquement.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetAllVehiclesQuery(page, pageSize, search, status), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error!);
    }

    /// <summary>Récupère un véhicule par son identifiant.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetVehicleByIdQuery(id), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : MapError(result.Error!);
    }

    /// <summary>Récupère les véhicules d'une enseigne. Non-Admin : enseigne propre uniquement.</summary>
    [HttpGet("store/{storeId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<VehicleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByStore(Guid storeId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetVehiclesByStoreQuery(storeId), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : MapError(result.Error!);
    }

    /// <summary>Crée un nouveau véhicule. Non-Admin : storeId imposé par le JWT.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateVehicleRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateVehicleCommand(request.Vin, request.Brand, request.Model, request.Year, request.Mileage, request.StoreId);
        var result = await _mediator.Send(command, cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : MapError(result.Error!);
    }

    /// <summary>Met à jour les informations d'un véhicule. Non-Admin : enseigne propre uniquement.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateVehicleRequest request, CancellationToken cancellationToken)
    {
        var command = new UpdateVehicleCommand(id, request.Brand, request.Model, request.Year, request.Mileage, request.StoreId);
        var result = await _mediator.Send(command, cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : MapError(result.Error!);
    }

    /// <summary>Modifie le statut d'un véhicule. Non-Admin : enseigne propre uniquement.</summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(VehicleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeVehicleStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ChangeVehicleStatusCommand(id, request.NewStatus), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : MapError(result.Error!);
    }

    /// <summary>Supprime un véhicule. Admin : tous ; StoreManager : enseigne propre uniquement.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin,StoreManager")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteVehicleCommand(id), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : MapError(result.Error!);
    }
}
