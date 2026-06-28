using FleetManager.Domain.Exceptions;
using FleetManager.Domain.ValueObjects;
using FluentAssertions;

namespace FleetManager.Tests.Security;

/// <summary>
/// Tests de sécurité sur le Value Object Vin.
/// Vérifie : normalisation en majuscules, longueur exacte de 17 caractères,
/// restriction aux caractères alphanumériques ASCII, et résistance aux injections.
/// </summary>
public class VinValueObjectTests
{
    private const string ValidVin = "1HGBH41JXMN109186";

    // ── Cas valides ───────────────────────────────────────────────────────────

    [Fact]
    public void Create_AvecVinValide_RetourneVin()
    {
        //Étant donné / Quand
        var vin = Vin.Create(ValidVin);

        //Alors
        vin.Value.Should().Be(ValidVin);
    }

    [Fact]
    public void Create_AvecMinuscules_ConvertiEnMajuscules()
    {
        //Étant donné — les VINs sont normalisés en majuscules
        var vin = Vin.Create("1hgbh41jxmn109186");

        //Alors
        vin.Value.Should().Be("1HGBH41JXmn109186".ToUpperInvariant());
    }

    [Fact]
    public void Create_AvecEspacesAutour_RetourneVinSansEspaces()
    {
        //Étant donné
        var vin = Vin.Create("  " + ValidVin + "  ");

        //Alors
        vin.Value.Should().Be(ValidVin);
    }

    [Fact]
    public void Create_AvecDeuxVinsIdentiques_SontEgaux()
    {
        //Étant donné
        var vin1 = Vin.Create(ValidVin);
        var vin2 = Vin.Create(ValidVin.ToLower());

        //Alors
        vin1.Should().Be(vin2);
    }

    [Fact]
    public void Create_AvecDeuxVinsDifferents_NeSontPasEgaux()
    {
        //Étant donné
        var vin1 = Vin.Create(ValidVin);
        var vin2 = Vin.Create("2HGBH41JXMN109186");

        //Alors
        vin1.Should().NotBe(vin2);
    }

    [Fact]
    public void ToString_RetourneVinEnMajuscules()
    {
        //Étant donné
        var vin = Vin.Create("1hgbh41jxmn109186");

        //Alors
        vin.ToString().Should().Be("1HGBH41JXmn109186".ToUpperInvariant());
    }

    [Fact]
    public void GetHashCode_DeuxVinsEgaux_RetourneMemHashCode()
    {
        //Étant donné
        var vin1 = Vin.Create(ValidVin);
        var vin2 = Vin.Create(ValidVin.ToLower());

        //Alors
        vin1.GetHashCode().Should().Be(vin2.GetHashCode());
    }

    // ── Cas invalides — champs vides ─────────────────────────────────────────

    [Fact]
    public void Create_AvecNull_LanceDomainException()
    {
        //Étant donné
        var act = () => Vin.Create(null!);

        //Alors
        act.Should().Throw<DomainException>().WithMessage("*empty*");
    }

    [Fact]
    public void Create_AvecChaineVide_LanceDomainException()
    {
        //Étant donné
        var act = () => Vin.Create("");

        //Alors
        act.Should().Throw<DomainException>().WithMessage("*empty*");
    }

    [Fact]
    public void Create_AvecEspacesSeuls_LanceDomainException()
    {
        //Étant donné
        var act = () => Vin.Create("   ");

        //Alors
        act.Should().Throw<DomainException>().WithMessage("*empty*");
    }

    // ── Longueur exacte (frontières) ────────────────────────────────────────

    [Fact]
    public void Create_AvecVin16Caracteres_LanceDomainException()
    {
        //Étant donné — un VIN de 16 chars doit être rejeté (trop court)
        var act = () => Vin.Create("1HGBH41JXMN10918");

        //Alors
        act.Should().Throw<DomainException>().WithMessage("*17*");
    }

    [Fact]
    public void Create_AvecVin18Caracteres_LanceDomainException()
    {
        //Étant donné — un VIN de 18 chars doit être rejeté (trop long)
        var act = () => Vin.Create("1HGBH41JXMN1091867");

        //Alors
        act.Should().Throw<DomainException>().WithMessage("*17*");
    }

    // ── Résistance aux injections via le champ VIN ────────────────────────────
    // Un VIN doit contenir exactement 17 caractères alphanumériques ASCII.
    // Ces chaînes ont toutes 17 caractères mais contiennent des caractères interdits.

    [Theory]
    [InlineData("1HGBH41JXMN109'86")]  // apostrophe (SQL injection)
    [InlineData("1HGBH41J<script>X")]   // balise HTML (XSS) — exactement 17 chars
    [InlineData("' OR 1=1 --AAAAAA")]   // SQL injection classique
    [InlineData("1HGBH41JXMN10918;")]   // point-virgule (injection commande)
    public void Create_AvecCaracteresSpeciaux17Chars_LanceDomainException(string vin)
    {
        //Étant donné
        var act = () => Vin.Create(vin);

        //Alors — rejeté car non-alphanumérique ASCII, même si longueur correcte
        act.Should().Throw<DomainException>().WithMessage("*alphanumeric*");
    }

    [Theory]
    [InlineData("用户@例子广告1234567")]    // caractères Unicode (12 chars Unicode ≠ 17 ASCII)
    [InlineData("ААААААААААААААААА")]     // 17 lettres cyrilliques
    public void Create_AvecCaracteresUnicode_LanceDomainException(string vin)
    {
        //Étant donné
        var act = () => Vin.Create(vin);

        //Alors — rejeté : caractères non-ASCII même si IsLetter() les accepterait
        act.Should().Throw<DomainException>();
    }
}
