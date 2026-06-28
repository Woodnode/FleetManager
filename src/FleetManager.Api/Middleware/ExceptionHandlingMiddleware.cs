using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;

namespace FleetManager.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next   = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (ValidationException ex)
        {
            await HandleValidationExceptionAsync(context, ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");
            await HandleUnexpectedExceptionAsync(context);
        }
    }

    private static Task HandleValidationExceptionAsync(HttpContext context, ValidationException ex)
    {
        var errors = ex.Errors
            .GroupBy(e => e.PropertyName)
            .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());

        var problem = new ValidationProblemDetails(errors)
        {
            Type   = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            Title  = "Validation failed",
            Status = (int)HttpStatusCode.BadRequest,
            Instance = context.Request.Path
        };

        return WriteProblemAsync(context, problem);
    }

    private static Task HandleUnexpectedExceptionAsync(HttpContext context)
    {
        // Never expose exception details — already logged above.
        var problem = new ProblemDetails
        {
            Type     = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
            Title    = "An unexpected error occurred.",
            Status   = (int)HttpStatusCode.InternalServerError,
            Instance = context.Request.Path
        };

        return WriteProblemAsync(context, problem);
    }

    private static Task WriteProblemAsync(HttpContext context, ProblemDetails problem)
    {
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode  = problem.Status ?? (int)HttpStatusCode.InternalServerError;

        var options = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
        return context.Response.WriteAsync(JsonSerializer.Serialize(problem, options));
    }
}
