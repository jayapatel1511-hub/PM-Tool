using System.Data.Common;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace Hub.Tests.Api;

public sealed class TaskContributionTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData d;
    public TaskContributionTests() { d = new(f); f.Clock.SetDate(2026, 11, 2); }
    public void Dispose() => f.Dispose();
    async Task Flag(bool value) => await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.work_screens", new { value })).Json();
    static string Path(Guid task) => $"/api/v1/tasks/{task}/contribution";
    async Task<(Project Project, JsonNode Task, Guid Member)> Setup()
    {
        await Flag(true); var project = await d.Project();
        var task = await d.NewTask(project.Id, extra: new { assigneeId = d.User(TestData.Alex), requiresReview = false });
        await (await f.As(TestData.Marc).Post($"/api/v1/tasks/{task.G("id")}/collaborators", new { userId = d.User(TestData.Jill) })).Json(204);
        return (project, task, Member(task.G("id"), TestData.Jill));
    }
    Guid Member(Guid task, string who) => f.Db(db => db.Collaborators.Single(c => c.TaskId == task && c.UserId == d.User(who)).Id);
    async Task<JsonNode> Set(Guid task, Guid member, int version, bool marked, string? note = null, string who = TestData.Jill, int expect = 200, string? reason = null, int? header = null) =>
        await (await f.As(who).Patch(Path(task), new { collaboratorId = member, rowVersion = version, marked, note, reason }, header)).Json(expect);
    async Task<JsonNode> Detail(Guid task, string who = TestData.Jill) => await (await f.As(who).GetAsync($"/api/v1/tasks/{task}")).Json();
    JsonNode Contribution(JsonNode detail, string who) => detail["collaborators"]!.AsArray().Single(c => c!.G("id") == d.User(who))!["contribution"]!;
    string ParentState() => f.Db(db => JsonSerializer.Serialize(new
    {
        Tasks = db.Tasks.IgnoreQueryFilters().OrderBy(t => t.Id).ToArray(), TaskStates = db.TaskStates.OrderBy(t => t.TaskId).ToArray(),
        Assignments = db.WorkAssignmentOccurrences.OrderBy(r => r.Token).ToArray(), Seen = db.WorkSeenAcknowledgements.OrderBy(r => r.RecipientId).ThenBy(r => r.Token).ToArray(),
        SeenCommands = db.WorkSeenCommandReceipts.OrderBy(r => r.UserId).ThenBy(r => r.RequestId).ToArray(), Bulk = db.TaskBulkCommandReceipts.OrderBy(r => r.Id).ToArray(),
        Comments = db.Comments.OrderBy(r => r.Id).ToArray(), Notices = db.Notifications.OrderBy(r => r.Id).ToArray(),
        Emails = db.Emails.OrderBy(r => r.Id).ToArray(), Outbox = db.Outbox.OrderBy(r => r.Id).ToArray(),
        Activity = db.ActivityLog.Where(r => r.ItemType != "TaskContribution").OrderBy(r => r.Id).ToArray()
    }));
    string ContributionState() => f.Db(db => JsonSerializer.Serialize(new
    {
        State = db.Set<TaskContribution>().AsNoTracking().OrderBy(c => c.Id).ToArray(),
        Audit = db.ActivityLog.AsNoTracking().Where(a => a.ItemType == "TaskContribution").OrderBy(a => a.Id).ToArray()
    }));

    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("200")]
    public async Task Own_membership_mark_withdraw_and_remark_have_independent_versions_one_audit_and_no_task_side_effects(string? input)
    {
        var s = await Setup(); var id = s.Task.G("id"); var note = input == "200" ? new string('n', 200) : input;
        var before = ParentState(); var empty = Contribution(await Detail(id), TestData.Jill);
        Assert.Equal(s.Member, empty.G("collaboratorId")); Assert.Equal(0, empty.I("rowVersion")); Assert.Null(empty["markedAt"]);
        Assert.True(empty["canMark"]!.GetValue<bool>()); Assert.False(empty["canWithdraw"]!.GetValue<bool>());
        Assert.Equal(0, f.Db(db => db.Set<TaskContribution>().Count()));
        var first = await Set(id, s.Member, 0, true, note);
        Assert.Equal(1, first.I("rowVersion")); Assert.Equal(f.Clock.Now, first["markedAt"]!.GetValue<DateTimeOffset>());
        Assert.Equal(TimeSpan.Zero, first["markedAt"]!.GetValue<DateTimeOffset>().Offset);
        Assert.Equal(string.IsNullOrWhiteSpace(note) ? null : note, first["note"]?.GetValue<string>());
        Assert.False(first["canMark"]!.GetValue<bool>()); Assert.True(first["canWithdraw"]!.GetValue<bool>());
        var log = f.Db(db => db.ActivityLog.Single(a => a.ItemType == "TaskContribution"));
        Assert.Equal("ContributionMarked", log.Action); Assert.Equal(s.Project.Id, log.ProjectId); Assert.Equal(s.Task.S("key"), log.ItemKey);
        Assert.Equal(d.User(TestData.Jill), log.ActorUserId); Assert.NotEqual(id, log.ItemId);
        var stored = f.Db(db => db.Set<TaskContribution>().Single());
        Assert.Equal((s.Member, id, d.User(TestData.Jill), s.Project.Id, s.Task.S("key")),
            (stored.CollaboratorId, stored.TaskId, stored.UserId, stored.ProjectId, stored.TaskKeySnapshot));
        Assert.Equal(f.Db(db => db.Tasks.Single(t => t.Id == id).Name), stored.TaskNameSnapshot);
        var bytes = ContributionState(); f.Clock.Now = f.Clock.Now.AddMinutes(1);
        await Set(id, s.Member, 1, true, note); Assert.Equal(bytes, ContributionState());
        await Set(id, s.Member, 0, true, note, expect: 409); Assert.Equal(bytes, ContributionState());
        var withdrawn = await Set(id, s.Member, 1, false);
        Assert.Equal(2, withdrawn.I("rowVersion")); Assert.Null(withdrawn["markedAt"]); Assert.Null(withdrawn["note"]);
        Assert.Equal(stored.Id, f.Db(db => db.Set<TaskContribution>().Single().Id));
        Assert.Equal(2, f.Db(db => db.ActivityLog.Count(a => a.ItemType == "TaskContribution")));
        Assert.Equal("ContributionWithdrawn", f.Db(db => db.ActivityLog.Where(a => a.ItemType == "TaskContribution").OrderByDescending(a => a.Id).First().Action));
        var afterWithdraw = ContributionState(); await Set(id, s.Member, 2, false); Assert.Equal(afterWithdraw, ContributionState());
        await Set(id, s.Member, 1, true, expect: 409); Assert.Equal(afterWithdraw, ContributionState());
        Assert.Equal(3, (await Set(id, s.Member, 2, true, "Another deliberate mark")).I("rowVersion"));
        Assert.Equal(before, ParentState());
        // A later withdrawal never rewrites the immutable first mark's stored note or identity.
        Assert.Equal(log.Changes, f.Db(db => db.ActivityLog.Single(a => a.Id == log.Id).Changes));
    }

    [Fact]
    public async Task Empty_withdrawal_is_noop_and_note_normalization_is_not_an_edit_while_marked_shortcut()
    {
        var s = await Setup(); var id = s.Task.G("id"); var before = ContributionState();
        var empty = await Set(id, s.Member, 0, false); Assert.Equal(0, empty.I("rowVersion")); Assert.Equal(before, ContributionState());
        await Set(id, s.Member, 1, false, expect: 409); Assert.Equal(before, ContributionState());
        Assert.Equal("My coordination", (await Set(id, s.Member, 0, true, "  My coordination  ")).S("note"));
        before = ContributionState(); await Set(id, s.Member, 1, true, "My coordination"); Assert.Equal(before, ContributionState());
        await Set(id, s.Member, 1, true, "Changed note", expect: 400);
        await Set(id, s.Member, 1, false, "", expect: 400); Assert.Equal(before, ContributionState());
        await Set(id, s.Member, 1, false); Assert.Null(Contribution(await Detail(id), TestData.Jill)["note"]);
    }

    [Fact]
    public async Task Required_types_strict_fields_note_limit_and_nested_body_version_are_enforced_before_any_write()
    {
        var s = await Setup(); var id = s.Task.G("id"); var baseline = ParentState(); var current = ContributionState();
        Dictionary<string, object?> Body() => new() { ["collaboratorId"] = s.Member, ["rowVersion"] = 0, ["marked"] = true };
        var cases = new List<Dictionary<string, object?>>();
        foreach (var missing in new[] { "collaboratorId", "rowVersion", "marked" }) { var body = Body(); body.Remove(missing); cases.Add(body); }
        foreach (var forged in new[] { "userId", "taskId", "projectId", "markedAt", "status", "progressPct" }) { var body = Body(); body[forged] = "forged"; cases.Add(body); }
        foreach (var (key, value) in new (string, object?)[] { ("collaboratorId", Guid.Empty), ("rowVersion", -1), ("rowVersion", "0"), ("marked", "true"), ("marked", null), ("note", new string('n', 201)) })
        { var body = Body(); body[key] = value; cases.Add(body); }
        foreach (var body in cases) await (await f.As(TestData.Jill).Patch(Path(id), body, 999)).Json(400);
        Assert.Equal(baseline, ParentState()); Assert.Equal(current, ContributionState());
        // Even a supplied parent/stale header cannot silently replace the nested body version.
        Assert.Equal(1, (await Set(id, s.Member, 0, true, header: 999)).I("rowVersion"));
        current = ContributionState(); await Set(id, s.Member, 0, false, expect: 409, header: 1); Assert.Equal(current, ContributionState());
    }

    [Fact]
    public async Task Seeing_a_mark_assignment_watching_or_management_never_grants_an_on_behalf_command()
    {
        var s = await Setup(); var id = s.Task.G("id");
        await (await f.As(TestData.Diane).Post($"/api/v1/items/Task/{id}/watchers", new { userId = d.User(TestData.Diane) })).Json(204);
        var before = ParentState(); var state = ContributionState();
        foreach (var who in new[] { TestData.Alex, TestData.Pm, TestData.Marc, TestData.Admin, TestData.Diane })
            await Set(id, s.Member, 0, true, who: who, expect: 403);
        var other = await d.NewTask(s.Project.Id); // deliberately a real foreign task, not a guessed missing ID
        before = ParentState(); await Set(other.G("id"), s.Member, 0, true, expect: 403);
        await Set(Guid.NewGuid(), s.Member, 0, true, expect: 404); Assert.Equal(state, ContributionState()); Assert.Equal(before, ParentState());
        await Set(id, s.Member, 0, true, "Visible own contribution");
        var peer = Contribution(await Detail(id, TestData.Alex), TestData.Jill);
        Assert.NotNull(peer["markedAt"]); Assert.Equal("Visible own contribution", peer.S("note"));
        Assert.False(peer["canMark"]!.GetValue<bool>()); Assert.False(peer["canWithdraw"]!.GetValue<bool>()); Assert.NotNull(peer["refusalReason"]);
        Assert.Equal(d.User(TestData.Jill), (await Detail(id))["collaborators"]![0]!.G("id"));
    }

    [Fact]
    public async Task Read_only_inactive_and_current_hidden_project_memberships_are_refused_without_mutation()
    {
        var s = await Setup(); var id = s.Task.G("id");
        await (await f.As(TestData.Marc).Post($"/api/v1/tasks/{id}/collaborators", new { userId = d.User(TestData.Rita) })).Json(204);
        var before = ContributionState(); await Set(id, Member(id, TestData.Rita), 0, true, who: TestData.Rita, expect: 403); Assert.Equal(before, ContributionState());
        await f.DbAsync(async db => { (await db.Users.SingleAsync(u => u.Id == d.User(TestData.Jill))).IsActive = false; return await db.SaveChangesAsync(); });
        await Set(id, s.Member, 0, true, expect: 403); Assert.Equal(before, ContributionState());
        await f.DbAsync(async db => { (await db.Users.SingleAsync(u => u.Id == d.User(TestData.Jill))).IsActive = true; return await db.SaveChangesAsync(); });
        await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/restricted_projects_enabled", new { value = true })).Json();
        await (await f.As(TestData.Pm).Patch($"/api/v1/projects/{s.Project.Id}", new { visibility = "Restricted" }, d.Version(s.Project.Id))).Json();
        await (await f.As(TestData.Jill).GetAsync($"/api/v1/tasks/{id}")).Json(); // relation currently has actual member visibility
        await f.DbAsync(async db => { var member = await db.ProjectMembers.SingleAsync(m => m.ProjectId == s.Project.Id && m.UserId == d.User(TestData.Jill) && m.RemovedAt == null); member.RemovedAt = f.Clock.Now; return await db.SaveChangesAsync(); });
        var parent = ParentState(); await Set(id, s.Member, 0, true, expect: 404);
        await (await f.As(TestData.Jill).GetAsync($"/api/v1/tasks/{id}")).Json(404);
        Assert.Equal(before, ContributionState()); Assert.Equal(parent, ParentState());
    }

    [Theory]
    [InlineData("Complete")] [InlineData("Cancelled")]
    public async Task Terminal_task_status_does_not_erase_or_implicitly_prohibit_a_current_collaborator_mark(string status)
    {
        var s = await Setup(); var id = s.Task.G("id");
        if (status == "Complete") { await d.Move(TestData.Alex, s.Task, "In Progress"); await d.Move(TestData.Alex, s.Task, "Complete"); }
        else await d.Move(TestData.Pm, s.Task, "Cancelled", new { reason = "Synthetic cancellation" });
        var before = ParentState(); await Set(id, s.Member, 0, true); await Set(id, s.Member, 1, false); Assert.Equal(before, ParentState());
    }

    [Fact]
    public async Task Completed_project_keeps_own_PM_only_policy_separates_note_and_accepts_canonical_long_correction_reason()
    {
        var s = await Setup(); var id = s.Task.G("id");
        await (await f.As(TestData.Marc).Post($"/api/v1/tasks/{id}/collaborators", new { userId = d.User(TestData.Pm) })).Json(204);
        await (await f.As(TestData.Pm).Post($"/api/v1/projects/{s.Project.Id}/transition", new { toStatus = "Complete", reason = "Synthetic closeout", closeout = new { tasks = "cancel" }, rowVersion = d.Version(s.Project.Id) })).Json();
        var member = Member(id, TestData.Pm); var before = ContributionState();
        await Set(id, s.Member, 0, true, expect: 403); await Set(id, member, 0, true, "This note is not a correction reason", TestData.Pm, 400);
        await Set(id, member, 0, true, "Still not the correction reason", TestData.Pm, 400, reason: "tiny");
        Assert.Equal(before, ContributionState()); var parent = ParentState();
        var longReason = "Synthetic correction under current PM authority: " + new string('r', 4500);
        Assert.True(longReason.Length > 4000);
        var marked = await Set(id, member, 0, true, "Coordination part", TestData.Pm, reason: "  " + longReason + "  ");
        Assert.Equal(1, marked.I("rowVersion")); Assert.Equal("Coordination part", marked.S("note"));
        Assert.Equal(longReason, f.Db(db => db.ActivityLog.Single(a => a.ItemType == "TaskContribution").Reason));
        Assert.Equal(parent, ParentState());
    }

    [Theory]
    [InlineData("Archived")] [InlineData("Cancelled")]
    public async Task Current_read_only_project_state_withholds_own_command_and_read_capabilities(string status)
    {
        var s = await Setup(); var id = s.Task.G("id"); await Set(id, s.Member, 0, true);
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == s.Project.Id)).Status = status; return await db.SaveChangesAsync(); });
        var before = ContributionState(); var parent = ParentState();
        await Set(id, s.Member, 1, false, expect: 403);
        var current = Contribution(await Detail(id), TestData.Jill);
        Assert.NotNull(current["markedAt"]); Assert.False(current["canMark"]!.GetValue<bool>()); Assert.False(current["canWithdraw"]!.GetValue<bool>());
        Assert.NotNull(current["refusalReason"]); Assert.Equal(before, ContributionState()); Assert.Equal(parent, ParentState());
    }

    [Fact]
    public async Task Removal_cascades_current_state_but_retains_audit_and_readded_membership_has_no_inherited_version()
    {
        var s = await Setup(); var id = s.Task.G("id"); await Set(id, s.Member, 0, true, "Original relation");
        var audit = f.Db(db => db.ActivityLog.Single(a => a.ItemType == "TaskContribution"));
        await (await f.As(TestData.Marc).DeleteAsync($"/api/v1/tasks/{id}/collaborators/{d.User(TestData.Jill)}")).Json(204);
        Assert.Empty(f.Db(db => db.Set<TaskContribution>().ToArray()));
        Assert.Equal(audit.Changes, f.Db(db => db.ActivityLog.Single(a => a.Id == audit.Id).Changes));
        await (await f.As(TestData.Marc).Post($"/api/v1/tasks/{id}/collaborators", new { userId = d.User(TestData.Jill) })).Json(204);
        var fresh = Member(id, TestData.Jill); Assert.NotEqual(s.Member, fresh);
        Assert.Equal(0, Contribution(await Detail(id), TestData.Jill).I("rowVersion"));
        await Set(id, s.Member, 0, true, expect: 403); await Set(id, s.Member, 1, false, expect: 403);
        Assert.Equal(1, (await Set(id, fresh, 0, true, "New relation only")).I("rowVersion"));
        var state = ContributionState();
        await (await f.As(TestData.Pm).Delete($"/api/v1/tasks/{id}", await d.TaskVersion(s.Task))).Json();
        await Set(id, fresh, 1, false, expect: 404); Assert.Equal(state, ContributionState());
        var version = f.Db(db => db.Tasks.IgnoreQueryFilters().Single(t => t.Id == id).RowVersion);
        await (await f.As(TestData.Pm).Post($"/api/v1/tasks/{id}/restore", new { rowVersion = version })).Json();
        Assert.Equal(1, Contribution(await Detail(id), TestData.Jill).I("rowVersion")); Assert.Equal(state, ContributionState());
    }

    [Fact]
    public async Task Flag_off_omits_addition_hides_before_history_totals_and_export_and_preserves_stored_evidence()
    {
        var s = await Setup(); var id = s.Task.G("id"); var note = "Synthetic contribution off privacy marker";
        await Set(id, s.Member, 0, true, note); var bytes = ContributionState();
        var on = await (await f.As(TestData.Pm).GetAsync($"/api/v1/items/Task/{id}/activity?pageSize=200")).Json();
        Assert.Single(on["items"]!.AsArray(), row => row!.S("itemType") == "TaskContribution");
        await Flag(false); var detail = await Detail(id);
        Assert.All(detail["collaborators"]!.AsArray(), row => Assert.False(row!.AsObject().ContainsKey("contribution")));
        await Set(id, s.Member, 1, false, expect: 404);
        var off = await (await f.As(TestData.Pm).GetAsync($"/api/v1/items/Task/{id}/activity?pageSize=200")).Json();
        Assert.Equal(on.I("totalCount") - 1, off.I("totalCount")); Assert.DoesNotContain(note, off.ToJsonString());
        var project = await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{s.Project.Id}/activity?itemType=TaskContribution&pageSize=1")).Json();
        Assert.Equal(0, project.I("totalCount")); Assert.Empty(project["items"]!.AsArray());
        var export = await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{s.Project.Id}/activity/export?itemType=TaskContribution");
        export.EnsureSuccessStatusCode(); Assert.DoesNotContain(note, await export.Content.ReadAsStringAsync());
        Assert.Equal(bytes, ContributionState()); await Flag(true);
        Assert.Equal(1, Contribution(await Detail(id), TestData.Jill).I("rowVersion")); Assert.Equal(bytes, ContributionState());
    }

    [Fact]
    public async Task Two_first_marks_wait_on_actual_membership_lock_then_one_succeeds_and_one_is_stale()
    {
        var s = await Setup(); var id = s.Task.G("id"); var parent = ParentState();
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var holder = f.DbAsync(async db => { await using var tx = await db.Database.BeginTransactionAsync();
            await db.Database.SqlQuery<Guid>($"SELECT id AS \"Value\" FROM hub.task_collaborator WHERE id={s.Member} FOR UPDATE").ToListAsync();
            locked.SetResult(); await release.Task.WaitAsync(TimeSpan.FromSeconds(45)); await tx.CommitAsync(); return 0; });
        await locked.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var body = new { collaboratorId = s.Member, rowVersion = 0, marked = true };
        var first = f.As(TestData.Jill).Patch(Path(id), body); var second = f.As(TestData.Jill).Patch(Path(id), body);
        try { await WaitForMembershipWaiters(2); Assert.False(first.IsCompleted); Assert.False(second.IsCompleted); }
        finally { release.TrySetResult(); }
        await holder; var results = await Task.WhenAll(first, second);
        Assert.Equal(new[] { HttpStatusCode.OK, HttpStatusCode.Conflict }, results.Select(r => r.StatusCode).Order());
        await results.Single(r => r.StatusCode == HttpStatusCode.OK).Json();
        var stale = await results.Single(r => r.StatusCode == HttpStatusCode.Conflict).Json(409); Assert.Equal("concurrency_conflict", stale.S("code"));
        Assert.Equal(1, f.Db(db => db.Set<TaskContribution>().Single().RowVersion));
        Assert.Single(f.Db(db => db.ActivityLog.Where(a => a.ItemType == "TaskContribution").ToArray())); Assert.Equal(parent, ParentState());
    }

    [Fact]
    public async Task Concurrent_same_version_mark_noop_and_withdrawal_cannot_resurrect_the_withdrawn_state()
    {
        var s = await Setup(); var id = s.Task.G("id"); await Set(id, s.Member, 0, true, "Retained original note");
        var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var holder = f.DbAsync(async db => { await using var tx = await db.Database.BeginTransactionAsync();
            await db.Database.SqlQuery<Guid>($"SELECT id AS \"Value\" FROM hub.task_collaborator WHERE id={s.Member} FOR UPDATE").ToListAsync();
            locked.SetResult(); await release.Task.WaitAsync(TimeSpan.FromSeconds(45)); await tx.CommitAsync(); return 0; });
        await locked.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var mark = f.As(TestData.Jill).Patch(Path(id), new { collaboratorId = s.Member, rowVersion = 1, marked = true, note = "Retained original note" });
        var withdraw = f.As(TestData.Jill).Patch(Path(id), new { collaboratorId = s.Member, rowVersion = 1, marked = false });
        try { await WaitForMembershipWaiters(2); Assert.False(mark.IsCompleted); Assert.False(withdraw.IsCompleted); }
        finally { release.TrySetResult(); }
        await holder; Assert.Equal(2, (await (await withdraw).Json()).I("rowVersion"));
        var reply = await mark;
        if (reply.StatusCode == HttpStatusCode.OK) Assert.Equal(1, (await reply.Json()).I("rowVersion"));
        else { Assert.Equal(HttpStatusCode.Conflict, reply.StatusCode); Assert.Equal("concurrency_conflict", (await reply.Json(409)).S("code")); }
        var current = f.Db(db => db.Set<TaskContribution>().Single()); Assert.Null(current.MarkedAt); Assert.Null(current.Note); Assert.Equal(2, current.RowVersion);
        Assert.Equal(2, f.Db(db => db.ActivityLog.Count(a => a.ItemType == "TaskContribution")));
    }

    async Task WaitForMembershipWaiters(int count)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        while (true)
        {
            var waiting = await f.DbAsync(db => db.Database.SqlQuery<int>($"""
                SELECT COUNT(*)::int AS "Value" FROM pg_stat_activity
                WHERE datname=current_database() AND pid<>pg_backend_pid()
                  AND wait_event_type='Lock' AND query LIKE '%task_collaborator%'
                """).SingleAsync(deadline.Token));
            if (waiting >= count) return;
            deadline.Token.ThrowIfCancellationRequested(); await Task.Yield();
        }
    }

    [Fact]
    public async Task Membership_removed_while_first_mark_waits_cannot_be_substituted_or_recreated()
    {
        var s = await Setup(); var id = s.Task.G("id"); var locked = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        // Controlled deletion in this disposable provider database holds the same row lock as canonical removal.
        var holder = f.DbAsync(async db => { await using var tx = await db.Database.BeginTransactionAsync();
            await db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM hub.task_collaborator WHERE id={s.Member}");
            locked.SetResult(); await release.Task.WaitAsync(TimeSpan.FromSeconds(45)); await tx.CommitAsync(); return 0; });
        await locked.Task.WaitAsync(TimeSpan.FromSeconds(15)); var request = Set(id, s.Member, 0, true, expect: 403);
        try { await WaitForMembershipWaiters(1); Assert.False(request.IsCompleted); }
        finally { release.TrySetResult(); }
        await holder; await request; Assert.Empty(f.Db(db => db.Set<TaskContribution>().ToArray()));
        Assert.Empty(f.Db(db => db.ActivityLog.Where(a => a.ItemType == "TaskContribution").ToArray()));
    }

    [Fact]
    public async Task Audit_insert_fault_rolls_back_both_first_state_and_automatic_log()
    {
        var s = await Setup(); var id = s.Task.G("id"); var parent = ParentState(); var state = ContributionState();
        await f.DbAsync(db => db.Database.ExecuteSqlRawAsync("""
            CREATE FUNCTION hub.reject_contribution_audit() RETURNS trigger LANGUAGE plpgsql AS $$
            BEGIN IF NEW.item_type='TaskContribution' THEN RAISE EXCEPTION 'Synthetic contribution audit failure'; END IF; RETURN NEW; END $$;
            CREATE TRIGGER reject_contribution_audit BEFORE INSERT ON hub.activity_log
            FOR EACH ROW EXECUTE FUNCTION hub.reject_contribution_audit();
            """));
        try { await Set(id, s.Member, 0, true, "Must roll back", expect: 500); Assert.Equal(parent, ParentState()); Assert.Equal(state, ContributionState()); }
        finally { await f.DbAsync(db => db.Database.ExecuteSqlRawAsync("DROP TRIGGER reject_contribution_audit ON hub.activity_log; DROP FUNCTION hub.reject_contribution_audit();")); }
        Assert.Equal(1, (await Set(id, s.Member, 0, true, "Deliberate retry after fault removal")).I("rowVersion"));
    }

    [Fact]
    public async Task Detail_batches_actual_memberships_in_one_state_query_and_flag_off_performs_none()
    {
        var s = await Setup(); var id = s.Task.G("id"); var capture = new ContributionReads();
        using var app = f.WithWebHostBuilder(builder => builder.ConfigureServices(services => services.AddDbContext<HubDb>(options => options.AddInterceptors(capture))));
        using var client = app.CreateClient(); client.DefaultRequestHeaders.Add("X-Dev-User", TestData.Pm);
        await (await client.GetAsync($"/api/v1/tasks/{id}")).Json(); capture.Queries.Clear();
        var small = await (await client.GetAsync($"/api/v1/tasks/{id}")).Json(); Assert.Single(capture.Queries);
        await f.DbAsync(async db => { // Synthetic scale cohort, current memberships rather than a reference-directory projection.
            for (var i = 0; i < 64; i++) { var user = new AppUser { Email = $"synthetic-contribution-{i}@hub.test", DisplayName = $"Synthetic contributor {i}" };
                db.Users.Add(user); db.Collaborators.Add(new TaskCollaborator { TaskId = id, UserId = user.Id, AddedAt = f.Clock.Now }); }
            return await db.SaveChangesAsync(); });
        capture.Queries.Clear(); var large = await (await client.GetAsync($"/api/v1/tasks/{id}")).Json(); Assert.Single(capture.Queries);
        Assert.Equal(small["collaborators"]!.AsArray().Count + 64, large["collaborators"]!.AsArray().Count);
        Assert.All(large["collaborators"]!.AsArray(), row => Assert.NotNull(row!["contribution"]));
        Assert.Equal(0, f.Db(db => db.Set<TaskContribution>().Count()));
        using var admin = app.CreateClient(); admin.DefaultRequestHeaders.Add("X-Dev-User", TestData.Admin);
        await (await admin.Put("/api/v1/admin/settings/flag.work_screens", new { value = false })).Json();
        capture.Queries.Clear(); var off = await (await client.GetAsync($"/api/v1/tasks/{id}")).Json();
        Assert.Empty(capture.Queries); Assert.All(off["collaborators"]!.AsArray(), row => Assert.False(row!.AsObject().ContainsKey("contribution")));
    }

    [Fact]
    public async Task Stored_off_contribution_is_excluded_from_personal_following_daily_updates_and_weekly_change_counts()
    {
        var s = await Setup(); var id = s.Task.G("id"); var user = d.User(TestData.Pm);
        await (await f.As(TestData.Pm).Put($"/api/v1/projects/{s.Project.Id}/follow", new { level = FollowLevel.AllActivity })).Json();
        var baseline = await (await f.As(TestData.Pm).GetAsync($"/api/v1/me/following?projectId={s.Project.Id}&pageSize=200")).Json();
        var since = f.Clock.Now; f.Clock.Now = f.Clock.Now.AddMinutes(1); await Set(id, s.Member, 0, true, "Synthetic preserved contribution digest marker");
        var bytes = ContributionState(); var now = f.Clock.Now; var today = DateOnly.FromDateTime(now.UtcDateTime);
        async Task<Digest.Result?> Daily(bool enabled) => await f.DbAsync(db => Digest.Build(db, user, "Synthetic PM", today, since, now, new OrgSettings { Flags = new Dictionary<string, bool> { ["work_screens"] = enabled } }, ""));
        async Task<WeeklySummary.Result?> Weekly(bool enabled) => await f.DbAsync(db => WeeklySummary.Build(db, user, "Synthetic PM", today, since, now, new OrgSettings { Flags = new Dictionary<string, bool> { ["work_screens"] = enabled } }, ""));
        var onFollow = await (await f.As(TestData.Pm).GetAsync($"/api/v1/me/following?projectId={s.Project.Id}&pageSize=200")).Json();
        Assert.Equal(baseline.I("totalCount") + 1, onFollow.I("totalCount"));
        Assert.Contains(onFollow["items"]!.AsArray(), row => row!["entry"]!.S("itemType") == "TaskContribution");
        Assert.Equal(1, Assert.Single((await Daily(true))!.ProjectUpdates).Count);
        Assert.Equal(1, Assert.Single((await Weekly(true))!.Projects).Changes);
        await Flag(false);
        var offFollow = await (await f.As(TestData.Pm).GetAsync($"/api/v1/me/following?projectId={s.Project.Id}&pageSize=1")).Json();
        Assert.Equal(baseline.I("totalCount"), offFollow.I("totalCount")); Assert.DoesNotContain("TaskContribution", offFollow.ToJsonString());
        var offDaily = await Daily(false); Assert.True(offDaily is null || offDaily.ProjectUpdates.Count == 0);
        Assert.Equal(0, Assert.Single((await Weekly(false))!.Projects).Changes);
        Assert.Equal(bytes, ContributionState()); await Flag(true);
        Assert.Equal(1, Assert.Single((await Daily(true))!.ProjectUpdates).Count); Assert.Equal(bytes, ContributionState());
    }

    sealed class ContributionReads : DbCommandInterceptor
    {
        internal readonly List<string> Queries = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { if (command.CommandText.Contains("task_contribution", StringComparison.OrdinalIgnoreCase)) Queries.Add(command.CommandText); return ValueTask.FromResult(result); }
    }
}
