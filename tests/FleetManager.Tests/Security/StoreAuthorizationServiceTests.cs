using FleetManager.Application.Services;
using FleetManager.Domain.Enums;
using FluentAssertions;

namespace FleetManager.Tests.Security;

/// <summary>
/// Tests unitaires de StoreAuthorizationService.
/// Ces tests vérifient les règles d'accès au cœur de la sécurité multi-tenant.
/// </summary>
public class StoreAuthorizationServiceTests
{
    private readonly StoreAuthorizationService _sut = new();

    private static readonly Guid StoreA = Guid.NewGuid();
    private static readonly Guid StoreB = Guid.NewGuid();

    // ── Admin : accès total ───────────────────────────────────────────────────

    [Fact]
    public void Admin_PeutAccederANimporteQuelleEnseigne()
    {
        _sut.CanAccessStore(UserRole.Admin, StoreA, StoreB).Should().BeTrue();
    }

    [Fact]
    public void Admin_SansStoreId_PeutAccederANimporteQuelleEnseigne()
    {
        //Étant donné — l'Admin n'a pas forcément de storeId dans son JWT
        _sut.CanAccessStore(UserRole.Admin, null, StoreB).Should().BeTrue();
    }

    // ── Rôle null : toujours refusé ───────────────────────────────────────────

    [Fact]
    public void RoleNull_MemeEnseigne_AccesRefuse()
    {
        //Étant donné — faille corrigée : null-role bypass via storeId correspondant
        _sut.CanAccessStore(null, StoreA, StoreA).Should().BeFalse();
    }

    [Fact]
    public void RoleNull_AutreEnseigne_AccesRefuse()
    {
        _sut.CanAccessStore(null, StoreA, StoreB).Should().BeFalse();
    }

    [Fact]
    public void RoleNull_SansStoreId_AccesRefuse()
    {
        _sut.CanAccessStore(null, null, StoreA).Should().BeFalse();
    }

    // ── Non-Admin : même enseigne ─────────────────────────────────────────────

    [Fact]
    public void Technician_MemeEnseigne_AccesAutorise()
    {
        _sut.CanAccessStore(UserRole.Technician, StoreA, StoreA).Should().BeTrue();
    }

    [Fact]
    public void StoreManager_MemeEnseigne_AccesAutorise()
    {
        _sut.CanAccessStore(UserRole.StoreManager, StoreA, StoreA).Should().BeTrue();
    }

    // ── Non-Admin : autre enseigne ────────────────────────────────────────────

    [Fact]
    public void Technician_AutreEnseigne_AccesRefuse()
    {
        _sut.CanAccessStore(UserRole.Technician, StoreA, StoreB).Should().BeFalse();
    }

    [Fact]
    public void StoreManager_AutreEnseigne_AccesRefuse()
    {
        _sut.CanAccessStore(UserRole.StoreManager, StoreA, StoreB).Should().BeFalse();
    }

    // ── Non-Admin sans storeId dans le JWT ────────────────────────────────────

    [Fact]
    public void Technician_SansStoreId_AccesRefuse()
    {
        //Étant donné — JWT malformé ou enseigne absente
        _sut.CanAccessStore(UserRole.Technician, null, StoreA).Should().BeFalse();
    }
}
