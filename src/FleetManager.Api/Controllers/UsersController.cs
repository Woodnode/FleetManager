using FleetManager.Api.DTOs.Requests;
using FleetManager.Application.Users.Commands;
using FleetManager.Application.Users.Queries;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FleetManager.Api.Controllers;

[Authorize]
public class UsersController : ApiControllerBase
{
    private readonly IMediator _mediator;

    public UsersController(IMediator mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Récupère les techniciens d'une enseigne.
    /// Admin : peut interroger n'importe quelle enseigne.
    /// StoreManager/Technicien : limité à sa propre enseigne.
    /// </summary>
    [HttpGet("technicians/{storeId:guid}")]
    [ProducesResponseType(typeof(IReadOnlyList<TechnicianDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetTechniciansByStore(Guid storeId, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetTechniciansByStoreQuery(storeId), cancellationToken);

        return result.IsSuccess
            ? Ok(result.Value)
            : MapError(result.Error!);
    }

    /// <summary>
    /// Active ou désactive un compte. Réservé aux administrateurs. Un compte désactivé ne peut plus
    /// se connecter, et ses refresh tokens sont révoqués.
    /// </summary>
    [HttpPatch("{id:guid}/active")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> SetActive(Guid id, [FromBody] SetUserActiveRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new SetUserActiveCommand(id, request.IsActive), cancellationToken);

        return result.IsSuccess
            ? NoContent()
            : MapError(result.Error!);
    }
}
