using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

internal sealed class WorkSeenTestContext : IDisposable
{
    public HubFactory Factory { get; } = new();
    public TestData Data { get; }
    public WorkSeenTestContext() => Data = new(Factory);
    public void Dispose() => Factory.Dispose();
    public async Task<JsonNode> Flag(string name, bool value) => await (await Factory.As(TestData.Admin).Put("/api/v1/admin/settings/flag." + name, new { value })).Json();
    public async Task Enable() => await Flag("work_screens", true);
    public async Task<JsonNode> Visit(string who = TestData.Alex, Guid? request = null) =>
        await (await Factory.As(who).Post("/api/v1/me/work/visits", new { requestId = request ?? Guid.NewGuid() })).Json();
    public async Task<JsonNode> MarkAll(string who = TestData.Alex, Guid? request = null, int status = 200) =>
        await (await Factory.As(who).Post("/api/v1/me/work/seen-all", new { requestId = request ?? Guid.NewGuid() })).Json(status);
    public async Task<JsonNode> Open(Guid item, Guid[] tokens, Guid? request = null, string who = TestData.Alex, string type = "Task", int status = 200) =>
        await (await Factory.As(who).Post("/api/v1/me/work/opened", new { requestId = request ?? Guid.NewGuid(), itemType = type, itemId = item, observedTokens = tokens })).Json(status);
    public Guid Latest(Guid item, string who = TestData.Alex, string role = "Assignee") =>
        Factory.Db(db => db.WorkAssignmentOccurrences.Where(row => row.ItemType == "Task" && row.ItemId == item
            && row.RecipientId == Data.User(who) && row.Role == role).OrderByDescending(row => row.SourceVersion).First().Token);
    public Task<IReadOnlyDictionary<WorkSeenKey, WorkSeenDecoration>> Read(Guid[] ids, string who = TestData.Alex, Guid? subject = null, string type = "Task") =>
        Factory.DbAsync(async db => {
            var userId = Data.User(who);
            var person = await db.Users.AsNoTracking().SingleAsync(user => user.Id == userId);
            var me = new CurrentUser { Id = userId, IsActive = person.IsActive, Resolved = true,
                Roles = (await db.UserRoles.Where(role => role.UserId == userId).Select(role => role.Role).ToListAsync()).ToHashSet() };
            var access = new Access(db, me);
            var org = await db.CurrentSettings();
            object[] entities = type switch {
                "Task" => (await db.Tasks.AsNoTracking().Where(row => ids.Contains(row.Id)).ToListAsync()).Cast<object>().ToArray(),
                "Risk" => (await db.Risks.AsNoTracking().Where(row => ids.Contains(row.Id)).ToListAsync()).Cast<object>().ToArray(),
                "Issue" => (await db.Issues.AsNoTracking().Where(row => ids.Contains(row.Id)).ToListAsync()).Cast<object>().ToArray(),
                "Decision" => (await db.Decisions.AsNoTracking().Where(row => ids.Contains(row.Id)).ToListAsync()).Cast<object>().ToArray(),
                "Action" => (await db.Actions.AsNoTracking().Where(row => ids.Contains(row.Id)).ToListAsync()).Cast<object>().ToArray(),
                _ => throw new ArgumentException("Unsupported test bound")
            };
            var bounds = entities.Select(entity => WorkAssignmentPublication.Bound(db, entity)).ToArray();
            return await WorkSeenReads.DecorateAsync(db, access, org,
                new(subject ?? userId, Clock.LocalDate(Factory.Clock.Now, org), Factory.Clock.Now), bounds);
        });
    public async Task Assign(Guid item, Guid? assignee)
    {
        var version = Factory.Db(db => db.Tasks.Single(task => task.Id == item).RowVersion);
        await (await Factory.As(TestData.Pm).Patch("/api/v1/tasks/" + item, new { assigneeId = assignee }, version)).Json();
    }
}

