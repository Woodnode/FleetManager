using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;
using FleetManager.Infrastructure.Services;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace FleetManager.Tests.Security;

/// <summary>
/// Tests de sécurité sur le service de génération de tokens JWT.
/// Vérifie : algorithme de signature (HS256, jamais 'none'), présence des claims
/// obligatoires, storeId conditionnel selon le rôle, expiration, unicité (jti),
/// issuer, audience et validité des identifiants (GUIDs).
/// </summary>
public class JwtTokenGeneratorTests
{
    private const string Secret        = "TestSecretKey_MinimumLength_32Chars!!";
    private const string Issuer        = "FleetManagerApi";
    private const string Audience      = "FleetManagerClient";
    private const int    ExpiryMinutes = 60;

    private readonly JwtTokenGenerator _generator;

    public JwtTokenGeneratorTests()
    {
        var settings = Options.Create(new JwtSettings
        {
            Secret        = Secret,
            Issuer        = Issuer,
            Audience      = Audience,
            ExpiryMinutes = ExpiryMinutes,
        });

        _generator = new JwtTokenGenerator(settings);
    }

    private static User BuildAdmin()
        => User.Create("Sophie", "Martin", "admin@fleet.fr",
                       "$2b$12$FakeHash", UserRole.Admin);

    private static User BuildTech()
        => User.Create("Lucas", "Moreau", "tech@fleet.fr",
                       "$2b$12$FakeHash", UserRole.Technician, Guid.NewGuid());

    private static User BuildManager()
        => User.Create("Thomas", "Dupont", "manager@fleet.fr",
                       "$2b$12$FakeHash", UserRole.StoreManager, Guid.NewGuid());

    private static JwtSecurityToken Decode(string token)
        => new JwtSecurityTokenHandler().ReadJwtToken(token);

    // ── Token bien formé ─────────────────────────────────────────────────────

    [Fact]
    public void GenerateToken_UtilisateurValide_RetourneTokenNonVide()
    {
        //Étant donné
        var user = BuildAdmin();

        //Quand
        var token = _generator.GenerateToken(user);

        //Alors
        token.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void GenerateToken_UtilisateurValide_RetourneTokenAuFormatJwt()
    {
        //Étant donné
        var user = BuildAdmin();

        //Quand
        var token = _generator.GenerateToken(user);

        //Alors — un JWT contient exactement 3 segments séparés par des points
        token.Split('.').Should().HaveCount(3);
    }

    // ── Algorithme de signature ───────────────────────────────────────────────

    [Fact]
    public void GenerateToken_AlgorithmeEstHmacSha256()
    {
        //Étant donné — l'algorithme 'none' est une faille critique (CVE connue)
        var jwt = Decode(_generator.GenerateToken(BuildAdmin()));

        //Alors — doit être signé HS256, jamais 'none' ni un algo faible
        jwt.Header.Alg.Should().Be(SecurityAlgorithms.HmacSha256);
        jwt.Header.Alg.Should().NotBe("none");
        jwt.Header.Alg.Should().NotBe("None");
    }

    // ── Claims obligatoires ──────────────────────────────────────────────────

    [Fact]
    public void GenerateToken_Admin_ContientClaimEmail()
    {
        //Étant donné
        var user = BuildAdmin();

        //Quand
        var jwt = Decode(_generator.GenerateToken(user));

        //Alors
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Email
                                      && c.Value == "admin@fleet.fr");
    }

    [Fact]
    public void GenerateToken_Admin_ContientClaimRole()
    {
        //Étant donné
        var user = BuildAdmin();

        //Quand
        var jwt = Decode(_generator.GenerateToken(user));

        //Alors
        jwt.Claims.Should().Contain(c => c.Type == ClaimTypes.Role
                                      && c.Value == "Admin");
    }

