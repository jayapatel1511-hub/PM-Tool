using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

// Actual provider-backed command witnesses for role-explanation mappings; no complete matrix claim.
public sealed class RoleCommandWitnessTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData data;
    static readonly DateOnly Day = new(2026, 11, 2);
    public RoleCommandWitnessTests() { data = new(f); f.Clock.SetDate(2026, 11, 2); }
    public void Dispose() => f.Dispose();
    int Version<T>(Guid id) where T : Audited => f.Db(db => db.Set<T>().AsNoTracking().Single(row => row.Id == id).RowVersion);

    string State(Guid project) => f.Db(db => JsonSerializer.Serialize(new {
        Project = db.Projects.AsNoTracking().Single(row => row.Id == project),
        Meetings = db.Meetings.IgnoreQueryFilters().AsNoTracking().Where(row => row.ProjectId == project).OrderBy(row => row.Id).ToArray(),
        Actions = db.Actions.AsNoTracking().Where(row => row.ProjectId == project).OrderBy(row => row.Id).ToArray(),
        Deliverables = db.Deliverables.IgnoreQueryFilters().AsNoTracking().Where(row => row.ProjectId == project).OrderBy(row => row.Id).ToArray(),
        Issues = db.DeliverableIssues.AsNoTracking().Where(row => row.ProjectId == project).OrderBy(row => row.Id).ToArray(),
        Comments = db.Comments.AsNoTracking().Where(row => row.ProjectId == project).OrderBy(row => row.Id).ToArray(),
        Activity = db.ActivityLog.AsNoTracking().OrderBy(row => row.Id).ToArray(),
        Commands = db.CoordinationCommands.AsNoTracking().OrderBy(row => row.Id).ToArray(),
        Receipts = db.Idempotency.AsNoTracking().OrderBy(row => row.Id).ToArray(),
        Outbox = db.Outbox.AsNoTracking().OrderBy(row => row.Id).ToArray(),
        Notices = db.Notifications.AsNoTracking().OrderBy(row => row.Id).ToArray(),
        Emails = db.Emails.AsNoTracking().OrderBy(row => row.Id).ToArray(),
    }, JsonOpts.Web));

    [Fact]
    public async Task Civil_only_project_lead_can_create_edit_and_join_an_electrical_context_meeting_while_team_member_is_refused()
    {
        var project = await data.Project(); var root = $"/api/v1/projects/{project.Id}";
        var civil = data.ProjectDiscipline(project.Id, "Civil"); var electrical = data.ProjectDiscipline(project.Id, "Electrical");
        var marc = data.User(TestData.Marc); var omar = data.User(TestData.Omar);
        var leadView = await (await f.As(TestData.Marc).GetAsync(root)).Json();
        Assert.False(leadView["permissions"]!["isPm"]!.GetValue<bool>());
        var leads = leadView["permissions"]!["leadOf"]!.AsArray().Select(value => value!.GetValue<Guid>()).ToArray();
        Assert.Contains(civil, leads); Assert.DoesNotContain(electrical, leads);
        var leadMe = await (await f.As(TestData.Marc).GetAsync("/api/v1/me")).Json();
        Assert.DoesNotContain(SystemRole.Admin, leadMe["systemRoles"]!.AsArray().Select(value => value!.GetValue<string>()));
        var memberView = await (await f.As(TestData.Alex).GetAsync(root)).Json();
        Assert.False(memberView["permissions"]!["isPm"]!.GetValue<bool>()); Assert.Empty(memberView["permissions"]!["leadOf"]!.AsArray());

        // Meeting has no discipline field. The actual Electrical lead creates it, and an actual discipline-owned
        // action supplies its Electrical context; do not invent a meeting scope field or infer scope from its title.
        var electricalMeeting = await (await f.As(TestData.Omar).Post(root + "/meetings", new {
            title = "Synthetic Electrical coordination", meetingDate = Day, meetingType = "Coordination",
            notesLink = "https://example.test/electrical-minutes", calendarEventId = (Guid?)null,
        })).Json(201);
        var id = electricalMeeting.G("id");
        var action = await (await f.As(TestData.Omar).Post($"/api/v1/meetings/{id}/actions", new {
            text = "Synthetic Electrical interface action", ownerType = "Discipline", ownerDisciplineId = electrical, dueDate = Day.AddDays(2),
        })).Json(201);
        var actionId = action.G("id");
        Assert.Equal(electrical, f.Db(db => db.Actions.Single(row => row.Id == actionId).OwnerDisciplineId));
        Assert.Equal(omar, f.Db(db => db.Meetings.Single(row => row.Id == id).CreatedBy));

        var createBody = new { title = "Synthetic cross-discipline design review", meetingDate = Day.AddDays(1), meetingType = "Design Review",
            notesLink = "https://example.test/design-review", calendarEventId = (Guid?)null };
        var created = await (await f.As(TestData.Marc).Post(root + "/meetings", createBody)).Json(201);
        var createdId = created.G("id");
        var storedCreated = f.Db(db => db.Meetings.AsNoTracking().Single(row => row.Id == createdId));
        Assert.Equal(project.Id, storedCreated.ProjectId); Assert.Equal(createBody.title, storedCreated.Title);
        Assert.Equal(marc, storedCreated.CreatedBy); Assert.Equal(Day.AddDays(1), storedCreated.MeetingDate);

        var editBody = new { title = "Synthetic Electrical interface review updated by Civil lead", meetingDate = Day,
            meetingType = "Coordination", notesLink = "https://example.test/revised-electrical-minutes", calendarEventId = (Guid?)null };
        var oldVersion = Version<Meeting>(id);
        await (await f.As(TestData.Marc).Patch($"/api/v1/meetings/{id}", editBody, oldVersion)).Json();
        var edited = f.Db(db => db.Meetings.AsNoTracking().Single(row => row.Id == id));
        Assert.Equal(editBody.title, edited.Title); Assert.Equal(editBody.notesLink, edited.NotesLink);
        Assert.Equal(omar, edited.CreatedBy); Assert.Equal(marc, edited.UpdatedBy); Assert.Equal(oldVersion + 1, edited.RowVersion);
        var beforeJoin = State(project.Id);
        var current = await (await f.As(TestData.Marc).Post(root + "/meetings/current", new { })).Json();
        Assert.Equal(id, current.G("id")); Assert.Equal(beforeJoin, State(project.Id));
        Assert.Equal(electrical, f.Db(db => db.Actions.Single(row => row.Id == actionId).OwnerDisciplineId));

        var before = State(project.Id);
        await (await f.As(TestData.Alex).Post(root + "/meetings", createBody)).Json(403); Assert.Equal(before, State(project.Id));
        await (await f.As(TestData.Alex).Patch($"/api/v1/meetings/{id}", editBody, Version<Meeting>(id))).Json(403); Assert.Equal(before, State(project.Id));
        await (await f.As(TestData.Alex).Post(root + "/meetings/current", new { })).Json(403); Assert.Equal(before, State(project.Id));
    }

    [Fact]
    public async Task Actual_deliverable_hold_retains_reason_and_previous_status_and_only_resumes_that_status_without_refusal_effects()
    {
        var project = await data.Project();
        var created = await (await f.As(TestData.Marc).Post($"/api/v1/projects/{project.Id}/deliverables", new {
            name = "Synthetic retained-state drawing", projectDisciplineId = data.ProjectDiscipline(project.Id, "Civil"),
            deliverableTypeId = await data.DeliverableType(), ownerId = data.User(TestData.Alex), requiresReview = false,
        })).Json(201);
        var id = created.G("id"); var path = $"/api/v1/deliverables/{id}/transition";
        await (await f.As(TestData.Marc).Post(path, new DeliverableEndpoints.TransitionBody(
            DeliverableStatus.InProgress, null, null, Version<Deliverable>(id)))).Json();
        var started = f.Db(db => db.Deliverables.AsNoTracking().Single(row => row.Id == id));
        Assert.Equal(DeliverableStatus.InProgress, started.Status); Assert.NotNull(started.StartedAt);

        var before = State(project.Id);
        var missingReason = await (await f.As(TestData.Marc).Post(path, new DeliverableEndpoints.TransitionBody(
            DeliverableStatus.OnHold, "  ", null, Version<Deliverable>(id)))).Json(400);
        Assert.NotNull(missingReason["errors"]?["reason"]); Assert.Equal(before, State(project.Id));
        const string reason = "Await the recorded utility clearance before resuming this drawing";
        await (await f.As(TestData.Marc).Post(path, new DeliverableEndpoints.TransitionBody(
            DeliverableStatus.OnHold, reason, null, Version<Deliverable>(id)))).Json();
        var held = f.Db(db => db.Deliverables.AsNoTracking().Single(row => row.Id == id));
        Assert.Equal(DeliverableStatus.OnHold, held.Status); Assert.Equal(reason, held.OnHoldReason);
        Assert.Equal(started.Status, held.PreviousStatus); Assert.Equal(started.StartedAt, held.StartedAt);
        Assert.Equal(started.RowVersion + 1, held.RowVersion); Assert.Equal(data.User(TestData.Marc), held.UpdatedBy);
        var detail = await (await f.As(TestData.Marc).GetAsync($"/api/v1/deliverables/{id}")).Json();
        Assert.Equal(reason, detail.S("onHoldReason")); Assert.Equal(started.Status, detail.S("previousStatus"));

        before = State(project.Id);
        // ReadyToIssue is ordinarily reachable from InProgress for this no-review deliverable, but a held record
        // cannot skip its retained state. This is an actual command refusal, not merely a disabled UI option.
        var refused = await (await f.As(TestData.Marc).Post(path, new DeliverableEndpoints.TransitionBody(
            DeliverableStatus.ReadyToIssue, null, null, Version<Deliverable>(id)))).Json(422);
        Assert.Equal("illegal_transition", refused.S("code")); Assert.Equal(before, State(project.Id));
        await (await f.As(TestData.Marc).Post(path, new DeliverableEndpoints.TransitionBody(
            held.PreviousStatus!, null, null, Version<Deliverable>(id)))).Json();
        var resumed = f.Db(db => db.Deliverables.AsNoTracking().Single(row => row.Id == id));
        Assert.Equal(held.PreviousStatus, resumed.Status); Assert.Equal(DeliverableStatus.InProgress, resumed.Status);
        Assert.Null(resumed.OnHoldReason); Assert.Equal(started.StartedAt, resumed.StartedAt);
        Assert.Equal(held.RowVersion + 1, resumed.RowVersion); Assert.Equal(data.User(TestData.Marc), resumed.UpdatedBy);
    }
}
