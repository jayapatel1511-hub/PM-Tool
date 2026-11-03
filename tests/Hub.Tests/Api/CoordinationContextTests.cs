using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 052/053 API coverage for handoff and readiness-constraint operational context.
public sealed class CoordinationContextTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData d;
    public CoordinationContextTests() => d = new(f);
    public void Dispose() => f.Dispose();

    async Task Enable() => (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.screen_patterns", new { value = true })).EnsureSuccessStatusCode();

    async Task<(Project P, Guid Handoff)> NewHandoff()
    {
        var p = await d.Project();
        var source = await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/deliverables", new
        {
            name = "Context source", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"),
            deliverableTypeId = await d.DeliverableType(), ownerId = d.User(TestData.Alex), revision = "A",
            transmittalUrl = "https://example.test/context.pdf"
        }).Result.Json(201);
        var target = await d.NewTask(p.Id, TestData.Omar, new { assigneeId = d.User(TestData.Omar), dueDate = "2026-09-18" }, "Electrical");
        var version = f.Db(db => db.Deliverables.Single(x => x.Id == source.G("id")).RowVersion);
        var body = new HandoffEndpoints.DraftBody(Guid.NewGuid(), "Context handoff", source.G("id"), version, "A", "https://example.test/context.pdf",
            d.ProjectDiscipline(p.Id, "Electrical"), d.User(TestData.Alex), d.User(TestData.Omar), target.G("id"), null,
            "Use the survey", "Criteria", new DateOnly(2026, 9, 20), new DateOnly(2026, 9, 22), null, null);
        var handoff = await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/handoffs", body).Result.Json(201);
        return (p, handoff.G("id"));
    }

    async Task<JsonNode> Handoff(Project p, Guid id, string who = TestData.Alex) =>
        await f.As(who).GetAsync($"/api/v1/projects/{p.Id}/handoffs/{id}").Result.Json();

    async Task<Guid> Constraint(Project p, string state = ConstraintState.Open, Guid? removal = null, Guid? affected = null)
    {
        var id = Guid.NewGuid();
        var target = await d.NewTask(p.Id, TestData.Alex, new { assigneeId = d.User(TestData.Alex) });
        await f.DbAsync(async db =>
        {
            var seq = await db.WorkConstraints.Where(x => x.ProjectId == p.Id).Select(x => (int?)x.Seq).MaxAsync() ?? 0;
            db.WorkConstraints.Add(new WorkConstraint { Id = id, ProjectId = p.Id, Seq = seq + 1, Key = $"{p.ProjectNumber}-CT{id.ToString("N")[..4]}",
                TargetType = "Task", TargetId = target.G("id"), Category = "Handoff", Description = "Context constraint",
                RemovalOwnerId = removal ?? d.User(TestData.Alex), AffectedOwnerId = affected ?? d.User(TestData.Marc),
                NeededBy = new DateOnly(2026, 9, 24), SourceUrl = "https://example.test/constraint", State = state });
            await db.SaveChangesAsync(); return 0;
        });
        return id;
    }

    async Task<JsonNode> ConstraintDetail(Project p, Guid id, string who = TestData.Alex) =>
        await f.As(who).GetAsync($"/api/v1/projects/{p.Id}/readiness/constraints/{id}").Result.Json();

    [Fact]
    public async Task Handoff_and_constraint_context_are_omitted_off_then_present_on_with_permissions_unchanged()
    {
        var (p, handoff) = await NewHandoff();
        var constraint = await Constraint(p);
        var rawHandoff = await Handoff(p, handoff);
        var rawConstraint = await ConstraintDetail(p, constraint);
        Assert.Null(rawHandoff["context"]); Assert.Null(rawConstraint["context"]);
        await Enable();
        var onHandoff = await Handoff(p, handoff);
        var onConstraint = await ConstraintDetail(p, constraint);
        Assert.NotNull(onHandoff["context"]); Assert.NotNull(onConstraint["context"]);
        Assert.Equal(rawHandoff["permissions"]!.ToJsonString(), onHandoff["permissions"]!.ToJsonString());
        foreach (var key in new[] { "id", "key", "targetType", "targetId" })
            Assert.Equal(rawConstraint[key]!.ToJsonString(), onConstraint[key]!.ToJsonString());
    }

    [Fact]
    public async Task Handoff_context_covers_sender_receiver_response_and_terminal_states()
    {
        await Enable();
        var setup = await NewHandoff();
        var sender = d.User(TestData.Alex); var receiver = d.User(TestData.Omar);
        foreach (var (status, actor, next) in new[]
        {
            (HandoffStatus.Draft, sender, RecordContextAction.Submit),
            (HandoffStatus.Submitted, receiver, RecordContextAction.Accept),
            (HandoffStatus.Accepted, receiver, RecordContextAction.Incorporate),
            (HandoffStatus.ClarificationRequested, sender, RecordContextAction.Respond),
            (HandoffStatus.Returned, sender, RecordContextAction.Respond),
            (HandoffStatus.Incorporated, (Guid?)null, RecordContextAction.None),
            (HandoffStatus.Cancelled, (Guid?)null, RecordContextAction.None),
        })
        {
            await f.DbAsync(async db => { (await db.Handoffs.SingleAsync(x => x.Id == setup.Handoff)).Status = status; await db.SaveChangesAsync(); return 0; });
            var context = (await Handoff(setup.P, setup.Handoff))["context"]!;
            Assert.Equal(next, context.S("nextAction"));
            Assert.Equal("2026-09-20", context.S("neededBy"));
            Assert.Equal(sender, context["owner"]!.G("id"));
            if (actor is { } expectedActor) Assert.Equal(expectedActor, context["nextActors"]![0]!.G("id"));
            else Assert.Empty(context["nextActors"]!.AsArray());
            Assert.Equal(status is HandoffStatus.Incorporated or HandoffStatus.Cancelled, context["terminal"]!.GetValue<bool>());
        }
    }

    [Fact]
    public async Task Handoff_context_marks_inactive_named_actor_without_changing_owner_fact()
    {
        await Enable();
        var setup = await NewHandoff();
        var sender = d.User(TestData.Alex);
        await f.DbAsync(async db =>
        {
            (await db.Users.SingleAsync(x => x.Id == sender)).IsActive = false;
            var h = await db.Handoffs.SingleAsync(x => x.Id == setup.Handoff); h.Status = HandoffStatus.Draft;
            await db.SaveChangesAsync(); return 0;
        });
        var inactive = (await Handoff(setup.P, setup.Handoff, TestData.Pm))["context"]!;
        Assert.Equal(sender, inactive["owner"]!.G("id"));
        Assert.Equal("Inactive", inactive["nextActors"]![0]!.S("state"));


    }

    [Fact]
    public async Task Constraint_context_uses_removal_or_affected_owner_and_exact_needed_date()
    {
        await Enable();
        var p = await d.Project();
        var removal = await Constraint(p, ConstraintState.Open);
        var proposed = await Constraint(p, ConstraintState.ResolutionProposed);
        var removed = await Constraint(p, ConstraintState.VerifiedRemoved);
        var cancelled = await Constraint(p, ConstraintState.Cancelled);
        var open = (await ConstraintDetail(p, removal))["context"]!;
        var resolution = (await ConstraintDetail(p, proposed))["context"]!;
        Assert.Equal(d.User(TestData.Alex), open["nextActors"]![0]!.G("id"));
        Assert.Equal(RecordContextAction.Remove, open.S("nextAction"));
        Assert.Equal(d.User(TestData.Marc), resolution["nextActors"]![0]!.G("id"));
        Assert.Equal(d.User(TestData.Alex), resolution["owner"]!.G("id"));
        Assert.Equal(RecordContextAction.VerifyRemoval, resolution.S("nextAction"));
        Assert.Equal("2026-09-24", resolution.S("neededBy"));
        foreach (var id in new[] { removed, cancelled })
        {
            var terminal = (await ConstraintDetail(p, id))["context"]!;
            Assert.True(terminal["terminal"]!.GetValue<bool>()); Assert.Equal(RecordContextAction.None, terminal.S("nextAction")); Assert.Empty(terminal["nextActors"]!.AsArray());
            Assert.Equal(d.User(TestData.Alex), terminal["owner"]!.G("id")); Assert.Equal("2026-09-24", terminal.S("neededBy"));
        }
    }

    [Fact]
    public async Task Constraint_context_marks_inactive_removal_and_affected_actors()
    {
        await Enable();
        var p = await d.Project();
        var id = await Constraint(p, ConstraintState.ResolutionProposed);
        await f.DbAsync(async db => { (await db.Users.SingleAsync(x => x.Id == d.User(TestData.Marc))).IsActive = false; await db.SaveChangesAsync(); return 0; });
        var context = (await ConstraintDetail(p, id))["context"]!;
        Assert.Equal(d.User(TestData.Marc), context["nextActors"]![0]!.G("id"));
        Assert.Equal("Inactive", context["nextActors"]![0]!.S("state"));
        Assert.Equal("Inactive", context.S("actorState"));
    }

    [Fact]
    public async Task Coordination_context_respects_restricted_deleted_and_cross_project_privacy()
    {
        await Enable();
        var setup = await NewHandoff();
        var constraint = await Constraint(setup.P);
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(x => x.Id == setup.P.Id)).Visibility = Visibility.Restricted; await db.SaveChangesAsync(); return 0; });
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Rita).GetAsync($"/api/v1/projects/{setup.P.Id}/handoffs/{setup.Handoff}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Rita).GetAsync($"/api/v1/projects/{setup.P.Id}/readiness/constraints/{constraint}")).StatusCode);
        var other = await d.Project();
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{other.Id}/readiness/constraints/{constraint}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{other.Id}/handoffs/{setup.Handoff}")).StatusCode);
        await f.DbAsync(async db => { db.WorkConstraints.Remove(await db.WorkConstraints.SingleAsync(x => x.Id == constraint)); await db.SaveChangesAsync(); return 0; });
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{setup.P.Id}/readiness/constraints/{constraint}")).StatusCode);
        await f.DbAsync(async db => { (await db.Handoffs.SingleAsync(x => x.Id == setup.Handoff)).DeletedAt = f.Clock.Now; await db.SaveChangesAsync(); return 0; });
        Assert.Equal(HttpStatusCode.NotFound, (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{setup.P.Id}/handoffs/{setup.Handoff}")).StatusCode);
    }
}
