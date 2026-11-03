using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Weekly Coordination keeps its established arrays/counts and adds a
/// de-duplicated context index.  These tests exercise the existing agenda
/// queries with ordinary task fixtures; they do not manufacture dashboard rows.
public sealed class WeeklyAgendaContextTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData d;
    public WeeklyAgendaContextTests() => d = new(f);
    public void Dispose() => f.Dispose();

    async Task<JsonNode> Get(string who, string path, int status = 200) => await (await f.As(who).GetAsync(path)).Json(status);
    async Task<JsonNode> Create(string path, object body) => await (await f.As(TestData.Pm).Post(path, body)).Json(201);
    async Task Flag(bool on) => (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.screen_patterns", new { value = on })).EnsureSuccessStatusCode();

    sealed record Setup(Project Project, Guid Due, Guid Upcoming, Guid Complete, Guid Held, Guid Civil, Guid Electrical);

    async Task<Setup> New()
    {
        var p = await d.Project();
        var civil = d.ProjectDiscipline(p.Id, "Civil");
        var electrical = d.ProjectDiscipline(p.Id, "Electrical");
        var due = (await d.NewTask(p.Id, TestData.Pm, new { name = "Due agenda task", assigneeId = d.User(TestData.Alex), dueDate = "2026-09-16" })).G("id");
        var upcoming = (await d.NewTask(p.Id, TestData.Pm, new { name = "Upcoming agenda task", assigneeId = d.User(TestData.Alex), dueDate = "2026-09-22" })).G("id");
        var completeTask = await d.NewTask(p.Id, TestData.Pm, new { name = "Completed agenda task", assigneeId = d.User(TestData.Alex), requiresReview = false });
        var complete = completeTask.G("id");
        await d.Move(TestData.Alex, completeTask, TaskStatuses.Complete);
        var heldTask = await d.NewTask(p.Id, TestData.Pm, new { name = "Held agenda task", assigneeId = d.User(TestData.Alex), dueDate = "2026-09-17" });
        var held = heldTask.G("id");
        await d.Move(TestData.Pm, heldTask, TaskStatuses.OnHold, new { reason = "Awaiting the weekly survey" });
        return new(p, due, upcoming, complete, held, civil, electrical);
    }

    static JsonNode ContextFor(JsonNode view, string kind, Guid id)
    {
        var entry = view["contexts"]!.AsArray().SingleOrDefault(x => x!["kind"]!.GetValue<string>() == kind && x["id"]!.GetValue<Guid>() == id);
        Assert.True(entry is not null, $"Missing {kind}:{id}. Contexts: {view["contexts"]!.ToJsonString()}");
        return entry!["context"]!;
    }

    [Fact]
    public async Task Flag_off_keeps_old_weekly_shape_and_flag_on_indexes_returned_task_contexts()
    {
        var s = await New(); await Flag(false);
        var off = await Get(TestData.Pm, $"/api/v1/projects/{s.Project.Id}/coordination");
        Assert.Null(off["contexts"]); Assert.Null(off["readinessInputs"]);
        var oldCounts = (off["dueThisWeek"]!.AsArray().Count, off["upcoming"]!["tasks"]!.AsArray().Count, off["held"]!["tasks"]!.AsArray().Count);
        await Flag(true);
        var on = await Get(TestData.Pm, $"/api/v1/projects/{s.Project.Id}/coordination");
        Assert.NotNull(on["readinessInputs"]);
        var legacy = on.DeepClone().AsObject(); legacy.Remove("contexts"); legacy.Remove("readinessInputs");
        Assert.Equal(off.ToJsonString(), legacy.ToJsonString());
        var due = ContextFor(on, RecordContextKind.Task, s.Due);
        Assert.Equal(d.User(TestData.Alex), due["owner"]!["id"]!.GetValue<Guid>());
        Assert.Equal(RecordContextAction.Start, due["nextAction"]!.GetValue<string>());
        Assert.Equal(oldCounts, (on["dueThisWeek"]!.AsArray().Count, on["upcoming"]!["tasks"]!.AsArray().Count, on["held"]!["tasks"]!.AsArray().Count));
        Assert.Equal(RecordContextAction.None, ContextFor(on, RecordContextKind.Task, s.Complete)["nextAction"]!.GetValue<string>());
        Assert.True(ContextFor(on, RecordContextKind.Task, s.Complete)["terminal"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Context_index_deduplicates_a_task_returned_by_due_and_top_sections()
    {
        var s = await New(); await Flag(true);
        var view = await Get(TestData.Pm, $"/api/v1/projects/{s.Project.Id}/coordination");
        var ids = view["contexts"]!.AsArray().Where(x => x!["kind"]!.GetValue<string>() == RecordContextKind.Task).Select(x => x!["id"]!.GetValue<Guid>()).ToArray();
        Assert.Equal(ids.Length, ids.Distinct().Count());
        Assert.Contains(s.Due, ids);
        Assert.Contains(s.Upcoming, ids);
    }

    [Fact]
    public async Task Discipline_scope_only_returns_contexts_for_the_scoped_rows()
    {
        var s = await New();
        var electricalTask = (await d.NewTask(s.Project.Id, TestData.Pm, new { name = "Electrical agenda task", assigneeId = d.User(TestData.Omar), dueDate = "2026-09-16" }, "Electrical")).G("id");
        await Flag(true);
        var view = await Get(TestData.Omar, $"/api/v1/projects/{s.Project.Id}/coordination?disciplineId={s.Electrical}");
        var ids = view["contexts"]!.AsArray().Where(x => x!["kind"]!.GetValue<string>() == RecordContextKind.Task).Select(x => x!["id"]!.GetValue<Guid>()).ToArray();
        Assert.Contains(electricalTask, ids);
        Assert.DoesNotContain(s.Due, ids);
    }

    [Fact]
    public async Task Returned_agenda_types_use_canonical_context_and_completed_decision_keeps_required_date()
    {
        var s = await New(); var root = $"/api/v1/projects/{s.Project.Id}"; var alex = d.User(TestData.Alex);
        var milestone = (await Create(root + "/milestones", new { name = "Agenda milestone", milestoneType = "Design Submission", date = "2026-09-18" })).G("id");
        var deliverable = (await Create(root + "/deliverables", new { name = "Agenda deliverable", projectDisciplineId = s.Civil,
            deliverableTypeId = await d.DeliverableType(), ownerId = alex, dueDate = "2026-09-16", requiresReview = false })).G("id");
        var decision = (await Create(root + "/decisions", new { subject = "Agenda decision", description = "Confirm survey scope", ownerUserId = alex,
            requiredByDate = "2026-09-15", impactLevel = "Medium", impactDescription = "Survey basis" })).G("id");
        var issue = (await Create(root + "/issues", new { title = "Agenda issue", ownerId = alex, raisedById = d.User(TestData.Pm), severity = "High",
            dateRaised = "2026-09-14", targetResolutionDate = "2026-09-18", projectDisciplineId = s.Civil })).G("id");
        var risk = (await Create(root + "/risks", new { title = "Agenda risk", ownerId = alex, probability = 3, impact = 3,
            reviewDate = "2026-09-19", projectDisciplineId = s.Civil })).G("id");
        var party = (await Create(root + "/external-parties", new { name = "Synthetic survey client", isClient = true })).G("id");
        var meeting = (await Create(root + "/meetings", new { title = "Agenda meeting", meetingDate = "2026-09-14" })).G("id");
        var action = (await Create($"/api/v1/meetings/{meeting}/actions", new { text = "Client survey action", ownerType = ActionOwnerType.ExternalParty,
            ownerExternalPartyId = party, dueDate = "2026-09-17" })).G("id");
        await Flag(true);
        await f.Evaluate(s.Project.Id);
        var view = await Get(TestData.Pm, root + "/coordination");
        foreach (var (kind, id) in new[] { (RecordContextKind.Milestone, milestone), (RecordContextKind.Deliverable, deliverable),
            (RecordContextKind.Decision, decision), (RecordContextKind.Issue, issue), (RecordContextKind.Risk, risk), (RecordContextKind.Action, action) })
            Assert.NotNull(ContextFor(view, kind, id));
        Assert.Equal("2026-09-18", ContextFor(view, RecordContextKind.Milestone, milestone).S("neededBy"));
        Assert.Equal(d.User(TestData.Pm), ContextFor(view, RecordContextKind.Milestone, milestone)["nextActors"]![0]!.G("id"));
        Assert.Equal(alex, ContextFor(view, RecordContextKind.Deliverable, deliverable)["owner"]!.G("id"));
        Assert.Equal("2026-09-19", ContextFor(view, RecordContextKind.Risk, risk).S("neededBy"));
        Assert.Equal(party, ContextFor(view, RecordContextKind.Action, action)["owner"]!.G("id"));
        Assert.Equal("ExternalParty", ContextFor(view, RecordContextKind.Action, action)["owner"]!.S("kind"));
        Assert.Equal(RecordContextAction.Decide, ContextFor(view, RecordContextKind.Decision, decision).S("nextAction"));
        await (await f.As(TestData.Alex).Post($"/api/v1/decisions/{decision}/transition", new DecisionEndpoints.TransitionBody(
            DecisionStatus.Decided, DecisionText: "Accept the survey basis", DecisionDate: new DateOnly(2026, 9, 14),
            RowVersion: f.Db(db => db.Decisions.Single(x => x.Id == decision).RowVersion)))).Json();
        view = await Get(TestData.Pm, root + "/coordination");
        Assert.Contains(view["completed"]!["decisions"]!.AsArray(), row => row!.G("id") == decision);
        var completed = ContextFor(view, RecordContextKind.Decision, decision);
        Assert.True(completed["terminal"]!.GetValue<bool>()); Assert.Empty(completed["nextActors"]!.AsArray());
        Assert.Equal("2026-09-15", completed.S("neededBy")); Assert.Equal(RecordContextAction.None, completed.S("nextAction"));
    }

    [Fact]
    public async Task Restricted_project_does_not_leak_agenda_or_contexts_to_nonmembers()
    {
        var s = await New(); await Flag(true);
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(x => x.Id == s.Project.Id)).Visibility = Visibility.Restricted; await db.SaveChangesAsync(); return 0; });
        await Get(TestData.Rita, $"/api/v1/projects/{s.Project.Id}/coordination", 404);
        await Get(TestData.Rita, $"/api/v1/projects/{s.Project.Id}/dashboard", 404);
    }
}
