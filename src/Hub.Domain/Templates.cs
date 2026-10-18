namespace Hub.Domain;

/// A template's structure as the planner needs it: offsets and roles instead of dates and people (§12.14).
public sealed record TplMilestone(Guid Id, int SortOrder, string Anchor, int? Offset);
public sealed record TplDeliverable(Guid Id, Guid DisciplineId, Guid? MilestoneId, int? Offset);
public sealed record TplTask(Guid Id, Guid DisciplineId, Guid? DeliverableId, int? Offset);
public sealed record TplDependency(Guid Predecessor, Guid Successor);
public sealed record TemplateSnap(IReadOnlyList<TplMilestone> Milestones, IReadOnlyList<TplDeliverable> Deliverables, IReadOnlyList<TplTask> Tasks, IReadOnlyList<TplDependency> Dependencies);

public static class TemplateAnchor { public const string ProjectStart = "ProjectStart", PreviousMilestone = "PreviousMilestone", None = "None"; public static readonly string[] All = [ProjectStart, PreviousMilestone, None]; }
public static class DateSource { public const string Contract = "Contract", Offset = "Offset", None = "None"; }

public sealed record PlannedMilestone(Guid Id, DateOnly? Date, string Source);
public sealed record PlannedItem(Guid Id, DateOnly? Due);
public sealed record TemplatePlan(IReadOnlyList<PlannedMilestone> Milestones, IReadOnlyList<PlannedItem> Deliverables, IReadOnlyList<PlannedItem> Tasks, IReadOnlyList<TplDependency> Dependencies)
{
    public int UndatedMilestones => Milestones.Count(m => m.Date is null);
    public int UndatedDeliverables => Deliverables.Count(d => d.Due is null);
    public int UndatedTasks => Tasks.Count(t => t.Due is null);
}

public sealed record TplRole(Guid Id, string Label, Guid? DisciplineId, Guid? StartMilestoneId, int StartOffsetDays,
    Guid? EndMilestoneId, int EndOffsetDays, decimal Hours, string? Note);
public sealed record PlannedRole(Guid Id, string Label, Guid? DisciplineId, DateOnly? From, DateOnly? Through, decimal Hours, string? Note, string? SkipReason);

/// Role dates use the same anchor and day-kind contract as template milestones. A skipped role is
/// returned with a reason so the wizard can explain it without making project creation fail.
public static class TemplateRolePlanner
{
    const string CalendarDays = "CalendarDays";
    const string WorkingDays = "WorkingDays";
    public static IReadOnlyList<PlannedRole> Plan(IEnumerable<TplRole> roles, IReadOnlyDictionary<Guid, DateOnly?> milestones,
        IReadOnlySet<Guid> disciplines, WorkCalendar? calendar = null) => Plan(roles, milestones, disciplines, CalendarDays, calendar);

    public static IReadOnlyList<PlannedRole> Plan(IEnumerable<TplRole> roles, IReadOnlyDictionary<Guid, DateOnly?> milestones,
        IReadOnlySet<Guid> disciplines, string offsetDayKind, WorkCalendar? calendar = null)
    {
        DateOnly? Shift(DateOnly? date, int days) =>
            date is null ? null : offsetDayKind == WorkingDays ? (calendar ?? WorkCalendar.Weekdays).Add(date.Value, days) : date.Value.AddDays(days);
        return roles.Select(role =>
        {
            if (role.DisciplineId is { } discipline && !disciplines.Contains(discipline)) return new PlannedRole(role.Id, role.Label, role.DisciplineId, null, null, role.Hours, role.Note, "discipline_not_selected");
            if (!milestones.TryGetValue(role.StartMilestoneId ?? Guid.Empty, out var start) || start is null)
                return new PlannedRole(role.Id, role.Label, role.DisciplineId, null, null, role.Hours, role.Note, "start_milestone_undated");
            if (!milestones.TryGetValue(role.EndMilestoneId ?? Guid.Empty, out var end) || end is null)
                return new PlannedRole(role.Id, role.Label, role.DisciplineId, null, null, role.Hours, role.Note, "end_milestone_undated");
            var from = Shift(start, role.StartOffsetDays);
            var through = Shift(end, role.EndOffsetDays);
            if (from is null || through is null) return new PlannedRole(role.Id, role.Label, role.DisciplineId, from, through, role.Hours, role.Note, "anchor_milestone_undated");
            if (through < from) return new PlannedRole(role.Id, role.Label, role.DisciplineId, from, through, role.Hours, role.Note, "through_before_from");
            if (through.Value.DayNumber - from.Value.DayNumber > 366) return new PlannedRole(role.Id, role.Label, role.DisciplineId, from, through, role.Hours, role.Note, "range_over_366_days");
            if (!(calendar ?? WorkCalendar.Weekdays).WorkingDays(from.Value, through.Value).Any())
                return new PlannedRole(role.Id, role.Label, role.DisciplineId, from, through, role.Hours, role.Note, "no_working_day");
            return new PlannedRole(role.Id, role.Label, role.DisciplineId, from, through, role.Hours, role.Note, null);
        }).ToList();
    }
}

