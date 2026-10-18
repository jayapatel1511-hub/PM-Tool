using Hub.Domain;

namespace Hub.Tests.Domain;

public sealed class ScheduleTests
{
    [Theory, Trait("Spec", "AC-INT-17")]
    [InlineData(ProjectStatus.Active, true, 12, true)]
    [InlineData(ProjectStatus.Active, true, 0, false)]
    [InlineData(ProjectStatus.Active, true, -1, false)]
    [InlineData(ProjectStatus.Active, false, 12, false)]
    [InlineData(ProjectStatus.Setup, true, 12, false)]
    [InlineData(ProjectStatus.Complete, true, 12, false)]
    [InlineData(ProjectStatus.Archived, true, 12, false)]
    [InlineData(ProjectStatus.Cancelled, true, 12, false)]
    public void Latest_planned_note_is_only_for_an_active_project_after_a_known_target(string status, bool known, int days, bool shown)
    {
        var target = new DateOnly(2026, 10, 28);
        var note = Evaluator.LatestPlanned(status, known ? target : null, [new(Guid.NewGuid(), "1234-M07", null, target.AddDays(days), false)]);
        Assert.Equal(shown, note is not null);
        if (shown) { Assert.Equal(target.AddDays(12), note!.Date); Assert.Equal("1234-M07", note.ItemKey); Assert.Equal(target, note.TargetDate); Assert.Equal("FR-INT-15", note.Rule); }
    }

    [Fact, Trait("Spec", "AC-INT-17")]
    public void Latest_planned_ignores_closed_and_undated_items_uses_known_start_and_breaks_ties_by_key()
    {
        var target = new DateOnly(2026, 10, 28);
        DateItem[] items = [new(Guid.NewGuid(), "CLOSED", null, target.AddDays(30), true), new(Guid.NewGuid(), "UNKNOWN", null, null, false),
            new(Guid.NewGuid(), "EARLIER", target.AddDays(1), target.AddDays(3), false), new(Guid.NewGuid(), "START-ONLY", target.AddDays(12), null, false),
            new(Guid.NewGuid(), "1234-M07", null, target.AddDays(12), false)];
        var note = Evaluator.LatestPlanned(ProjectStatus.Active, target, items);
        Assert.Equal("1234-M07", note!.ItemKey); Assert.Equal(target.AddDays(12), note.Date);
        Assert.Equal(note, Evaluator.LatestPlanned(ProjectStatus.Active, target, items.Reverse()));
        Assert.Equal("START-ONLY", Evaluator.LatestPlanned(ProjectStatus.Active, target, items.Where(i => i.Key != "1234-M07"))!.ItemKey);
        Assert.Null(Evaluator.LatestPlanned(ProjectStatus.Active, target, []));
        Assert.Null(Evaluator.LatestPlanned(ProjectStatus.Active, target, items.Take(2)));
    }

    [Theory, Trait("Spec", "AC-INT-16")]
    [InlineData(false)] [InlineData(true)]
    public void Baselines_beside_originals_do_not_change_the_rules_engine(bool on)
    {
        var s = new Scenario(); var original = s.Today.AddDays(20);
        s.Settings = s.Settings with { Flags = new Dictionary<string, bool> { ["schedule_baseline"] = on } };
        s.Milestone("M", original.AddDays(17), original: original);
        s.Deliverable("D", original.AddDays(17), originalDue: original);
        var before = System.Text.Json.JsonSerializer.Serialize(s.Run());
        s.Milestones[0] = s.Milestones[0] with { BaselineDate = original.AddDays(14) };
        s.Deliverables[0] = s.Deliverables[0] with { BaselineStartDate = original, BaselineDueDate = original.AddDays(14) };
        var after = s.Run(); Assert.Equal(17, after.Milestones.Single().SlipDays); Assert.Equal(17, after.Deliverables.Single().SlipDays);
        Assert.Equal(before, System.Text.Json.JsonSerializer.Serialize(after));
    }