public sealed class MyWorkSeenTests
{
    sealed class LoseFirstCommittedResponse(HttpMessageHandler inner) : DelegatingHandler(inner)
    {
        int remaining = 1;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode && Interlocked.Exchange(ref remaining, 0) == 1)
            {
                // The real server command has returned after transaction commit, but the caller receives no response.
                response.Dispose();
                throw new HttpRequestException("Synthetic transport failure after server commit, before client response.");
            }
            return response;
        }
    }

    [Fact]
    public async Task Client_transport_failure_after_real_commit_replays_exact_durable_cut_not_late_assignments()
    {
        using var s = new WorkSeenTestContext(); await s.Enable(); var p = await s.Data.Project(); await s.Visit();
        var first = await s.Data.NewTask(p.Id, extra: new { assigneeId = s.Data.User(TestData.Alex) });
        var request = Guid.NewGuid();
        // Fault injection is at the actual in-process HTTP handler boundary; physical TCP/proxy loss remains separate.
        using var transport = new LoseFirstCommittedResponse(s.Factory.Server.CreateHandler());
        using var client = new HttpClient(transport) { BaseAddress = new Uri("http://localhost") };
        client.DefaultRequestHeaders.Add("X-Dev-User", TestData.Alex);
        await Assert.ThrowsAsync<HttpRequestException>(async () => {
            using var response = await client.Post("/api/v1/me/work/seen-all", new { requestId = request });
        });
        var committed = s.Factory.Db(db => db.WorkSeenCommandReceipts.Single(row => row.UserId == s.Data.User(TestData.Alex) && row.RequestId == request).ResponseJson);
        Assert.True(s.Factory.Db(db => db.WorkSeenAcknowledgements.Any(row => row.Token == s.Latest(first.G("id")))));
        var late = await s.Data.NewTask(p.Id, extra: new { assigneeId = s.Data.User(TestData.Alex) });
        var replay = await s.MarkAll(request: request);
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(committed), replay));
        Assert.Equal("New", (await s.Read([late.G("id")]))[new("Task", late.G("id"))].State);
        Assert.Single(s.Factory.Db(db => db.WorkSeenCommandReceipts.Where(row => row.UserId == s.Data.User(TestData.Alex) && row.RequestId == request).ToArray()));
    }

    [Fact]
    public async Task First_visit_baselines_without_GET_writes_and_later_visits_preserve_pending_tokens()
    {
        using var s = new WorkSeenTestContext(); await s.Enable();
        var p = await s.Data.Project();
        var old = await s.Data.NewTask(p.Id, extra: new { assigneeId = s.Data.User(TestData.Alex) });
        await (await s.Factory.As(TestData.Alex).GetAsync("/api/v1/me/work")).Json();
        Assert.False(s.Factory.Db(db => db.UserSettings.Any(row => row.UserId == s.Data.User(TestData.Alex) && row.MyWorkFirstVisitAt != null)));
        var request = Guid.NewGuid(); var first = await s.Visit(request: request);
        Assert.Equal(first.ToJsonString(), (await s.Visit(request: request)).ToJsonString());
        Assert.Equal("Seen", (await s.Read([old.G("id")]))[new("Task", old.G("id"))].State);
        var added = await s.Data.NewTask(p.Id, extra: new { assigneeId = s.Data.User(TestData.Alex) });
        await s.Visit();
        Assert.Equal("New", (await s.Read([added.G("id")]))[new("Task", added.G("id"))].State);
        var before = s.Factory.Db(db => new { Version = db.Tasks.Single(row => row.Id == added.G("id")).RowVersion,
            Logs = db.ActivityLog.Count(), Notifications = db.Notifications.Count(), Outbox = db.Outbox.Count() });
        await s.Open(added.G("id"), [s.Latest(added.G("id"))]);
        Assert.Equal("Seen", (await s.Read([added.G("id")]))[new("Task", added.G("id"))].State);
        var after = s.Factory.Db(db => new { Version = db.Tasks.Single(row => row.Id == added.G("id")).RowVersion,
            Logs = db.ActivityLog.Count(), Notifications = db.Notifications.Count(), Outbox = db.Outbox.Count() });
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Mounted_replacement_payload_requires_new_requestId_and_old_open_never_clears_new_token()
    {
        using var s = new WorkSeenTestContext(); await s.Enable(); var p = await s.Data.Project(); await s.Visit();
        var task = await s.Data.NewTask(p.Id, extra: new { assigneeId = s.Data.User(TestData.Alex) }); var id = task.G("id");
        var old = s.Latest(id); await s.Assign(id, s.Data.User(TestData.Jill)); await s.Assign(id, s.Data.User(TestData.Alex));
        var current = s.Latest(id); Assert.NotEqual(old, current);
        var request = Guid.NewGuid(); var reply = await s.Open(id, [old], request);
        Assert.Equal("New", (await s.Read([id]))[new("Task", id)].State);
        Assert.Equal(reply.ToJsonString(), (await s.Open(id, [old, old], request)).ToJsonString());
        await s.Open(id, [current], request, status: 422);
        await s.Open(id, [current], Guid.NewGuid());
        Assert.Equal("Seen", (await s.Read([id]))[new("Task", id)].State);
    }

    [Fact]
    public async Task Private_ReadOnly_commands_are_allowed_but_flagoff_extra_subject_and_foreign_tokens_refuse()
    {
        using var s = new WorkSeenTestContext(); await s.Enable(); var p = await s.Data.Project();
        await s.Visit(TestData.Rita); await s.MarkAll(TestData.Rita);
        await (await s.Factory.As(TestData.Alex).Post("/api/v1/me/work/visits", new { requestId = Guid.NewGuid(), userId = s.Data.User(TestData.Jill) })).Json(400);
        await s.Visit(); var task = await s.Data.NewTask(p.Id, extra: new { assigneeId = s.Data.User(TestData.Alex) });
        Assert.Empty(await s.Read([task.G("id")], subject: s.Data.User(TestData.Jill)));
        await s.Open(task.G("id"), [s.Latest(task.G("id"))], who: TestData.Jill, status: 404);
        await s.Flag("work_screens", false);
        await (await s.Factory.As(TestData.Alex).Post("/api/v1/me/work/visits", new { requestId = Guid.NewGuid() })).Json(404);
        Assert.Empty(await s.Read([task.G("id")]));
        await s.Flag("work_screens", true);
        Assert.Equal("New", (await s.Read([task.G("id")]))[new("Task", task.G("id"))].State);
    }

    [Fact]
    public async Task MarkAll_clears_beyond_a_task_page_and_replay_does_not_select_newer_assignments()
    {
        using var s = new WorkSeenTestContext(); await s.Enable(); var p = await s.Data.Project(); await s.Visit();
        var ids = new List<Guid>();
        for (var index = 0; index < 205; index++)
            ids.Add((await s.Data.NewTask(p.Id, extra: new { assigneeId = s.Data.User(TestData.Alex) })).G("id"));
        Assert.All((await s.Read(ids.ToArray())).Values, value => Assert.Equal("New", value.State));
        var request = Guid.NewGuid(); var reply = await s.MarkAll(request: request);
        Assert.All((await s.Read(ids.ToArray())).Values, value => Assert.Equal("Seen", value.State));
        var later = await s.Data.NewTask(p.Id, extra: new { assigneeId = s.Data.User(TestData.Alex) });
        Assert.Equal(reply.ToJsonString(), (await s.MarkAll(request: request)).ToJsonString());
        Assert.Equal("New", (await s.Read([later.G("id")]))[new("Task", later.G("id"))].State);
    }

    [Fact]
    public async Task First_visit_token_baseline_includes_hidden_closed_legacy_occurrences_without_public_item_facts()
    {
        using var s = new WorkSeenTestContext(); await s.Enable(); var p = await s.Data.Project();
        var task = await s.Data.NewTask(p.Id, extra: new { assigneeId = s.Data.User(TestData.Alex) }); var id = task.G("id");
        await s.Data.Move(TestData.Pm, task, "In Progress"); await s.Data.Move(TestData.Pm, task, "Complete");
        var baseline = Guid.NewGuid();
        await s.Factory.DbAsync(async db => {
            // Explicit pre-migration/visibility fixture; never used by a production read as a repair path.
            await db.WorkAssignmentOccurrences.Where(row => row.ItemType == "Task" && row.ItemId == id).ExecuteDeleteAsync();
            var canonical = await db.Tasks.SingleAsync(row => row.Id == id);
            (await db.Projects.SingleAsync(row => row.Id == p.Id)).Visibility = Hub.Domain.Visibility.Restricted;
            (await db.ProjectMembers.SingleAsync(row => row.ProjectId == p.Id && row.UserId == s.Data.User(TestData.Alex))).RemovedAt = s.Factory.Clock.Now;
            db.WorkAssignmentOccurrences.Add(new WorkAssignmentOccurrence { Token = baseline, RecipientId = s.Data.User(TestData.Alex),
                ItemType = "Task", ItemId = id, ProjectId = p.Id, Role = "Assignee", SourceVersion = canonical.RowVersion,
                Provenance = "BaselineSeed", RecordedAt = s.Factory.Clock.Now });
            return await db.SaveChangesAsync();
        });
        Assert.Empty(await s.Read([id]));
        var result = await s.Visit();
        Assert.DoesNotContain(id.ToString(), result.ToJsonString());
        s.Factory.Db(db => {
            Assert.Equal("InitialBaseline", db.WorkSeenAcknowledgements.Single(row => row.Token == baseline).Provenance);
            var occurrence = db.WorkAssignmentOccurrences.Single(row => row.Token == baseline);
            Assert.Null(occurrence.SourceOccurredAt); Assert.Null(occurrence.SourceActivityId); return 0;
        });
        await s.Factory.DbAsync(async db => {
            (await db.ProjectMembers.SingleAsync(row => row.ProjectId == p.Id && row.UserId == s.Data.User(TestData.Alex))).RemovedAt = null;
            return await db.SaveChangesAsync();
        });
        Assert.Equal("Seen", (await s.Read([id]))[new("Task", id)].State);
    }

    [Fact]
    public async Task Off_period_reassignment_survives_and_replay_after_recipient_revocation_refuses()
    {
        using var s = new WorkSeenTestContext(); await s.Enable(); var p = await s.Data.Project(); await s.Visit();
        var task = await s.Data.NewTask(p.Id, extra: new { assigneeId = s.Data.User(TestData.Alex) }); var id = task.G("id");
        var token = s.Latest(id); var request = Guid.NewGuid(); await s.Open(id, [token], request);
        await s.Flag("work_screens", false); await s.Assign(id, s.Data.User(TestData.Jill)); await s.Assign(id, s.Data.User(TestData.Alex));
        Assert.NotEqual(token, s.Latest(id)); await s.Flag("work_screens", true);
        Assert.Equal("New", (await s.Read([id]))[new("Task", id)].State);
        await s.Assign(id, s.Data.User(TestData.Jill));
        await s.Open(id, [token], request, status: 404);
        Assert.Single(s.Factory.Db(db => db.WorkSeenCommandReceipts.Where(row => row.UserId == s.Data.User(TestData.Alex) && row.RequestId == request).ToArray()));
    }

    [Fact]
    public async Task Each_observed_role_is_acknowledged_individually_and_receipts_do_not_expire_after_a_day()
    {
        using var s = new WorkSeenTestContext(); await s.Enable(); var p = await s.Data.Project(); await s.Visit();
        await (await s.Factory.As(TestData.Admin).Put("/api/v1/admin/settings/allow_self_review", new { value = true })).Json();
        var task = await s.Data.NewTask(p.Id, extra: new { assigneeId = s.Data.User(TestData.Alex), reviewerId = s.Data.User(TestData.Alex) }); var id = task.G("id");
        var first = (await s.Read([id]))[new("Task", id)]; Assert.Equal(2, first.ObservedTokens.Count);
        await s.Open(id, [first.ObservedTokens[0].Token]);
        Assert.Equal("New", (await s.Read([id]))[new("Task", id)].State);
        var request = Guid.NewGuid(); var tokens = first.ObservedTokens.Select(row => row.Token).ToArray();
        var reply = await s.Open(id, tokens, request); s.Factory.Clock.Now = s.Factory.Clock.Now.AddDays(3);
        Assert.Equal(reply.ToJsonString(), (await s.Open(id, tokens.Reverse().ToArray(), request)).ToJsonString());
        Assert.Equal("Seen", (await s.Read([id]))[new("Task", id)].State);
    }

    [Fact]
    public async Task Atomic_seen_metadata_bypasses_generic_receipts_and_action_reuse_refuses()
    {
        using var s = new WorkSeenTestContext(); await s.Enable();
        using var client = s.Factory.As(TestData.Alex);
        var header = "seen-private-" + Guid.NewGuid().ToString("N"); client.DefaultRequestHeaders.Add("Idempotency-Key", header);
        var request = Guid.NewGuid();
        var visit = await (await client.Post("/api/v1/me/work/visits", new { requestId = request })).Json();
        Assert.Equal(visit.ToJsonString(), (await (await client.Post("/api/v1/me/work/visits", new { requestId = request })).Json()).ToJsonString());
        await (await client.Post("/api/v1/me/work/seen-all", new { requestId = request })).Json(422);
        Assert.False(s.Factory.Db(db => db.Idempotency.Any(row => row.UserId == s.Data.User(TestData.Alex) && row.Key == header)));
    }
}
