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
}