    [Fact, Trait("Spec", "AC-INT-16")]
    public void Since_rebaseline_is_a_separate_signed_calendar_difference_and_missing_stays_unknown()
    {
        var original = new DateOnly(2026, 10, 16); var baseline = original.AddDays(14);
        Assert.Equal(3, Evaluator.SinceBaseline(original.AddDays(17), baseline));
        Assert.Equal(-3, Evaluator.SinceBaseline(original.AddDays(11), baseline));
        Assert.Equal(0, Evaluator.SinceBaseline(baseline, baseline));
        Assert.Null(Evaluator.SinceBaseline(original, null)); Assert.Null(Evaluator.SinceBaseline(null, baseline));
        Assert.True(Evaluator.SlipShown(null, baseline, null, original, 3));
        Assert.False(Evaluator.SlipShown(null, original.AddDays(17), null, baseline, 3));
        Assert.True(Evaluator.SlipShown(null, original.AddDays(18), null, baseline, 3));
    }

    [Theory, Trait("Spec", "AC-INT-14")]
    [InlineData(0, 0, 0, false)]
    [InlineData(0, 0, 2, true)]
    [InlineData(3, 0, 2, false)]
    [InlineData(3, 0, 3, false)]
    [InlineData(3, 0, 4, true)]
    [InlineData(3, 0, -3, false)]
    [InlineData(3, 0, -4, true)]
    [InlineData(3, 4, 0, true)]
    [InlineData(3, -4, 0, true)]
    [InlineData(3, 3, 0, false)]
    [InlineData(3, 4, 2, true)]
    public void Slip_display_uses_either_date_and_a_strict_calendar_day_threshold(int threshold, int startMove, int dueMove, bool shown)
    {
        var start = new DateOnly(2026, 10, 16); var due = start.AddDays(14);
        Assert.Equal(shown, Evaluator.SlipShown(start.AddDays(startMove), due.AddDays(dueMove), start, due, threshold));
    }

    [Fact, Trait("Spec", "AC-INT-14")]
    public void Missing_dates_do_not_invent_a_measurable_difference_but_zero_keeps_legacy_start_changes()
    {
        var due = new DateOnly(2026, 10, 16);
        Assert.False(Evaluator.SlipShown(null, null, null, null, 0));
        Assert.True(Evaluator.SlipShown(due, due, null, due, 0));
        Assert.True(Evaluator.SlipShown(null, due, due, due, 0));
        Assert.False(Evaluator.SlipShown(due, due, null, due, 3));
        Assert.False(Evaluator.SlipShown(null, due, due, due, 3));
        Assert.True(Evaluator.SlipShown(null, due.AddDays(4), null, due, 3));
    }

    [Fact, Trait("Spec", "AC-INT-14")]
    public void Slip_display_setting_defaults_to_zero_and_rejects_invalid_thresholds()
    {
        var def = OrgSettings.Def("slip_display_days")!;
        Assert.Equal(0, def.Default); Assert.Equal(0, new OrgSettings().SlipDisplayDays);
        var value = System.Text.Json.JsonSerializer.SerializeToElement(3);
        Assert.Equal(3, OrgSettings.From(new Dictionary<string, System.Text.Json.JsonElement> { [def.Key] = value }).SlipDisplayDays);
        foreach (var valid in new[] { 0, 3, 3650 }) Assert.Null(OrgSettings.Validate(def, System.Text.Json.JsonSerializer.SerializeToElement(valid)));
        foreach (var invalid in new object[] { -1, 3651, 3.5, "3", true }) Assert.NotNull(OrgSettings.Validate(def, System.Text.Json.JsonSerializer.SerializeToElement(invalid)));
    }

    static readonly DateOnly Thursday = new(2026, 10, 15);
    static DateItem Item(TaskSnap t) => new(t.Id, t.Key, t.StartDate, t.DueDate, TaskStatuses.IsTerminal(t.Status));
    static DateItem Item(DeliverableSnap d) => new(d.Id, d.Key, d.StartDate, d.DueDate, DeliverableStatus.IsTerminal(d.Status));