    [Fact]
    public void GenerateToken_Admin_ContientClaimSubjectEgalAuId()
    {
        //Étant donné
        var user = BuildAdmin();

        //Quand
        var jwt = Decode(_generator.GenerateToken(user));

        //Alors
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.Sub
                                      && c.Value == user.Id.ToString());
    }

    [Fact]
    public void GenerateToken_SubjectEstUnGuidValide()
    {
        //Étant donné — le Sub doit être un GUID, pas une valeur vide ou arbitraire
        var user = BuildAdmin();
        var jwt  = Decode(_generator.GenerateToken(user));
        var sub  = jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;

        //Alors
        Guid.TryParse(sub, out _).Should().BeTrue();
        sub.Should().Be(user.Id.ToString());
    }

    [Fact]
    public void GenerateToken_Admin_ContientClaimPrenom()
    {
        //Étant donné
        var user = BuildAdmin();

        //Quand
        var jwt = Decode(_generator.GenerateToken(user));

        //Alors
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.GivenName
                                      && c.Value == "Sophie");
    }

    [Fact]
    public void GenerateToken_Admin_ContientClaimNom()
    {
        //Étant donné
        var user = BuildAdmin();

        //Quand
        var jwt = Decode(_generator.GenerateToken(user));

        //Alors
        jwt.Claims.Should().Contain(c => c.Type == JwtRegisteredClaimNames.FamilyName
                                      && c.Value == "Martin");
    }

    // ── Claim storeId conditionnel ────────────────────────────────────────────

    [Fact]
    public void GenerateToken_TechnicienAvecEnseigne_ContientClaimStoreId()
    {
        //Étant donné
        var user = BuildTech();

        //Quand
        var jwt = Decode(_generator.GenerateToken(user));

        //Alors
        jwt.Claims.Should().Contain(c => c.Type == "storeId"
                                      && c.Value == user.StoreId!.Value.ToString());
    }

    [Fact]
    public void GenerateToken_StoreManagerAvecEnseigne_ContientClaimStoreId()
    {
        //Étant donné — le StoreManager doit aussi avoir son storeId dans le token
        var user = BuildManager();

        //Quand
        var jwt = Decode(_generator.GenerateToken(user));

        //Alors
        jwt.Claims.Should().Contain(c => c.Type == "storeId"
                                      && c.Value == user.StoreId!.Value.ToString());
    }

    [Fact]
    public void GenerateToken_AdminSansEnseigne_NePasContenirClaimStoreId()
    {
        //Étant donné
        var user = BuildAdmin();

        //Quand
        var jwt = Decode(_generator.GenerateToken(user));

        //Alors
        jwt.Claims.Should().NotContain(c => c.Type == "storeId");
    }

    // ── Expiration ────────────────────────────────────────────────────────────

    [Fact]
    public void GenerateToken_Expiration_EstDansExpiryMinutes()
    {
        //Étant donné
        var avant = DateTime.UtcNow.AddMinutes(ExpiryMinutes - 1);
        var apres = DateTime.UtcNow.AddMinutes(ExpiryMinutes + 1);

        //Quand
        var jwt = Decode(_generator.GenerateToken(BuildAdmin()));

        //Alors
        jwt.ValidTo.Should().BeAfter(avant).And.BeBefore(apres);
    }

    [Fact]
    public void GenerateToken_TokenNonExpireImmediatement()
    {
        //Étant donné / Quand
        var jwt = Decode(_generator.GenerateToken(BuildAdmin()));

        //Alors
        jwt.ValidTo.Should().BeAfter(DateTime.UtcNow);
    }

    // ── Unicité (protection contre la réutilisation de token) ────────────────

    [Fact]
    public void GenerateToken_MemeUtilisateurDeuxFois_JtiDifferents()
    {
        //Étant donné — le JTI (JWT ID) garantit l'unicité de chaque token émis
        var user = BuildAdmin();

        //Quand
        var jwt1 = Decode(_generator.GenerateToken(user));
        var jwt2 = Decode(_generator.GenerateToken(user));

        var jti1 = jwt1.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;
        var jti2 = jwt2.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

        //Alors
        jti1.Should().NotBe(jti2);
    }

    [Fact]
    public void GenerateToken_JtiEstUnGuidValide()
    {
        //Étant donné — le JTI doit être un GUID non-vide (pas une valeur prédictible)
        var jwt = Decode(_generator.GenerateToken(BuildAdmin()));
        var jti = jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Jti).Value;

        //Alors
        jti.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(jti, out _).Should().BeTrue();
    }

    // ── Issuer / Audience ─────────────────────────────────────────────────────

    [Fact]
    public void GenerateToken_ContientIssuerCorrect()
    {
        //Étant donné / Quand
        var jwt = Decode(_generator.GenerateToken(BuildAdmin()));

        //Alors
        jwt.Issuer.Should().Be(Issuer);
    }

    [Fact]
    public void GenerateToken_ContientAudienceCorrecte()
    {
        //Étant donné / Quand
        var jwt = Decode(_generator.GenerateToken(BuildAdmin()));

        //Alors
        jwt.Audiences.Should().Contain(Audience);
    }
}
