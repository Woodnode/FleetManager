using FluentAssertions;
using NetArchTest.Rules;

namespace FleetManager.Tests.Architecture;

/// <summary>
/// Vérifie les règles de dépendance de la Clean Architecture.
/// Un test qui échoue signifie qu'une couche dépend d'une couche interdite.
/// </summary>
public class ArchitectureTests
{
    private const string DomainNs         = "FleetManager.Domain";
    private const string ApplicationNs    = "FleetManager.Application";
    private const string InfrastructureNs = "FleetManager.Infrastructure";
    private const string ApiNs            = "FleetManager.Api";

    private static readonly System.Reflection.Assembly DomainAssembly =
        typeof(FleetManager.Domain.Entities.Vehicle).Assembly;

    private static readonly System.Reflection.Assembly ApplicationAssembly =
        typeof(FleetManager.Application.DependencyInjection).Assembly;

    private static readonly System.Reflection.Assembly InfrastructureAssembly =
        typeof(FleetManager.Infrastructure.DependencyInjection).Assembly;

    private static readonly System.Reflection.Assembly ApiAssembly =
        typeof(FleetManager.Api.Controllers.ApiControllerBase).Assembly;

    // ── Domain ───────────────────────────────────────────────────────────────

    [Fact]
    public void Domain_ShouldNotDependOn_Application()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot().HaveDependencyOn(ApplicationNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"Domain must not reference Application. Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Domain_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot().HaveDependencyOn(InfrastructureNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"Domain must not reference Infrastructure. Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Domain_ShouldNotDependOn_Api()
    {
        var result = Types.InAssembly(DomainAssembly)
            .ShouldNot().HaveDependencyOn(ApiNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"Domain must not reference Api. Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    // ── Application ──────────────────────────────────────────────────────────

    [Fact]
    public void Application_ShouldNotDependOn_Infrastructure()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot().HaveDependencyOn(InfrastructureNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"Application must not reference Infrastructure (Dependency Inversion). Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Application_ShouldNotDependOn_Api()
    {
        var result = Types.InAssembly(ApplicationAssembly)
            .ShouldNot().HaveDependencyOn(ApiNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"Application must not reference Api. Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    // ── Infrastructure ────────────────────────────────────────────────────────

    [Fact]
    public void Infrastructure_ShouldNotDependOn_Api()
    {
        var result = Types.InAssembly(InfrastructureAssembly)
            .ShouldNot().HaveDependencyOn(ApiNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"Infrastructure must not reference Api. Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    // ── Naming conventions ────────────────────────────────────────────────────

    [Fact]
    public void Handlers_ShouldResideIn_ApplicationNamespace()
    {
        var result = Types.InAssemblies([DomainAssembly, ApplicationAssembly, InfrastructureAssembly, ApiAssembly])
            .That().HaveNameEndingWith("Handler")
            .Should().ResideInNamespace(ApplicationNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"All MediatR handlers must live in the Application layer. Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Controllers_ShouldResideIn_ApiNamespace()
    {
        var result = Types.InAssemblies([DomainAssembly, ApplicationAssembly, InfrastructureAssembly, ApiAssembly])
            .That().HaveNameEndingWith("Controller")
            .Should().ResideInNamespace(ApiNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"All controllers must live in the Api layer. Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }

    [Fact]
    public void Repositories_ShouldResideIn_InfrastructureNamespace()
    {
        // Concrete implementations (not interfaces) must be in Infrastructure
        var result = Types.InAssemblies([DomainAssembly, ApplicationAssembly, InfrastructureAssembly, ApiAssembly])
            .That().HaveNameEndingWith("Repository").And().AreNotInterfaces()
            .Should().ResideInNamespace(InfrastructureNs)
            .GetResult();

        result.IsSuccessful.Should().BeTrue(
            because: $"Concrete repository implementations must live in Infrastructure. Failing types: {string.Join(", ", result.FailingTypeNames ?? [])}");
    }
}
