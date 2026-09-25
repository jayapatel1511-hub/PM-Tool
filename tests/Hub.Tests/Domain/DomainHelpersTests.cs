using System.Text.Json;
using Hub.Domain;

namespace Hub.Tests.Domain;

/// Links (§12.7 DOC-01/02), the working-day calendar (§10.4), organisation settings (§10.4) and vocabulary helpers (§10).
public sealed class DomainHelpersTests
{
    [Theory]
    [InlineData(@"\\fileserver\projects\2026-0417", true, LinkType.NetworkFolder, "2026-0417")]
    [InlineData("https://contoso.sharepoint.com/sites/2026-0417/Shared%20Documents/Design", true, LinkType.SharePoint, "Design")]
    [InlineData("https://contoso-my.sharepoint.com/personal/jay/Documents/notes.docx", true, LinkType.OneDrive, "notes.docx")]
    [InlineData("https://1drv.ms/f/s!abc", true, LinkType.OneDrive, "s!abc")]
    [InlineData("https://onedrive.live.com/?id=1", true, LinkType.OneDrive, "https://onedrive.live.com/?id=1")]
    [InlineData("https://teams.microsoft.com/l/channel/19%3a1/General", true, LinkType.Teams, "General")]
    [InlineData("https://eu.teams.microsoft.com/l/team/x", true, LinkType.Teams, "x")]
    [InlineData("https://teams.live.com/meet/1", true, LinkType.Teams, "1")]
    [InlineData("http://portal.client.ca/docs/", true, LinkType.Other, "docs")]
    [InlineData("ftp://files.example.com/a", false, LinkType.Other, "a")]
    [InlineData("file:///C:/secret.txt", false, LinkType.Other, "secret.txt")]
    [InlineData("not a link", false, LinkType.Other, "not a link")]
    public void Links_are_validated_typed_and_titled(string url, bool valid, string type, string title)
    {
        Assert.Equal(valid, Links.IsValid(url));
        Assert.Equal(type, Links.Detect(url));
        Assert.Equal(title, Links.DefaultTitle(url));
    }

    [Fact]
    public void Working_day_calendar_skips_weekends_and_holidays()
    {
        var thanksgiving = new DateOnly(2026, 10, 12); // Monday
        var cal = new WorkCalendar([thanksgiving]);
        var fri = new DateOnly(2026, 10, 9);
        Assert.True(cal.IsWorkingDay(fri));
        Assert.False(cal.IsWorkingDay(fri.AddDays(1)));
        Assert.False(cal.IsWorkingDay(thanksgiving));
        Assert.Equal(1, cal.Between(fri, new DateOnly(2026, 10, 13))); // Sat, Sun, holiday skipped; Tue counts
        Assert.Equal(-1, cal.Between(new DateOnly(2026, 10, 13), fri));
        Assert.Equal(0, cal.Between(fri, fri));
        Assert.Equal(new DateOnly(2026, 10, 14), cal.Add(fri, 2));
        Assert.Equal(fri, cal.Add(fri, 0));
        Assert.Equal(new[] { fri, new DateOnly(2026, 10, 13) }, cal.WorkingDays(fri, new DateOnly(2026, 10, 13)));
        Assert.True(WorkCalendar.Weekdays.IsWorkingDay(thanksgiving));
    }

    static JsonElement J(object v) => JsonSerializer.SerializeToElement(v);

    [Fact]
    public void Settings_read_stored_values_and_fall_back_to_defaults()
    {
        var defaults = OrgSettings.From(new Dictionary<string, JsonElement>());
        Assert.Equal(new OrgSettings().TaskDueSoonDays, defaults.TaskDueSoonDays);
        Assert.All(Rules.Ids, r => Assert.True(defaults.IsRuleEnabled(r)));
        var s = OrgSettings.From(new Dictionary<string, JsonElement>
        {
            ["task_due_soon_days"] = J(5), ["task_stale_days"] = J("ten"), ["allow_self_review"] = J(true), ["weekend_digests"] = J(1),
            ["org_time_zone"] = J("America/Toronto"), ["date_format"] = J(7), ["rule_enabled.A-10"] = J(false),
            ["notify_default.TaskAssigned"] = J(new { app = true, email = false }), ["notify_default.ReviewRequested"] = J("yes"),
        });
        Assert.Equal(5, s.TaskDueSoonDays);
        Assert.Equal(defaults.TaskStaleDays, s.TaskStaleDays); // wrong type keeps the default
        Assert.True(s.AllowSelfReview);
        Assert.False(s.WeekendDigests);
        Assert.Equal("America/Toronto", s.OrgTimeZone);
        Assert.Equal(defaults.DateFormat, s.DateFormat);
        Assert.False(s.IsRuleEnabled("A-10"));
        Assert.Equal(new Channels(true, false), s.NotificationDefaults[NotificationEvents.TaskAssigned]);
        var rr = NotificationEvents.Get(NotificationEvents.ReviewRequested);
        Assert.Equal(new Channels(rr.App, rr.Email), s.NotificationDefaults[NotificationEvents.ReviewRequested]);
    }

