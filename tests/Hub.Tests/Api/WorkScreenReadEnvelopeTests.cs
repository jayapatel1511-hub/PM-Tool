using System.Data;
using System.Data.Common;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Hub.Tests.Api;

public sealed class WorkScreenReadEnvelopeTests
{
    static string ReadState(WorkSeenTestContext s) => s.Factory.Db(db => JsonSerializer.Serialize(new {
        Occurrences = db.WorkAssignmentOccurrences.AsNoTracking().OrderBy(row => row.Token).ToArray(),
        Acknowledgements = db.WorkSeenAcknowledgements.AsNoTracking().OrderBy(row => row.RecipientId).ThenBy(row => row.Token).ToArray(),
        Receipts = db.WorkSeenCommandReceipts.AsNoTracking().OrderBy(row => row.UserId).ThenBy(row => row.RequestId).ToArray(),
        Activity = db.ActivityLog.Count(),
        Tasks = db.Tasks.OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion }).ToArray(),
        Deliverables = db.Deliverables.OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion }).ToArray(),
        Decisions = db.Decisions.OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion }).ToArray(),
        Actions = db.Actions.OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion }).ToArray(),
        Risks = db.Risks.OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion }).ToArray(),
        Issues = db.Issues.OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion }).ToArray(),
        Handoffs = db.Handoffs.OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion }).ToArray(),
        Changes = db.ChangeNotices.OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion }).ToArray(),
        Projects = db.Projects.OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion }).ToArray(),
        Milestones = db.Milestones.OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion }).ToArray(),
        OpenRoles = db.OpenRoles.OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion }).ToArray(),
        Visits = db.UserSettings.OrderBy(row => row.UserId).Select(row => new { row.UserId, row.MyWorkFirstVisitAt,
            row.MyWorkPreviousVisitAt, row.MyWorkLastVisitAt, row.MyWorkLastMarkAllAt, row.MyWorkSeenRevision }).ToArray()
    }));

    [Fact]
    public async Task Returned_envelopes_are_read_only_and_private_seen_is_omitted_for_other_subjects_and_dark_flags()
    {
        using var s = new WorkSeenTestContext();
        var p = await s.Data.Project();
        var task = await s.Data.NewTask(p.Id, TestData.Pm, new { assigneeId = s.Data.User(TestData.Alex) });
        var id = task.G("id");
        var client = s.Factory.As(TestData.Alex);
        async Task<JsonNode> Get(string path) => await (await client.GetAsync(path)).Json();
        var dark = await Get("/api/v1/me/work");
        var darkRow = dark["tasks"]!.AsArray().Single(row => row!.G("id") == id)!;
        Assert.False(darkRow.AsObject().ContainsKey("seen"));
        Assert.False(darkRow.AsObject().ContainsKey("workActivity"));
        await s.Enable();
        var before = ReadState(s);
        var own = await Get("/api/v1/me/work");
        var row = own["tasks"]!.AsArray().Single(item => item!.G("id") == id)!;
        Assert.Equal("Unenrolled", row["seen"]!.S("state"));
        Assert.Equal(s.Latest(id), row["seen"]!["observedTokens"]!.AsArray().Single()!.G("token"));
        Assert.Equal(s.Data.User(TestData.Pm), row["workActivity"]!.G("actorId"));
        Assert.Equal(0, row["workActivity"]!["commentCount"]!.GetValue<int>());
        var detail = await Get($"/api/v1/tasks/{id}");
        Assert.True(JsonNode.DeepEquals(row["seen"], detail["seen"]));
        Assert.True(JsonNode.DeepEquals(row["workActivity"], detail["workActivity"]));
        var page = await Get($"/api/v1/tasks?ids={id}&seenSubjectId={s.Data.User(TestData.Alex)}");
        Assert.True(JsonNode.DeepEquals(row["seen"], page["items"]![0]!["seen"]));
        var foreignSubject = await Get($"/api/v1/tasks?ids={id}&seenSubjectId={s.Data.User(TestData.Jill)}");
        Assert.False(foreignSubject["items"]![0]!.AsObject().ContainsKey("seen"));
        Assert.Equal(ReadState(s), before);
        var supervisor = await (await s.Factory.As(TestData.Admin).GetAsync($"/api/v1/me/work?userId={s.Data.User(TestData.Alex)}")).Json();
        var inspected = supervisor["tasks"]!.AsArray().Single(item => item!.G("id") == id)!;
        Assert.False(inspected.AsObject().ContainsKey("seen"));
        Assert.True(JsonNode.DeepEquals(row["workActivity"], inspected["workActivity"]));
        Assert.Equal(before, ReadState(s));
    }

    [Fact]
    public async Task Same_task_in_work_review_and_watching_uses_identical_current_event_and_assignment_evidence()
    {
        using var s = new WorkSeenTestContext(); await s.Enable(); await s.Flag("my_work_follow_up", true);
        await (await s.Factory.As(TestData.Admin).Put("/api/v1/admin/settings/allow_self_review", new { value = true })).Json();
        var p = await s.Data.Project();
        var task = await s.Data.NewTask(p.Id, TestData.Pm, new {
            assigneeId = s.Data.User(TestData.Alex), reviewerId = s.Data.User(TestData.Alex), requiresReview = true,
            dueDate = "2026-09-01"
        });
        await s.Data.Move(TestData.Pm, task, "In Progress");
        await s.Data.Move(TestData.Pm, task, "Ready for Review");
        var id = task.G("id");
        await (await s.Factory.As(TestData.Alex).Post($"/api/v1/items/Task/{id}/watchers", new { userId = s.Data.User(TestData.Alex) })).Json(204);
        await s.Factory.Evaluate(p.Id);
        var before = ReadState(s);
        var client = s.Factory.As(TestData.Alex);
        var own = await (await client.GetAsync("/api/v1/me/work")).Json();
        var row = own["tasks"]!.AsArray().Single(item => item!.G("id") == id)!;
        var review = own["reviews"]!["tasks"]!.AsArray().Single(item => item!.G("id") == id)!;
        var watching = (await (await client.GetAsync("/api/v1/me/work/watching")).Json())["items"]!.AsArray().Single(item => item!.G("id") == id)!;
        Assert.Equal(2, row["seen"]!["observedTokens"]!.AsArray().Count);
        foreach (var other in new[] { review, watching })
        {
            Assert.True(JsonNode.DeepEquals(row["seen"], other["seen"]));
            Assert.True(JsonNode.DeepEquals(row["workActivity"], other["workActivity"]));
        }
        var attention = own["attention"]!["items"]!.AsArray()
            .Where(item => item!.S("itemType") == "Task" && item.G("itemId") == id).ToArray();
        Assert.NotEmpty(attention);
        foreach (var other in attention)
        {
            Assert.True(JsonNode.DeepEquals(row["seen"], other!["seen"]));
            Assert.True(JsonNode.DeepEquals(row["workActivity"], other["workActivity"]));
        }
        Assert.Equal(before, ReadState(s));
    }

    [Fact]
    public async Task Other_person_view_omits_the_current_actors_real_assignment_state_on_the_same_task()
    {
        using var s = new WorkSeenTestContext(); await s.Enable();
        var p = await s.Data.Project(); await s.Visit(TestData.Sam);
        var task = await s.Data.NewTask(p.Id, TestData.Pm, new { assigneeId = s.Data.User(TestData.Sam) });
        var id = task.G("id");
        await (await s.Factory.As(TestData.Pm).Post($"/api/v1/tasks/{id}/collaborators", new { userId = s.Data.User(TestData.Alex) })).Json(204);
        var before = ReadState(s);
        var client = s.Factory.As(TestData.Sam);
        var own = await (await client.GetAsync("/api/v1/me/work")).Json();
        var ownRow = own["tasks"]!.AsArray().Single(row => row!.G("id") == id)!;
        Assert.Equal("New", ownRow["seen"]!.S("state"));
        var other = await (await client.GetAsync($"/api/v1/me/work?userId={s.Data.User(TestData.Alex)}")).Json();
        var otherRow = other["tasks"]!.AsArray().Single(row => row!.G("id") == id)!;
        Assert.False(otherRow.AsObject().ContainsKey("seen"));
        Assert.True(JsonNode.DeepEquals(ownRow["workActivity"], otherRow["workActivity"]));
        var list = await (await client.GetAsync($"/api/v1/tasks?ids={id}&seenSubjectId={s.Data.User(TestData.Alex)}")).Json();
        Assert.False(list["items"]![0]!.AsObject().ContainsKey("seen"));
        Assert.Equal(before, ReadState(s));
    }

    internal sealed record DetailCase(string Kind, Guid Id, string Path, string Who);

    // Every canonical fixture below is produced by an implemented public command, including the
    // handoff and second source revision that actually produces a ChangeNotice.
    internal static async Task<(Project Project, DetailCase[] Cases)> NineDetails(WorkSeenTestContext s)
    {
        var p = await s.Data.Project(); var f = s.Factory; var d = s.Data;
        var civil = d.ProjectDiscipline(p.Id, "Civil"); var electrical = d.ProjectDiscipline(p.Id, "Electrical");
        var alex = d.User(TestData.Alex); var marc = d.User(TestData.Marc); var omar = d.User(TestData.Omar);
        async Task<JsonNode> Post(string path, object body, int status = 201, string who = TestData.Pm) =>
            await (await f.As(who).Post(path, body)).Json(status);
        var task = await d.NewTask(p.Id, TestData.Pm, new { assigneeId = alex, reviewerId = marc });
        var del = await Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Synthetic envelope source", projectDisciplineId = civil,
            deliverableTypeId = await d.DeliverableType(), ownerId = alex, reviewerId = marc, revision = "A", requiresReview = false });
        var decision = await Post($"/api/v1/projects/{p.Id}/decisions", new { subject = "Synthetic envelope decision", description = "Synthetic",
            requiredByDate = "2026-10-01", impactLevel = Impact.Low, impactDescription = "Synthetic envelope impact", ownerUserId = alex });
        var meeting = await Post($"/api/v1/projects/{p.Id}/meetings/current", new { }, 200);
        var action = await Post($"/api/v1/meetings/{meeting.G("id")}/actions", new { text = "Synthetic envelope action", ownerType = "User", ownerUserId = alex });
        var risk = await Post($"/api/v1/projects/{p.Id}/risks", new { title = "Synthetic envelope risk", description = "Synthetic", probability = 2, impact = 2, ownerId = alex });
        var issue = await Post($"/api/v1/projects/{p.Id}/issues", new { title = "Synthetic envelope issue", description = "Synthetic", severity = Impact.Low, ownerId = alex });
        var target = await d.NewTask(p.Id, TestData.Omar, new { assigneeId = omar }, "Electrical");
        var version = f.Db(db => db.Deliverables.Single(row => row.Id == del.G("id")).RowVersion);
        var handoff = await Post($"/api/v1/projects/{p.Id}/handoffs", new HandoffEndpoints.DraftBody(Guid.NewGuid(), "Synthetic envelope handoff",
            del.G("id"), version, "A", "https://example.test/envelope-A.pdf", electrical, alex, omar, target.G("id"), null,
            "Use synthetic survey", "Check synthetic extents", new(2026, 10, 1), new(2026, 10, 2), null, null));
        ChangeEndpoints.RegisterBody Registration(string revision, Guid? old = null) => new(Guid.NewGuid(), del.G("id"), version,
            civil, alex, "Deliverable", "synthetic-envelope", "Synthetic envelope source", revision, "https://example.test/" + revision + ".pdf",
            "Synthetic", "Synthetic corridor", null, old, old is null ? null : f.Db(db => db.SourceHeads.Single(row => row.ProjectId == p.Id).RowVersion),
            old is null ? null : "Synthetic source change", old is null ? null : new DateOnly(2026, 9, 14), old is null ? null : new DateOnly(2026, 10, 1));
        var revision = await Post($"/api/v1/projects/{p.Id}/source-revisions", Registration("A"), 200, TestData.Alex);
        var change = await Post($"/api/v1/projects/{p.Id}/source-revisions", Registration("B", revision.G("id")), 200, TestData.Alex);
        // Ordinary owned Risks/Issues enter the existing MyWork union through Watching; ownership
        // alone must not manufacture a stream entry. Keep the fixture on that actual route.
        await s.Flag("my_work_follow_up", true);
        foreach (var (kind, row) in new[] { ("Risk", risk), ("Issue", issue) })
            await (await f.As(TestData.Alex).Post($"/api/v1/items/{kind}/{row.G("id")}/watchers", new { userId = alex })).Json(204);
        DetailCase Detail(string kind, JsonNode row, string route) => new(kind, row.G("id"), $"/api/v1/{route}/{row.G("id")}", TestData.Alex);
        return (p, [Detail("Task", task, "tasks"), Detail("Deliverable", del, "deliverables"), Detail("Decision", decision, "decisions"),
            Detail("Action", action, "actions"), Detail("Risk", risk, "risks"), Detail("Issue", issue, "issues"),
            new("Handoff", handoff.G("id"), $"/api/v1/projects/{p.Id}/handoffs/{handoff.G("id")}", TestData.Alex),
            new("ChangeNotice", change.G("id"), $"/api/v1/projects/{p.Id}/changes/{change.G("id")}", TestData.Alex),
            new("Project", p.Id, $"/api/v1/projects/{p.Id}", TestData.Pm)]);
    }

    [Fact]
    public async Task All_nine_authorised_details_omit_dark_metadata_and_return_exact_private_tokens_without_get_writes()
    {
        using var s = new WorkSeenTestContext();
        var (_, cases) = await NineDetails(s);
        var before = ReadState(s);
        foreach (var item in cases)
        {
            var dark = await (await s.Factory.As(item.Who).GetAsync(item.Path)).Json();
            Assert.False(dark.AsObject().ContainsKey("seen"), item.Kind);
            Assert.False(dark.AsObject().ContainsKey("workActivity"), item.Kind);
        }
        Assert.Equal(before, ReadState(s));
        await s.Enable(); before = ReadState(s);
        foreach (var item in cases)
        {
            var detail = await (await s.Factory.As(item.Who).GetAsync(item.Path)).Json();
            Assert.Equal("Unenrolled", detail["seen"]!.S("state"));
            var recipient = s.Data.User(item.Who);
            var expected = s.Factory.Db(db => db.WorkAssignmentOccurrences.Where(row => row.ItemType == item.Kind && row.ItemId == item.Id && row.RecipientId == recipient)
                .Select(row => row.Token).ToArray());
            Assert.NotEmpty(expected);
            Assert.Equal(expected.Order(), detail["seen"]!["observedTokens"]!.AsArray().Select(row => row!.G("token")).Order());
            var activity = detail["workActivity"]!;
            s.Factory.Db(db => {
                var source = db.ActivityLog.Single(row => row.Id == activity.G("eventId"));
                Assert.Equal(item.Kind, source.ItemType); Assert.Equal(item.Id, source.ItemId);
                Assert.Equal(source.ActorUserId, activity["actorId"]!.GetValue<Guid>());
                Assert.Equal(source.OccurredAt, activity["occurredAt"]!.GetValue<DateTimeOffset>());
                return 0;
            });
        }
        Assert.Equal(before, ReadState(s));
        // Disabling presentation preserves existing evidence and never serialises private tokens.
        await s.Flag("work_screens", false); before = ReadState(s);
        foreach (var item in cases)
        {
            var dark = await (await s.Factory.As(item.Who).GetAsync(item.Path)).Json();
            Assert.False(dark.AsObject().ContainsKey("seen"), item.Kind);
            Assert.False(dark.AsObject().ContainsKey("workActivity"), item.Kind);
        }
        Assert.Equal(before, ReadState(s));
    }

    [Fact]
    public async Task Restricted_project_hides_all_nine_details_before_any_private_or_activity_evidence_can_escape()
    {
        using var s = new WorkSeenTestContext(); var (p, cases) = await NineDetails(s); await s.Enable();
        await (await s.Factory.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true })).Json();
        var version = s.Factory.Db(db => db.Projects.Single(row => row.Id == p.Id).RowVersion);
        await (await s.Factory.As(TestData.Pm).Patch($"/api/v1/projects/{p.Id}", new { visibility = "Restricted" }, version)).Json();
        var before = ReadState(s);
        foreach (var item in cases)
        {
            var response = await (await s.Factory.As(TestData.Rita).GetAsync(item.Path)).Json(404);
            Assert.False(response.AsObject().ContainsKey("seen"), item.Kind);
            Assert.False(response.AsObject().ContainsKey("workActivity"), item.Kind);
            var tokens = s.Factory.Db(db => db.WorkAssignmentOccurrences.Where(row => row.ItemType == item.Kind && row.ItemId == item.Id).Select(row => row.Token).ToArray());
            foreach (var token in tokens) Assert.DoesNotContain(token.ToString(), response.ToJsonString());
        }
        var list = await (await s.Factory.As(TestData.Rita).GetAsync($"/api/v1/tasks?ids={cases[0].Id}")).Json();
        Assert.Empty(list["items"]!.AsArray());
        Assert.Equal(before, ReadState(s));
    }

    [Fact]
    public async Task Milestone_and_open_role_never_receive_fabricated_assignment_seen_state()
    {
        using var s = new WorkSeenTestContext(); await s.Enable();
        var p = await s.Data.Project(); var client = s.Factory.As(TestData.Pm);
        var milestone = await (await client.Post($"/api/v1/projects/{p.Id}/milestones", new { name = "Synthetic no-owner milestone",
            milestoneType = MilestoneType.DesignSubmission, date = "2026-10-01", projectDisciplineId = s.Data.ProjectDiscipline(p.Id, "Civil"), isClientFacing = true })).Json(201);
        var role = await (await client.Post($"/api/v1/projects/{p.Id}/open-roles", new { roleLabel = "Synthetic unfilled demand",
            projectDisciplineId = s.Data.ProjectDiscipline(p.Id, "Civil"), fromDate = "2026-10-01", throughDate = "2026-10-02", plannedHours = 8, note = "Synthetic" })).Json(201);
        var before = ReadState(s);
        var myWork = await (await s.Factory.As(TestData.Alex).GetAsync("/api/v1/me/work")).Json();
        var milestoneRow = myWork["milestones"]!.AsArray().Single(row => row!.G("id") == milestone.G("id"))!;
        Assert.False(milestoneRow.AsObject().ContainsKey("seen"));
        Assert.NotNull(milestoneRow["workActivity"]);
        var roles = await (await client.GetAsync($"/api/v1/projects/{p.Id}/open-roles")).Json();
        Assert.False(roles.AsArray().Single(row => row!.G("id") == role.G("id"))!.AsObject().ContainsKey("seen"));
        Assert.False(s.Factory.Db(db => db.WorkAssignmentOccurrences.Any(row => row.ItemId == milestone.G("id") || row.ItemId == role.G("id"))));
        Assert.Equal(before, ReadState(s));
    }

    [Fact]
    public async Task Coordination_reuses_the_handoff_detail_evidence_in_register_outgoing_and_blocker_rows_without_get_writes()
    {
        using var s = new WorkSeenTestContext(); var (p, cases) = await NineDetails(s); await s.Enable();
        var handoff = cases.Single(item => item.Kind == "Handoff"); var client = s.Factory.As(TestData.Alex);
        var before = ReadState(s);
        var detail = await (await client.GetAsync(handoff.Path)).Json();
        var projection = await (await client.GetAsync($"/api/v1/projects/{p.Id}/discipline-coordination?from=2026-09-01&to=2026-10-31")).Json();
        var register = projection["handoffs"]!.AsArray().Single(row => row!.G("id") == handoff.Id)!;
        var outgoing = projection["outgoing"]!.AsArray().Single(row => row!.G("id") == handoff.Id)!;
        var blocker = projection["blockerGroups"]!.AsArray().Single(row => row!.G("handoffId") == handoff.Id)!;
        foreach (var row in new[] { register, outgoing, blocker })
        {
            Assert.True(JsonNode.DeepEquals(detail["seen"], row["seen"]));
            Assert.True(JsonNode.DeepEquals(detail["workActivity"], row["workActivity"]));
        }
        Assert.NotEmpty(register["seen"]!["observedTokens"]!.AsArray());
        Assert.Equal(before, ReadState(s));
    }

    sealed class SelectionBarrier(Guid itemId) : DbCommandInterceptor
    {
        int armed;
        public TaskCompletionSource<bool> Selected { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Arm() => Interlocked.Exchange(ref armed, 1);
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData data,
            DbDataReader result, CancellationToken ct = default)
        {
            var sql = command.CommandText.Replace("\"", "");
            if (data.Context?.Database.CurrentTransaction?.GetDbTransaction().IsolationLevel == IsolationLevel.RepeatableRead
                && sql.Contains("FROM hub.task AS", StringComparison.Ordinal)
                && command.Parameters.Cast<DbParameter>().Any(parameter => parameter.Value is Guid id && id == itemId)
                && Interlocked.CompareExchange(ref armed, 0, 1) == 1)
            {
                // The original canonical SELECT has executed in the request's real MVCC snapshot.
                // Hold its reader before the remaining response/activity/occurrence queries, with no sleeps.
                Selected.TrySetResult(true);
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(45), ct);
            }
            return result;
        }
    }

    [Fact]
    public async Task Detail_snapshot_keeps_original_version_event_and_token_across_committed_away_and_back_assignment()
    {
        using var s = new WorkSeenTestContext(); await s.Enable(); var p = await s.Data.Project(); await s.Visit();
        var task = await s.Data.NewTask(p.Id, TestData.Pm, new { assigneeId = s.Data.User(TestData.Alex) }); var id = task.G("id");
        var barrier = new SelectionBarrier(id);
        using var app = s.Factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.AddDbContext<HubDb>(options => options.AddInterceptors(barrier))));
        using var client = app.CreateClient(); client.DefaultRequestHeaders.Add("X-Dev-User", TestData.Alex);
        var original = await (await client.GetAsync($"/api/v1/tasks/{id}")).Json();
        var oldToken = original["seen"]!["observedTokens"]![0]!.G("token");
        var privateBefore = s.Factory.Db(db => new { Acks = db.WorkSeenAcknowledgements.Count(), Receipts = db.WorkSeenCommandReceipts.Count(),
            Revision = db.UserSettings.Single(row => row.UserId == s.Data.User(TestData.Alex)).MyWorkSeenRevision });
        barrier.Arm(); var pending = client.GetAsync($"/api/v1/tasks/{id}");
        JsonNode held;
        try
        {
            await barrier.Selected.Task.WaitAsync(TimeSpan.FromSeconds(30));
            s.Factory.Clock.Now = s.Factory.Clock.Now.AddMinutes(1); await s.Assign(id, s.Data.User(TestData.Jill));
            s.Factory.Clock.Now = s.Factory.Clock.Now.AddMinutes(1); await s.Assign(id, s.Data.User(TestData.Alex));
        }
        finally { barrier.Release.TrySetResult(true); }
        held = await (await pending.WaitAsync(TimeSpan.FromSeconds(30))).Json();
        Assert.Equal(original["task"]!.I("rowVersion"), held["task"]!.I("rowVersion"));
        Assert.True(JsonNode.DeepEquals(original["seen"], held["seen"]));
        Assert.True(JsonNode.DeepEquals(original["workActivity"], held["workActivity"]));
        var next = await (await client.GetAsync($"/api/v1/tasks/{id}")).Json();
        Assert.True(next["task"]!.I("rowVersion") > held["task"]!.I("rowVersion"));
        var newToken = next["seen"]!["observedTokens"]![0]!.G("token"); Assert.NotEqual(oldToken, newToken);
        Assert.NotEqual(held["workActivity"]!.G("eventId"), next["workActivity"]!.G("eventId"));
        Assert.Equal("New", next["seen"]!.S("state"));
        s.Factory.Db(db => {
            var occurrence = db.WorkAssignmentOccurrences.Single(row => row.Token == newToken);
            Assert.Equal(next["task"]!.I("rowVersion"), occurrence.SourceVersion);
            Assert.Equal(next["workActivity"]!.G("eventId"), occurrence.SourceActivityId);
            return 0;
        });
        Assert.Equal(privateBefore, s.Factory.Db(db => new { Acks = db.WorkSeenAcknowledgements.Count(), Receipts = db.WorkSeenCommandReceipts.Count(),
            Revision = db.UserSettings.Single(row => row.UserId == s.Data.User(TestData.Alex)).MyWorkSeenRevision }));
    }
}
