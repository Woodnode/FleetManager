using FleetManager.Api.Infrastructure;
using FluentAssertions;
using Microsoft.AspNetCore.Http;

namespace FleetManager.Tests.Security;

/// <summary>
/// Les cookies d'authentification restent HttpOnly et Secure quelle que soit la configuration ;
/// seul SameSite s'adapte au mode de déploiement (même origine en production, deux ports en local).
/// </summary>
public class AuthCookiePolicyTests
{
    [Theory]
    [InlineData("Lax",    SameSiteMode.Lax)]
    [InlineData("lax",    SameSiteMode.Lax)]
    [InlineData(" Strict ", SameSiteMode.Strict)]
    [InlineData("None",   SameSiteMode.None)]
    [InlineData("",       SameSiteMode.Lax)]
    [InlineData(null,     SameSiteMode.Lax)]
    public void Configuration_EstLue(string? configured, SameSiteMode expected)
    {
        AuthCookiePolicy.FromConfiguration(configured).SameSite.Should().Be(expected);
    }

    [Fact]
    public void SansConfiguration_LaValeurParDefautEstLax()
    {
        // Un déploiement qui oublie Auth__CookieSameSite ne doit pas retomber sur None (cookies envoyés en intersite).
        AuthCookiePolicy.FromConfiguration(null).SameSite.Should().Be(SameSiteMode.Lax);
    }

    [Fact]
    public void ValeurInvalide_ArreteLeDemarrage()
    {
        var act = () => AuthCookiePolicy.FromConfiguration("laxx");

        act.Should().Throw<InvalidOperationException>().WithMessage("*None, Lax, Strict*");
    }

    [Fact]
    public void CookiesToujoursHttpOnlyEtSecure()
    {
        foreach (var value in new[] { "None", "Lax", "Strict" })
        {
            var options = AuthCookiePolicy.FromConfiguration(value).Build();
            options.HttpOnly.Should().BeTrue();
            options.Secure.Should().BeTrue();
        }
    }

    [Fact]
    public void CookieDeRafraichissement_EstLimiteASonChemin()
    {
        // Le cookie est posé avec ce chemin : sans lui, la suppression à la déconnexion n'a aucun effet.
        var options = AuthCookiePolicy.FromConfiguration("Lax")
            .Build(expires: DateTimeOffset.UtcNow.AddDays(7), path: AuthCookiePolicy.RefreshTokenPath);

        // Routes d'authentification seulement : /auth/refresh et /auth/logout le reçoivent.
        options.Path.Should().Be("/api/v1/auth");
        options.Expires.Should().NotBeNull();
    }
}