    [Fact]
    public void Setting_values_are_validated_by_kind()
    {
        string? V(string key, object value) => OrgSettings.Validate(OrgSettings.Def(key)!, J(value));
        Assert.Null(V("task_due_soon_days", 7));
        Assert.Equal("setting.int", V("task_due_soon_days", -1));
        Assert.Equal("setting.int", V("task_due_soon_days", 4000));
        Assert.Equal("setting.int", V("task_due_soon_days", "7"));
        Assert.Null(V("allow_self_review", false));
        Assert.Equal("setting.bool", V("allow_self_review", "no"));
        Assert.Null(V("digest_send_time_local", "07:30"));
        Assert.Equal("setting.time", V("digest_send_time_local", "7.30"));
        Assert.Equal("setting.time", V("digest_send_time_local", 730));
        Assert.Null(V("org_time_zone", "America/Halifax"));
        Assert.Equal("setting.timezone", V("org_time_zone", "Mars/Olympus"));
        Assert.Equal("setting.timezone", V("org_time_zone", 3));
        Assert.Null(V("project_number_format", "^[0-9]{4}-[0-9]{4}$"));
        Assert.Equal("setting.regex", V("project_number_format", "(["));
        Assert.Equal("setting.regex", V("project_number_format", true));
        Assert.Null(V("notify_default.TaskAssigned", new { app = true, email = false }));
        Assert.Equal("setting.channels", V("notify_default.TaskAssigned", new { app = true }));
        Assert.Equal("setting.channels", V("notify_default.TaskAssigned", new { app = "y", email = false }));
        Assert.Equal("setting.channels", V("notify_default.TaskAssigned", new { app = true, email = 1 }));
        Assert.Equal("setting.channels", V("notify_default.TaskAssigned", "both"));
        Assert.Null(V("date_format", "d MMM yyyy"));
        Assert.Equal("setting.text", V("date_format", ""));
        Assert.Equal("setting.text", V("date_format", new string('x', 201)));
        Assert.Equal("setting.text", V("date_format", 5));
        Assert.Null(OrgSettings.Def("no_such_setting"));
    }

    [Fact]
    public void Vocabulary_classifies_statuses()
    {
        Assert.Equal(new[] { ProjectStatus.Archived, ProjectStatus.Cancelled }, ProjectStatus.All.Where(ProjectStatus.IsReadOnly));
        Assert.Equal(new[] { ProjectStatus.Setup, ProjectStatus.Active, ProjectStatus.OnHold }, ProjectStatus.All.Where(ProjectStatus.IsLive));
        Assert.Equal(new[] { TaskStatuses.Complete, TaskStatuses.Cancelled }, TaskStatuses.All.Where(TaskStatuses.IsTerminal));
        Assert.Equal(6, TaskStatuses.All.Count(TaskStatuses.IsOpen));
        Assert.Equal(new[] { TaskStatuses.ReadyForReview, TaskStatuses.InReview }, TaskStatuses.All.Where(TaskStatuses.IsReview));
        Assert.Equal(new[] { TaskStatuses.InProgress, TaskStatuses.ReadyForReview, TaskStatuses.InReview, TaskStatuses.RevisionRequired }, TaskStatuses.All.Where(TaskStatuses.IsStarted));
        Assert.Equal(new[] { DeliverableStatus.Issued, DeliverableStatus.Accepted, DeliverableStatus.Cancelled }, DeliverableStatus.All.Where(DeliverableStatus.IsTerminal));
        Assert.Equal(new[] { DecisionStatus.Pending, DecisionStatus.UnderReview, DecisionStatus.Deferred }, DecisionStatus.All.Where(DecisionStatus.IsOpen));
        Assert.Equal(new[] { RiskStatus.Open, RiskStatus.Monitoring }, RiskStatus.All.Where(RiskStatus.IsOpen));
        Assert.Equal(new[] { IssueStatus.Open, IssueStatus.InProgress }, IssueStatus.All.Where(IssueStatus.IsOpen));
        Assert.Equal(new[] { ActionStatus.Open, ActionStatus.InProgress }, ActionStatus.All.Where(ActionStatus.IsOpen));
        Assert.Equal(new[] { MilestoneType.DesignSubmission, MilestoneType.PermitSubmission, MilestoneType.Tender, MilestoneType.IFC }, MilestoneType.All.Where(MilestoneType.IsSubmission));
    }

    [Fact]
    public void Rankings_order_severity_priority_and_health()
    {
        Assert.Equal(new[] { Health.Red, Health.Yellow, Health.Green, Health.Grey }, Health.All.OrderBy(Health.Severity));
        Assert.Equal(3, Health.Severity(null));
        Assert.Equal(new[] { Priority.Critical, Priority.High, Priority.Medium, Priority.Low }, Priority.All.OrderBy(Priority.Rank));
        Assert.Equal(3, Priority.Rank(null));
        Assert.Equal(new[] { Severity.Critical, Severity.Warning, Severity.Info }, new[] { Severity.Info, Severity.Critical, Severity.Warning }.OrderBy(Severity.Rank));
        Assert.Equal(new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday },
            Weekday.All.Select(Weekday.Parse));
        Assert.Equal(DayOfWeek.Monday, Weekday.Parse(null));
        Assert.Equal(SystemRole.All.Order(), SystemRole.AppRoles.Values.Order());
    }
}
