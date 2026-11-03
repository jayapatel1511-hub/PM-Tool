using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 052/053 coverage for context attached to the existing weekly
/// coordination projection.  The handoffs are created through the real API;
/// no synthetic row or guessed workflow state is used here.
public sealed class WeeklyCoordinationContextTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData d;
    public WeeklyCoordinationContextTests() => d = new(f);
    public void Dispose() => f.Dispose();

    async Task<JsonNode> Get(string who, string path, int status = 200) => await (await f.As(who).GetAsync(path)).Json(status);
    async Task<JsonNode> Post(string who, string path, object body, int status = 200) => await (await f.As(who).Post(path, body)).Json(status);
    async Task Flag(bool on) => (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.screen_patterns", new { value = on })).EnsureSuccessStatusCode();

    sealed record Setup(Project Project, Guid Submitted, Guid Draft, Guid Electrical, Guid Alex, Guid Omar, Guid TargetTask, Guid SourceRevision);

    async Task<Setup> New()
    {
        var p = await d.Project();
        var civil = d.ProjectDiscipline(p.Id, "Civil");
        var electrical = d.ProjectDiscipline(p.Id, "Electrical");
        var source = await (await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/deliverables", new
        {
            name = "Survey basis", projectDisciplineId = civil, deliverableTypeId = await d.DeliverableType(),
            ownerId = d.User(TestData.Alex), revision = "A", requiresReview = false
        })).Json(201);
        var target = await d.NewTask(p.Id, TestData.Omar, new { name = "Service alignment", assigneeId = d.User(TestData.Omar), dueDate = "2026-09-16" }, "Electrical");
        var version = f.Db(db => db.Deliverables.Single(x => x.Id == source.G("id")).RowVersion);
        HandoffEndpoints.DraftBody Body(string title) => new(Guid.NewGuid(), title, source.G("id"), version, "A", "https://example.test/survey-A.pdf",
            electrical, d.User(TestData.Alex), d.User(TestData.Omar), target.G("id"), null, "Set the service alignment", "Survey covers the corridor",
            new DateOnly(2026, 9, 16), new DateOnly(2026, 9, 18), null, null);
        var root = $"/api/v1/projects/{p.Id}/handoffs";
        var submitted = (await Post(TestData.Marc, root, Body("Submitted survey"), 201)).G("id");
        var rowVersion = f.Db(db => db.Handoffs.Single(x => x.Id == submitted).RowVersion);
        await Post(TestData.Alex, root + $"/{submitted}/transition", new HandoffEndpoints.MoveBody(Guid.NewGuid(), HandoffStatus.Submitted, rowVersion, "Send to electrical", null));
        var draft = (await Post(TestData.Marc, root, Body("Draft survey"), 201)).G("id");
        var sourceRevision = f.Db(db => db.HandoffRevisions.Where(x => x.HandoffId == submitted).Select(x => x.SourceRevisionId).Single());
        return new(p, submitted, draft, electrical, d.User(TestData.Alex), d.User(TestData.Omar), target.G("id"), sourceRevision);
    }

    static JsonNode Row(JsonNode projection, Guid id) => projection["handoffs"]!.AsArray().Single(x => x!["id"]!.GetValue<Guid>() == id)!;

    [Fact]
    public async Task Flag_off_preserves_projection_shape_and_flag_on_adds_handoff_context()
    {
        var s = await New();
        await Flag(false);
        var off = await Get(TestData.Omar, $"/api/v1/projects/{s.Project.Id}/discipline-coordination");
        Assert.False(Row(off, s.Submitted).AsObject().ContainsKey("context"));
        var offCounts = (off["handoffsTotal"]!.GetValue<int>(), off["incomingTotal"]!.GetValue<int>(), off["outgoingTotal"]!.GetValue<int>(), off["page"]!.GetValue<int>(), off["pageSize"]!.GetValue<int>());
        await Flag(true);
        var on = await Get(TestData.Omar, $"/api/v1/projects/{s.Project.Id}/discipline-coordination");
        var incoming = Row(on, s.Submitted);
        Assert.NotNull(incoming["context"]);
        Assert.Equal(HandoffStatus.Submitted, incoming["status"]!.GetValue<string>());
        Assert.Equal(s.Alex, incoming["context"]!["owner"]!["id"]!.GetValue<Guid>());
        Assert.Equal(s.Omar, incoming["context"]!["nextActors"]![0]!["id"]!.GetValue<Guid>());
        Assert.Equal(RecordContextAction.Accept, incoming["context"]!["nextAction"]!.GetValue<string>());
        Assert.Equal(offCounts, (on["handoffsTotal"]!.GetValue<int>(), on["incomingTotal"]!.GetValue<int>(), on["outgoingTotal"]!.GetValue<int>(), on["page"]!.GetValue<int>(), on["pageSize"]!.GetValue<int>()));
    }

    [Fact]
    public async Task Draft_handoff_context_names_sending_owner_and_submission_action()
    {
        var s = await New(); await Flag(true);
        var view = await Get(TestData.Alex, $"/api/v1/projects/{s.Project.Id}/discipline-coordination");
        var draft = Row(view, s.Draft);
        Assert.Equal(HandoffStatus.Draft, draft["status"]!.GetValue<string>());
        Assert.Equal(s.Alex, draft["context"]!["owner"]!["id"]!.GetValue<Guid>());
        Assert.Equal(RecordContextAction.Submit, draft["context"]!["nextAction"]!.GetValue<string>());
        Assert.Equal("2026-09-16", draft["context"]!["neededBy"]!.GetValue<string>());
    }

    [Fact]
    public async Task Workspace_and_project_reads_expose_context_only_for_visible_projects()
    {
        var s = await New(); await Flag(true);
        var project = await Get(TestData.Alex, $"/api/v1/projects/{s.Project.Id}/discipline-coordination");
        var workspace = await Get(TestData.Alex, $"/api/v1/discipline-coordination?projectId={s.Project.Id}");
        var workspaceProject = workspace["projects"]!.AsArray().Single(x => x!["id"]!.GetValue<Guid>() == s.Project.Id);
        Assert.NotNull(workspaceProject["data"]!["handoffs"]!.AsArray().Single(x => x!["id"]!.GetValue<Guid>() == s.Submitted)!["context"]);
        Assert.Equal(project["handoffs"]!.AsArray().Count, workspaceProject["data"]!["handoffs"]!.AsArray().Count);
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == s.Project.Id)).Visibility = Visibility.Restricted; await db.SaveChangesAsync(); return 0; });
        await Get(TestData.Rita, $"/api/v1/projects/{s.Project.Id}/discipline-coordination", 404);
        await Get(TestData.Rita, $"/api/v1/discipline-coordination?projectId={s.Project.Id}", 404);
    }

    [Fact]
    public async Task Paging_keeps_totals_and_context_on_rows()
    {
        var s = await New(); await Flag(true);
        var all = await Get(TestData.Omar, $"/api/v1/projects/{s.Project.Id}/discipline-coordination");
        var page = await Get(TestData.Omar, $"/api/v1/projects/{s.Project.Id}/discipline-coordination?page=1&pageSize=1");
        Assert.Equal(all["handoffsTotal"]!.GetValue<int>(), page["handoffsTotal"]!.GetValue<int>());
        Assert.Equal(1, page["handoffs"]!.AsArray().Count);
        Assert.NotNull(page["handoffs"]![0]!["context"]);
    }

    [Fact]
    public async Task Adopted_input_use_has_named_owner_and_honest_unknown_next_step()
    {
        var s = await New(); await Flag(true);
        var root = $"/api/v1/projects/{s.Project.Id}";
        await Post(TestData.Omar, root + "/input-uses", new ChangeEndpoints.AdoptBody(Guid.NewGuid(), "Task", s.TargetTask,
            f.Db(db => db.Tasks.Single(x => x.Id == s.TargetTask).RowVersion), s.SourceRevision, s.SourceRevision, null,
            "Coordinate the service alignment", "Included in the weekly coordination set"));
        var view = await Get(TestData.Omar, root + "/discipline-coordination");
        var use = view["uses"]!.AsArray().Single(x => x!["targetId"]!.GetValue<Guid>() == s.TargetTask);
        Assert.NotNull(use["context"]);
        Assert.Equal(s.Omar, use["context"]!["owner"]!["id"]!.GetValue<Guid>());
        Assert.Null(use["context"]!["neededBy"]);
        Assert.Equal(RecordContextAction.NotAssessed, use["context"]!["nextAction"]!.GetValue<string>());
        Assert.Equal(s.TargetTask, use["context"]!["inputs"]![0]!["id"]!.GetValue<Guid>());
    }

    [Fact]
    public async Task Foreign_target_input_use_is_unavailable_without_leaking_foreign_identity()
    {
        var s = await New(); await Flag(true);
        var foreign = await d.Project();
        var foreignTask = (await d.NewTask(foreign.Id, TestData.Omar, new { name = "Private foreign task", assigneeId = s.Omar }, "Electrical")).G("id");
        await f.DbAsync(async db =>
        {
            db.InputUses.Add(new InputUse { ProjectId = s.Project.Id, TargetType = "Task", TargetId = foreignTask, OwnerId = s.Omar,
                SourceIdentity = "survey", SourceRevisionId = s.SourceRevision, IntendedUse = "Foreign scope fixture", AdoptedAt = f.Clock.Now, AdoptedBy = s.Omar });
            await db.SaveChangesAsync(); return 0;
        });
        var view = await Get(TestData.Omar, $"/api/v1/projects/{s.Project.Id}/discipline-coordination");
        var use = view["uses"]!.AsArray().Single(x => x!["targetId"]!.GetValue<Guid>() == foreignTask);
        Assert.Null(use["targetKey"]); Assert.Null(use["targetName"]);
        Assert.NotNull(use["sourceUrl"]); // Its own-project source remains available.
        Assert.Empty(use["context"]!["inputs"]!.AsArray());
        Assert.Equal(1, use["context"]!["unavailableInputCount"]!.GetValue<int>());
        Assert.DoesNotContain("Private foreign task", use.ToJsonString());
        Assert.True(use["context"] is null || use["context"]!["nextAction"]!.GetValue<string>() == RecordContextAction.NotAssessed);
    }

    [Fact]
    public async Task Foreign_source_details_are_redacted_while_local_target_remains_available()
    {
        var s = await New(); await Flag(true); var foreign = await d.Project();
        var civil = d.ProjectDiscipline(foreign.Id, "Civil");
        var deliverable = (await Post(TestData.Marc, $"/api/v1/projects/{foreign.Id}/deliverables", new {
            name = "Foreign source title", projectDisciplineId = civil, deliverableTypeId = await d.DeliverableType(),
            ownerId = s.Alex, revision = "A", requiresReview = false }, 201)).G("id");
        var revision = (await Post(TestData.Alex, $"/api/v1/projects/{foreign.Id}/source-revisions", new ChangeEndpoints.RegisterBody(
            Guid.NewGuid(), deliverable, f.Db(db => db.Deliverables.Single(x => x.Id == deliverable).RowVersion), civil,
            s.Alex, "Deliverable", "foreign-source", "Foreign source title", "A", "https://example.test/foreign-private.pdf",
            "Survey", "Scope", null, null, null, null, null, null))).G("id");
        // A damaged imported link has valid FKs but references another project's source.
        await f.DbAsync(async db => {
            db.InputUses.Add(new InputUse { ProjectId = s.Project.Id, TargetType = "Task", TargetId = s.TargetTask, OwnerId = s.Omar,
                SourceIdentity = "foreign-import", SourceRevisionId = revision, IntendedUse = "Imported provenance",
                AdoptedAt = f.Clock.Now, AdoptedBy = s.Omar });
            await db.SaveChangesAsync(); return 0;
        });
        var use = (await Get(TestData.Omar, $"/api/v1/projects/{s.Project.Id}/discipline-coordination"))["uses"]!.AsArray()
            .Single(x => x!["sourceRevisionId"]!.GetValue<Guid>() == revision)!;
        Assert.NotNull(use["targetKey"]); Assert.Null(use["sourceUrl"]); Assert.Null(use["sourceKey"]); Assert.Null(use["revision"]);
        Assert.DoesNotContain("foreign-private.pdf", use.ToJsonString()); Assert.DoesNotContain("Foreign source title", use.ToJsonString());
        Assert.Equal(s.TargetTask, Assert.Single(use["context"]!["inputs"]!.AsArray())!.G("id"));
        Assert.Equal(1, use["context"]!["unavailableInputCount"]!.GetValue<int>());
    }

    [Fact]
    public async Task Draft_and_cancelled_review_rows_keep_owner_without_inventing_a_date_or_action()
    {
        var s = await New(); await Flag(true);
        var civil = d.ProjectDiscipline(s.Project.Id, "Civil");
        var review = (await Post(TestData.Marc, $"/api/v1/projects/{s.Project.Id}/reviews", new ReviewEndpoints.CreateBody(
            Guid.NewGuid(), "Weekly survey review", "Confirm the survey", civil, d.User(TestData.Marc), [s.SourceRevision],
            [new ReviewEndpoints.Assignment(s.Electrical, s.Omar, new DateOnly(2026, 9, 24))], true, null))).G("id");
        var root = $"/api/v1/projects/{s.Project.Id}/discipline-coordination";
        var draft = (await Get(TestData.Marc, root))["reviews"]!.AsArray().Single(x => x!["id"]!.GetValue<Guid>() == review);
        Assert.Equal(d.User(TestData.Marc), draft["context"]!["owner"]!["id"]!.GetValue<Guid>());
        Assert.Null(draft["context"]!["neededBy"]);
        Assert.Equal(RecordContextAction.NotAssessed, draft["context"]!["nextAction"]!.GetValue<string>());
        await Post(TestData.Marc, $"/api/v1/projects/{s.Project.Id}/reviews/{review}/action", new ReviewEndpoints.ActionBody(Guid.NewGuid(),
            f.Db(db => db.ReviewPackages.Single(x => x.Id == review).RowVersion), "cancel", "Cancel weekly review"));
        var cancelled = (await Get(TestData.Marc, root))["reviews"]!.AsArray().Single(x => x!["id"]!.GetValue<Guid>() == review);
        Assert.True(cancelled["context"]!["terminal"]!.GetValue<bool>());
        Assert.Equal(RecordContextAction.None, cancelled["context"]!["nextAction"]!.GetValue<string>());
    }
}
