using FleetManager.Domain.Entities;
using FleetManager.Domain.Enums;
using FleetManager.Domain.Exceptions;
using FluentAssertions;

namespace FleetManager.Tests.Security;

/// <summary>
/// Tests de sécurité sur l'entité User.
/// Vérifie les invariants de domaine liés aux rôles, à l'affectation
/// aux enseignes, à la validité des champs obligatoires et à l'intégrité des données.
/// </summary>
public class UserEntitySecurityTests
{
    private static readonly Guid StoreId = Guid.NewGuid();
    private const string ValidHash = "$2b$12$HashSimuleHashSimuleHashSimuleHashS";

    // ── Création valide selon le rôle ────────────────────────────────────────

    [Fact]
    public void Create_AdminSansEnseigne_RetourneUtilisateur()
    {
        //Étant donné / Quand
        var user = User.Create("Sophie", "Martin", "admin@fleet.fr", ValidHash, UserRole.Admin);

        //Alors
        user.Role.Should().Be(UserRole.Admin);
        user.StoreId.Should().BeNull();
    }

    [Fact]
    public void Create_TechnicienAvecEnseigne_RetourneUtilisateur()
    {
        //Étant donné / Quand
        var user = User.Create("Lucas", "Moreau", "tech@fleet.fr", ValidHash, UserRole.Technician, StoreId);

        //Alors
        user.Role.Should().Be(UserRole.Technician);
        user.StoreId.Should().Be(StoreId);
    }

    [Fact]
    public void Create_ManagerAvecEnseigne_RetourneUtilisateur()
    {
        //Étant donné / Quand
        var user = User.Create("Thomas", "Dupont", "manager@fleet.fr", ValidHash, UserRole.StoreManager, StoreId);

        //Alors
        user.Role.Should().Be(UserRole.StoreManager);
        user.StoreId.Should().Be(StoreId);
    }

    [Fact]
    public void Create_AdminAvecStoreId_StoreIdEstStocke()
    {
        //Étant donné — Admin peut être associé à une enseigne (facultatif, pas d'invariant)
        var user = User.Create("Sophie", "Martin", "admin@fleet.fr", ValidHash, UserRole.Admin, StoreId);

        //Alors
        user.StoreId.Should().Be(StoreId);
    }

    // ── Invariant rôle → enseigne obligatoire ────────────────────────────────

    [Fact]
    public void Create_TechnicienSansEnseigne_LanceDomainException()
    {
        //Étant donné
        var act = () => User.Create("Lucas", "Moreau", "tech@fleet.fr", ValidHash, UserRole.Technician);

        //Alors
        act.Should().Throw<DomainException>().WithMessage("*store*");
    }

    [Fact]
    public void Create_ManagerSansEnseigne_LanceDomainException()
    {
        //Étant donné
        var act = () => User.Create("Thomas", "Dupont", "mgr@fleet.fr", ValidHash, UserRole.StoreManager);

        //Alors
        act.Should().Throw<DomainException>().WithMessage("*store*");
    }

