using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// AC-SCR-77 / AC-SCR-84: manual metadata through the canonical link commands, not source/approval registration.
public sealed class DocumentLinkReferenceTests
{
    [Fact] // AC-SCR-77: actual actor/time/parent capture, neutral manual provenance, unchanged command reply.
    public async Task Registration_records_six_fields_and_compares_declared_C_with_current_D_without_other_effects()
    {
        using var t = new DocumentLinkReferenceFixture(); await t.Flag(true); var s = await t.New(); var unrelated = t.Unrelated(s.Project.Id);
        var made = await t.Add(s, reference: new("  Shared register  ", "  EXT-001  ", "  C  "));
        Assert.Equal(DocumentLinkReferenceFixture.ReplyKeys, made.AsObject().Select(property => property.Key));
        var id = made.G("id"); var stored = t.Stored(id);
        Assert.Equal("Shared register", stored.SourceSystem); Assert.Equal("EXT-001", stored.StableSourceId); Assert.Equal("C", stored.DeclaredRevision);
        Assert.Equal(t.Data.User(TestData.Alex), stored.ReferenceRegisteredBy); Assert.Equal(t.F.Clock.Now, stored.ReferenceRegisteredAt);
        Assert.Equal(s.Deliverable, stored.ReferenceDeliverableId); Assert.Equal(stored.AddedBy, stored.ReferenceRegisteredBy); Assert.Equal(stored.AddedAt, stored.ReferenceRegisteredAt);
        var row = t.Row(await t.List(s), id); var context = row["referenceContext"]!;
        Assert.Equal("Manual", context["reference"]!.S("registrationKind")); Assert.Equal(t.Data.User(TestData.Alex), context["reference"]!["registeredByUser"]!.G("id"));
        Assert.Equal("Different", context["comparison"]!.S("state")); Assert.Equal("C", context["comparison"]!.S("declaredRevision")); Assert.Equal("D", context["comparison"]!.S("currentRevision"));
        Assert.Equal(s.Deliverable, context["target"]!.G("id")); Assert.Equal(t.Version<Deliverable>(s.Deliverable), context["target"]!.I("rowVersion"));
        Assert.Equal("D", context["target"]!.S("currentRevision")); Assert.Equal(unrelated, t.Unrelated(s.Project.Id));
    }

    [Fact] // AC-SCR-77: replacement restamps metadata, not original AddedBy/AddedAt; omission retains it, null clears it.
    public async Task Replacement_title_only_edit_and_clear_keep_original_link_authorship()
    {
        using var t = new DocumentLinkReferenceFixture(); await t.Flag(true); var s = await t.New(); var id = (await t.Add(s, reference: new("Original", "001", "C"))).G("id");
        var original = t.Stored(id); t.F.Clock.Now = t.F.Clock.Now.AddHours(1);
        var replacement = await t.Patch(id, new { reference = new DocumentLinkReferenceInput("Replacement", null, "D") }, TestData.Pm);
        Assert.Equal(DocumentLinkReferenceFixture.ReplyKeys, replacement.AsObject().Select(property => property.Key));
        var updated = t.Stored(id); Assert.Equal(t.Data.User(TestData.Pm), updated.ReferenceRegisteredBy); Assert.Equal(t.F.Clock.Now, updated.ReferenceRegisteredAt);
        Assert.True(updated.RowVersion > original.RowVersion); Assert.Equal(original.AddedBy, updated.AddedBy); Assert.Equal(original.AddedAt, updated.AddedAt);
        var stamp = updated.ReferenceRegisteredAt; t.F.Clock.Now = t.F.Clock.Now.AddHours(1);
        await t.Patch(id, new { title = "Retitled pointer", linkType = LinkType.Other }); updated = t.Stored(id);
        Assert.Equal(stamp, updated.ReferenceRegisteredAt); Assert.Equal(t.Data.User(TestData.Pm), updated.ReferenceRegisteredBy);
        Assert.Equal("Replacement", updated.SourceSystem); Assert.Equal("D", updated.DeclaredRevision);
        await t.Patch(id, new { reference = new DocumentLinkReferenceInput("Replacement", null, "D") }, TestData.Pm);
        Assert.Equal(t.F.Clock.Now, t.Stored(id).ReferenceRegisteredAt); // Deliberate identical re-registration is a new manual statement.
        await t.Patch(id, new { reference = (object?)null }); updated = t.Stored(id);
        t.AssertClear(updated); Assert.Equal(original.AddedBy, updated.AddedBy); Assert.Equal(original.AddedAt, updated.AddedAt);
        Assert.Contains(t.F.Db(db => db.ActivityLog.Where(log => log.ItemId == id).Select(log => log.Changes).ToArray()), changes => changes.Contains("ReferenceRegisteredBy", StringComparison.Ordinal));
    }

