using System.Net;
using AutoNexusMobile.Core.Api;
using FluentAssertions;

namespace AutoNexusMobile.Tests;

public class FleetApiTests
{
    private static readonly Guid VehicleId = Guid.Parse("8c1d7a52-3e9f-4b60-a2d4-7f5e1c9b3a18");

    [Fact]
    public async Task EtantDonneFiltres_QuandListerVehicules_AlorsParametresDuWeb()
    {
        var http = new FakeHttp((_, _) => FakeHttp.Json(new PagedResult<VehicleDto>([], 0, 2, 20, 0, false, true)));

        await new FleetApi(FakeHttp.Client(http)).GetVehiclesAsync(2, 20, " clio ", VehicleStatus.InIntervention);

        http.Requests.Single().Path.Should().Be("/api/v1/vehicles?page=2&pageSize=20&search=clio&status=InIntervention");
    }

    [Fact]
    public async Task EtantDonneJourneeLocale_QuandMesInterventions_AlorsBornesEnHeureMurale()
    {
        var http = new FakeHttp((_, _) => FakeHttp.Json(Array.Empty<InterventionDto>()));
        var from = new DateTime(2026, 10, 17, 0, 0, 0, DateTimeKind.Local);

        await new FleetApi(FakeHttp.Client(http)).GetMyInterventionsAsync(from, from.AddDays(1));

        http.Requests.Single().Path.Should().Be("/api/v1/interventions/mine?from=2026-10-17T00%3A00%3A00&to=2026-10-18T00%3A00%3A00");
    }

    [Fact]
    public async Task EtantDonneCloture_QuandChangerStatut_AlorsKilometrageEtStatutEnTexte()
    {
        var http = new FakeHttp((_, _) => FakeHttp.Status(HttpStatusCode.OK, "{\"id\":\"8c1d7a52-3e9f-4b60-a2d4-7f5e1c9b3a18\",\"vehicleId\":\"8c1d7a52-3e9f-4b60-a2d4-7f5e1c9b3a18\",\"storeId\":\"8c1d7a52-3e9f-4b60-a2d4-7f5e1c9b3a18\",\"technicianId\":\"8c1d7a52-3e9f-4b60-a2d4-7f5e1c9b3a18\",\"type\":\"Repair\",\"typeLabel\":\"Réparation\",\"status\":\"Completed\",\"statusLabel\":\"Terminée\",\"plannedStartDate\":\"2026-10-17T08:00:00\",\"plannedEndDate\":\"2026-10-17T17:00:00\"}"));

        var result = await new FleetApi(FakeHttp.Client(http)).ChangeInterventionStatusAsync(VehicleId, InterventionStatus.Completed, "  Disques changés ", 48_210);

        http.Requests.Single().Method.Should().Be(HttpMethod.Patch);
        var sent = System.Text.Json.JsonDocument.Parse(http.Requests.Single().Body!).RootElement;
        sent.GetProperty("newStatus").GetString().Should().Be("Completed");
        sent.GetProperty("comment").GetString().Should().Be("Disques changés");
        sent.GetProperty("mileage").GetInt32().Should().Be(48_210);
        result.Status.Should().Be(InterventionStatus.Completed);
        result.PlannedStartDate.Should().Be(new DateTime(2026, 10, 17, 8, 0, 0));
        result.PlannedStartDate.Kind.Should().Be(DateTimeKind.Unspecified);
    }

    [Fact]
    public async Task EtantDonneNivExact_QuandChercherParNiv_AlorsSeulLeVehiculeCorrespondant()
    {
        var http = new FakeHttp((_, _) => FakeHttp.Json(new PagedResult<VehicleDto>(
        [
            new(Guid.NewGuid(), "VF1RFB00X67891234", "Renault", "Clio", 2022, 18_000, VehicleStatus.Available, "Disponible", Guid.NewGuid(), "Paris"),
            new(VehicleId, "VF1RFB00X6789123", "Renault", "Clio", 2022, 18_000, VehicleStatus.Available, "Disponible", Guid.NewGuid(), "Paris"),
        ], 2, 1, 20, 1, false, false)));

        var found = await new FleetApi(FakeHttp.Client(http)).FindVehicleByVinAsync("vf1rfb00x67891234");

        found!.Vin.Should().Be("VF1RFB00X67891234");
    }

