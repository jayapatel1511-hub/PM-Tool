using Hub.Domain;

namespace Hub.Tests.Domain;

/// §12.14 and Appendix A worked examples for instantiation dates (packet 012, FR-004, FR-005).
public sealed class TemplatePlannerTests
{
    [Fact]
    public void Template_roles_use_anchor_offsets_and_report_each_skip_reason()
    {
        var start = new DateOnly(2026, 10, 5);
        var from = Guid.NewGuid(); var through = Guid.NewGuid(); var selected = Guid.NewGuid(); var omitted = Guid.NewGuid();
        var roles = new[]
        {
            new TplRole(Guid.NewGuid(), "Civil technologist", selected, from, 0, through, -5, 120, null),
            new TplRole(Guid.NewGuid(), "Optional discipline", omitted, from, 0, through, 0, 80, null),
            new TplRole(Guid.NewGuid(), "Undated anchor", selected, Guid.NewGuid(), 0, through, 0, 80, null),
        };
        var plan = TemplateRolePlanner.Plan(roles, new Dictionary<Guid, DateOnly?> { [from] = start, [through] = start.AddDays(30) }, new HashSet<Guid> { selected }, "CalendarDays");
        Assert.Equal((start, start.AddDays(25)), (plan[0].From, plan[0].Through));
        Assert.Equal("discipline_not_selected", plan[1].SkipReason);
        Assert.Equal("start_milestone_undated", plan[2].SkipReason);
    }

    [Fact]
    public void Template_roles_follow_the_project_office_working_calendar()
    {
        var selected = Guid.NewGuid(); var from = Guid.NewGuid(); var through = Guid.NewGuid();
        var role = new TplRole(Guid.NewGuid(), "Surveyor", selected, from, 1, through, 0, 40, null);
        var plan = TemplateRolePlanner.Plan([role], new Dictionary<Guid, DateOnly?> { [from] = new DateOnly(2026, 10, 9), [through] = new DateOnly(2026, 10, 20) }, new HashSet<Guid> { selected }, "WorkingDays", WorkCalendar.Weekdays);
        Assert.Equal(new DateOnly(2026, 10, 12), plan.Single().From);
    }
    static readonly DateOnly Start = new(2026, 10, 5); // Appendix A.2
    static readonly Guid Pm = Guid.NewGuid(), Civil = Guid.NewGuid(), Geo = Guid.NewGuid(), Transport = Guid.NewGuid();
    static readonly Guid M01 = Guid.NewGuid(), M02 = Guid.NewGuid(), M03 = Guid.NewGuid(), M04 = Guid.NewGuid(), M05 = Guid.NewGuid(), M08 = Guid.NewGuid();
    static readonly Guid Chain = Guid.NewGuid(), Loose = Guid.NewGuid();
    static readonly Guid Estimate = Guid.NewGuid(), Package85 = Guid.NewGuid(), Pavement = Guid.NewGuid(), Traffic = Guid.NewGuid(), Unplaced = Guid.NewGuid();
    static readonly Guid Grading = Guid.NewGuid(), Pipes = Guid.NewGuid(), IssuePavement = Guid.NewGuid(), TrafficPlan = Guid.NewGuid(), Controls = Guid.NewGuid(), Meetings = Guid.NewGuid(), OnUnplaced = Guid.NewGuid(), PmReview = Guid.NewGuid();