    [Theory, Trait("Spec", "AC-INT-09")]
    [InlineData(true, false, DependencyTypes.FinishToStart)]
    [InlineData(true, true, DependencyTypes.FinishToStart)]
    [InlineData(true, false, DependencyTypes.StartToStart)]
    [InlineData(true, true, DependencyTypes.StartToStart)]
    [InlineData(true, false, DependencyTypes.FinishToFinish)]
    [InlineData(true, true, DependencyTypes.FinishToFinish)]
    [InlineData(false, false, DependencyTypes.FinishToStart)]
    [InlineData(false, true, DependencyTypes.FinishToStart)]
    [InlineData(false, false, DependencyTypes.StartToStart)]
    [InlineData(false, true, DependencyTypes.StartToStart)]
    [InlineData(false, false, DependencyTypes.FinishToFinish)]
    [InlineData(false, true, DependencyTypes.FinishToFinish)]
    public void Earliest_dates_with_lag_produce_no_evaluator_D12(bool task, bool working, string type)
    {
        var s = new Scenario { Today = Thursday, Settings = new() { WorkingDaysEnabled = working } };
        var after = DependencyRules.After(working ? WorkCalendar.Weekdays : null);
        var expected = new DateOnly(2026, 10, working ? 19 : 17);
        var a = s.Task("A", start: Thursday, due: Thursday);
        var b = s.Task("B", start: Thursday, due: Thursday);
        var da = s.Deliverable("DA", Thursday, start: Thursday);
        var db = s.Deliverable("DB", Thursday, start: Thursday);
        var items = task ? s.Tasks.Select(Item).ToArray() : s.Deliverables.Select(Item).ToArray();
        var edge = new DependencySnap(Guid.NewGuid(), items[0].Id, items[1].Id, 2, type);
        var context = DependencyRules.Context(items[1], items, [edge], null, task, true, after);
        Assert.Equal(type == DependencyTypes.FinishToFinish ? null : expected, context.EarliestStart?.Date);
        Assert.Equal(type == DependencyTypes.FinishToFinish || task && type == DependencyTypes.FinishToStart ? expected : null, context.EarliestDue?.Date);
        foreach (var reason in (context.EarliestStart?.Reasons ?? []).Concat(context.EarliestDue?.Reasons ?? []))
        {
            Assert.Contains(items[0].Key, reason.Text); Assert.Contains(type, reason.Text); Assert.Contains("lag 2", reason.Text);
        }
        Assert.Equal("D-12", context.Inconsistencies.Single().Rule);
        if (task)
        {
            s.Deps.Add(edge); s.Replace(s.Tasks, b, b with { StartDate = context.EarliestStart?.Date, DueDate = context.EarliestDue?.Date });
            Assert.DoesNotContain(s.Run().T(b).Inconsistencies, r => r.Rule == "D-12");
        }
        else
        {
            s.DeliverableDeps.Add(edge); s.Replace(s.Deliverables, db, db with { StartDate = context.EarliestStart?.Date, DueDate = context.EarliestDue?.Date });
            Assert.DoesNotContain(s.Run().D(db).Inconsistencies, r => r.Rule == "D-12");
        }
    }

    [Fact, Trait("Spec", "AC-INT-09")]
    public void All_bounds_contribute_to_the_maximum_and_office_holidays_are_skipped()
    {
        var a = new DateItem(Guid.NewGuid(), "A", null, Thursday, false);
        var b = new DateItem(Guid.NewGuid(), "B", null, Thursday.AddDays(1), false);
        var c = new DateItem(Guid.NewGuid(), "C", Thursday, Thursday, false);
        DependencySnap[] links = [new(Guid.NewGuid(), a.Id, c.Id, 2), new(Guid.NewGuid(), b.Id, c.Id, 2), new(Guid.NewGuid(), a.Id, c.Id, 9, DependencyTypes.StartToStart)];
        var context = DependencyRules.Context(c, [a, b, c], links, null, true, true, DependencyRules.After(new WorkCalendar([Thursday.AddDays(4)])));
        Assert.Equal(new DateOnly(2026, 10, 21), context.EarliestStart?.Date); Assert.Equal(context.EarliestStart?.Date, context.EarliestDue?.Date);
        Assert.Equal(2, context.EarliestStart!.Reasons.Count); Assert.Equal(2, context.EarliestDue!.Reasons.Count);
        var missing = DependencyRules.Context(c, [a with { DueDate = null }, c], [links[0]], null, true, true, DependencyRules.After(null));
        Assert.Null(missing.EarliestStart); Assert.Null(missing.EarliestDue); Assert.Empty(missing.Inconsistencies);
    }

