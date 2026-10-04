using System.Text.Json;
using Hub.Domain;

namespace Hub.Tests.Domain;

public sealed class PlanningTests
{
    static readonly DateOnly W = new(2026, 10, 5);
    static readonly DateTimeOffset Now = new(2026, 10, 5, 13, 0, 0, TimeSpan.Zero);
    static EntryWeek E(string confidence, decimal hours, Guid? project = null, int offset = 0, int age = 0, Guid? id = null)
        => new(id ?? Guid.NewGuid(), project, confidence, hours, W.AddDays(offset * 7), Now.AddDays(-age));

    [Theory]
    [InlineData("8 h proposal support", 8, "proposal support", false)]
    [InlineData("8h proposal support", 8, "proposal support", false)]
    [InlineData("8 — proposal support", 8, "proposal support", false)]
    [InlineData("8 hrs - proposal support", 8, "proposal support", false)]
    [InlineData("8: proposal support", 8, "proposal support", false)]
    [InlineData("~8 h upcoming review", 8, "upcoming review", true)]
    [InlineData("~ 8 h upcoming review", 8, "upcoming review", true)]
    [InlineData("7.5h admin", 7.5, "admin", false)]
    [InlineData("  12 h  Hwy 7 review support ", 12, "Hwy 7 review support", false)]
    [InlineData("8 hotels", 8, "hotels", false)]
    public void Quick_add_supported_forms(string text, decimal hours, string label, bool possible)
    {
        var result = PlanningRules.ParseQuickAdd(text, 80);
        Assert.Equal(new QuickAdd(hours, label, possible), result);
    }

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData(" ")]
    [InlineData("8 h")] [InlineData("8 hr")] [InlineData("8 hrs")]
    [InlineData("8 hour")] [InlineData("8 hours")] [InlineData("8")]
    [InlineData("h proposal")] [InlineData("-8 h x")]
    [InlineData("8.25 h x")] [InlineData("100 h x")] [InlineData("0 h x")]
    public void Quick_add_refuses_invalid_input(string? text) => Assert.Null(PlanningRules.ParseQuickAdd(text, 80));

    [Fact]
    public void Quick_add_label_length_boundary()
    {
        Assert.NotNull(PlanningRules.ParseQuickAdd("8 h " + new string('x', 120), 80));
        Assert.Null(PlanningRules.ParseQuickAdd("8 h " + new string('x', 121), 80));
    }

    [Fact]
    public void Week_range_requires_Mondays_and_at_most_104_inclusive_weeks()
    {
        Assert.Null(PlanningRules.ValidateWeeks(W, W.AddDays(14)));
        Assert.Equal("planning.week", PlanningRules.ValidateWeeks(W.AddDays(1), W.AddDays(14)));
        Assert.Equal("planning.week", PlanningRules.ValidateWeeks(W, W.AddDays(15)));
        Assert.Equal("error.date_range", PlanningRules.ValidateWeeks(W.AddDays(14), W));
        Assert.Null(PlanningRules.ValidateWeeks(W, W.AddDays(721)));
        Assert.Equal("planning.span", PlanningRules.ValidateWeeks(W, W.AddDays(728)));
    }

    [Theory]
    [InlineData(8, true)] [InlineData(7.5, true)] [InlineData(80, true)]
    [InlineData(0, false)] [InlineData(-1, false)] [InlineData(8.25, false)] [InlineData(80.5, false)]
    public void Half_hour_positive_bounded_hours(decimal hours, bool valid)
        => Assert.Equal(valid ? null : "planning.hours", PlanningRules.ValidateHours(hours, 80));

    [Theory]
    [InlineData(-7, 0)] [InlineData(0, 8)] [InlineData(7, 8)] [InlineData(14, 8)]
    [InlineData(21, 0)] [InlineData(1, 0)]
    public void Weekly_hours_are_inclusive_and_Monday_only(int offset, decimal expected)
        => Assert.Equal(expected, PlanningRules.WeekHours(W, W.AddDays(14), 8, W.AddDays(offset)));

    [Theory]
    [InlineData(true, false, "Confirmed", "Confirmed")]
    [InlineData(true, true, "Confirmed", "Possible")]
    [InlineData(false, false, "Draft", "Expected")]
    [InlineData(false, true, "Draft", "Possible")]
    public void Defaults_keep_confidence_and_visibility_distinct(bool self, bool possible, string visibility, string confidence)
        => Assert.Equal((visibility, confidence), PlanningRules.Defaults(self, possible));

    [Theory]
    [InlineData(false, "Draft", "Published", true)]
    [InlineData(false, "Published", "Confirmed", true)]
    [InlineData(false, "Confirmed", "Draft", true)]
    [InlineData(false, "Draft", "Confirmed", true)]
    [InlineData(false, "Draft", "Draft", false)]
    [InlineData(false, "Bogus", "Draft", false)]
    [InlineData(false, "Draft", "Bogus", false)]
    [InlineData(true, "Confirmed", "Published", false)]
    public void Visibility_changes_require_manager_entry_and_different_valid_states(bool self, string from, string to, bool allowed)
        => Assert.Equal(allowed, PlanningRules.StepVisibility(self, from, to));

    [Theory]
    [InlineData(8, 0, "Unavailable", 8, 0)]
    [InlineData(8, 4, "Reduced", 4, 0)]
    [InlineData(8, 10, "Additional", 0, 2)]
    [InlineData(8, 6, "Additional", 0, 0)]
    [InlineData(8, 10, "Reduced", 0, 0)]
    [InlineData(8, null, null, 0, 0)]
    [InlineData(8, 0, "Bogus", 0, 0)]
    [InlineData(0, 0, "Unavailable", 0, 0)]
    public void Time_away_and_extra_capacity_follow_category(decimal normal, object? available, string? category, decimal away, decimal extra)
        => Assert.Equal((away, extra), PlanningRules.TimeAway(normal, available is null ? null : Convert.ToDecimal(available), category));

    [Fact]
    public void Time_away_refuses_negative_inputs()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => PlanningRules.TimeAway(-1, null, null));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlanningRules.TimeAway(8, -1, "Reduced"));
    }

    [Fact]
    public void Bands_include_possible_as_context_without_consuming_remaining()
    {
        var bands = PlanningRules.Combine(40, [E("Confirmed", 30), E("Expected", 13), E("Possible", 8)], new Dictionary<Guid, decimal>());
        Assert.Equal((0m, 30m, 13m, 8m, -3m), (bands.Approved, bands.Confirmed, bands.Expected, bands.Possible, bands.Remaining));
        Assert.All(bands.Entries, x => Assert.Equal(0, x.Covered));
    }

    [Theory]
    [InlineData("Confirmed", 16, 12, 4, 16, 0, 0)]
    [InlineData("Expected", 8, 8, 0, 12, 0, 0)]
    [InlineData("Possible", 20, 12, 8, 12, 0, 8)]
    public void Approved_hours_cover_the_same_project_first(string confidence, decimal hours, decimal covered, decimal counted, decimal confirmed, decimal expected, decimal possible)
    {
        var project = Guid.NewGuid(); var entry = E(confidence, hours, project);
        var bands = PlanningRules.Combine(40, [entry], new Dictionary<Guid, decimal> { [project] = 12 });
        Assert.Equal(new EntryCount(entry.Id, counted, covered), bands.Entries.Single());
        Assert.Equal((confirmed, expected, possible), (bands.Confirmed, bands.Expected, bands.Possible));
    }

    [Fact]
    public void Coverage_orders_confidence_then_start_creation_and_identifier()
    {
        var p = Guid.NewGuid();
        var confirmed = E("Confirmed", 8, p);
        var expected = E("Expected", 6, p);
        var bands = PlanningRules.Combine(40, [expected, confirmed], new Dictionary<Guid, decimal> { [p] = 12 });
        Assert.Equal([new(confirmed.Id, 0, 8), new(expected.Id, 2, 4)], bands.Entries);
        Assert.Equal(2m, bands.Expected);
        var early = E("Expected", 8, p, -1);
        var late = E("Expected", 8, p);
        bands = PlanningRules.Combine(40, [late, early], new Dictionary<Guid, decimal> { [p] = 10 });
        Assert.Equal([new(early.Id, 0, 8), new(late.Id, 6, 2)], bands.Entries);
        var oldest = E("Expected", 8, p, age: 1);
        var newer = E("Expected", 8, p);
        Assert.Equal(oldest.Id, PlanningRules.Combine(40, [newer, oldest], new Dictionary<Guid, decimal> { [p] = 8 }).Entries[0].Id);
        var low = E("Expected", 8, p, id: Guid.Parse("00000000-0000-0000-0000-000000000001"));
        var high = E("Expected", 8, p, id: Guid.Parse("00000000-0000-0000-0000-000000000002"));
        Assert.Equal(low.Id, PlanningRules.Combine(40, [high, low], new Dictionary<Guid, decimal> { [p] = 8 }).Entries[0].Id);
    }

    [Fact]
    public void Zero_other_project_and_empty_bands_reconcile()
    {
        var p = Guid.NewGuid(); var q = Guid.NewGuid();
        var bands = PlanningRules.Combine(40, [E("Expected", 5, q), E("Expected", 6)], new Dictionary<Guid, decimal> { [p] = 12, [q] = 0 });
        Assert.Equal((12m, 11m, 17m), (bands.Confirmed, bands.Expected, bands.Remaining));
        var empty = PlanningRules.Combine(40, [], new Dictionary<Guid, decimal>());
        Assert.Equal((0m, 0m, 0m, 0m, 40m), (empty.Approved, empty.Confirmed, empty.Expected, empty.Possible, empty.Remaining));
        Assert.Empty(empty.Entries);
        Assert.Throws<ArgumentOutOfRangeException>(() => PlanningRules.Combine(-1, [], new Dictionary<Guid, decimal>()));
        Assert.Throws<ArgumentOutOfRangeException>(() => PlanningRules.Combine(40, [E("Confirmed", -1)], new Dictionary<Guid, decimal>()));
    }

    [Theory]
    [InlineData(30, 13, 8, 40, 105, true)] [InlineData(30, 13, 8, 40, 110, false)]
    [InlineData(42, 0, 0, 40, 105, false)] [InlineData(1, 0, 0, 0, 105, true)]
    [InlineData(0, 0, 5, 0, 105, false)]
    public void Over_planned_uses_strict_threshold_and_handles_zero_capacity(decimal confirmed, decimal expected, decimal possible, decimal capacity, int threshold, bool result)
        => Assert.Equal(result, PlanningRules.OverPlanned(new(0, confirmed, expected, possible, 0, []), capacity, threshold));

    [Fact]
    public void Under_planned_requires_complete_eligible_consecutive_future_weeks()
    {
        UnderWeek[] weeks = [new(40, 0, 10, false), new(40, 0, 10, false), new(40, 0, 30, false), new(40, 0, 0, false)];
        Assert.Equal([true, false, false, false], PlanningRules.UnderPlanned(weeks, 0, 50, 2, false));
        Assert.Equal([false, false, false, false], PlanningRules.UnderPlanned(weeks, 1, 50, 2, false));
        Assert.Equal([false, false, false, false], PlanningRules.UnderPlanned(weeks, 0, 50, 2, true));
        Assert.Equal([false, false, false, false], PlanningRules.UnderPlanned(weeks, 0, 0, 2, false));
        Assert.Equal([false, false, false, false], PlanningRules.UnderPlanned(weeks, 0, 50, 0, false));
        Assert.Equal([false, false, false, false], PlanningRules.UnderPlanned([weeks[0], weeks[1] with { TimeAway = 8 }, weeks[2], weeks[3]], 0, 50, 2, false));
        Assert.Equal([false, false, false, false], PlanningRules.UnderPlanned([weeks[0] with { UnestimatedDue = true }, weeks[1], weeks[2], weeks[3]], 0, 50, 2, false));
        Assert.Equal([false, false], PlanningRules.UnderPlanned([new(0, 0, 0, false), new(0, 0, 0, false)], -1, 50, 2, false));
        Assert.Equal([false, false], PlanningRules.UnderPlanned([new(0, 0, 1, false), new(0, 0, 0, false)], 0, 50, 2, false));
    }

    [Fact]
    public void Stale_is_strict_and_excludes_ended_entries()
    {
        Assert.True(PlanningRules.Stale(W.AddDays(7), W, Now.AddDays(-29), Now, 28));
        Assert.False(PlanningRules.Stale(W, W, Now.AddDays(-28), Now, 28));
        Assert.False(PlanningRules.Stale(W.AddDays(-7), W, Now.AddDays(-29), Now, 28));
    }

    [Theory]
    [InlineData("planning_horizon_weeks", 6, 26)]
    [InlineData("planning_over_pct", 50, 300)]
    [InlineData("planning_under_pct", 0, 100)]
    [InlineData("planning_under_weeks", 1, 12)]
    [InlineData("planning_stale_days", 1, 365)]
    [InlineData("planning_max_hours_per_week", 1, 168)]
    [InlineData("coordination_lookahead_weeks", 1, 12)]
    public void Each_setting_uses_its_own_range(string key, int min, int max)
    {
        var def = OrgSettings.Defs.Single(x => x.Key == key);
        Assert.Null(OrgSettings.Validate(def, JsonSerializer.SerializeToElement(min)));
        Assert.Null(OrgSettings.Validate(def, JsonSerializer.SerializeToElement(max)));
        Assert.NotNull(OrgSettings.Validate(def, JsonSerializer.SerializeToElement(min - 1)));
        Assert.NotNull(OrgSettings.Validate(def, JsonSerializer.SerializeToElement(max + 1)));
        Assert.NotNull(OrgSettings.Validate(def, JsonSerializer.SerializeToElement("8")));
        Assert.NotNull(OrgSettings.Validate(def, JsonSerializer.SerializeToElement(7.5)));
    }
}
