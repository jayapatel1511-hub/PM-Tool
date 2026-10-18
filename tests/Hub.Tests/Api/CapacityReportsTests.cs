using System.Net;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

[Collection("api")]
public sealed class CapacityReportsTests(HubFactory f)
{
    readonly TestData d = new(f);
    async Task Flag(bool on) => (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.capacity_reports", new { value = on })).EnsureSuccessStatusCode();

    [Fact] // AC-PRO-04, AC-PRO-05, AC-PRO-07, AC-PRO-23
    public async Task Team_capacity_reuses_twelve_week_planner_scope_drafts_and_exports()
    {
        await Flag(false);
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Sam).GetAsync("/api/v1/reports/team-capacity")).StatusCode);
        var number = TestData.Number();
        var person = (await (await f.As($"synthetic-report-{number}@hub.test").GetAsync("/api/v1/me")).Json()).G("id");
        var supervisorClient = f.As($"synthetic-supervisor-{number}@hub.test");
        var sam = (await (await supervisorClient.GetAsync("/api/v1/me")).Json()).G("id");
        await f.DbAsync(async db => { db.UserRoles.Add(new UserSystemRole { UserId = sam, Role = SystemRole.Supervisor }); var user = await db.Users.SingleAsync(u => u.Id == person); user.SupervisorId = sam; user.WeeklyCapacityHours = 40;
            db.PlanningEntries.Add(new PlanningEntry { PersonId = person, CreatedBy = sam, StartWeek = new(2026, 9, 14), EndWeek = new(2026, 9, 14), HoursPerWeek = 6,
                Label = "Synthetic own draft", SourceCategory = PlanningSource.Admin, Confidence = PlanningConfidence.Expected, Visibility = PlanningVisibility.Draft });
            return await db.SaveChangesAsync(); });
        await Flag(true);
        try {
            const string query = "from=2026-09-14&weeks=12&includeMyDrafts=true";
            var grid = await (await supervisorClient.GetAsync($"/api/v1/planning/grid?{query}")).Json();
            var report = await (await supervisorClient.GetAsync($"/api/v1/reports/team-capacity?{query}")).Json();
            string[] fields = ["capacity", "timeAway", "confirmed", "expected", "possible", "remaining"];
            for (var measure = 0; measure < fields.Length; measure++)
            for (var week = 0; week < 12; week++) {
                var sum = grid["people"]!.AsArray().Sum(p => p!["cells"]![week]![fields[measure]]!.GetValue<decimal>());
                Assert.Equal(decimal.Round(sum, 1), report["rows"]![measure]![$"w{week}"]!.GetValue<decimal>());
            }
            var withoutDraft = await (await supervisorClient.GetAsync("/api/v1/reports/team-capacity?from=2026-09-14&weeks=12&includeMyDrafts=false")).Json();
            Assert.Equal(6, report["rows"]![3]!["w0"]!.GetValue<decimal>() - withoutDraft["rows"]![3]!["w0"]!.GetValue<decimal>());
            Assert.Contains(withoutDraft["parameters"]!.AsArray(), p => p!["label"]!.GetValue<string>() == "Include my private drafts" && p["value"]!.GetValue<string>() == "No");
            foreach (var format in new[] { "csv", "xlsx" }) (await supervisorClient.GetAsync($"/api/v1/reports/team-capacity/export?{query}&format={format}")).EnsureSuccessStatusCode();
            Assert.True(await f.DbAsync(db => db.ActivityLog.AnyAsync(a => a.Action == "Exported")));
        } finally { await Flag(false); }
    }

    [Fact] // AC-PRO-06
    public async Task Source_hours_apply_approved_coverage_and_unlinked_list_filters()
    {
        var person = (await (await f.As($"synthetic-source-{Guid.NewGuid():N}@hub.test").GetAsync("/api/v1/me")).Json()).G("id");
        var project = await d.Project(); var sam = d.User(TestData.Sam); var week = new DateOnly(2026, 9, 14);
        await f.DbAsync(async db => {
            var user = await db.Users.SingleAsync(u => u.Id == person); user.SupervisorId = sam; user.WeeklyCapacityHours = 40;
            db.Allocations.Add(new ResourceAllocation { ProjectId = project.Id, PersonId = person, FromDate = week, ThroughDate = week.AddDays(4), PlannedHours = 12, Status = AllocationStatus.Confirmed, CreatedBy = d.User(TestData.Pm) });
            db.PlanningEntries.AddRange(new PlanningEntry { PersonId = person, CreatedBy = sam, ProjectId = project.Id, Label = "Synthetic covered", SourceCategory = PlanningSource.MajorProject, HoursPerWeek = 8,
                StartWeek = week, EndWeek = week, Confidence = PlanningConfidence.Expected, Visibility = PlanningVisibility.Published },
                new PlanningEntry { PersonId = person, CreatedBy = sam, Label = "Synthetic report admin", SourceCategory = PlanningSource.Admin, HoursPerWeek = 6,
                StartWeek = week, EndWeek = week, Confidence = PlanningConfidence.Confirmed, Visibility = PlanningVisibility.Published });
            return await db.SaveChangesAsync();
        });
        await Flag(true);
        try {
            var report = await (await f.As(TestData.Sam).GetAsync("/api/v1/reports/allocations-by-source?from=2026-09-14&weeks=1")).Json();
            var total = report["rows"]!.AsArray().Last()!["w0"]!.GetValue<decimal>();
            var team = await (await f.As(TestData.Sam).GetAsync("/api/v1/reports/team-capacity?from=2026-09-14&weeks=1")).Json();
            Assert.Equal(team["rows"]![2]!["w0"]!.GetValue<decimal>() + team["rows"]![3]!["w0"]!.GetValue<decimal>() + team["rows"]![4]!["w0"]!.GetValue<decimal>(), total);
            var unlinked = await (await f.As(TestData.Sam).GetAsync("/api/v1/reports/allocations-by-source?from=2026-09-14&weeks=1&show=entries-without-project&source=Admin")).Json();
            Assert.Contains(unlinked["rows"]!.AsArray(), r => r!["label"]!.GetValue<string>() == "Synthetic report admin");
            Assert.DoesNotContain(unlinked["rows"]!.AsArray(), r => r!["label"]!.GetValue<string>() == "Synthetic covered");
            Assert.Contains("withoutProject=true", unlinked.S("listLink"));
        } finally { await Flag(false); }
    }
    [Fact] // AC-PRO-07: the filtered list carries the actual report horizon and Monday, including defaults.
    public async Task Source_report_list_link_preserves_supervisor_default_horizon_and_normalized_week()
    {
        var admin = f.As(TestData.Admin); var sam = d.User(TestData.Sam); var lena = d.User(TestData.Lena); var week = new DateOnly(2026, 9, 14);
        foreach (var supervisor in new[] { sam, lena })
        {
            var person = (await (await f.As($"synthetic-link-{Guid.NewGuid():N}@hub.test").GetAsync("/api/v1/me")).Json()).G("id");
            await f.DbAsync(async db => { (await db.Users.SingleAsync(u => u.Id == person)).SupervisorId = supervisor; return await db.SaveChangesAsync(); });
            (await admin.Post("/api/v1/planning/entries", new { personId = person, hoursPerWeek = 4m, startWeek = week, endWeek = week, label = "Synthetic report list", visibility = PlanningVisibility.Published })).EnsureSuccessStatusCode();
        }
        await Flag(true);
        try
        {
            const string from = "2026-09-15";
            var query = $"supervisorId={sam}&from={from}";
            var grid = await (await admin.GetAsync($"/api/v1/planning/grid?{query}")).Json();
            var report = await (await admin.GetAsync($"/api/v1/reports/allocations-by-source?show=entries-without-project&{query}")).Json();
            var link = report.S("listLink");
            Assert.Contains($"supervisorId={sam}", link); Assert.Contains("from=2026-09-14", link); Assert.Contains($"weeks={grid["weeks"]!.AsArray().Count}", link);
            var list = await (await admin.GetAsync(link.Replace("/planner?view=list&", "/api/v1/planning/entries?"))).Json();
            Assert.Equal(report.I("total"), list.I("totalCount"));
            Assert.All(list["items"]!.AsArray(), entry => Assert.Null(entry!["projectId"]));
        }
        finally { await Flag(false); }
    }

}
