using FleetManager.Api.DTOs.Requests;
using FleetManager.Application.Stores.Commands;
using FleetManager.Application.Stores.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FleetManager.Application.Common;

namespace FleetManager.Api.Controllers;

[Authorize]
public class StoresController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public StoresController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>Récupère toutes les enseignes.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<StoreDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAllStoresQuery(), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error!);
    }

    /// <summary>Crée une nouvelle enseigne. Réservé aux administrateurs.</summary>
    [HttpPost]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(StoreDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateStoreRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new CreateStoreCommand(request.Name, request.Address, request.PostalCode, request.City),
            cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetAll), result.Value)
            : MapError(result.Error!);
    }

    /// <summary>Met à jour une enseigne existante. Réservé aux administrateurs.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(typeof(StoreDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateStoreRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new UpdateStoreCommand(id, request.Name, request.Address, request.PostalCode, request.City),
            cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : MapError(result.Error!);
    }

    /// <summary>Supprime une enseigne. Échoue si elle contient encore des véhicules.</summary>
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new DeleteStoreCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : MapError(result.Error!);
    }
}
