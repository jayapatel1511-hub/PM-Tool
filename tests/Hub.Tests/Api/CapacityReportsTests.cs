using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

[Collection("api")]
public sealed class CapacityReportsTests(HubFactory f)
{
    readonly TestData d = new(f);
    async Task Flag(bool on) => (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.capacity_reports", new { value = on })).EnsureSuccessStatusCode();

    [Fact, Trait("flag-off", "capacity_reports"), Trait("flag-on", "capacity_reports")] // AC-PRO-04, AC-PRO-05, AC-PRO-07, AC-PRO-23
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

    [Fact, Trait("flag-on", "capacity_reports")] // AC-PRO-06
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
    [Fact, Trait("flag-on", "capacity_reports")] // AC-PRO-07: the filtered list carries the actual report horizon and Monday, including defaults.
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

    /// Property names in order and the kind of each value: what a client of the JSON can depend on.
    static string Shape(JsonNode? n) => string.Join(",", n!.AsObject().Select(p => $"{p.Key}:{Kind(p.Value)}"));
    static string Kind(JsonNode? v) => v switch
    {
        null => "null", JsonArray => "array", JsonObject => "object",
        JsonValue x => x.GetValueKind() switch { JsonValueKind.String => "string", JsonValueKind.Number => "number", JsonValueKind.True or JsonValueKind.False => "bool", _ => "other" },
        _ => "other",
    };

    [Fact, Trait("flag-on", "capacity_reports"), Trait("flag-on", "route_counter")] // 040-S15: the catalogue, the run envelope and the rows that were anonymous objects keep their JSON
    public async Task Report_catalogue_envelope_and_rows_keep_their_json_shape()
    {
        var p = await d.Project(); var sam = d.User(TestData.Sam); var week = new DateOnly(2026, 9, 14);
        var person = (await (await f.As($"synthetic-shape-{Guid.NewGuid():N}@hub.test").GetAsync("/api/v1/me")).Json()).G("id");
        var routeKey = $"shape-{Guid.NewGuid():N}"[..20];
        var today = DateOnly.FromDateTime(f.Clock.GetUtcNow().UtcDateTime);
        var task = await d.NewTask(p.Id, TestData.Pm, new { name = "Synthetic shaped hours" });
        (await f.As(TestData.Pm).Post("/api/v1/time", new { taskId = task.G("id"), workDate = "2026-08-10", hours = 2.5m, note = "Synthetic note" })).EnsureSuccessStatusCode();
        await f.DbAsync(async db =>
        {
            (await db.Users.SingleAsync(u => u.Id == person)).SupervisorId = sam;
            db.PlanningEntries.Add(new PlanningEntry { PersonId = person, CreatedBy = sam, Label = "Synthetic shaped entry", SourceCategory = PlanningSource.Admin, HoursPerWeek = 6,
                StartWeek = week, EndWeek = week, Confidence = PlanningConfidence.Confirmed, Visibility = PlanningVisibility.Published });
            db.HealthSnapshots.Add(new ProjectHealthSnapshot { ProjectId = p.Id, SnapshotDate = new DateOnly(2026, 9, 14), ComputedHealth = Health.Green, ReportedHealth = Health.Yellow });
            db.RouteCounts.Add(new RouteCount { Day = today.AddDays(-1), RouteKey = routeKey, Opens = 3, Under500Ms = 2, From500To999Ms = 1 });
            return await db.SaveChangesAsync();
        });
        await Flag(true);
        (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.route_counter", new { value = true })).EnsureSuccessStatusCode();
        try
        {
            var catalogue = await (await f.As(TestData.Admin).GetAsync("/api/v1/reports")).Json();
            JsonNode Entry(string code) => catalogue.AsArray().Single(r => r!.S("code") == code)!;
            Assert.Equal("code:string,title:string,description:string,params:array,itemType:null,columns:array", Shape(Entry("team-capacity")));
            Assert.Equal("code:string,title:string,description:string,params:array,itemType:string,columns:array", Shape(Entry("tasks-due-this-week")));
            Assert.Equal("path:string,header:string,type:string", Shape(Entry("tasks-due-this-week")["columns"]![0]));
            var show = Entry("allocations-by-source")["params"]!.AsArray().Single(x => x!.S("key") == "show")!;
            Assert.Equal("key:string,label:string,type:string,default:string,options:array", Shape(show));
            Assert.Equal("value:string,label:string", Shape(show["options"]![0]));
            var weeks = Entry("team-capacity")["params"]!.AsArray().Single(x => x!.S("key") == "weeks")!;
            Assert.Equal("key:string,label:string,type:string,default:string,options:null", Shape(weeks));

            var team = await (await f.As(TestData.Sam).GetAsync("/api/v1/reports/team-capacity?from=2026-09-14&weeks=2")).Json();
            Assert.Equal("code:string,title:string,itemType:null,columns:array,rows:array,total:number,totalIsLowerBound:bool,truncated:bool,listLink:string,parameters:array", Shape(team));
            Assert.Equal("path:string,header:string,type:string", Shape(team["columns"]![1]));
            Assert.Equal("label:string,value:string", Shape(team["parameters"]![0]));
            Assert.Equal("measure:string,w0:number,w1:number", Shape(team["rows"]![0]));

            var unlinked = await (await f.As(TestData.Sam).GetAsync("/api/v1/reports/allocations-by-source?from=2026-09-14&weeks=1&show=entries-without-project&source=Admin")).Json();
            var entry = unlinked["rows"]!.AsArray().Single(r => r!.S("label") == "Synthetic shaped entry")!;
            Assert.Equal("personName:string,ownerName:string,label:string,source:string,hoursPerWeek:number,startWeek:string,endWeek:string,confidence:string,visibility:string", Shape(entry));
            Assert.Equal(("2026-09-14", 6m), (entry.S("startWeek"), entry["hoursPerWeek"]!.GetValue<decimal>()));

            var routes = await (await f.As(TestData.Admin).GetAsync("/api/v1/reports/pilot-route-use?weeks=2")).Json();
            var route = routes["rows"]!.AsArray().Single(r => r!.S("routeKey") == routeKey)!;
            Assert.Equal("routeKey:string,week:string,opens:number,under500Ms:number,from500To999Ms:number,from1000To1999Ms:number,from2000To4999Ms:number,over5000Ms:number", Shape(route));
            Assert.Equal((3, 2, 1, 0), (route.I("opens"), route.I("under500Ms"), route.I("from500To999Ms"), route.I("over5000Ms")));

            var hours = await (await f.As(TestData.Pm).GetAsync($"/api/v1/reports/task-hours?from=2026-08-10&to=2026-08-16&scope=team&projectId={p.Id}")).Json();
            Assert.Equal("workDate:string,person:string,projectNumber:string,taskKey:string,taskName:string,hours:number,note:string,itemType:string,id:string", Shape(hours["rows"]![0]));
            Assert.Equal("person:string,hours:number", Shape(hours["rows"]![1]));

            var history = await (await f.As(TestData.Pm).GetAsync($"/api/v1/reports/health-history?projectId={p.Id}&from=2026-09-14&to=2026-09-14")).Json();
            Assert.Equal("projectNumber:string,name:string,date:string,computed:string,reported:string", Shape(history["rows"]![0]));
            Assert.Equal(("Green", "Yellow"), (history["rows"]![0]!.S("computed"), history["rows"]![0]!.S("reported")));

            var measures = await (await f.As(TestData.Pm).GetAsync($"/api/v1/reports/pilot-measures?projectId={p.Id}&weeks=1")).Json();
            Assert.Equal("weekOf,projects,reviewed,unassigned,noDueDate,blocked,people,updaters,updatersPct", string.Join(",", measures["rows"]![0]!.AsObject().Select(x => x.Key))); // a count is null until a snapshot falls in the week

            var staff = await (await f.As(TestData.Lena).GetAsync($"/api/v1/reports/staff-assignments?scope=all&projectId={p.Id}")).Json();
            Assert.Equal("person:string,projectNumber:string,projectName:string,projectStatus:string,roles:string,primaryDiscipline:string,addedAt:string,addedBy:string,openTasks:number,overdueTasks:number", Shape(staff["rows"]!.AsArray().Single(r => r!.S("person") == "Alex Chen")));
        }
        finally { await Flag(false); (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.route_counter", new { value = false })).EnsureSuccessStatusCode(); }
    }

}