    static readonly TemplateSnap Snap = new(
        [
            new(M01, 1, TemplateAnchor.ProjectStart, 7), new(M02, 2, TemplateAnchor.ProjectStart, 45), new(M03, 3, TemplateAnchor.ProjectStart, 90),
            new(M04, 4, TemplateAnchor.ProjectStart, 150), new(M05, 5, TemplateAnchor.ProjectStart, 210), new(M08, 8, TemplateAnchor.ProjectStart, 310),
            new(Chain, 9, TemplateAnchor.PreviousMilestone, 30), new(Loose, 10, TemplateAnchor.None, 5),
        ],
        [
            new(Estimate, Pm, M03, -2), new(Package85, Civil, M05, -3), new(Pavement, Geo, M04, -30), new(Traffic, Transport, M05, null), new(Unplaced, Civil, null, 4),
        ],
        [
            new(Grading, Civil, Package85, -12), new(Pipes, Civil, Package85, null), new(IssuePavement, Geo, Pavement, 0), new(TrafficPlan, Transport, Traffic, -5),
            new(Controls, Pm, null, 3), new(Meetings, Pm, null, null), new(OnUnplaced, Civil, Unplaced, -1), new(PmReview, Pm, Package85, -1),
        ],
        [new(IssuePavement, Grading), new(Grading, Pipes), new(TrafficPlan, Grading)]);

    static readonly HashSet<Guid> Six = [Pm, Civil, Geo];
    static readonly Dictionary<Guid, DateOnly?> Contract = new() { [M03] = new DateOnly(2027, 1, 15), [M04] = new DateOnly(2027, 3, 12), [M05] = new DateOnly(2027, 5, 14), [M08] = null };

    [Fact]
    public void Typed_dates_win_offsets_fill_the_rest_and_blanks_stay_undated() // Appendix A.2, §12.14 step 2
    {
        var plan = TemplatePlanner.Plan(Snap, Start, Contract, Six);
        var m = plan.Milestones.ToDictionary(x => x.Id);
        Assert.Equal((new DateOnly(2026, 10, 12), DateSource.Offset), (m[M01].Date!.Value, m[M01].Source));
        Assert.Equal(new DateOnly(2026, 11, 19), m[M02].Date);
        Assert.Equal((new DateOnly(2027, 1, 15), DateSource.Contract), (m[M03].Date!.Value, m[M03].Source));
        Assert.Equal((null, DateSource.None), (m[M08].Date, m[M08].Source)); // left for later confirmation
        Assert.Equal((null, DateSource.None), (m[Chain].Date, m[Chain].Source)); // its previous milestone is undated
        Assert.Null(m[Loose].Date); // no anchor
        Assert.Equal(3, plan.UndatedMilestones);
    }

    [Fact]
    public void A_milestone_after_a_dated_one_follows_it()
    {
        var plan = TemplatePlanner.Plan(Snap, Start, new Dictionary<Guid, DateOnly?> { [M08] = new DateOnly(2027, 8, 11) }, Six);
        Assert.Equal((new DateOnly(2027, 9, 10), DateSource.Offset), (plan.Milestones.Single(x => x.Id == Chain).Date!.Value, plan.Milestones.Single(x => x.Id == Chain).Source));
    }

    [Fact]
    public void Deliverables_and_tasks_follow_their_milestone_and_deliverable() // §12.14 step 3
    {
        var plan = TemplatePlanner.Plan(Snap, Start, Contract, Six);
        var d = plan.Deliverables.ToDictionary(x => x.Id, x => x.Due);
        var t = plan.Tasks.ToDictionary(x => x.Id, x => x.Due);
        Assert.Equal(new DateOnly(2027, 1, 13), d[Estimate]); // M03 − 2
        Assert.Equal(new DateOnly(2027, 5, 11), d[Package85]); // M05 − 3
        Assert.Equal(new DateOnly(2027, 2, 10), d[Pavement]); // M04 − 30
        Assert.Null(d[Unplaced]); // no target milestone
        Assert.Equal(new DateOnly(2027, 4, 29), t[Grading]); // 85 % package − 12 (Appendix A sample)
        Assert.Equal(new DateOnly(2027, 5, 11), t[Pipes]); // no offset: the deliverable's own date
        Assert.Equal(new DateOnly(2026, 10, 8), t[Controls]); // no deliverable: start + 3
        Assert.Null(t[Meetings]); // no deliverable, no offset
        Assert.Null(t[OnUnplaced]); // its deliverable is undated
        Assert.Equal((1, 2), (plan.UndatedDeliverables, plan.UndatedTasks));
        Assert.Null(TemplatePlanner.Plan(Snap, null, Contract, Six).Tasks.Single(x => x.Id == Controls).Due); // no start date yet
    }

