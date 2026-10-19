using AutoNexusMobile.Core.Api;

namespace AutoNexusMobile.Core.Fleet;

public enum AgendaSection { Overdue, InProgress, Today, Done }

/// <summary>
/// Journée du technicien : les interventions de GET /interventions/mine rangées dans l'ordre où
/// il les traite. Les dates de l'API sont des heures murales : elles se comparent telles quelles
/// à l'heure de l'appareil.
/// </summary>
public sealed class DayAgenda
{
    private DayAgenda(DateTime day, IReadOnlyList<(AgendaSection Section, IReadOnlyList<InterventionDto> Items)> sections)
    {
        Day = day;
        Sections = sections;
    }

    public DateTime Day { get; }

    public IReadOnlyList<(AgendaSection Section, IReadOnlyList<InterventionDto> Items)> Sections { get; }

    public int OpenCount => Sections.Where(s => s.Section != AgendaSection.Done).Sum(s => s.Items.Count);

    public int DoneCount => Sections.Where(s => s.Section == AgendaSection.Done).Sum(s => s.Items.Count);

    /// <summary>Début et fin de la journée locale, à passer à l'API.</summary>
    public static (DateTime From, DateTime To) Bounds(DateTime localNow)
    {
        var start = DateTime.SpecifyKind(localNow.Date, DateTimeKind.Unspecified);
        return (start, start.AddDays(1));
    }

    public static AgendaSection Classify(InterventionDto intervention, DateTime localNow) => intervention.Status switch
    {
        InterventionStatus.Completed or InterventionStatus.Cancelled => AgendaSection.Done,
        InterventionStatus.InProgress when intervention.PlannedEndDate < localNow => AgendaSection.Overdue,
        InterventionStatus.InProgress => AgendaSection.InProgress,
        _ when intervention.PlannedEndDate < localNow => AgendaSection.Overdue,
        _ => AgendaSection.Today,
    };

    public static DayAgenda Build(IEnumerable<InterventionDto> interventions, DateTime localNow)
    {
        var order = new[] { AgendaSection.Overdue, AgendaSection.InProgress, AgendaSection.Today, AgendaSection.Done };
        var grouped = interventions
            .GroupBy(i => Classify(i, localNow))
            .ToDictionary(g => g.Key, g => (IReadOnlyList<InterventionDto>)g.OrderBy(i => i.PlannedStartDate).ThenBy(i => i.Id).ToList());

        var sections = order
            .Where(grouped.ContainsKey)
            .Select(section => (section, grouped[section]))
            .ToList();

        return new DayAgenda(localNow.Date, sections);
    }

    public static string Title(AgendaSection section) => section switch
    {
        AgendaSection.Overdue => "En retard",
        AgendaSection.InProgress => "En cours",
        AgendaSection.Today => "À faire aujourd'hui",
        AgendaSection.Done => "Terminées",
        _ => section.ToString(),
    };
}
