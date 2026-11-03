using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// S19: independent project progress facts and the existing per-project edit command
/// used by the bulk UI. No alternative mutation endpoint or permission is introduced.
public sealed class ProjectProgressBulkTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData d;
    public ProjectProgressBulkTests() => d = new(f);
    public void Dispose() => f.Dispose();
    Task<JsonNode> Read(string path, string user = TestData.Admin) => f.As(user).GetAsync(path).Result.Json();
    async Task Flag(string key, bool value) => (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/flag.{key}", new { value })).EnsureSuccessStatusCode();
    Task<int> Edits(Guid id) => f.DbAsync(db => db.ActivityLog.CountAsync(x => x.ItemType == ItemType.Project && x.ItemId == id && x.Action == "Updated"));

    [Fact]
    public async Task Sixty_percent_of_dates_and_thirteen_of_twenty_six_tasks_show_separate_facts_without_changing_health()
    {
        f.Clock.SetDate(2026, 3, 18); // exactly 72 of 120 days, independent of task completion
        var p = await d.Project(tweak: body => { body["startDate"] = "2026-01-05"; body["targetCompletionDate"] = "2026-05-05"; });
        var created = new List<Guid>();
        for (var i = 0; i < 30; i++) created.Add((await d.NewTask(p.Id, extra: new { name = $"Synthetic progress task {i + 1}" })).G("id"));
        await f.DbAsync(async db =>
        {
            foreach (var task in await db.Tasks.Where(t => created.Contains(t.Id)).ToListAsync())
            {
                var index = created.IndexOf(task.Id);
                if (index < 13) { task.Status = TaskStatuses.Complete; task.ProgressPct = 100; task.CompletedAt = f.Clock.GetUtcNow(); }
                else if (index >= 26) { task.Status = TaskStatuses.Cancelled; task.CancelledReason = "Excluded from progress denominator"; }
            }
            await db.SaveChangesAsync(); return 0;
        });
        await f.Evaluate(p.Id);
        var off = await Read($"/api/v1/projects?Ids={p.Id}"); var offPortfolio = await Read($"/api/v1/portfolio?Ids={p.Id}");
        var health = await f.DbAsync(db => db.ProjectStates.Where(x => x.ProjectId == p.Id).Select(x => new { x.ComputedHealth, x.HealthReasons, x.Inputs, x.Counts, x.EvaluatedAt, x.ProgressPct }).SingleAsync());
        Assert.Equal(50, health.ProgressPct);
        await Flag("project_screens", true);
        var on = await Read($"/api/v1/projects?Ids={p.Id}"); var row = Assert.Single(on["items"]!.AsArray())!;
        Assert.Equal(60, row["progressFacts"]!.I("elapsedPct")); Assert.Equal(50, row["progressFacts"]!.I("progressPct")); Assert.Equal(50, row.I("progressPct"));
        Assert.Equal(d.Version(p.Id), row.I("rowVersion"));
        var portfolio = await Read($"/api/v1/portfolio?Ids={p.Id}"); var portfolioRow = Assert.Single(portfolio["projects"]!.AsArray())!;
        Assert.Equal(row["progressFacts"]!.ToJsonString(), portfolioRow["progressFacts"]!.ToJsonString());
        Assert.Equal(health, await f.DbAsync(db => db.ProjectStates.Where(x => x.ProjectId == p.Id).Select(x => new { x.ComputedHealth, x.HealthReasons, x.Inputs, x.Counts, x.EvaluatedAt, x.ProgressPct }).SingleAsync()));
        foreach (var key in new[] { "staffing", "span", "progressFacts", "rowVersion" }) row.AsObject().Remove(key);
        Assert.Equal(off.ToJsonString(), on.ToJsonString());
        Assert.NotNull(portfolioRow["staffing"]);
        Assert.NotNull(portfolio["changes"]); Assert.NotNull(portfolio["asOf"]);
        Assert.Null(portfolio["cardFacts"]); // visual_system remains off in this independent part check
        foreach (var key in new[] { "progressFacts", "rowVersion", "staffing" }) portfolioRow.AsObject().Remove(key);
        portfolio.AsObject().Remove("changes"); portfolio.AsObject().Remove("asOf");
        Assert.Equal(offPortfolio.ToJsonString(), portfolio.ToJsonString());
        await Flag("project_screens", false);
        var disabled = Assert.Single((await Read($"/api/v1/projects?Ids={p.Id}"))["items"]!.AsArray())!;
        Assert.False(disabled.AsObject().ContainsKey("progressFacts")); Assert.False(disabled.AsObject().ContainsKey("rowVersion"));
    }

    [Fact]
    public void Missing_or_invalid_date_spans_are_unknown_and_the_date_formula_never_changes_canonical_progress()
    {
        var today = new DateOnly(2026, 3, 18);
        Assert.Null(ProjectListCompleteness.Progress(null, today, today, null).ElapsedPct);
        Assert.Null(ProjectListCompleteness.Progress(today, null, today, 50).ElapsedPct);
        Assert.Null(ProjectListCompleteness.Progress(today, today, today, 50).ElapsedPct);
        Assert.Null(ProjectListCompleteness.Progress(today, today.AddDays(-1), today, 50).ElapsedPct);
        Assert.Equal(110, ProjectListCompleteness.Progress(today.AddDays(-11), today.AddDays(-1), today, 17).ElapsedPct);
        Assert.Equal(-10, ProjectListCompleteness.Progress(today.AddDays(1), today.AddDays(11), today, 17).ElapsedPct);
        Assert.Equal(17, ProjectListCompleteness.Progress(today.AddDays(1), today.AddDays(11), today, 17).ProgressPct);
    }

    [Fact]
    public async Task Selected_versions_keep_two_allowed_edits_and_refuse_uneditable_and_stale_rows_independently()
    {
        var a = await d.Project(pm: TestData.Marc); var b = await d.Project(pm: TestData.Marc);
        var refused = await d.Project(); var stale = await d.Project(pm: TestData.Marc);
        var projects = new[] { a, b, refused, stale }; var ids = projects.Select(p => p.Id).ToArray();
        await Flag("project_screens", true);
        var selected = (await Read($"/api/v1/projects?Ids={string.Join(',', ids)}", TestData.Marc))["items"]!.AsArray().ToDictionary(r => r!.G("id"), r => r!.I("rowVersion"));
        await f.As(TestData.Marc).Patch($"/api/v1/projects/{stale.Id}", new { name = "Changed after selection" }, selected[stale.Id]).Result.Json();
        var before = new Dictionary<Guid, int>(); foreach (var p in projects) before[p.Id] = await Edits(p.Id);
        var outcomes = new Dictionary<Guid, HttpStatusCode>();
        foreach (var p in projects)
            outcomes[p.Id] = (await f.As(TestData.Marc).Patch($"/api/v1/projects/{p.Id}", new { priority = Priority.High, rowVersion = selected[p.Id] }, selected[p.Id])).StatusCode;
        Assert.Equal(HttpStatusCode.OK, outcomes[a.Id]); Assert.Equal(HttpStatusCode.OK, outcomes[b.Id]);
        Assert.Equal(HttpStatusCode.Forbidden, outcomes[refused.Id]); Assert.Equal(HttpStatusCode.Conflict, outcomes[stale.Id]);
        foreach (var p in projects)
        {
            var updated = await f.DbAsync(db => db.Projects.AsNoTracking().SingleAsync(x => x.Id == p.Id));
            var changed = p.Id == a.Id || p.Id == b.Id;
            Assert.Equal(changed ? Priority.High : Priority.Medium, updated.Priority); Assert.Equal(ProjectStatus.Active, updated.Status);
            Assert.Equal(before[p.Id] + (changed ? 1 : 0), await Edits(p.Id));
        }
        Assert.Equal("Changed after selection", await f.DbAsync(db => db.Projects.Where(x => x.Id == stale.Id).Select(x => x.Name).SingleAsync()));
    }

    [Fact]
    public async Task Phase_and_org_enabled_lead_unit_use_the_normal_edit_command_and_each_real_change_has_one_activity_entry()
    {
        var a = await d.Project(); var b = await d.Project();
        var (phaseId, unitId) = await f.DbAsync(async db =>
        {
            var phase = new Phase { Name = "Synthetic bulk phase" }; db.Phases.Add(phase);
            var level = await db.Set<OrgLevel>().OrderBy(x => x.SortOrder).FirstOrDefaultAsync();
            if (level is null) { level = new OrgLevel { Name = "Synthetic level", SortOrder = 0 }; db.Add(level); }
            var unit = new OrgUnit { Name = "Synthetic lead unit", LevelId = level.Id }; db.Add(unit);
            await db.SaveChangesAsync(); return (phase.Id, unit.Id);
        });
        await Flag("project_screens", true);
        foreach (var p in new[] { a, b })
        {
            var before = await Edits(p.Id);
            await f.As(TestData.Pm).Patch($"/api/v1/projects/{p.Id}", new { phaseId }, d.Version(p.Id)).Result.Json();
            Assert.Equal(before + 1, await Edits(p.Id));
            Assert.Equal(phaseId, await f.DbAsync(db => db.Projects.Where(x => x.Id == p.Id).Select(x => x.PhaseId).SingleAsync()));
        }
        await Flag("org_structure", true);
        foreach (var p in new[] { a, b })
        {
            var before = await Edits(p.Id);
            await f.As(TestData.Pm).Patch($"/api/v1/projects/{p.Id}", new { leadUnitId = unitId }, d.Version(p.Id)).Result.Json();
            Assert.Equal(before + 1, await Edits(p.Id));
            Assert.Equal(unitId, await f.DbAsync(db => db.Projects.Where(x => x.Id == p.Id).Select(x => x.LeadUnitId).SingleAsync()));
        }
        await Flag("org_structure", false);
        var version = d.Version(a.Id); var count = await Edits(a.Id);
        await f.As(TestData.Pm).Patch($"/api/v1/projects/{a.Id}", new { leadUnitId = (Guid?)null }, version).Result.Json();
        Assert.Equal(unitId, await f.DbAsync(db => db.Projects.Where(x => x.Id == a.Id).Select(x => x.LeadUnitId).SingleAsync()));
        Assert.Equal(version, d.Version(a.Id)); Assert.Equal(count, await Edits(a.Id));
    }

    [Fact]
    public async Task Unchanged_values_have_no_extra_activity_and_read_only_or_missing_versions_are_refused()
    {
        var p = await d.Project(); var version = d.Version(p.Id); var count = await Edits(p.Id);
        var same = await f.As(TestData.Pm).Patch($"/api/v1/projects/{p.Id}", new { priority = Priority.Medium }, version).Result.Json();
        Assert.Equal(version, same.I("rowVersion")); Assert.Equal(count, await Edits(p.Id));
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Rita).Patch($"/api/v1/projects/{p.Id}", new { priority = Priority.High }, version)).StatusCode);
        Assert.Equal((HttpStatusCode)428, (await f.As(TestData.Pm).Patch($"/api/v1/projects/{p.Id}", new { priority = Priority.High })).StatusCode);
        Assert.Equal(Priority.Medium, await f.DbAsync(db => db.Projects.Where(x => x.Id == p.Id).Select(x => x.Priority).SingleAsync()));
        Assert.Equal(count, await Edits(p.Id));
    }
}
