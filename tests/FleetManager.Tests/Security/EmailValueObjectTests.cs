using FleetManager.Domain.Exceptions;
using FleetManager.Domain.ValueObjects;
using FluentAssertions;

namespace FleetManager.Tests.Security;

/// <summary>
/// Tests de sécurité sur le Value Object Email.
/// Vérifie la normalisation, le rejet des formats invalides et la résistance
/// aux tentatives d'injection passées via le champ email.
/// </summary>
public class EmailValueObjectTests
{
    // ── Cas valides ───────────────────────────────────────────────────────────

    [Fact]
    public void Create_AvecEmailValide_RetourneEmailNormalise()
    {
        //Étant donné
        var input = "User@Example.COM";

        //Quand
        var email = Email.Create(input);

        //Alors
        email.Value.Should().Be("user@example.com");
    }

    [Fact]
    public void Create_AvecEspacesAutourDeLEmail_RetourneEmailSansEspaces()
    {
        //Étant donné
        var input = "   admin@fleetmanager.fr   ";

        //Quand
        var email = Email.Create(input);

        //Alors
        email.Value.Should().Be("admin@fleetmanager.fr");
    }

    [Fact]
    public void Create_EmailMinimal_EstAccepte()
    {
        //Étant donné — format minimal valide selon le regex
        var act = () => Email.Create("a@b.fr");

        //Alors
        act.Should().NotThrow();
    }

    [Fact]
    public void Create_AvecDeuxEmailsIdentiquesMinMaj_SontEgaux()
    {
        //Étant donné
        var email1 = Email.Create("Admin@Fleet.fr");
        var email2 = Email.Create("admin@fleet.fr");

        //Alors
        email1.Should().Be(email2);
    }

    [Fact]
    public void Create_AvecDeuxEmailsDifferents_NeSontPasEgaux()
    {
        //Étant donné
        var email1 = Email.Create("user1@fleet.fr");
        var email2 = Email.Create("user2@fleet.fr");

        //Alors
        email1.Should().NotBe(email2);
    }

    // ── Cas invalides — champs vides ─────────────────────────────────────────

    [Fact]
    public void Create_AvecNull_LanceDomainException()
    {
        //Étant donné — null doit être rejeté comme vide
        var act = () => Email.Create(null!);

        //Alors
        act.Should().Throw<DomainException>().WithMessage("*empty*");
    }

    [Fact]
    public void Create_AvecChaineVide_LanceDomainException()
    {
        //Étant donné
        var act = () => Email.Create("");

        //Alors
        act.Should().Throw<DomainException>().WithMessage("*empty*");
    }

    [Fact]
    public void Create_AvecEspacesSeuls_LanceDomainException()
    {
        //Étant donné
        var act = () => Email.Create("   ");

        //Alors
        act.Should().Throw<DomainException>().WithMessage("*empty*");
    }

    // ── Cas invalides — format ────────────────────────────────────────────────

    [Theory]
    [InlineData("pasdearobase.com")]
    [InlineData("pasdearobase")]
    [InlineData("@sanslocal.fr")]
    public void Create_SansArobase_LanceDomainException(string input)
    {
        //Étant donné
        var act = () => Email.Create(input);

        //Alors
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("sansdot@domaine")]
    [InlineData("sansdot@")]
    public void Create_SansPoint_LanceDomainException(string input)
    {
        //Étant donné
        var act = () => Email.Create(input);

        //Alors
        act.Should().Throw<DomainException>();
    }

    [Theory]
    [InlineData("deux@@arobase.fr")]
    [InlineData("user@@domain.fr")]
    public void Create_AvecDeuxArobases_LanceDomainException(string input)
    {
        //Étant donné — deux arobase invalide le format
        var act = () => Email.Create(input);

        //Alors
        act.Should().Throw<DomainException>();
    }

    // ── Résistance aux injections ─────────────────────────────────────────────

    [Theory]
    [InlineData("' OR '1'='1")]
    [InlineData("admin'--")]
    [InlineData("admin@test.fr; DROP TABLE Users;--")]
    [InlineData("<script>alert('xss')</script>@evil.com")]
    [InlineData("a@b.com\r\nBcc: victim@evil.com")]
    public void Create_AvecTentativeInjection_LanceDomainException(string input)
    {
        //Étant donné
        var act = () => Email.Create(input);

        //Alors
        act.Should().Throw<DomainException>();
    }

    // ── Résistance aux caractères Unicode ────────────────────────────────────

    [Theory]
    [InlineData("用户@例子.广告")]
    [InlineData("utilisateur@domainé.fr")]
    public void Create_AvecCaracteresUnicode_LanceDomainException(string input)
    {
        //Étant donné — le regex restreint aux caractères ASCII standard
        var act = () => Email.Create(input);

        //Alors
        act.Should().Throw<DomainException>();
    }

    // ── Résistance aux entrées très longues (potentiel DoS regex) ────────────

    [Fact]
    public void Create_AvecEmailTresLong_LanceDomainException()
    {
        //Étant donné — entrée de 10 000 caractères
        var input = new string('a', 9996) + "@b.fr";

        //Quand
        var act = () => Email.Create(input);

        //Alors — rejeté (regex refuse la partie locale de 9996 chars)
        // Vérifie aussi l'absence de ReDoS (pas de timeout)
        act.Should().Throw<DomainException>();
    }

    // ── Propriétés du Value Object ────────────────────────────────────────────

    [Fact]
    public void ToString_RetourneValeurNormalisee()
    {
        //Étant donné
        var email = Email.Create("USER@FLEET.FR");

        //Alors
        email.ToString().Should().Be("user@fleet.fr");
    }

    [Fact]
    public void GetHashCode_DeuxEmailsEgaux_RetourneMemHashCode()
    {
        //Étant donné
        var email1 = Email.Create("user@fleet.fr");
        var email2 = Email.Create("USER@FLEET.FR");

        //Alors
        email1.GetHashCode().Should().Be(email2.GetHashCode());
    }
}