    [Fact]
    public void An_unticked_discipline_takes_its_items_and_their_dependencies_with_it() // §12.14 step 6, US2 scenario 2
    {
        var withTransport = TemplatePlanner.Plan(Snap, Start, Contract, new HashSet<Guid>([.. Six, Transport]));
        Assert.Contains(withTransport.Deliverables, x => x.Id == Traffic);
        Assert.Equal(new DateOnly(2027, 5, 14), withTransport.Deliverables.Single(x => x.Id == Traffic).Due); // no offset: on the milestone
        Assert.Equal(3, withTransport.Dependencies.Count);

        var without = TemplatePlanner.Plan(Snap, Start, Contract, Six);
        Assert.DoesNotContain(without.Deliverables, x => x.Id == Traffic);
        Assert.DoesNotContain(without.Tasks, x => x.Id == TrafficPlan);
        Assert.Equal([(IssuePavement, Grading), (Grading, Pipes)], without.Dependencies.Select(x => (x.Predecessor, x.Successor)));

        var noGeo = TemplatePlanner.Plan(Snap, Start, Contract, new HashSet<Guid> { Pm, Civil });
        Assert.Equal([(Grading, Pipes)], noGeo.Dependencies.Select(x => (x.Predecessor, x.Successor))); // a cross-discipline link goes with Geotechnical
        var noCivil = TemplatePlanner.Plan(Snap, Start, Contract, new HashSet<Guid> { Pm, Geo });
        Assert.DoesNotContain(noCivil.Tasks, x => x.Id == PmReview); // a PM task on a Civil deliverable leaves with the deliverable
        Assert.Equal(new DateOnly(2027, 5, 10), without.Tasks.Single(x => x.Id == PmReview).Due);
    }

    // Packet 045 S06 (§49.4): optional working-day offsets. 2026-10-09 is a Friday.
    static readonly DateOnly Fri = new(2026, 10, 9), Sat = new(2026, 10, 10), Mon = new(2026, 10, 12), Tue = new(2026, 10, 13);
    static readonly Guid A = Guid.NewGuid(), B = Guid.NewGuid(), Dl = Guid.NewGuid(), Dl0 = Guid.NewGuid(), Tk = Guid.NewGuid(), Tk0 = Guid.NewGuid(), Loose1 = Guid.NewGuid();
    static TemplateSnap Small(int? msOffset, int? delOffset, int? taskOffset) => new(
        [new(A, 1, TemplateAnchor.ProjectStart, msOffset), new(B, 2, TemplateAnchor.PreviousMilestone, 1)],
        [new(Dl, Pm, A, delOffset), new(Dl0, Pm, A, null)],
        [new(Tk, Pm, Dl, taskOffset), new(Tk0, Pm, Dl, 0), new(Loose1, Pm, null, 1)],
        []);
    static readonly HashSet<Guid> PmOnly = [Pm];
    static readonly Dictionary<Guid, DateOnly?> NoTyped = [];

    [Fact]
    public void Calendar_days_are_unchanged_without_a_calendar() // FR-RCV-11, AC-RCV-14
    {
        var before = TemplatePlanner.Plan(Snap, Start, Contract, Six);
        var after = TemplatePlanner.Plan(Snap, Start, Contract, Six, null);
        Assert.Equal(before.Milestones, after.Milestones);
        Assert.Equal(before.Deliverables, after.Deliverables);
        Assert.Equal(before.Tasks, after.Tasks);
        var everyDay = new WorkCalendar(scheduled: _ => true); // a calendar where every day works counts like calendar days
        Assert.Equal(before.Tasks, TemplatePlanner.Plan(Snap, Start, Contract, Six, everyDay).Tasks);
        Assert.Equal(before.Deliverables, TemplatePlanner.Plan(Snap, Start, Contract, Six, everyDay).Deliverables);
    }