/// Instantiation dates (§12.14 steps 2, 3 and 6). A typed date wins; a milestone explicitly left blank stays undated;
/// otherwise its anchor and offset decide (project start, or the milestone before it). A deliverable is due its target
/// milestone's date plus its offset; a task its deliverable's due date plus its offset, or the project start plus its
/// offset when it has no deliverable. Anything whose basis is undated is undated. Only the chosen disciplines' items are
/// planned, and a dependency is kept only when both of its tasks are. With a calendar, offsets count its working days
/// (FR-RCV-12); without one they count calendar days exactly as before (FR-RCV-11).
public static class TemplatePlanner
{
    public static TemplatePlan Plan(TemplateSnap t, DateOnly? start, IReadOnlyDictionary<Guid, DateOnly?> typed, IReadOnlySet<Guid> disciplines, WorkCalendar? calendar = null)
    {
        DateOnly Shift(DateOnly d, int days) => calendar is null ? d.AddDays(days) : calendar.Add(d, days);
        var milestones = new List<PlannedMilestone>();
        DateOnly? previous = null;
        foreach (var m in t.Milestones.OrderBy(m => m.SortOrder))
        {
            PlannedMilestone planned;
            if (typed.TryGetValue(m.Id, out var date)) planned = new(m.Id, date, date is null ? DateSource.None : DateSource.Contract);
            else
            {
                var basis = m.Anchor switch { TemplateAnchor.ProjectStart => start, TemplateAnchor.PreviousMilestone => previous, _ => null };
                var computed = basis is { } b && m.Offset is { } o ? Shift(b, o) : (DateOnly?)null;
                planned = new(m.Id, computed, computed is null ? DateSource.None : DateSource.Offset);
            }
            milestones.Add(planned);
            previous = planned.Date;
        }
        var msDate = milestones.ToDictionary(m => m.Id, m => m.Date);

        var deliverables = t.Deliverables.Where(d => disciplines.Contains(d.DisciplineId))
            .Select(d => new PlannedItem(d.Id, d.MilestoneId is { } mid && msDate.GetValueOrDefault(mid) is { } md ? Shift(md, d.Offset ?? 0) : null)).ToList();
        var delDue = deliverables.ToDictionary(d => d.Id, d => d.Due);

        var tasks = t.Tasks.Where(k => disciplines.Contains(k.DisciplineId) && (k.DeliverableId is null || delDue.ContainsKey(k.DeliverableId.Value)))
            .Select(k => new PlannedItem(k.Id, k.DeliverableId is { } did
                ? delDue[did] is { } due ? Shift(due, k.Offset ?? 0) : null
                : start is { } s && k.Offset is { } off ? Shift(s, off) : null)).ToList();
        var kept = tasks.Select(k => k.Id).ToHashSet();

        return new TemplatePlan(milestones, deliverables, tasks, t.Dependencies.Where(d => kept.Contains(d.Predecessor) && kept.Contains(d.Successor)).ToList());
    }
}
