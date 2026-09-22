using FleetManager.Api.Controllers;
using FleetManager.Application.Common;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace FleetManager.Tests.Security;

/// <summary>
/// Les erreurs de l'API doivent être des ProblemDetails (RFC 7807) : le front lit le champ
/// "detail" pour afficher le message. FORBIDDEN reste masqué en 404 (protection IDOR).
/// </summary>
public class ApiErrorMappingTests
{
    private sealed class TestController : ApiControllerBase
    {
        public IActionResult Map(Error error) => MapError(error);
    }

    private static TestController CreateController()
    {
        var services = new ServiceCollection().AddLogging().AddMvcCore().Services.BuildServiceProvider();
        return new TestController
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { RequestServices = services } },
        };
    }

    [Theory]
    [InlineData("NOT_FOUND",    404)]
    [InlineData("FORBIDDEN",    404)]
    [InlineData("CONFLICT",     409)]
    [InlineData("UNAUTHORIZED", 401)]
    [InlineData("VALIDATION",   400)]
    public void MapError_RetourneUnProblemDetailsDirectement(string code, int expectedStatus)
    {
        var result = CreateController().Map(new Error(code, "Message métier"));

        var objectResult = result.Should().BeAssignableTo<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(expectedStatus);
        var problem = objectResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problem.Status.Should().Be(expectedStatus);
        problem.Detail.Should().Be("Message métier");
    }

    [Fact]
    public void MapError_AvecDetails_LesAjouteAuxExtensions()
    {
        var id = Guid.NewGuid();
        var error = Error.Conflict("VIN archivé", new Dictionary<string, object?> { ["archivedVehicleId"] = id });

        var problem = (ProblemDetails)((ObjectResult)CreateController().Map(error)).Value!;

        problem.Extensions.Should().ContainKey("archivedVehicleId").WhoseValue.Should().Be(id);
    }

    [Fact]
    public void MapError_Forbidden_NeDivulgueJamaisLesDetails()
    {
        var error = new Error("FORBIDDEN", "Refusé", new Dictionary<string, object?> { ["storeName"] = "Lyon" });

        var problem = (ProblemDetails)((ObjectResult)CreateController().Map(error)).Value!;

        problem.Extensions.Should().NotContainKey("storeName");
    }
}
