using FleetManager.Application.Auth.Commands;
using FleetManager.Application.Validators;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace FleetManager.Tests.Security;

/// <summary>
/// Tests de sécurité sur le validateur de la commande de connexion.
/// Vérifie les règles FluentValidation : email obligatoire et bien formé,
/// mot de passe obligatoire, résistance aux tentatives d'injection,
/// et rejet des espaces seuls.
/// </summary>
public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    // ── Commande valide ───────────────────────────────────────────────────────

    [Fact]
    public void Validate_CommandeValide_EstValide()
    {
        //Étant donné
        var command = new LoginCommand("admin@fleet.fr", "MonMotDePasse1");

        //Quand
        var result = _validator.TestValidate(command);

        //Alors
        result.ShouldNotHaveAnyValidationErrors();
    }

    // ── Validation de l'email ────────────────────────────────────────────────

    [Fact]
    public void Validate_EmailVide_RetourneErreur()
    {
        //Étant donné
        var command = new LoginCommand("", "MonMotDePasse1");

        //Quand
        var result = _validator.TestValidate(command);

        //Alors
        result.ShouldHaveValidationErrorFor(x => x.Email)
              .WithErrorMessage("L'email est requis.");
    }

    [Fact]
    public void Validate_EmailEspacesSeuls_RetourneErreur()
    {
        //Étant donné — un email composé uniquement d'espaces doit être rejeté
        var command = new LoginCommand("   ", "MonMotDePasse1");

        //Quand
        var result = _validator.TestValidate(command);

        //Alors
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("pasunformat")]
    [InlineData("@sanslocal.fr")]
    [InlineData("sansdot@domaine")]
    [InlineData("deux@@arobase.fr")]
    public void Validate_EmailMalFormate_RetourneErreurFormat(string email)
    {
        //Étant donné
        var command = new LoginCommand(email, "MonMotDePasse1");

        //Quand
        var result = _validator.TestValidate(command);

        //Alors
        result.ShouldHaveValidationErrorFor(x => x.Email)
              .WithErrorMessage("Format d'email invalide.");
    }

    // ── Injection dans le champ email ─────────────────────────────────────────

    [Theory]
    [InlineData("' OR '1'='1")]
    [InlineData("admin'--@fleet.fr")]
    [InlineData("<script>@evil.com")]
    [InlineData("user@fleet.fr; DROP TABLE Users;--")]
    public void Validate_EmailAvecInjection_RetourneErreurFormat(string emailMalveillant)
    {
        //Étant donné
        var command = new LoginCommand(emailMalveillant, "MonMotDePasse1");

        //Quand
        var result = _validator.TestValidate(command);

        //Alors — l'injection est rejetée par la validation de format email
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    // ── Validation du mot de passe ────────────────────────────────────────────

    [Fact]
    public void Validate_MotDePasseVide_RetourneErreur()
    {
        //Étant donné
        var command = new LoginCommand("admin@fleet.fr", "");

        //Quand
        var result = _validator.TestValidate(command);

        //Alors
        result.ShouldHaveValidationErrorFor(x => x.Password)
              .WithErrorMessage("Le mot de passe est requis.");
    }

    [Fact]
    public void Validate_MotDePasseEspacesSeuls_RetourneErreur()
    {
        //Étant donné — un mot de passe composé uniquement d'espaces doit être rejeté
        var command = new LoginCommand("admin@fleet.fr", "   ");

        //Quand
        var result = _validator.TestValidate(command);

        //Alors
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    // ── Combinaisons de champs invalides ─────────────────────────────────────

    [Fact]
    public void Validate_EmailEtMotDePasseVides_RetourneDeuxErreurs()
    {
        //Étant donné
        var command = new LoginCommand("", "");

        //Quand
        var result = _validator.TestValidate(command);

        //Alors
        result.ShouldHaveValidationErrorFor(x => x.Email);
        result.ShouldHaveValidationErrorFor(x => x.Password);
        result.Errors.Should().HaveCount(2);
    }
}