    [Fact] // AC-SCR-77 / AC-SCR-84: address changes invalidate metadata, including an old edit while dark.
    public async Task Url_replacement_invalidates_unless_explicitly_reregistered_and_dark_reads_do_not_erase_data()
    {
        using var t = new DocumentLinkReferenceFixture(); await t.Flag(true); var s = await t.New(); var id = (await t.Add(s, reference: new("Register", "001", "C"))).G("id");
        var original = t.Stored(id); await t.Flag(false);
        var dark = t.Row(await t.List(s), id); Assert.False(dark.AsObject().ContainsKey("referenceContext")); Assert.Equal("C", t.Stored(id).DeclaredRevision);
        await t.Patch(id, new { title = "Old title-only edit while dark" }); Assert.Equal(original.ReferenceRegisteredAt, t.Stored(id).ReferenceRegisteredAt);
        var before = t.State(s.Project.Id);
        await t.Patch(id, new { reference = (object?)null }, status: 404);
        await t.Patch(id, new { reference = new DocumentLinkReferenceInput("Register", null, "D") }, status: 404);
        Assert.Equal(before, t.State(s.Project.Id));
        await t.Patch(id, new { url = "https://example.test/new-address.pdf" }); t.AssertClear(t.Stored(id));
        await t.Flag(true); Assert.Equal("NotRecorded", t.Context(t.Row(await t.List(s), id)).S("state"));
        t.F.Clock.Now = t.F.Clock.Now.AddHours(2);
        await t.Patch(id, new { url = "https://example.test/explicit-new-address.pdf", reference = new DocumentLinkReferenceInput("Second register", "002", "D") });
        Assert.Equal("D", t.Stored(id).DeclaredRevision); Assert.Equal(t.F.Clock.Now, t.Stored(id).ReferenceRegisteredAt);
        Assert.Equal(original.AddedBy, t.Stored(id).AddedBy); Assert.Equal(original.AddedAt, t.Stored(id).AddedAt);
        var stamp = t.Stored(id).ReferenceRegisteredAt;
        await t.Patch(id, new { url = "  https://example.test/explicit-new-address.pdf  " }); Assert.Equal(stamp, t.Stored(id).ReferenceRegisteredAt);
    }

