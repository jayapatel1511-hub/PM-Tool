using Hub.Domain;

namespace Hub.Tests.Domain;

/// §12.14 and Appendix A worked examples for instantiation dates (packet 012, FR-004, FR-005).
public sealed class TemplatePlannerTests
{
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
}