    [Theory, Trait("Spec", "AC-INT-10")]
    [InlineData(true, "T-04")]
    [InlineData(false, "DL-03")]
    public void Latest_due_uses_the_same_parent_rule_as_the_evaluator(bool task, string rule)
    {
        var s = new Scenario(); var milestone = s.Milestone("M", Thursday); var deliverable = s.Deliverable("D", Thursday, milestone.Id);
        var child = s.Task("T", due: Thursday.AddDays(1), deliverable: deliverable.Id);
        if (!task) deliverable = s.Replace(s.Deliverables, deliverable, deliverable with { DueDate = Thursday.AddDays(1) });
        var item = task ? Item(child) : Item(deliverable);
        var context = DependencyRules.Context(item, [item], [], new(task ? "D" : "M", Thursday), task, true, DependencyRules.After(null));
        Assert.Equal(Thursday, context.LatestDue?.Date); Assert.Equal(rule, context.LatestDue!.Reasons.Single().Rule);
        Assert.Equal(task ? s.Run().T(child).Inconsistencies.Single() : s.Run().D(deliverable).Inconsistencies.Single(), context.Inconsistencies.Single());
        Assert.Empty(DependencyRules.Context(item with { DueDate = Thursday }, [item], [], new("Parent", Thursday), task, true, DependencyRules.After(null)).Inconsistencies);
    }

    [Fact, Trait("Spec", "AC-INT-13")]
    public void Successor_warnings_are_new_for_the_same_pair_unique_and_only_for_evaluated_items()
    {
        var a = new DateItem(Guid.NewGuid(), "A", Thursday, Thursday, false);
        var b = new DateItem(Guid.NewGuid(), "B", Thursday.AddDays(1), Thursday.AddDays(8), false);
        var c = new DateItem(Guid.NewGuid(), "C", Thursday.AddDays(-1), Thursday.AddDays(8), false);
        var done = b with { Id = Guid.NewGuid(), Key = "Done", Terminal = true };
        var other = a with { Id = Guid.NewGuid(), Key = "Other", DueDate = Thursday.AddDays(2) };
        DateItem[] items = [a, b, c, done, other];
        DependencySnap[] links = [new(Guid.NewGuid(), a.Id, b.Id), new(Guid.NewGuid(), a.Id, b.Id), new(Guid.NewGuid(), a.Id, c.Id), new(Guid.NewGuid(), a.Id, done.Id), new(Guid.NewGuid(), other.Id, b.Id)];
        var context = DependencyRules.Context(a with { DueDate = Thursday.AddDays(4) }, items, links, null, true, true, DependencyRules.After(null));
        Assert.Equal(b.Id, context.Successors.Single().Id); Assert.Equal("D-12", context.Successors.Single().Reason.Rule);
        Assert.Equal(Thursday, a.DueDate); Assert.Empty(DependencyRules.Context(a, items, links, null, true, true, DependencyRules.After(null)).Successors);
        Assert.Empty(DependencyRules.Context(a with { DueDate = Thursday.AddDays(4) }, items, links, null, true, false, DependencyRules.After(null)).Successors);
        Assert.Empty(DependencyRules.Context(b with { Terminal = true }, items, links, null, true, true, DependencyRules.After(null)).Inconsistencies);
    }

    [Fact, Trait("Spec", "AC-INT-13")]
    public void Dragged_items_report_only_new_parent_and_predecessor_inconsistencies()
    {
        var a = new DateItem(Guid.NewGuid(), "A", Thursday, Thursday, false);
        var b = new DateItem(Guid.NewGuid(), "B", Thursday.AddDays(1), Thursday.AddDays(2), false);
        DependencySnap[] edges = [new(Guid.NewGuid(), a.Id, b.Id)]; var limit = new DateLimit("Parent", Thursday.AddDays(3));
        var proposed = b with { StartDate = Thursday.AddDays(-1), DueDate = Thursday.AddDays(4) };
        var context = DependencyRules.Context(proposed, [a, b], edges, limit, true, true, DependencyRules.After(null));
        Assert.Equal(new[] { "T-04", "D-12" }, context.NewInconsistencies.Select(r => r.Rule));
        var already = DependencyRules.Context(proposed with { StartDate = Thursday.AddDays(-2), DueDate = Thursday.AddDays(5) }, [a, proposed], edges, limit, true, true, DependencyRules.After(null));
        Assert.Equal(2, already.Inconsistencies.Count); Assert.Empty(already.NewInconsistencies);
        Assert.Empty(DependencyRules.Context(proposed, [a, b], edges, limit, true, false, DependencyRules.After(null)).NewInconsistencies);
    }
}