    [Theory] // AC-SCR-77: invalid/forged nested metadata refuses atomically on create and replacement.
    [InlineData("missingSource")]
    [InlineData("controlSource")]
    [InlineData("controlId")]
    [InlineData("controlRevision")]
    [InlineData("longSource")]
    [InlineData("longId")]
    [InlineData("longRevision")]
    [InlineData("registrar")]
    [InlineData("timestamp")]
    [InlineData("target")]
    [InlineData("verified")]
    [InlineData("unknown")]
    [InlineData("number")]
    public async Task Invalid_nested_input_cannot_change_link_url_version_audit_or_provenance(string invalid)
    {
        using var t = new DocumentLinkReferenceFixture(); await t.Flag(true); var s = await t.New();
        var reference = new JsonObject { ["sourceSystem"] = "Register", ["stableSourceId"] = "001", ["declaredRevision"] = "C" };
        switch (invalid)
        {
            case "missingSource": reference["sourceSystem"] = "   "; break;
            case "controlSource": reference["sourceSystem"] = "Register\n"; break;
            case "controlId": reference["stableSourceId"] = "id\t1"; break;
            case "controlRevision": reference["declaredRevision"] = "C\u2028D"; break;
            case "longSource": reference["sourceSystem"] = new string('x', 201); break;
            case "longId": reference["stableSourceId"] = new string('x', 201); break;
            case "longRevision": reference["declaredRevision"] = new string('x', 51); break;
            case "registrar": reference["registeredBy"] = t.Data.User(TestData.Pm).ToString(); break;
            case "timestamp": reference["registeredAt"] = "2020-01-01T00:00:00Z"; break;
            case "target": reference["referenceDeliverableId"] = s.Deliverable.ToString(); break;
            case "verified": reference["verified"] = true; break;
            case "unknown": reference["currentRevision"] = "D"; break;
            case "number": reference["declaredRevision"] = 42; break;
        }
        var before = t.State(s.Project.Id);
        await (await t.F.As(TestData.Alex).Post(t.AddPath(s), new { url = DocumentLinkReferenceFixture.Url, reference })).Json(400);
        Assert.Equal(before, t.State(s.Project.Id));
        var id = (await t.Add(s, reference: new("Valid original", "001", "C"))).G("id"); before = t.State(s.Project.Id);
        await t.Patch(id, new { url = "https://example.test/refused-new-url.pdf", reference }, status: 400);
        Assert.Equal(before, t.State(s.Project.Id));
    }

    [Fact] // AC-SCR-84: old serialized bodies/replies and shared producers retain the dark contract.
    public async Task Shared_creator_requires_explicit_enablement_and_old_body_shape_remains_exact()
    {
        using var t = new DocumentLinkReferenceFixture(); var s = await t.New();
        var old = new { url = DocumentLinkReferenceFixture.Url, title = (string?)null, linkType = (string?)null };
        Assert.Equal(JsonSerializer.Serialize(old, JsonOpts.Web), JsonSerializer.Serialize(new DocumentLinkEndpoints.AddBody(old.url), JsonOpts.Web));
        var before = t.State(s.Project.Id);
        await (await t.F.As(TestData.Alex).Post(t.AddPath(s), new DocumentLinkEndpoints.AddBody(old.url, Reference: new("Register", null, "C")))).Json(404);
        Assert.Equal(before, t.State(s.Project.Id));
        await t.Flag(true); before = t.State(s.Project.Id);
        await t.F.DbAsync(async db => {
            var error = await Assert.ThrowsAsync<ApiException>(() => DocumentLinkEndpoints.Add(db, s.Project.Id, ItemType.Task, s.Task, "Synthetic task",
                new(DocumentLinkReferenceFixture.Url, Reference: new("Register", null, "C")), t.Data.User(TestData.Alex), t.F.Clock.Now));
            Assert.Equal(404, error.Status); return 0;
        });
        Assert.Equal(before, t.State(s.Project.Id)); // Even with the organisation flag on, an old producer has not opted in.
        var enabled = await t.F.DbAsync(db => DocumentLinkEndpoints.Add(db, s.Project.Id, ItemType.Task, s.Task, "Synthetic task",
            new(DocumentLinkReferenceFixture.Url, Reference: new("Register", null, "C")), t.Data.User(TestData.Alex), t.F.Clock.Now, referenceEnabled: true));
        Assert.Equal(s.Deliverable, enabled.ReferenceDeliverableId);
        var legacy = await t.F.DbAsync(db => DocumentLinkEndpoints.Add(db, s.Project.Id, ItemType.Task, s.Task, "Synthetic task",
            new(DocumentLinkReferenceFixture.Url), t.Data.User(TestData.Alex), t.F.Clock.Now)); t.AssertClear(legacy);
    }