    // ── Invariant champs obligatoires ────────────────────────────────────────

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_PrenomVideOuEspaces_LanceDomainException(string firstName)
    {
        //Étant donné
        var act = () => User.Create(firstName, "Martin", "user@fleet.fr", ValidHash, UserRole.Admin);

        //Alors
        act.Should().Throw<DomainException>().WithMessage("*First name*");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_NomVideOuEspaces_LanceDomainException(string lastName)
    {
        //Étant donné
        var act = () => User.Create("Sophie", lastName, "user@fleet.fr", ValidHash, UserRole.Admin);

        //Alors
        act.Should().Throw<DomainException>().WithMessage("*Last name*");
    }

    [Fact]
    public void Create_EmailInvalide_LanceDomainException()
    {
        //Étant donné
        var act = () => User.Create("Sophie", "Martin", "pas-un-email", ValidHash, UserRole.Admin);

        //Alors
        act.Should().Throw<DomainException>();
    }

    // ── Intégrité du stockage ─────────────────────────────────────────────────

    [Fact]
    public void Create_PasswordHashStockeVerbatim_SansModification()
    {
        //Étant donné — le domaine ne doit PAS re-hacher ; c'est le rôle de l'Infrastructure
        var hash = "$2b$12$SomeAlreadyHashedValue";

        //Quand
        var user = User.Create("Sophie", "Martin", "admin@fleet.fr", hash, UserRole.Admin);

        //Alors — le hash est stocké tel quel, sans transformation
        user.PasswordHash.Should().Be(hash);
    }

    // ── Normalisation ─────────────────────────────────────────────────────────

    [Fact]
    public void Create_PrenomAvecEspaces_RetournePrenomSansEspaces()
    {
        //Étant donné / Quand
        var user = User.Create("  Sophie  ", "Martin", "admin@fleet.fr", ValidHash, UserRole.Admin);

        //Alors
        user.FirstName.Should().Be("Sophie");
    }

    [Fact]
    public void Create_EmailEnMajuscules_RetourneEmailNormalise()
    {
        //Étant donné / Quand
        var user = User.Create("Sophie", "Martin", "ADMIN@FLEET.FR", ValidHash, UserRole.Admin);

        //Alors
        user.Email.Value.Should().Be("admin@fleet.fr");
    }

    [Fact]
    public void Create_RetourneIdUnique()
    {
        //Étant donné / Quand
        var user1 = User.Create("A", "B", "u1@fleet.fr", ValidHash, UserRole.Admin);
        var user2 = User.Create("C", "D", "u2@fleet.fr", ValidHash, UserRole.Admin);

        //Alors
        user1.Id.Should().NotBe(user2.Id);
    }

    // ── Changement de rôle ────────────────────────────────────────────────────

    [Fact]
    public void ChangeRole_VersAdminSansEnseigne_Succes()
    {
        //Étant donné
        var user = User.Create("Lucas", "Moreau", "tech@fleet.fr", ValidHash, UserRole.Technician, StoreId);

        //Quand
        user.ChangeRole(UserRole.Admin, null);

        //Alors
        user.Role.Should().Be(UserRole.Admin);
    }

    [Fact]
    public void ChangeRole_VersAdmin_EffaceLeStoreId()
    {
        //Étant donné — un technicien (avec enseigne) passe Admin
        var user = User.Create("Lucas", "Moreau", "tech@fleet.fr", ValidHash, UserRole.Technician, StoreId);

        //Quand
        user.ChangeRole(UserRole.Admin, null);

        //Alors — le storeId doit être effacé
        user.StoreId.Should().BeNull();
    }

    [Fact]
    public void ChangeRole_VersTechnicienAvecEnseigne_MettreAJourRoleEtEnseigne()
    {
        //Étant donné — un Admin passe Technicien avec une enseigne
        var user = User.Create("Sophie", "Martin", "admin@fleet.fr", ValidHash, UserRole.Admin);
        var nouvelleEnseigne = Guid.NewGuid();

        //Quand
        user.ChangeRole(UserRole.Technician, nouvelleEnseigne);

        //Alors
        user.Role.Should().Be(UserRole.Technician);
        user.StoreId.Should().Be(nouvelleEnseigne);
    }

    [Fact]
    public void ChangeRole_VersManagerAvecEnseigne_Succes()
    {
        //Étant donné
        var user = User.Create("Sophie", "Martin", "admin@fleet.fr", ValidHash, UserRole.Admin);
        var nouvelleEnseigne = Guid.NewGuid();

        //Quand
        user.ChangeRole(UserRole.StoreManager, nouvelleEnseigne);

        //Alors
        user.Role.Should().Be(UserRole.StoreManager);
        user.StoreId.Should().Be(nouvelleEnseigne);
    }

    [Fact]
    public void ChangeRole_VersTechnicienSansEnseigne_LanceDomainException()
    {
        //Étant donné
        var user = User.Create("Sophie", "Martin", "admin@fleet.fr", ValidHash, UserRole.Admin);

        //Quand
        var act = () => user.ChangeRole(UserRole.Technician, null);

        //Alors
        act.Should().Throw<DomainException>().WithMessage("*store*");
    }

    [Fact]
    public void ChangeRole_VersManagerSansEnseigne_LanceDomainException()
    {
        //Étant donné
        var user = User.Create("Sophie", "Martin", "admin@fleet.fr", ValidHash, UserRole.Admin);

        //Quand
        var act = () => user.ChangeRole(UserRole.StoreManager, null);

        //Alors
        act.Should().Throw<DomainException>().WithMessage("*store*");
    }

    // ── FullName ──────────────────────────────────────────────────────────────

    [Fact]
    public void FullName_RetournePrenomEtNomConcatenes()
    {
        //Étant donné / Quand
        var user = User.Create("Sophie", "Martin", "admin@fleet.fr", ValidHash, UserRole.Admin);

        //Alors
        user.FullName.Should().Be("Sophie Martin");
    }
}
