using AutoNexusMobile.Core.Api;
using AutoNexusMobile.Core.Fleet;
using AutoNexusMobile.Core.Realtime;
using FluentAssertions;

namespace AutoNexusMobile.Tests;

public class VinReaderTests
{
    [Theory]
    [InlineData("VF1RFB00X67891234", "VF1RFB00X67891234")]
    [InlineData("vf1rfb00x67891234", "VF1RFB00X67891234")]
    [InlineData(" VF1 RFB-00X6 7891234 ", "VF1RFB00X67891234")]
    // Code 39 nord-américain : « I » d'import devant le NIV.
    [InlineData("IVF1RFB00X67891234", "VF1RFB00X67891234")]
    // QR d'un certificat : le NIV est un champ parmi d'autres.
    [InlineData("CI=AB-123-CD;E=VF1RFB00X67891234;D.1=RENAULT", "VF1RFB00X67891234")]
    public void EtantDonneCodeLu_QuandLireNiv_AlorsNivNormalise(string raw, string expected)
    {
        VinReader.FromScan(raw).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("3017620422003")]                 // EAN d'un produit
    [InlineData("VF1RFB00X6789123")]              // 16 caractères
    [InlineData("VF1RFB00X6789123O")]             // O interdit
    [InlineData("https://autonexus.example/v/1")] // URL sans NIV
    public void EtantDonneCodeSansNiv_QuandLireNiv_AlorsNull(string? raw)
    {
        VinReader.FromScan(raw).Should().BeNull();
    }

    [Theory]
    [InlineData("VF1RFB00X67891234", true)]
    [InlineData("VF1RFB00X6789123I", false)]
    [InlineData("VF1RFB00X678912345", false)]
    public void EtantDonneSaisie_QuandValider_AlorsReglesIso3779(string vin, bool valid)
    {
        VinReader.IsValid(vin).Should().Be(valid);
    }
}

public class DayAgendaTests
{
    private static readonly DateTime Now = new(2026, 10, 17, 10, 30, 0);

    private static InterventionDto At(InterventionStatus status, DateTime start, DateTime end, string label = "") => new(
        Guid.NewGuid(), Guid.NewGuid(), "Renault", "Clio " + label, "VF1RFB00X67891234", Guid.NewGuid(), "Paris Centre",
        Guid.NewGuid(), "Lucas Moreau", InterventionType.Maintenance, "Maintenance", status, status.ToString(), start, end, null, null);

    [Fact]
    public void EtantDonneJourneeMixte_QuandConstruire_AlorsRetardPuisEnCoursPuisAFairePuisTerminees()
    {
        var late = At(InterventionStatus.Planned, Now.AddDays(-2), Now.AddDays(-1), "retard");
        var lateInProgress = At(InterventionStatus.InProgress, Now.AddHours(-5), Now.AddHours(-1), "retard en cours");
        var running = At(InterventionStatus.InProgress, Now.AddHours(-2), Now.AddHours(3), "en cours");
        var afternoon = At(InterventionStatus.Planned, Now.AddHours(4), Now.AddHours(6), "après-midi");
        var morning = At(InterventionStatus.Planned, Now.AddHours(1), Now.AddHours(2), "matin");
        var done = At(InterventionStatus.Completed, Now.AddHours(-4), Now.AddHours(-3), "faite");

        var agenda = DayAgenda.Build([afternoon, done, running, late, morning, lateInProgress], Now);

        agenda.Sections.Select(s => s.Section).Should().Equal(AgendaSection.Overdue, AgendaSection.InProgress, AgendaSection.Today, AgendaSection.Done);
        agenda.Sections[0].Items.Should().Equal(late, lateInProgress);
        agenda.Sections[2].Items.Should().Equal(morning, afternoon);
        agenda.OpenCount.Should().Be(5);
        agenda.DoneCount.Should().Be(1);
    }

    [Fact]
    public void EtantDonneHeureLocale_QuandBornes_AlorsMinuitAMinuitSansFuseau()
    {
        var (from, to) = DayAgenda.Bounds(new DateTime(2026, 10, 17, 23, 59, 0, DateTimeKind.Local));

        from.Should().Be(new DateTime(2026, 10, 17));
        to.Should().Be(new DateTime(2026, 10, 18));
        from.Kind.Should().Be(DateTimeKind.Unspecified);
    }