    [Fact] // AC-SCR-77 / AC-SCR-84: existing author/PM and If-Match rules stay authoritative.
    public async Task Reference_edits_preserve_authority_missing_stale_version_and_invalid_url_refusals()
    {
        using var t = new DocumentLinkReferenceFixture(); await t.Flag(true); var s = await t.New(); var id = (await t.Add(s, reference: new("Register", null, "C"))).G("id");
        var before = t.State(s.Project.Id); var change = new { reference = new DocumentLinkReferenceInput("Replacement", null, "D") };
        await t.Patch(id, change, TestData.Omar, status: 403); await t.Patch(id, change, TestData.Rita, status: 403);
        await (await t.F.As(TestData.Alex).Patch($"/api/v1/links/{id}", change)).Json(428);
        await t.Patch(id, change, version: t.Version<DocumentLink>(id) + 1, status: 409);
        await t.Patch(id, new { url = "javascript:alert(1)", reference = new DocumentLinkReferenceInput("Replacement", null, "D") }, status: 400);
        Assert.Equal(before, t.State(s.Project.Id));
        var shown = t.Version<DocumentLink>(id); await t.Patch(id, new { title = "A genuine concurrent edit" }); before = t.State(s.Project.Id);
        await t.Patch(id, change, version: shown, status: 409); Assert.Equal(before, t.State(s.Project.Id));
        await t.F.DbAsync(async db => { (await db.Projects.SingleAsync(project => project.Id == s.Project.Id)).Status = ProjectStatus.Archived; return await db.SaveChangesAsync(); });
        before = t.State(s.Project.Id); await t.Patch(id, change, TestData.Pm, status: 403); Assert.Equal(before, t.State(s.Project.Id));
    }

    [Fact] // AC-SCR-77: explicit target capture rejects malformed parents; clearing cannot edit a deleted owner.
    public async Task Foreign_parent_and_deleted_owner_refuse_metadata_without_partial_links_or_changes()
    {
        using var t = new DocumentLinkReferenceFixture(); await t.Flag(true); var s = await t.New(); var other = await t.New();
        await t.F.DbAsync(async db => { (await db.Tasks.SingleAsync(task => task.Id == s.Task)).DeliverableId = other.Deliverable; return await db.SaveChangesAsync(); });
        var before = t.State(s.Project.Id);
        await (await t.F.As(TestData.Alex).Post(t.AddPath(s), new DocumentLinkEndpoints.AddBody(DocumentLinkReferenceFixture.Url, Reference: new("Register", null, "C")))).Json(400);
        Assert.Equal(before, t.State(s.Project.Id));
        await t.F.DbAsync(async db => { (await db.Tasks.SingleAsync(task => task.Id == s.Task)).DeliverableId = s.Deliverable; return await db.SaveChangesAsync(); });
        var id = (await t.Add(s, reference: new("Register", null, "C"))).G("id");
        await t.F.DbAsync(async db => { (await db.Tasks.SingleAsync(task => task.Id == s.Task)).DeletedAt = t.F.Clock.Now; return await db.SaveChangesAsync(); });
        before = t.State(s.Project.Id);
        await t.Patch(id, new { reference = (object?)null }, status: 404); await t.Patch(id, new { reference = new DocumentLinkReferenceInput("Register", null, "D") }, status: 404);
        Assert.Equal(before, t.State(s.Project.Id));
    }
}

