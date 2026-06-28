using FleetManager.Infrastructure.Services;
using FluentAssertions;

namespace FleetManager.Tests.Security;

/// <summary>
/// Tests de sécurité sur le service de hachage des mots de passe (BCrypt).
/// Vérifie : irréversibilité, salage aléatoire, facteur de travail 12,
/// vérification correcte, rejet des mots de passe incorrects ou malveillants,
/// et robustesse aux entrées malformées.
/// </summary>
public class PasswordHasherTests
{
    // Tests via l'implémentation concrète (PasswordHasher) — même comportement que les mocks
    private readonly PasswordHasher _hasher = new();

    // Helpers en accès direct BCrypt pour les tests sur les propriétés du hash
    private static string Hash(string password)
        => BCrypt.Net.BCrypt.HashPassword(password, workFactor: 12);

    private static bool Verify(string password, string hash)
        => BCrypt.Net.BCrypt.Verify(password, hash);

    // ── Propriétés de hachage ─────────────────────────────────────────────────

    [Fact]
    public void Hash_MotDePasseValide_RetourneHashNonVide()
    {
        //Étant donné
        var motDePasse = "Fleet@2024";

        //Quand
        var hash = Hash(motDePasse);

        //Alors
        hash.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Hash_MotDePasseValide_NePasStockerEnClair()
    {
        //Étant donné
        var motDePasse = "Fleet@2024";

        //Quand
        var hash = Hash(motDePasse);

        //Alors — le hash ne doit jamais contenir le mot de passe en clair
        hash.Should().NotContain(motDePasse);
    }

    [Fact]
    public void Hash_MemeMotDePasseDeuxFois_RetourneHashsDifferents()
    {
        //Étant donné — BCrypt génère un sel aléatoire à chaque appel
        var motDePasse = "Fleet@2024";

        //Quand
        var hash1 = Hash(motDePasse);
        var hash2 = Hash(motDePasse);

        //Alors — deux hashes du même mot de passe doivent être distincts (sel différent)
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void Hash_RetourneHashAvecPrefixeBCrypt()
    {
        //Étant donné
        var hash = Hash("monMotDePasse1");

        //Alors — format BCrypt reconnaissable ($2b$ ou $2a$)
        hash.Should().MatchRegex(@"^\$2[ab]\$\d+\$");
    }

    [Fact]
    public void Hash_FacteurDeTravailEst12_EncodesDansLeHash()
    {
        //Étant donné — le facteur de travail 12 est encodé dans le hash ($2b$12$...)
        // Un facteur trop bas serait une régression de sécurité
        var hash = _hasher.Hash("Fleet@2024");

        //Alors
        // BCrypt.Net génère $2a$ ou $2b$ selon la version — les deux sont valides
        hash.Should().MatchRegex(@"^\$2[ab]\$12\$");
    }

    // ── Vérification correcte ────────────────────────────────────────────────

    [Fact]
    public void Verify_MotDePasseCorrect_RetourneTrue()
    {
        //Étant donné
        var motDePasse = "Fleet@2024";
        var hash = Hash(motDePasse);

        //Quand
        var resultat = Verify(motDePasse, hash);

        //Alors
        resultat.Should().BeTrue();
    }

    [Fact]
    public void Verify_MotDePasseIncorrect_RetourneFalse()
    {
        //Étant donné
        var hash = Hash("Fleet@2024");

        //Quand
        var resultat = Verify("mauvaisMotDePasse", hash);

        //Alors
        resultat.Should().BeFalse();
    }

    [Fact]
    public void Verify_MotDePasseVideContreHash_RetourneFalse()
    {
        //Étant donné
        var hash = Hash("Fleet@2024");

        //Quand
        var resultat = Verify("", hash);

        //Alors
        resultat.Should().BeFalse();
    }

    [Fact]
    public void Verify_MotDePasseDifferentCasse_RetourneFalse()
    {
        //Étant donné — BCrypt est sensible à la casse
        var hash = Hash("Fleet@2024");

        //Quand
        var resultat = Verify("fleet@2024", hash);

        //Alors
        resultat.Should().BeFalse();
    }

    // ── Robustesse aux entrées malformées ────────────────────────────────────

    [Fact]
    public void Verify_AvecHashMalForme_RetourneFalse()
    {
        //Étant donné — hash non-BCrypt (base de données corrompue, tentative de bypass)
        // L'implémentation doit retourner false plutôt que lever une exception
        var act = () => _hasher.Verify("Fleet@2024", "notabcrypthash");

        //Alors — pas d'exception + résultat false
        act.Should().NotThrow();
        _hasher.Verify("Fleet@2024", "notabcrypthash").Should().BeFalse();
    }

    [Fact]
    public void Verify_AvecHashVide_RetourneFalse()
    {
        //Étant donné — hash vide (champ manquant en base de données)
        var act = () => _hasher.Verify("Fleet@2024", "");

        //Alors — pas d'exception + résultat false
        act.Should().NotThrow();
        _hasher.Verify("Fleet@2024", "").Should().BeFalse();
    }

    // ── Résistance aux entrées malveillantes ──────────────────────────────────

    [Theory]
    [InlineData("' OR '1'='1")]
    [InlineData("admin'--")]
    [InlineData("<script>alert(1)</script>")]
    [InlineData("../../../../etc/passwd")]
    public void Verify_MotDePasseInjection_RetourneFalse(string motDePasseMalveillant)
    {
        //Étant donné
        var hash = Hash("Fleet@2024");

        //Quand
        var resultat = Verify(motDePasseMalveillant, hash);

        //Alors — toute injection est simplement traitée comme un mauvais mot de passe
        resultat.Should().BeFalse();
    }

    [Fact]
    public void Verify_MotDePasseTresLong_NeLancePasException()
    {
        //Étant donné — protection contre les attaques par password très long (DoS BCrypt)
        // Note: BCrypt tronque silencieusement à 72 octets — on vérifie la robustesse
        var motDePasseLong = new string('A', 1000) + "1";
        var hash = Hash(motDePasseLong);

        //Quand
        var act = () => Verify(motDePasseLong, hash);

        //Alors — ne doit pas lever d'exception
        act.Should().NotThrow();
    }
}
