namespace FleetManager.Api.Middleware;

public class SecurityHeadersMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context)
    {
        context.Response.Headers["X-Content-Type-Options"]  = "nosniff";
        context.Response.Headers["X-Frame-Options"]         = "DENY";
        context.Response.Headers["Referrer-Policy"]         = "strict-origin-when-cross-origin";
        context.Response.Headers["Permissions-Policy"]      = "geolocation=(), microphone=(), camera=()";
        context.Response.Headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
        await next(context);
    }
}