internal sealed class DocumentLinkReferenceFixture : IDisposable
{
    internal const string Url = "https://example.test/manual-pointer.pdf";
    internal static readonly string[] ReplyKeys = ["id", "title", "url", "linkType", "rowVersion"];
    internal HubFactory F { get; } = new();
    internal TestData Data { get; }
    internal sealed record Setup(Project Project, Guid Deliverable, Guid Task);
    internal DocumentLinkReferenceFixture() { Data = new(F); F.Clock.SetDate(2026, 11, 2); }
    public void Dispose() => F.Dispose();
    internal async Task Flag(bool enabled) => (await F.As(TestData.Admin).Put("/api/v1/admin/settings/flag.people_screens", new { value = enabled })).EnsureSuccessStatusCode();
    internal int Version<T>(Guid id) where T : class, IVersioned => F.Db(db => db.Set<T>().AsNoTracking().Single(row => EF.Property<Guid>(row, "Id") == id).RowVersion);
    internal DocumentLink Stored(Guid id) => F.Db(db => db.DocumentLinks.IgnoreQueryFilters().AsNoTracking().Single(link => link.Id == id));
    internal async Task<Guid> Drawing(Project project, string title = "Synthetic registered drawing", string? revision = "D") =>
        (await (await F.As(TestData.Marc).Post($"/api/v1/projects/{project.Id}/deliverables", new { name = title, revision,
            projectDisciplineId = Data.ProjectDiscipline(project.Id, "Civil"), deliverableTypeId = await Data.DeliverableType(), ownerId = Data.User(TestData.Alex), requiresReview = false })).Json(201)).G("id");
    internal async Task<Setup> New(bool parent = true)
    {
        var project = await Data.Project(); var drawing = await Drawing(project);
        var task = (await Data.NewTask(project.Id, TestData.Marc, new { assigneeId = Data.User(TestData.Alex), deliverableId = parent ? (Guid?)drawing : null })).G("id");
        return new(project, drawing, task);
    }
    internal string AddPath(Setup s, string type = ItemType.Task, Guid? item = null) => $"/api/v1/items/{type}/{item ?? s.Task}/links";
    internal async Task<JsonNode> Add(Setup s, DocumentLinkReferenceInput? reference = null, string type = ItemType.Task, Guid? item = null,
        string who = TestData.Alex, string url = Url) => await (await F.As(who).Post(AddPath(s, type, item), new DocumentLinkEndpoints.AddBody(url, Reference: reference))).Json(201);
    internal async Task<JsonNode> List(Setup s, string type = ItemType.Task, Guid? item = null, string who = TestData.Pm) => await (await F.As(who).GetAsync(AddPath(s, type, item))).Json();
    internal JsonNode Row(JsonNode list, Guid id, string collection = "links") => list[collection]!.AsArray().Single(row => row!.G("id") == id)!;
    internal JsonNode Context(JsonNode row) => row["referenceContext"]!["comparison"]!;
    internal async Task<JsonNode> Patch(Guid id, object body, string who = TestData.Alex, int? version = null, int status = 200) =>
        await (await F.As(who).Patch($"/api/v1/links/{id}", body, version ?? Version<DocumentLink>(id))).Json(status);
    internal void AssertClear(DocumentLink link)
    {
        Assert.Null(link.SourceSystem); Assert.Null(link.StableSourceId); Assert.Null(link.DeclaredRevision);
        Assert.Null(link.ReferenceRegisteredBy); Assert.Null(link.ReferenceRegisteredAt); Assert.Null(link.ReferenceDeliverableId);
    }
    internal string State(Guid project) => F.Db(db => JsonSerializer.Serialize(new {
        links = db.DocumentLinks.IgnoreQueryFilters().AsNoTracking().Where(link => link.ProjectId == project).OrderBy(link => link.Id).ToArray(),
        activity = db.ActivityLog.Count(log => log.ProjectId == project), unrelated = Unrelated(db, project)
    }, JsonOpts.Web));
    internal string Unrelated(Guid project) => F.Db(db => JsonSerializer.Serialize(Unrelated(db, project), JsonOpts.Web));
    static object Unrelated(HubDb db, Guid project) => new {
        project = db.Projects.Where(row => row.Id == project).Select(row => new { row.Id, row.RowVersion }).ToArray(),
        tasks = db.Tasks.IgnoreQueryFilters().Where(row => row.ProjectId == project).OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion }).ToArray(),
        drawings = db.Deliverables.IgnoreQueryFilters().Where(row => row.ProjectId == project).OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion }).ToArray(),
        sources = db.SourceRevisions.Count(), heads = db.SourceHeads.Count(), uses = db.InputUses.Count(), reviews = db.ReviewPackages.IgnoreQueryFilters().Count(),
        rounds = db.ReviewRounds.Count(), findings = db.ReviewFindings.Count(), findingEvents = db.FindingEvents.Count(), commands = db.CoordinationCommands.Count(),
        submissions = db.SubmissionPackages.IgnoreQueryFilters().Count(), issues = db.DeliverableIssues.Count(), receipts = db.Idempotency.Count(),
        occurrences = db.WorkAssignmentOccurrences.Count(), seenCommands = db.WorkSeenCommandReceipts.Count(), seen = db.WorkSeenAcknowledgements.Count(), visits = db.UserSettings.Sum(row => row.MyWorkSeenRevision)
    };
}