    [Fact]
    public void Working_days_skip_weekends_and_office_holidays() // FR-RCV-12, AC-RCV-15
    {
        var plan = TemplatePlanner.Plan(Small(1, 1, 1), Fri, NoTyped, PmOnly, WorkCalendar.Weekdays);
        Assert.Equal((Mon, DateSource.Offset), (plan.Milestones[0].Date!.Value, plan.Milestones[0].Source));
        Assert.Equal(Tue, plan.Milestones[1].Date); // previous milestone + 1 working day
        Assert.Equal(Tue, plan.Deliverables.Single(x => x.Id == Dl).Due);
        Assert.Equal(new DateOnly(2026, 10, 14), plan.Tasks.Single(x => x.Id == Tk).Due);
        Assert.Equal(Mon, plan.Tasks.Single(x => x.Id == Loose1).Due); // no deliverable: start + 1 working day

        var holiday = TemplatePlanner.Plan(Small(1, 1, 1), Fri, NoTyped, PmOnly, new WorkCalendar([Mon]));
        Assert.Equal(Tue, holiday.Milestones[0].Date); // the Monday is an office holiday
        Assert.Equal(Tue, holiday.Tasks.Single(x => x.Id == Loose1).Due);
        Assert.Equal(Sat, TemplatePlanner.Plan(Small(1, 1, 1), Fri, NoTyped, PmOnly).Milestones[0].Date); // calendar days: Fri + 1 is Saturday
    }

    [Fact]
    public void Negative_offsets_count_back_to_a_working_day() // FR-RCV-12, AC-RCV-16
    {
        var plan = TemplatePlanner.Plan(Small(1, -2, -1), Fri, new Dictionary<Guid, DateOnly?> { [A] = Tue }, PmOnly, WorkCalendar.Weekdays);
        Assert.Equal(Fri, plan.Deliverables.Single(x => x.Id == Dl).Due); // Tuesday − 2 working days
        Assert.Equal(new DateOnly(2026, 10, 8), plan.Tasks.Single(x => x.Id == Tk).Due);
        Assert.Equal(new DateOnly(2026, 10, 8), TemplatePlanner.Plan(Small(1, -2, -1), Fri, new Dictionary<Guid, DateOnly?> { [A] = Tue }, PmOnly, new WorkCalendar([Mon])).Deliverables.Single(x => x.Id == Dl).Due);
        Assert.Equal(new DateOnly(2026, 10, 11), TemplatePlanner.Plan(Small(1, -2, -1), Fri, new Dictionary<Guid, DateOnly?> { [A] = Tue }, PmOnly).Deliverables.Single(x => x.Id == Dl).Due); // calendar days unchanged
    }

    [Fact]
    public void Offset_zero_and_typed_dates_never_move() // FR-RCV-12, AC-RCV-16
    {
        var plan = TemplatePlanner.Plan(Small(1, 0, 0), Fri, new Dictionary<Guid, DateOnly?> { [A] = Sat }, PmOnly, WorkCalendar.Weekdays);
        Assert.Equal((Sat, DateSource.Contract), (plan.Milestones[0].Date!.Value, plan.Milestones[0].Source)); // typed Saturday stays
        Assert.Equal(Sat, plan.Deliverables.Single(x => x.Id == Dl).Due); // offset 0
        Assert.Equal(Sat, plan.Deliverables.Single(x => x.Id == Dl0).Due); // no offset
        Assert.Equal(Sat, plan.Tasks.Single(x => x.Id == Tk0).Due);
        Assert.Equal(Mon, plan.Milestones[1].Date); // a later offset still lands on a working day
        Assert.Equal(Sat, WorkCalendar.Weekdays.Add(Sat, 0));
    }
}
