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
    protected IActionResult MapError(Error error) => error.Code switch
    {
        "NOT_FOUND"    => NotFound(Problem(
                            title:    "Resource not found.",
                            detail:   error.Message,
                            statusCode: 404,
                            type:     "https://tools.ietf.org/html/rfc7231#section-6.5.4")),
        "FORBIDDEN"    => NotFound(Problem(
                            title:    "Resource not found.",
                            detail:   error.Message,
                            statusCode: 404,
                            type:     "https://tools.ietf.org/html/rfc7231#section-6.5.4")),
        "CONFLICT"     => Conflict(Problem(
                            title:    "Conflict.",
                            detail:   error.Message,
                            statusCode: 409,
                            type:     "https://tools.ietf.org/html/rfc7231#section-6.5.8")),
        "UNAUTHORIZED" => Unauthorized(Problem(
                            title:    "Unauthorized.",
                            detail:   error.Message,
                            statusCode: 401,
                            type:     "https://tools.ietf.org/html/rfc7235#section-3.1")),
        _              => BadRequest(Problem(
                            title:    "Bad request.",
                            detail:   error.Message,
                            statusCode: 400,
                            type:     "https://tools.ietf.org/html/rfc7231#section-6.5.1"))
    };
}