    [Fact]
    public async Task EtantDonneProblemDetails_QuandErreur_AlorsMessageMetier()
    {
        var http = new FakeHttp((_, _) => FakeHttp.Status(HttpStatusCode.BadRequest,
            "{\"title\":\"Bad request.\",\"status\":400,\"detail\":\"Impossible de supprimer un véhicule avec une intervention planifiée ou en cours.\"}"));

        var act = () => new FleetApi(FakeHttp.Client(http)).DeleteVehicleAsync(VehicleId);

        (await act.Should().ThrowAsync<ApiException>())
            .Which.Message.Should().Be("Impossible de supprimer un véhicule avec une intervention planifiée ou en cours.");
    }

    [Fact]
    public async Task EtantDonneErreurDeValidation_QuandErreur_AlorsPremierMessage()
    {
        var http = new FakeHttp((_, _) => FakeHttp.Status(HttpStatusCode.BadRequest,
            "{\"title\":\"Validation\",\"status\":400,\"errors\":{\"Mileage\":[\"Le kilométrage doit être compris entre 0 et 2 000 000 km.\"]}}"));

        var act = () => new FleetApi(FakeHttp.Client(http)).ChangeInterventionStatusAsync(VehicleId, InterventionStatus.Completed, null, -1);

        (await act.Should().ThrowAsync<ApiException>()).Which.Message.Should().StartWith("Le kilométrage doit être");
    }

    [Fact]
    public async Task EtantDonneNivArchive_QuandCreerVehicule_AlorsConflitRestaurable()
    {
        var http = new FakeHttp((_, _) => FakeHttp.Status(HttpStatusCode.Conflict,
            "{\"status\":409,\"detail\":\"Ce VIN appartient à un véhicule archivé.\",\"archivedVehicleId\":\"8c1d7a52-3e9f-4b60-a2d4-7f5e1c9b3a18\",\"brand\":\"Peugeot\",\"model\":\"208\",\"year\":2021,\"storeName\":\"Lyon Part-Dieu\"}"));

        var act = () => new FleetApi(FakeHttp.Client(http)).CreateVehicleAsync(new CreateVehicleRequest("VF3XXXXXXXX123456", "Peugeot", "208", 2021, 0, Guid.NewGuid()));

        var conflict = (await act.Should().ThrowAsync<ApiException>()).Which.ArchivedVinConflict;
        conflict.Should().NotBeNull();
        conflict!.ArchivedVehicleId.Should().Be(VehicleId);
        conflict.Brand.Should().Be("Peugeot");
        conflict.Year.Should().Be(2021);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, "Élément introuvable. Il a peut-être été supprimé.")]
    [InlineData(HttpStatusCode.Forbidden, "Cette action n'est pas autorisée pour votre rôle.")]
    [InlineData(HttpStatusCode.BadGateway, "Le serveur a rencontré une erreur. Réessayez dans un instant.")]
    public async Task EtantDonneCorpsVide_QuandErreur_AlorsMessageGenerique(HttpStatusCode status, string message)
    {
        var http = new FakeHttp((_, _) => FakeHttp.Status(status, "<html>relais</html>"));

        var act = () => new FleetApi(FakeHttp.Client(http)).GetStoresAsync();

        (await act.Should().ThrowAsync<ApiException>()).Which.Message.Should().Be(message);
    }

    [Fact]
    public async Task EtantDonneServeurInjoignable_QuandAppel_AlorsMessageReseau()
    {
        var http = new FakeHttp((_, _) => throw new HttpRequestException("refused"));

        var act = () => new FleetApi(FakeHttp.Client(http)).GetDashboardAsync();

        (await act.Should().ThrowAsync<ApiException>()).Which.Message.Should().StartWith("Serveur injoignable");
    }
}
