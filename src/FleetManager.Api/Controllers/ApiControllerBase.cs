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
    protected IActionResult MapError(Error error)
    {
        var (status, title, type) = error.Code switch
        {
            "NOT_FOUND" or "FORBIDDEN" => (StatusCodes.Status404NotFound, "Resource not found.", "https://tools.ietf.org/html/rfc7231#section-6.5.4"),
            "CONFLICT"     => (StatusCodes.Status409Conflict,     "Conflict.",     "https://tools.ietf.org/html/rfc7231#section-6.5.8"),
            "UNAUTHORIZED" => (StatusCodes.Status401Unauthorized, "Unauthorized.", "https://tools.ietf.org/html/rfc7235#section-3.1"),
            _              => (StatusCodes.Status400BadRequest,   "Bad request.",  "https://tools.ietf.org/html/rfc7231#section-6.5.1")
        };

        var result = (ObjectResult)Problem(title: title, detail: error.Message, statusCode: status, type: type);

        // Les détails d'une erreur FORBIDDEN ne sont jamais exposés : elle se présente comme un 404.
        if (error.Details is not null && error.Code != "FORBIDDEN" && result.Value is ProblemDetails problem)
            foreach (var (key, value) in error.Details)
                problem.Extensions[key] = value;

        return result;
    }
}
