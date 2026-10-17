using FleetManager.Api.DTOs.Requests;
using FleetManager.Application.Interventions.Commands;
using FleetManager.Application.Interventions.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetManager.Api.Controllers;

[Authorize]
public class InterventionsController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public InterventionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Récupère les interventions paginées. Admin : toutes ; autres : enseigne uniquement.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? status = null,
        [FromQuery] string? type = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetAllInterventionsQuery(page, pageSize, status, type), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error!);
    }

    /// <summary>
    /// Journée du technicien connecté : ses interventions prévues entre <paramref name="from"/> et
    /// <paramref name="to"/> (heures murales comme les dates stockées, par défaut le jour UTC
    /// courant, 7 jours au plus) et
    /// celles encore ouvertes dont la fin prévue est passée.
    /// </summary>
    [HttpGet("mine")]
    [Authorize(Roles = "Technician")]
    [ProducesResponseType(typeof(IReadOnlyList<InterventionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetMine(
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetMyInterventionsQuery(from, to), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error!);
    }

    /// <summary>Récupère une intervention par son identifiant.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(InterventionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetInterventionByIdQuery(id), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : MapError(result.Error!);
    }

    /// <summary>Récupère les interventions liées à un véhicule. Non-Admin : enseigne propre uniquement.</summary>
    [HttpGet("vehicle/{vehicleId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<InterventionDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetByVehicle(Guid vehicleId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetInterventionsByVehicleQuery(vehicleId), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : MapError(result.Error!);
    }

    /// <summary>Crée une nouvelle intervention. Non-Admin : storeId imposé par le JWT.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(InterventionDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Create([FromBody] CreateInterventionRequest request, CancellationToken cancellationToken)
    {
        var command = new CreateInterventionCommand(
            request.VehicleId, request.StoreId, request.TechnicianId,
            request.Type, request.PlannedStartDate, request.PlannedEndDate, request.Comment);

        var result = await _mediator.Send(command, cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value!.Id }, result.Value)
            : MapError(result.Error!);
    }

    /// <summary>Modifie le statut d'une intervention. À la clôture, le kilométrage relevé met à jour le véhicule.</summary>
    [HttpPatch("{id:guid}/status")]
    [ProducesResponseType(typeof(InterventionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ChangeStatus(Guid id, [FromBody] ChangeInterventionStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new ChangeInterventionStatusCommand(id, request.NewStatus, request.Comment, request.Mileage),
            cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : MapError(result.Error!);
    }
}
