using FleetManager.Application.Common;
using FleetManager.Application.Interfaces;
using MediatR;
using Microsoft.Extensions.Logging;

namespace FleetManager.Application.Behaviors;

public class AuditBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ILogger<AuditBehavior<TRequest, TResponse>> _logger;
    private readonly ICurrentUserService _currentUser;

    public AuditBehavior(
        ILogger<AuditBehavior<TRequest, TResponse>> logger,
        ICurrentUserService currentUser)
    {
        _logger      = logger;
        _currentUser = currentUser;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        var userId      = _currentUser.UserId?.ToString() ?? "anonymous";

        _logger.LogInformation(
            "Executing {Request} — user: {UserId}",
            requestName, userId);

        var response = await next();

        // Log success/failure when the response carries a Result
        if (response is Result result)
        {
            if (result.IsSuccess)
                _logger.LogInformation("Completed {Request} — success", requestName);
            else
                _logger.LogWarning(
                    "Completed {Request} — failure: [{Code}] {Message}",
                    requestName, result.Error!.Code, result.Error.Message);
        }
        else
        {
            _logger.LogInformation("Completed {Request}", requestName);
        }

        return response;
    }
}
