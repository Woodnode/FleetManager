using FleetManager.Application.Auth.Commands;
using FleetManager.Application.Validators;
using FleetManager.Domain.Enums;
using FluentAssertions;
using FluentValidation.TestHelper;

namespace FleetManager.Tests.Security;

/// <summary>
/// Tests de sécurité sur le validateur d'inscription.
/// Vérifie la politique de mot de passe (longueur, majuscule, chiffre),
/// la validation des champs obligatoires, les limites de longueur,
/// les frontières exactes et la résistance aux injections.
/// </summary>
public class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();

    private static RegisterCommand CommandeValide(
        string email    = "nouveau@fleet.fr",
        string password = "Fleet@2024",
        string firstName = "Jean",
        string lastName  = "Dupont",
        UserRole role    = UserRole.Admin,
        Guid? storeId    = null)
        => new(firstName, lastName, email, password, role, storeId);

    // ── Commande valide ───────────────────────────────────────────────────────

    [Fact]
    public void Validate_CommandeValide_EstValide()
    {
        //Étant donné / Quand
        var result = _validator.TestValidate(CommandeValide());

        //Alors
        result.ShouldNotHaveAnyValidationErrors();
    }

    // ── Politique de mot de passe ─────────────────────────────────────────────

    [Fact]
    public void Validate_MotDePasseTropCourt_RetourneErreurLongueur()
    {
        //Étant donné — 7 caractères, minimum 8 requis
        var command = CommandeValide(password: "Short1A");

        //Quand
        var result = _validator.TestValidate(command);

        //Alors
        result.ShouldHaveValidationErrorFor(x => x.Password)
              .WithErrorMessage("Le mot de passe doit contenir au moins 8 caractères.");
    }

    [Fact]
    public void Validate_MotDePasseExactement7Caracteres_RetourneErreur()
    {
        //Étant donné — frontière basse : 7 chars exactement
        var command = CommandeValide(password: "Fleet@2");

        //Quand
        var result = _validator.TestValidate(command);

        //Alors
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Validate_MotDePasseExactement8Caracteres_EstValide()
    {
        //Étant donné — frontière haute : 8 chars exactement (minimum légal)
        var command = CommandeValide(password: "Fleet@28");

        //Quand
        var result = _validator.TestValidate(command);

        //Alors
        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    [Fact]
    public void Validate_MotDePasseSansMajuscule_RetourneErreur()
    {
        //Étant donné
        var command = CommandeValide(password: "fleet@2024");

        //Quand
        var result = _validator.TestValidate(command);

        //Alors
        result.ShouldHaveValidationErrorFor(x => x.Password)
              .WithErrorMessage("Le mot de passe doit contenir au moins une majuscule.");
    }

    [Fact]
    public void Validate_MotDePasseSansChiffre_RetourneErreur()
    {
        //Étant donné
        var command = CommandeValide(password: "Fleet@xxxx");

        //Quand
        var result = _validator.TestValidate(command);

        //Alors
        result.ShouldHaveValidationErrorFor(x => x.Password)
              .WithErrorMessage("Le mot de passe doit contenir au moins un chiffre.");
    }

    [Fact]
    public void Validate_MotDePasseVide_RetourneErreur()
    {
        //Étant donné
        var command = CommandeValide(password: "");

        //Quand
        var result = _validator.TestValidate(command);

        //Alors
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }

    [Theory]
    [InlineData("Fleet@2024")]
    [InlineData("Abcdefg8")]
    [InlineData("SuperSecret1!")]
    public void Validate_MotDePasseValide_AucuneErreur(string password)
    {
        //Étant donné / Quand
        var result = _validator.TestValidate(CommandeValide(password: password));

        //Alors
        result.ShouldNotHaveValidationErrorFor(x => x.Password);
    }

    // ── Validation email ──────────────────────────────────────────────────────

    [Fact]
    public void Validate_EmailMalFormate_RetourneErreur()
    {
        //Étant donné
        var command = CommandeValide(email: "pasunemail");

        //Quand
        var result = _validator.TestValidate(command);

        //Alors
        result.ShouldHaveValidationErrorFor(x => x.Email)
              .WithErrorMessage("Format d'email invalide.");
    }

    [Fact]
    public void Validate_EmailVide_RetourneErreur()
    {
        //Étant donné
        var command = CommandeValide(email: "");

        //Quand
        var result = _validator.TestValidate(command);

        //Alors
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Theory]
    [InlineData("admin'--@fleet.fr")]
    [InlineData("user@fleet.fr; DROP TABLE Users;--")]
    [InlineData("<script>@evil.com")]
    public void Validate_EmailAvecInjection_RetourneErreur(string emailMalveillant)
    {
        //Étant donné — les injections dans l'email doivent être rejetées à l'inscription aussi
        var command = CommandeValide(email: emailMalveillant);

        //Quand
        var result = _validator.TestValidate(command);

        //Alors
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    // ── Validation des champs texte ───────────────────────────────────────────

    [Fact]
    public void Validate_PrenomVide_RetourneErreur()
    {
        //Étant donné
        var command = CommandeValide(firstName: "");

        //Quand
        var result = _validator.TestValidate(command);

        //Alors
        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Fact]
    public void Validate_PrenomEspacesSeuls_RetourneErreur()
    {
        //Étant donné — espaces seuls = vide pour FluentValidation
        var command = CommandeValide(firstName: "   ");

        //Quand
        var result = _validator.TestValidate(command);

        //Alors
        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Fact]
    public void Validate_NomVide_RetourneErreur()
    {
        //Étant donné
        var command = CommandeValide(lastName: "");

        //Quand
        var result = _validator.TestValidate(command);

        //Alors
        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Fact]
    public void Validate_NomEspacesSeuls_RetourneErreur()
    {
        //Étant donné — espaces seuls = vide pour FluentValidation
        var command = CommandeValide(lastName: "   ");

        //Quand
        var result = _validator.TestValidate(command);

        //Alors
        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    [Fact]
    public void Validate_PrenomDepasse100Caracteres_RetourneErreur()
    {
        //Étant donné
        var command = CommandeValide(firstName: new string('A', 101));

        //Quand
        var result = _validator.TestValidate(command);

        //Alors
        result.ShouldHaveValidationErrorFor(x => x.FirstName);
    }

    [Fact]
    public void Validate_NomDepasse100Caracteres_RetourneErreur()
    {
        //Étant donné
        var command = CommandeValide(lastName: new string('Z', 101));

        //Quand
        var result = _validator.TestValidate(command);

        //Alors
        result.ShouldHaveValidationErrorFor(x => x.LastName);
    }

    // ── Validation du rôle ────────────────────────────────────────────────────

    [Fact]
    public void Validate_RoleInvalide_RetourneErreur()
    {
        //Étant donné — rôle hors de l'enum
        var command = CommandeValide(role: (UserRole)999);

        //Quand
        var result = _validator.TestValidate(command);

        //Alors
        result.ShouldHaveValidationErrorFor(x => x.Role);
    }
}
