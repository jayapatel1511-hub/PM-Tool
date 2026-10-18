using Hub.Domain;

namespace Hub.Tests.Domain;

public sealed class OnboardingAndRouteCounterTests
{
    [Fact] // AC-PRO-02
    public void Read_only_has_no_getting_started_steps()
    {
        Assert.Empty(Onboarding.StepsFor([SystemRole.ReadOnly], new()));
        Assert.Equal(new[] { "change-status", "comment", "self-plan", "create-project", "add-milestone", "assign-task", "activate-project" },
            Onboarding.StepsFor([SystemRole.ProjectManager], new()).Select(x => x.Key));
    }

    [Fact]
    public void AC_PRO_01_Combined_roles_keep_catalogue_order_without_duplicate_everyone_steps()
    {
        var groups = Onboarding.Groups([SystemRole.ProjectManager, SystemRole.Supervisor], new());
        Assert.Equal(new[] { "Everyone", SystemRole.ProjectManager, SystemRole.Supervisor }, groups.Select(g => g.Role));
        Assert.Equal(10, groups.Sum(g => g.Steps.Count));
        Assert.Empty(Onboarding.Groups([SystemRole.ReadOnly], new()));
    }

    [Theory] // AC-PRO-08
    [InlineData("projects/:number/tasks", "projects/:number/tasks")]
    [InlineData("my-work", "my-work")]
    [InlineData("/projects/123/dashboard", null)]
    [InlineData("/projects/123/tasks?project=secret", null)]
    public void Route_matching_never_keeps_identifiers(string path, string? expected)
        => Assert.Equal(expected, RouteCounter.Match(path));

    [Fact] // AC-PRO-08
    public void Load_time_bands_are_fixed_and_ordered()
    {
        Assert.Equal("under-500", RouteCounter.Band(TimeSpan.FromMilliseconds(499)));
        Assert.Equal("500-999", RouteCounter.Band(TimeSpan.FromMilliseconds(500)));
        Assert.Equal("1000-1999", RouteCounter.Band(TimeSpan.FromMilliseconds(1000)));
        Assert.Equal("2000-4999", RouteCounter.Band(TimeSpan.FromMilliseconds(2000)));
        Assert.Equal("5000+", RouteCounter.Band(TimeSpan.FromMilliseconds(5000)));
    }

    [Fact] // AC-PRO-10, AC-PRO-11
    public void Template_roles_explain_skips_and_support_working_days()
    {
        var discipline = Guid.NewGuid();
        var start = Guid.NewGuid();
        var end = Guid.NewGuid();
        var roles = new[]
        {
            new TplRole(Guid.NewGuid(), "Civil technologist", discipline, start, 0, end, -5, 120, null),
            new TplRole(Guid.NewGuid(), "Skipped", Guid.NewGuid(), start, 0, end, 0, 10, null),
        };
        var planned = TemplateRolePlanner.Plan(roles, new Dictionary<Guid, DateOnly?>
        {
            [start] = new DateOnly(2026, 10, 5), [end] = new DateOnly(2026, 10, 16)
        }, new HashSet<Guid> { discipline }, "WorkingDays", WorkCalendar.Weekdays);
        Assert.Equal(new DateOnly(2026, 10, 5), planned[0].From);
        Assert.Equal(new DateOnly(2026, 10, 9), planned[0].Through);
        Assert.Equal("discipline_not_selected", planned[1].SkipReason);
    }
}
