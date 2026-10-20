using FleetManager.Api.Infrastructure;
using FleetManager.Api.DTOs.Requests;
using FleetManager.Application.Auth.Commands;
using FleetManager.Application.Auth.Queries;
using FleetManager.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.RateLimiting;

namespace FleetManager.Api.Controllers;

public class AuthController : ApiControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;
    private readonly AuthCookiePolicy _cookies;

    public AuthController(IMediator mediator, ICurrentUserService currentUser, AuthCookiePolicy cookies)
    {
        _mediator    = mediator;
        _currentUser = currentUser;
        _cookies = cookies;
    }

    /// <summary>
    /// Authentifie un utilisateur. Les tokens sont émis dans des cookies httpOnly, ou dans le corps
    /// pour l'application mobile (en-tête <c>X-Client: mobile</c>).
    /// </summary>
    [HttpPost("login")]
    [EnableRateLimiting("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new LoginCommand(request.Email, request.Password), cancellationToken);

        if (result.IsFailure)
            return Unauthorized(new { error = result.Error!.Message });

        return Ok(StartSession(result.Value!));
    }

    /// <summary>
    /// Émet une nouvelle paire access/refresh token à partir du refresh token en cookie, ou dans le
    /// corps pour l'application mobile.
    /// </summary>
    [HttpPost("refresh")]
    [EnableRateLimiting("refresh")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(
        [FromBody(EmptyBodyBehavior = EmptyBodyBehavior.Allow)] RefreshTokenRequest? request,
        CancellationToken cancellationToken)
    {
        // Le mobile ne lit jamais le cookie : une page tierce qui se ferait passer pour lui ne
        // pourrait pas changer le cookie d'un navigateur en jetons lisibles dans le corps.
        var refreshToken = NativeClient.IsRequestedBy(Request)
            ? request?.RefreshToken
            : Request.Cookies.TryGetValue(AuthCookiePolicy.RefreshTokenCookie, out var cookieToken) ? cookieToken : null;

        if (string.IsNullOrWhiteSpace(refreshToken))
            return Unauthorized(new { error = "Refresh token manquant." });

        var result = await _mediator.Send(new RefreshTokenCommand(refreshToken), cancellationToken);

        if (result.IsFailure)
            return Unauthorized(new { error = result.Error!.Message });

        return Ok(StartSession(result.Value!));
    }

    /// <summary>
    /// Retourne les informations de l'utilisateur authentifié (vérifie le cookie httpOnly).
    /// </summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult Me()
    {
        return Ok(new
        {
            userId    = _currentUser.UserId,
            firstName = _currentUser.FirstName,
            lastName  = _currentUser.LastName,
            role      = _currentUser.Role?.ToString(),
            storeId   = _currentUser.StoreId,
        });
    }

    /// <summary>
    /// Déconnecte l'utilisateur : révoque le JTI et les refresh tokens, puis supprime les cookies.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await _mediator.Send(new LogoutCommand(), cancellationToken);

        // Mêmes attributs qu'à la pose, sinon le navigateur ne supprime pas les cookies.
        Response.Cookies.Delete(AuthCookiePolicy.AccessTokenCookie,  _cookies.Build());
        Response.Cookies.Delete(AuthCookiePolicy.RefreshTokenCookie, _cookies.Build(path: AuthCookiePolicy.RefreshTokenPath));
        return NoContent();
    }

    /// <summary>
    /// Crée un nouveau compte utilisateur. Réservé aux administrateurs.
    /// </summary>
    [HttpPost("register")]
    [Authorize(Roles = "Admin")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request, CancellationToken cancellationToken)
    {
        var command = new RegisterCommand(
            request.FirstName, request.LastName, request.Email,
            request.Password, request.Role, request.StoreId);

        var result = await _mediator.Send(command, cancellationToken);

        return result.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, result.Value)
            : MapError(result.Error!);
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private object StartSession(AuthResultDto dto)
    {
        if (NativeClient.IsRequestedBy(Request))
        {
            return new
            {
                userId       = dto.UserId,
                firstName    = dto.FirstName,
                lastName     = dto.LastName,
                email        = dto.Email,
                role         = dto.Role.ToString(),
                storeId      = dto.StoreId,
                accessToken  = dto.AccessToken,
                refreshToken = dto.RefreshToken,
            };
        }

        SetTokenCookies(dto.AccessToken, dto.RefreshToken);
        return new
        {
            userId    = dto.UserId,
            firstName = dto.FirstName,
            lastName  = dto.LastName,
            email     = dto.Email,
            role      = dto.Role.ToString(),
            storeId   = dto.StoreId,
        };
    }

    private void SetTokenCookies(string accessToken, string? refreshToken)
    {
        Response.Cookies.Append(AuthCookiePolicy.AccessTokenCookie, accessToken,
            _cookies.Build(expires: DateTimeOffset.UtcNow.AddMinutes(15)));

        if (refreshToken is not null)
        {
            Response.Cookies.Append(AuthCookiePolicy.RefreshTokenCookie, refreshToken,
                _cookies.Build(expires: DateTimeOffset.UtcNow.AddDays(7), path: AuthCookiePolicy.RefreshTokenPath));
        }
    }
}
