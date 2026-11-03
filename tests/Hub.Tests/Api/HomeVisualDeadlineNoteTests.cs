using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Hub.Tests.Api;

/// FR/AC-SCR-13: recorded review episode, baseline truth, precedence and unchanged dark rows.
public sealed class HomeVisualDeadlineNoteTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData d;
    public HomeVisualDeadlineNoteTests() => d = new(f);
    public void Dispose() => f.Dispose();
    async Task Setting(string key, object value) => (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/{key}", new { value })).EnsureSuccessStatusCode();
    async Task<JsonNode> Home(Guid project, string extra = "") => await (await f.As(TestData.Pm).GetAsync($"/api/v1/home?projects={project}&from=2026-10-31&to=2026-10-31{extra}")).Json();
    static JsonNode Row(JsonNode body, Guid id) => body["upcomingDeadlines"]!["items"]!.AsArray().Single(x => x!.G("id") == id)!;
    async Task<JsonNode> Deliverable(Project p) => await (await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/deliverables", new {
        name = "Synthetic review deliverable", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), deliverableTypeId = await d.DeliverableType(),
        ownerId = d.User(TestData.Alex), reviewerId = d.User(TestData.Marc), requiresReview = true, dueDate = "2026-10-31" })).Json(201);
    async Task MoveDeliverable(Guid id, string status)
    {
        var version = f.Db(db => db.Deliverables.Single(x => x.Id == id).RowVersion);
        (await f.As(TestData.Pm).Post($"/api/v1/deliverables/{id}/transition", new { toStatus = status, rowVersion = version })).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Five_day_recorded_review_wins_over_three_day_baseline_and_applies_to_paged_rows()
    {
        await Setting("flag.home_overview", true); await Setting("flag.schedule_baseline", true);
        f.Clock.SetDate(2026, 10, 26);
        var p = await d.Project();
        var task = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = d.User(TestData.Alex), reviewerId = d.User(TestData.Marc), requiresReview = true, dueDate = "2026-10-31" });
        f.Clock.Now = f.Clock.Now.AddMinutes(1); await d.Move(TestData.Alex, task, TaskStatuses.InProgress);
        f.Clock.Now = f.Clock.Now.AddMinutes(1); await d.Move(TestData.Alex, task, TaskStatuses.ReadyForReview);
        var deliverable = await Deliverable(p); var did = deliverable.G("id");
        f.Clock.Now = f.Clock.Now.AddMinutes(1); await MoveDeliverable(did, DeliverableStatus.InProgress);
        f.Clock.Now = f.Clock.Now.AddMinutes(1); await MoveDeliverable(did, DeliverableStatus.InReview);
        var milestone = await (await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/milestones", new { name = "Synthetic baseline milestone", milestoneType = "Kickoff", date = "2026-10-31" })).Json(201);
        f.Clock.SetDate(2026, 10, 28); await d.Move(TestData.Marc, task, TaskStatuses.InReview);
        f.Clock.SetDate(2026, 10, 31);
        await f.DbAsync(async db =>
        {
            var del = await db.Deliverables.SingleAsync(x => x.Id == did); del.BaselineDueDate = new(2026, 10, 28); del.OriginalDueDate = new(2026, 9, 1);
            var m = await db.Milestones.SingleAsync(x => x.Id == milestone.G("id")); m.BaselineDate = new(2026, 10, 28); m.OriginalDate = new(2026, 9, 1);
            return await db.SaveChangesAsync();
        });
        var writes = f.Db(db => db.ActivityLog.Count()); var body = await Home(p.Id);
        foreach (var id in new[] { task.G("id"), did })
        {
            var note = Row(body, id)["note"]!; Assert.Equal("InReview", note.S("kind")); Assert.Equal(5, note.I("days"));
        }
        Assert.Equal("LaterThanBaseline", Row(body, milestone.G("id"))["note"]!.S("kind"));
        Assert.Equal(3, Row(body, milestone.G("id"))["note"]!.I("days"));
        var page2 = await Home(p.Id, "&deadlinePage=2&deadlinePageSize=1");
        Assert.Equal(3, page2["upcomingDeadlines"]!.I("total"));
        Assert.NotNull(Assert.Single(page2["upcomingDeadlines"]!["items"]!.AsArray())!["note"]);
        Assert.Equal(writes, f.Db(db => db.ActivityLog.Count()));
        await Setting("flag.home_overview", false);
        Assert.All((await Home(p.Id))["upcomingDeadlines"]!["items"]!.AsArray(), x => Assert.False(x!.AsObject().ContainsKey("note")));
    }

    [Fact]
    public async Task Baseline_uses_strict_slip_threshold_never_original_dates_and_respects_its_own_dark_flag()
    {
        await Setting("flag.home_overview", true); f.Clock.SetDate(2026, 10, 31);
        var p = await d.Project(); var ids = new List<Guid>();
        foreach (var delta in new[] { 3, 2, 0, -1 })
        {
            var m = await (await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/milestones", new { name = $"Synthetic slip {delta}", milestoneType = "Kickoff", date = "2026-10-31" })).Json(201);
            ids.Add(m.G("id"));
            await f.DbAsync(async db => { var row = await db.Milestones.SingleAsync(x => x.Id == m.G("id")); row.BaselineDate = new DateOnly(2026, 10, 31).AddDays(-delta); row.OriginalDate = new(2026, 1, 1); return await db.SaveChangesAsync(); });
        }
        Assert.All((await Home(p.Id))["upcomingDeadlines"]!["items"]!.AsArray(), x => Assert.Null(x!["note"]));
        await Setting("flag.schedule_baseline", true); await Setting("slip_display_days", 2);
        var body = await Home(p.Id);
        Assert.Equal(3, Row(body, ids[0])["note"]!.I("days"));
        foreach (var id in ids.Skip(1)) Assert.Null(Row(body, id)["note"]);
        await Setting("slip_display_days", 3);
        Assert.Null(Row(await Home(p.Id), ids[0])["note"]);
    }

    [Fact]
    public async Task Missing_or_malformed_review_history_never_becomes_zero_age_or_baseline_and_foreign_evidence_is_ignored()
    {
        await Setting("flag.home_overview", true); await Setting("flag.schedule_baseline", true); f.Clock.SetDate(2026, 10, 31);
        var p = await d.Project(); var other = await d.Project(); var del = await Deliverable(p); var id = del.G("id");
        // Explicitly model a legacy imported status without an audit transition. Production command validation is not changed.
        await f.DbAsync(db => db.Deliverables.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, DeliverableStatus.InReview).SetProperty(x => x.BaselineDueDate, new DateOnly(2026, 10, 28))));
        Assert.Null(Row(await Home(p.Id), id)["note"]);
        await f.DbAsync(async db =>
        {
            db.ActivityLog.Add(new ActivityLog { ProjectId = other.Id, ItemType = ItemType.Deliverable, ItemId = id, Action = "StatusChanged", Categories = ["status"], OccurredAt = f.Clock.Now.AddDays(-5),
                Changes = JsonSerializer.Serialize(new[] { new { field = "Status", old = DeliverableStatus.InProgress, @new = DeliverableStatus.InReview } }) });
            return await db.SaveChangesAsync();
        });
        Assert.Null(Row(await Home(p.Id), id)["note"]);
        await f.DbAsync(async db =>
        {
            db.ActivityLog.Add(new ActivityLog { ProjectId = p.Id, ItemType = ItemType.Deliverable, ItemId = id, Action = "StatusChanged", Categories = ["status"], OccurredAt = f.Clock.Now.AddDays(-2), Changes = "{}" });
            return await db.SaveChangesAsync();
        });
        Assert.Null(Row(await Home(p.Id), id)["note"]);
    }

    [Fact]
    public async Task Review_days_use_organisation_calendar_date_not_elapsed_24_hour_blocks()
    {
        await Setting("flag.home_overview", true);
        f.Clock.SetDate(2026, 10, 20);
        var p = await d.Project(); var del = await Deliverable(p); var id = del.G("id");
        f.Clock.Now = new DateTimeOffset(2026, 10, 31, 12, 0, 0, TimeSpan.Zero);
        await f.DbAsync(db => db.Deliverables.Where(x => x.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, DeliverableStatus.InReview)));
        await f.DbAsync(async db =>
        {
            db.ActivityLog.Add(new ActivityLog { ProjectId = p.Id, ItemType = ItemType.Deliverable, ItemId = id, Action = "StatusChanged", Categories = ["status"],
                OccurredAt = new DateTimeOffset(2026, 10, 30, 2, 30, 0, TimeSpan.Zero), // Oct29 23:30 Halifax.
                Changes = JsonSerializer.Serialize(new[] { new { field = "Status", old = DeliverableStatus.InProgress, @new = DeliverableStatus.InReview } }) });
            return await db.SaveChangesAsync();
        });
        Assert.Equal(2, Row(await Home(p.Id), id)["note"]!.I("days"));
    }

    static HomeVisualDeadlineNotes.Evidence Evidence(DateTimeOffset at, string from, string to) => new(Guid.NewGuid(), at, "StatusChanged", ["status"],
        JsonSerializer.Serialize(new[] { new { field = "Status", old = from, @new = to } }));

    [Fact]
    public void Current_review_episode_starts_at_current_round_ready_event_not_an_old_round()
    {
        var now = new DateTimeOffset(2026, 10, 31, 13, 0, 0, TimeSpan.Zero);
        var rows = new[] { Evidence(now.AddDays(-9), TaskStatuses.InProgress, TaskStatuses.ReadyForReview), Evidence(now.AddDays(-8), TaskStatuses.ReadyForReview, TaskStatuses.InReview),
            Evidence(now.AddDays(-6), TaskStatuses.InReview, TaskStatuses.RevisionRequired), Evidence(now.AddDays(-5), TaskStatuses.RevisionRequired, TaskStatuses.InProgress),
            Evidence(now.AddDays(-3), TaskStatuses.InProgress, TaskStatuses.ReadyForReview), Evidence(now.AddDays(-2), TaskStatuses.ReadyForReview, TaskStatuses.InReview) };
        Assert.Equal(now.AddDays(-3), HomeVisualDeadlineNotes.ReviewEntered(TaskStatuses.InReview, rows, now));
    }

    [Fact]
    public void Incomplete_disconnected_future_and_ambiguous_status_evidence_never_invents_an_entry_time()
    {
        var now = new DateTimeOffset(2026, 10, 31, 13, 0, 0, TimeSpan.Zero);
        var entry = Evidence(now.AddDays(-5), TaskStatuses.InProgress, TaskStatuses.ReadyForReview);
        Assert.Null(HomeVisualDeadlineNotes.ReviewEntered(TaskStatuses.InReview, [entry], now));
        Assert.Null(HomeVisualDeadlineNotes.ReviewEntered(TaskStatuses.InReview, [Evidence(now.AddDays(-2), TaskStatuses.ReadyForReview, TaskStatuses.InReview)], now));
        Assert.Null(HomeVisualDeadlineNotes.ReviewEntered(TaskStatuses.ReadyForReview, [entry with { At = now.AddDays(1) }], now));
        Assert.Null(HomeVisualDeadlineNotes.ReviewEntered(TaskStatuses.InReview, [entry, Evidence(entry.At, TaskStatuses.ReadyForReview, TaskStatuses.InReview)], now));
        Assert.Null(HomeVisualDeadlineNotes.ReviewEntered(TaskStatuses.ReadyForReview, [entry, entry with { Id = Guid.NewGuid(), At = now.AddDays(-1), Changes = "{}" }], now));
        var unrelated = new HomeVisualDeadlineNotes.Evidence(Guid.NewGuid(), now.AddDays(-1), "Commented", ["comment"], "{}");
        Assert.Equal(entry.At, HomeVisualDeadlineNotes.ReviewEntered(TaskStatuses.ReadyForReview, [entry, unrelated], now));
    }
}
