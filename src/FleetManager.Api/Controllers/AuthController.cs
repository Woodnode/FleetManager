using FleetManager.Api.DTOs.Requests;
using FleetManager.Application.Auth.Commands;
using FleetManager.Application.Interfaces;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace FleetManager.Api.Controllers;

public class AuthController : ApiControllerBase
{
    private readonly IMediator _mediator;
    private readonly ICurrentUserService _currentUser;

    public AuthController(IMediator mediator, ICurrentUserService currentUser)
    {
        _mediator    = mediator;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Authentifie un utilisateur. Les tokens sont émis dans des cookies httpOnly.
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

        var dto = result.Value!;
        SetTokenCookies(dto.AccessToken, dto.RefreshToken);

        return Ok(new
        {
            userId    = dto.UserId,
            firstName = dto.FirstName,
            lastName  = dto.LastName,
            email     = dto.Email,
            role      = dto.Role.ToString(),
            storeId   = dto.StoreId,
        });
    }

    /// <summary>
    /// Émet une nouvelle paire access/refresh token à partir du refresh token en cookie.
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(CancellationToken cancellationToken)
    {
        if (!Request.Cookies.TryGetValue("refresh_token", out var refreshToken))
            return Unauthorized(new { error = "Refresh token manquant." });

        var result = await _mediator.Send(new RefreshTokenCommand(refreshToken), cancellationToken);

        if (result.IsFailure)
            return Unauthorized(new { error = result.Error!.Message });

        var dto = result.Value!;
        SetTokenCookies(dto.AccessToken, dto.RefreshToken);

        return Ok(new
        {
            userId    = dto.UserId,
            firstName = dto.FirstName,
            lastName  = dto.LastName,
            email     = dto.Email,
            role      = dto.Role.ToString(),
            storeId   = dto.StoreId,
        });
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

        var cookieOptions = new CookieOptions
        {
            HttpOnly = true,
            Secure   = true,
            SameSite = SameSiteMode.None,
        };
        Response.Cookies.Delete("access_token",  cookieOptions);
        Response.Cookies.Delete("refresh_token", cookieOptions);
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

    private void SetTokenCookies(string accessToken, string? refreshToken)
    {
        Response.Cookies.Append("access_token", accessToken, new CookieOptions
        {
            HttpOnly = true,
            Secure   = true,
            SameSite = SameSiteMode.None,
            Expires  = DateTimeOffset.UtcNow.AddMinutes(15)
        });

        if (refreshToken is not null)
        {
            Response.Cookies.Append("refresh_token", refreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure   = true,
                SameSite = SameSiteMode.None,
                Expires  = DateTimeOffset.UtcNow.AddDays(7),
                Path     = "/api/v1/auth/refresh"
            });
        }
    }
}