    [Fact]
    public void EtantDonneAucuneIntervention_QuandConstruire_AlorsAucuneSection()
    {
        DayAgenda.Build([], Now).Sections.Should().BeEmpty();
    }
}

public class RolePermissionsTests
{
    [Theory]
    [InlineData(UserRole.Admin, true, true, true)]
    [InlineData(UserRole.StoreManager, true, true, false)]
    [InlineData(UserRole.Technician, false, false, false)]
    public void EtantDonneRole_QuandDroits_AlorsAlignesSurLApi(UserRole role, bool vehicles, bool archives, bool stores)
    {
        RolePermissions.CanManageVehicles(role).Should().Be(vehicles);
        RolePermissions.CanSeeArchives(role).Should().Be(archives);
        RolePermissions.CanPlanInterventions(role).Should().Be(vehicles);
        RolePermissions.CanManageStores(role).Should().Be(stores);
        RolePermissions.CanChangeVehicleStatus(role).Should().Be(vehicles);
    }

    [Fact]
    public void EtantDonneTechnicien_QuandInterventionDUnCollegue_AlorsAucuneAction()
    {
        var moi = Guid.NewGuid();

        RolePermissions.CanActOnIntervention(UserRole.Technician, moi, moi).Should().BeTrue();
        RolePermissions.CanActOnIntervention(UserRole.Technician, moi, Guid.NewGuid()).Should().BeFalse();
        RolePermissions.CanActOnIntervention(UserRole.StoreManager, moi, Guid.NewGuid()).Should().BeTrue();
        RolePermissions.CanActOnIntervention(null, moi, moi).Should().BeFalse();
    }

    [Theory]
    [InlineData(InterventionStatus.Planned, new[] { InterventionStatus.InProgress, InterventionStatus.Cancelled })]
    [InlineData(InterventionStatus.InProgress, new[] { InterventionStatus.Completed, InterventionStatus.Cancelled })]
    [InlineData(InterventionStatus.Completed, new InterventionStatus[0])]
    [InlineData(InterventionStatus.Cancelled, new InterventionStatus[0])]
    public void EtantDonneStatut_QuandTransitions_AlorsCellesDuDomaine(InterventionStatus status, InterventionStatus[] expected)
    {
        RolePermissions.NextStatuses(status).Should().Equal(expected);
    }

    [Fact]
    public void EtantDonneStatutsCible_QuandLibelleAction_AlorsVerbesDuParcoursTechnicien()
    {
        FleetFormat.ActionLabel(InterventionStatus.InProgress).Should().Be("Commencer");
        FleetFormat.ActionLabel(InterventionStatus.Completed).Should().Be("Terminer");
        FleetFormat.ActionLabel(InterventionStatus.Cancelled).Should().Be("Annuler");
    }

    [Fact]
    public void EtantDonneNombres_QuandFormater_AlorsFrancaisDeFrance()
    {
        FleetFormat.Kilometers(48210).Should().Be("48 210 km");
        FleetFormat.Date(new DateTime(2026, 10, 7)).Should().Be("07/10/2026");
        FleetFormat.Window(new DateTime(2026, 10, 17, 8, 0, 0), new DateTime(2026, 10, 17, 17, 0, 0)).Should().Be("17 oct. 08:00 - 17:00");
    }
}

public class RealtimeParseTests
{
    [Theory]
    [InlineData(new[] { "vehicles" }, FleetChange.Vehicles)]
    [InlineData(new[] { "interventions", "vehicles" }, FleetChange.Vehicles | FleetChange.Interventions)]
    [InlineData(new[] { "stores" }, FleetChange.Stores | FleetChange.Vehicles | FleetChange.Interventions)]
    [InlineData(new[] { "inconnu" }, FleetChange.None)]
    public void EtantDonneChargeUtile_QuandAnalyser_AlorsEnsemblesARecharger(string[] entities, FleetChange expected)
    {
        FleetRealtime.Parse(entities).Should().Be(expected);
    }
}
