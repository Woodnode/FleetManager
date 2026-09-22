using Asp.Versioning;
using FleetManager.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace FleetManager.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
public abstract class ApiControllerBase : ControllerBase
{
    // FORBIDDEN intentionally maps to NotFound (IDOR protection: don't reveal resource existence).
    // Problem(...) fixe déjà le code HTTP : l'envelopper dans NotFound()/Conflict() sérialisait
    // l'ObjectResult lui-même ({ value, formatters, contentTypes, ... }) au lieu d'un ProblemDetails.
    protected IActionResult MapError(Error error) => error.Code switch
    {
        "NOT_FOUND" or "FORBIDDEN" => Problem(
                            title:    "Resource not found.",
                            detail:   error.Message,
                            statusCode: StatusCodes.Status404NotFound,
                            type:     "https://tools.ietf.org/html/rfc7231#section-6.5.4"),
        "CONFLICT"     => Problem(
                            title:    "Conflict.",
                            detail:   error.Message,
                            statusCode: StatusCodes.Status409Conflict,
                            type:     "https://tools.ietf.org/html/rfc7231#section-6.5.8"),
        "UNAUTHORIZED" => Problem(
                            title:    "Unauthorized.",
                            detail:   error.Message,
                            statusCode: StatusCodes.Status401Unauthorized,
                            type:     "https://tools.ietf.org/html/rfc7235#section-3.1"),
        _              => Problem(
                            title:    "Bad request.",
                            detail:   error.Message,
                            statusCode: StatusCodes.Status400BadRequest,
                            type:     "https://tools.ietf.org/html/rfc7231#section-6.5.1")
    };
}
