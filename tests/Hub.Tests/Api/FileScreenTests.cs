using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// S44: FR/AC-SCR-77 real-provider filters, missing direct links and manual comparison;
/// FR/AC-SCR-84 dark wire, current authority, refusal/privacy and read-only side effects.
public sealed class FileScreenTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData d;
    public FileScreenTests() { d = new(f); f.Clock.Now = DateTimeOffset.Parse("2026-11-04T15:00:00Z"); }
    public void Dispose() => f.Dispose();
    async Task Flag(bool on) => await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.people_screens", new { value = on })).Json();
    async Task<JsonNode> Files(string scope, string extra = "", string who = TestData.Pm, int expect = 200) =>
        await (await f.As(who).GetAsync($"/api/v1/files?projects={scope}{extra}")).Json(expect);
    async Task<JsonNode> Deliverable(Guid project, string name = "Synthetic drawing", string discipline = "Civil", string? revision = "D") =>
        await (await f.As(TestData.Pm).Post($"/api/v1/projects/{project}/deliverables", new { name,
            projectDisciplineId = d.ProjectDiscipline(project, discipline), deliverableTypeId = await d.DeliverableType(), revision })).Json(201);
    async Task<JsonNode> Link(string kind, Guid id, string title, string who = TestData.Pm, object? reference = null, string? linkType = null, string? url = null) =>
        await (await f.As(who).Post($"/api/v1/items/{kind}/{id}/links", new { title, url = url ?? "https://example.test/" + Guid.NewGuid().ToString("N"), linkType, reference })).Json(201);
    static JsonNode Screen(JsonNode value) => value["screen"]!;
    static JsonNode Missing(JsonNode value) => Screen(value)["missingDeliverables"]!;
    static IEnumerable<JsonNode> Links(JsonNode value) => value["projects"]!.AsArray().SelectMany(project => project!["items"]!.AsArray())
        .SelectMany(item => item!["links"]!.AsArray()).Select(link => link!);
    static JsonNode Context(JsonNode value, Guid id) => Screen(value)["links"]!.AsArray().Single(row => row!.G("linkId") == id)!;
    static Guid[] MissingIds(JsonNode value) => Missing(value)["items"]!.AsArray().Select(row => row!.G("id")).ToArray();
    static string[] Keys(JsonNode value) => value.AsObject().Select(property => property.Key).ToArray();
    static JsonNode Legacy(JsonNode value)
    {
        var clone = value.DeepClone(); clone.AsObject().Remove("screen");
        foreach (var link in Links(clone)) link.AsObject().Remove("addedByUser");
        return clone;
    }
    string State() => f.Db(db => JsonSerializer.Serialize(new {
        Activity = db.ActivityLog.Count(), Notices = db.Notifications.Count(), Mail = db.Emails.Count(), Outbox = db.Outbox.Count(), Receipts = db.Idempotency.Count(),
        Seen = db.WorkSeenAcknowledgements.Count(), SeenReceipts = db.WorkSeenCommandReceipts.Count(),
        Preferences = db.UserSettings.OrderBy(row => row.UserId).Select(row => new { row.UserId, row.MyWorkSeenRevision, row.MyWorkFirstVisitAt, row.MyWorkLastVisitAt, row.LastDigestAt }).ToArray(),
        Links = db.DocumentLinks.IgnoreQueryFilters().OrderBy(row => row.Id).Select(row => new { row.Id, row.ProjectId, row.ItemType, row.ItemId,
            row.Title, row.Url, row.LinkType, row.AddedBy, row.AddedAt, row.RowVersion, row.DeletedAt, row.SourceSystem, row.StableSourceId, row.DeclaredRevision,
            row.ReferenceRegisteredBy, row.ReferenceRegisteredAt, row.ReferenceDeliverableId }).ToArray(),
        Deliverables = db.Deliverables.IgnoreQueryFilters().OrderBy(row => row.Id).Select(row => new { row.Id, row.ProjectId, row.Status, row.RowVersion, row.Revision, row.DeletedAt }).ToArray(),
        Tasks = db.Tasks.IgnoreQueryFilters().OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion, row.DeliverableId, row.ProjectDisciplineId }).ToArray(),
        Follows = db.Follows.OrderBy(row => row.Id).Select(row => new { row.Id, row.LastSeenAt }).ToArray(),
        Sources = db.SourceRevisions.Count(), Heads = db.SourceHeads.Count(), Inputs = db.InputUses.Count()
    }));

    [Fact]
    public async Task Shared_global_discipline_and_actual_added_by_filter_before_projection_and_reuse_the_manual_C_versus_D_context()
    {
        var first = await d.Project(); var second = await d.Project(); var civil = await d.Discipline("Civil"); var electrical = await d.Discipline("Electrical");
        var drawing = await Deliverable(first.Id); var otherDrawing = await Deliverable(second.Id);
        var electricalTask = await d.NewTask(first.Id, TestData.Pm, new { deliverableId = drawing.G("id"), assigneeId = d.User(TestData.Alex) }, "Electrical");
        await Flag(true);
        var declared = await Link(ItemType.Deliverable, drawing.G("id"), "Needle drawing", reference: new { sourceSystem = "Synthetic register", stableSourceId = "S44-01", declaredRevision = "C" });
        var other = await Link(ItemType.Deliverable, otherDrawing.G("id"), "Needle second drawing");
        var own = await Link(ItemType.Task, electricalTask.G("id"), "Needle task", TestData.Alex);
        var projectLink = await Link(ItemType.Project, first.Id, "Needle project");
        // A PM can register another author's link; that does not change its original author.
        await (await f.As(TestData.Pm).Patch($"/api/v1/links/{own.G("id")}", new { reference = new { sourceSystem = "Synthetic register", declaredRevision = "D" } }, own.I("rowVersion"))).Json();
        var scope = $"{first.Id},{second.Id}"; var before = State();
        var filtered = await Files(scope, $"&q=Needle&disciplineId={civil}&addedById={d.User(TestData.Pm)}&addedThisWeek=true");
        Assert.Equal(new[] { declared.G("id"), other.G("id") }.Order(), Links(filtered).Select(row => row.G("id")).Order());
        Assert.Equal(2, filtered.I("total")); Assert.False(filtered["truncated"]!.GetValue<bool>());
        Assert.Equal(civil, Screen(filtered)["applied"]!.G("disciplineId"));
        Assert.Single(Screen(filtered)["disciplines"]!.AsArray().Where(row => row!.G("id") == civil));
        Assert.Contains(Screen(filtered)["disciplines"]!.AsArray(), row => row!.G("id") == electrical);
        Assert.Contains(Screen(filtered)["authors"]!.AsArray(), row => row!.G("id") == d.User(TestData.Alex));
        Assert.DoesNotContain(Links(filtered), row => row.G("id") == own.G("id") || row.G("id") == projectLink.G("id"));
        var metadata = Context(filtered, declared.G("id"));
        Assert.Equal(declared.I("rowVersion"), metadata.I("rowVersion")); Assert.True(metadata["canChange"]!.GetValue<bool>());
        Assert.Equal(civil, metadata["discipline"]!.G("id"));
        var reference = metadata["referenceContext"]!;
        Assert.Equal("Different", reference["comparison"]!.S("state"));
        Assert.Equal("C", reference["comparison"]!.S("declaredRevision")); Assert.Equal("D", reference["comparison"]!.S("currentRevision"));
        Assert.Equal(drawing.G("id"), reference["target"]!.G("id")); Assert.Equal("Manual", reference["reference"]!.S("registrationKind"));
        var alex = await Files(scope, $"&disciplineId={electrical}&addedById={d.User(TestData.Alex)}");
        Assert.Equal(own.G("id"), Assert.Single(Links(alex)).G("id"));
        Assert.Equal(d.User(TestData.Pm), Context(alex, own.G("id"))["referenceContext"]!["reference"]!.G("registeredBy"));
        Assert.Equal(d.User(TestData.Alex), Links(alex).Single()["addedByUser"]!.G("id"));
        Assert.Equal(before, State());
    }

    [Fact]
    public async Task Same_name_inactive_and_missing_authors_use_actual_added_by_without_inventing_person_or_choice_identity()
    {
        var project = await d.Project(); var alex = d.User(TestData.Alex); var jill = d.User(TestData.Jill);
        var task = await d.NewTask(project.Id, TestData.Pm, new { assigneeId = alex });
        await d.NewTask(project.Id, TestData.Pm, new { assigneeId = jill });
        var one = await Link(ItemType.Task, task.G("id"), "Same", TestData.Alex);
        var two = await Link(ItemType.Task, task.G("id"), "Same", TestData.Jill);
        var orphan = new DocumentLink { ProjectId = project.Id, ItemType = ItemType.Project, ItemId = project.Id, Title = "Orphan", Url = "https://example.test/orphan",
            AddedBy = Guid.NewGuid(), AddedAt = f.Clock.Now, LinkType = LinkType.Other };
        await f.DbAsync(async db => {
            foreach (var user in await db.Users.Where(user => user.Id == alex || user.Id == jill).ToArrayAsync()) { user.DisplayName = "Same synthetic name"; if (user.Id == jill) user.IsActive = false; }
            db.DocumentLinks.Add(orphan); return await db.SaveChangesAsync();
        });
        await Flag(true); var scope = project.Id.ToString(); var before = State();
        var first = await Files(scope, $"&addedById={alex}"); Assert.Equal(one.G("id"), Assert.Single(Links(first)).G("id"));
        var second = await Files(scope, $"&addedById={jill}"); Assert.Equal(two.G("id"), Assert.Single(Links(second)).G("id"));
        Assert.False(Links(second).Single()["addedByUser"]!["isActive"]!.GetValue<bool>());
        var choices = Screen(second)["authors"]!.AsArray(); Assert.Equal(2, choices.Count); Assert.Equal(2, choices.Select(row => row!.G("id")).Distinct().Count());
        Assert.All(choices, row => Assert.Equal("Same synthetic name", row!.S("displayName")));
        var missing = await Files(scope, $"&addedById={orphan.AddedBy}"); var row = Assert.Single(Links(missing));
        Assert.Equal(orphan.Id, row.G("id")); Assert.Null(row["addedBy"]); Assert.False(row.AsObject().ContainsKey("addedByUser"));
        Assert.DoesNotContain(Screen(missing)["authors"]!.AsArray(), choice => choice!.G("id") == orphan.AddedBy);
        Assert.Empty(Links(await Files(scope, $"&addedById={Guid.NewGuid()}")));
        Assert.Empty(Links(await Files(scope, $"&disciplineId={Guid.NewGuid()}")));
        Assert.Equal(before, State());
    }

    [Fact]
    public async Task Missing_is_an_independent_live_direct_link_anti_join_including_on_hold_and_ignoring_link_filters()
    {
        var project = await d.Project(); var pd = d.ProjectDiscipline(project.Id, "Civil"); var global = await d.Discipline("Civil"); var dt = await d.DeliverableType();
        var linked = await Deliverable(project.Id, "S44 missing linked");
        var taskOnly = await Deliverable(project.Id, "S44 missing task-only");
        var task = await d.NewTask(project.Id, TestData.Pm, new { deliverableId = taskOnly.G("id") });
        var direct = await Link(ItemType.Deliverable, linked.G("id"), "Hidden by search");
        await Link(ItemType.Task, task.G("id"), "Hidden by search"); await Link(ItemType.Project, project.Id, "Hidden by search");
        string[] open = [DeliverableStatus.NotStarted, DeliverableStatus.InProgress, DeliverableStatus.InReview, DeliverableStatus.RevisionRequired, DeliverableStatus.ReadyToIssue, DeliverableStatus.OnHold];
        var fixtures = open.Concat([DeliverableStatus.Issued, DeliverableStatus.Accepted, DeliverableStatus.Cancelled]).Select((status, i) => new Deliverable {
            ProjectId = project.Id, ProjectDisciplineId = pd, DeliverableTypeId = dt, Name = "S44 missing " + status, Key = "S44-M-" + i,
            Seq = 100 + i, Status = status, Revision = "D" }).ToArray();
        var deleted = new Deliverable { ProjectId = project.Id, ProjectDisciplineId = pd, DeliverableTypeId = dt, Name = "S44 missing deleted", Key = "S44-M-deleted", Seq = 200, DeletedAt = f.Clock.Now };
        await f.DbAsync(async db => { db.Deliverables.AddRange(fixtures.Append(deleted)); (await db.DocumentLinks.SingleAsync(row => row.Id == direct.G("id"))).AddedAt = f.Clock.Now.AddDays(-20); return await db.SaveChangesAsync(); });
        await Flag(true); var scope = project.Id.ToString(); var before = State();
        var filtered = await Files(scope, $"&q=S44%20missing&type=Network%20Folder&addedById={d.User(TestData.Jill)}&addedThisWeek=true&disciplineId={global}");
        Assert.Empty(Links(filtered)); Assert.Equal(7, Missing(filtered).I("total"));
        Assert.Equal(fixtures.Take(open.Length).Select(row => row.Id).Append(taskOnly.G("id")).Order(), MissingIds(filtered).Order());
        Assert.DoesNotContain(linked.G("id"), MissingIds(filtered)); Assert.False(Missing(filtered)["truncated"]!.GetValue<bool>());
        Assert.All(Missing(filtered)["items"]!.AsArray(), row => { Assert.Equal(global, row!.G("disciplineId")); Assert.True(row["canAddLink"]!.GetValue<bool>()); });
        Assert.Equal(before, State());
        var deleteVersion = f.Db(db => db.DocumentLinks.Where(row => row.Id == direct.G("id")).Select(row => row.RowVersion).Single());
        Assert.True(deleteVersion > direct.I("rowVersion"), "The fixture's AddedAt edit advances the canonical link version.");
        (await f.As(TestData.Pm).Delete($"/api/v1/links/{direct.G("id")}", deleteVersion)).EnsureSuccessStatusCode();
        var after = await Files(scope, "&q=S44%20missing&type=Network%20Folder");
        Assert.Equal(8, Missing(after).I("total")); Assert.Contains(linked.G("id"), MissingIds(after));
        Assert.Empty(MissingIds(await Files(scope, "&q=no-such-deliverable")));
    }

    [Fact]
    public async Task Actual_comment_author_PM_viewer_read_only_and_project_state_rules_control_current_actions_and_commands()
    {
        var project = await d.Project(); var task = await d.NewTask(project.Id, TestData.Pm, new { assigneeId = d.User(TestData.Alex) });
        var missing = await Deliverable(project.Id);
        await (await f.As(TestData.Pm).Post($"/api/v1/projects/{project.Id}/members", new { userId = d.User(TestData.Jill), roles = new[] { ProjectRole.Viewer } })).Json(201);
        var link = await Link(ItemType.Task, task.G("id"), "Alex authored", TestData.Alex);
        var viewerLink = await Link(ItemType.Task, task.G("id"), "Viewer authored", TestData.Jill);
        await Flag(true); var scope = project.Id.ToString();
        var asPm = await Files(scope); Assert.True(Context(asPm, link.G("id"))["canChange"]!.GetValue<bool>());
        Assert.True(Context(await Files(scope, who: TestData.Alex), link.G("id"))["canChange"]!.GetValue<bool>());
        var asLead = await Files(scope, who: TestData.Omar);
        Assert.False(Context(asLead, link.G("id"))["canChange"]!.GetValue<bool>()); // lead can add, not change another author's link
        Assert.True(Missing(asLead)["items"]!.AsArray().Single(row => row!.G("id") == missing.G("id"))!["canAddLink"]!.GetValue<bool>());
        Assert.False(asLead["projects"]![0]!["canAdd"]!.GetValue<bool>()); // work-item add is not project-link add
        Assert.False(Missing(await Files(scope, who: TestData.Diane))["items"]!.AsArray().Single(row => row!.G("id") == missing.G("id"))!["canAddLink"]!.GetValue<bool>());
        Assert.True(Context(await Files(scope, who: TestData.Jill), viewerLink.G("id"))["canChange"]!.GetValue<bool>());
        var readOnly = await Files(scope, who: TestData.Rita);
        Assert.False(Context(readOnly, link.G("id"))["canChange"]!.GetValue<bool>());
        Assert.False(Missing(readOnly)["items"]!.AsArray().Single(row => row!.G("id") == missing.G("id"))!["canAddLink"]!.GetValue<bool>());
        var before = State();
        await (await f.As(TestData.Rita).Post($"/api/v1/items/Deliverable/{missing.G("id")}/links", new { url = "https://example.test/refused" })).Json(403);
        await (await f.As(TestData.Omar).Patch($"/api/v1/links/{link.G("id")}", new { title = "Refused" }, link.I("rowVersion"))).Json(403);
        Assert.Equal(before, State());
        // Access.Context includes lead facts even after a discipline is deactivated.
        var electricalPd = d.ProjectDiscipline(project.Id, "Electrical");
        await f.DbAsync(async db => { (await db.ProjectDisciplines.SingleAsync(row => row.Id == electricalPd)).IsActive = false; return await db.SaveChangesAsync(); });
        Assert.True(Missing(await Files(scope, who: TestData.Omar))["items"]!.AsArray().Single(row => row!.G("id") == missing.G("id"))!["canAddLink"]!.GetValue<bool>());
        await Link(ItemType.Deliverable, missing.G("id"), "Canonical lead add", TestData.Omar);
        await (await f.As(TestData.Pm).Patch($"/api/v1/projects/{project.Id}", new { allowViewerComments = false }, d.Version(project.Id))).Json();
        Assert.False(Context(await Files(scope, who: TestData.Jill), viewerLink.G("id"))["canChange"]!.GetValue<bool>());
        before = State();
        await (await f.As(TestData.Jill).Patch($"/api/v1/links/{viewerLink.G("id")}", new { title = "Refused" }, viewerLink.I("rowVersion"))).Json(403);
        Assert.Equal(before, State());
        var updated = await (await f.As(TestData.Pm).Patch($"/api/v1/links/{link.G("id")}", new { title = "PM update" }, link.I("rowVersion"))).Json();
        Assert.Equal(updated.I("rowVersion"), Context(await Files(scope), link.G("id")).I("rowVersion"));
        before = State();
        await (await f.As(TestData.Pm).Patch($"/api/v1/links/{link.G("id")}", new { title = "Stale refusal" }, link.I("rowVersion"))).Json(409);
        Assert.Equal(before, State());
        // On Hold remains writable under the existing link matrix; do not invent a stricter Files gate.
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(row => row.Id == project.Id)).Status = ProjectStatus.OnHold; return await db.SaveChangesAsync(); });
        Assert.True(Context(await Files(scope), link.G("id"))["canChange"]!.GetValue<bool>());
        before = State(); await Files(scope); await Files(scope, who: TestData.Alex); Assert.Equal(before, State());
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(row => row.Id == project.Id)).Status = ProjectStatus.Archived; return await db.SaveChangesAsync(); });
        before = State(); var archived = await Files(scope); Assert.Empty(Links(archived)); Assert.Empty(MissingIds(archived));
        await (await f.As(TestData.Pm).Patch($"/api/v1/links/{link.G("id")}", new { title = "Archived refusal" }, updated.I("rowVersion"))).Json(403);
        Assert.Equal(before, State());
    }

    [Fact]
    public async Task Scope_deleted_owners_and_project_mismatches_are_removed_before_counts_choices_and_missing_membership()
    {
        var visible = await d.Project(); var hidden = await d.Project(tweak: body => body["members"] = Array.Empty<object>());
        var archived = await d.Project(); var cancelled = await d.Project();
        var publicTask = await d.NewTask(visible.Id, TestData.Pm); var secret = await Deliverable(hidden.Id, "Hidden synthetic drawing");
        var genuinelyMissing = await Deliverable(visible.Id, "Visible unlinked drawing");
        var removed = await d.NewTask(visible.Id, TestData.Pm);
        var valid = await Link(ItemType.Task, publicTask.G("id"), "Visible");
        var hiddenLink = await Link(ItemType.Deliverable, secret.G("id"), "Secret link");
        var removedLink = await Link(ItemType.Task, removed.G("id"), "Deleted owner link");
        var malformed = new[] {
            new DocumentLink { ProjectId = visible.Id, ItemType = ItemType.Deliverable, ItemId = secret.G("id"), Title = "Foreign owner", Url = "https://example.test/foreign", AddedBy = d.User(TestData.Diane), AddedAt = f.Clock.Now },
            new DocumentLink { ProjectId = visible.Id, ItemType = ItemType.Project, ItemId = hidden.Id, Title = "Wrong project owner", Url = "https://example.test/project-mismatch", AddedBy = d.User(TestData.Diane), AddedAt = f.Clock.Now },
            new DocumentLink { ProjectId = visible.Id, ItemType = ItemType.Task, ItemId = Guid.NewGuid(), Title = "Missing owner", Url = "https://example.test/missing", AddedBy = d.User(TestData.Diane), AddedAt = f.Clock.Now },
            new DocumentLink { ProjectId = hidden.Id, ItemType = ItemType.Deliverable, ItemId = genuinelyMissing.G("id"), Title = "Wrong-project direct link", Url = "https://example.test/wrong-direct", AddedBy = d.User(TestData.Diane), AddedAt = f.Clock.Now }
        };
        await f.DbAsync(async db => {
            (await db.Projects.SingleAsync(row => row.Id == hidden.Id)).Visibility = Visibility.Restricted;
            (await db.Projects.SingleAsync(row => row.Id == archived.Id)).Status = ProjectStatus.Archived;
            (await db.Projects.SingleAsync(row => row.Id == cancelled.Id)).Status = ProjectStatus.Cancelled;
            (await db.Tasks.SingleAsync(row => row.Id == removed.G("id"))).DeletedAt = f.Clock.Now;
            db.DocumentLinks.AddRange(malformed); return await db.SaveChangesAsync();
        });
        await Flag(true); var before = State(); var scope = $"{visible.Id},{hidden.Id},{archived.Id},{cancelled.Id}";
        var response = await Files(scope, who: TestData.Alex);
        Assert.Equal(valid.G("id"), Assert.Single(Links(response)).G("id")); Assert.Equal(1, response.I("total"));
        Assert.Single(Screen(response)["links"]!.AsArray()); Assert.Equal(1, Missing(response).I("total")); Assert.Equal(genuinelyMissing.G("id"), Assert.Single(MissingIds(response)));
        Assert.DoesNotContain(hidden.Id.ToString(), response.ToJsonString()); Assert.DoesNotContain(secret.G("id").ToString(), response.ToJsonString());
        Assert.DoesNotContain(Screen(response)["authors"]!.AsArray(), row => row!.G("id") == d.User(TestData.Diane));
        Assert.Empty(Links(await Files(hidden.Id.ToString(), who: TestData.Alex)));
        var admin = await Files(scope, who: TestData.Admin); Assert.Contains(Links(admin), row => row.G("id") == hiddenLink.G("id"));
        Assert.DoesNotContain(Links(admin), row => row.G("id") == removedLink.G("id") || malformed.Any(bad => bad.Id == row.G("id")));
        Assert.Equal(before, State());
    }

    [Theory]
    [InlineData("&disciplineId=bad", "disciplineId")]
    [InlineData("&disciplineId=00000000-0000-0000-0000-000000000000", "disciplineId")]
    [InlineData("&disciplineId=11111111-1111-1111-1111-111111111111,22222222-2222-2222-2222-222222222222", "disciplineId")]
    [InlineData("&addedById=bad", "addedById")]
    [InlineData("&addedById=00000000-0000-0000-0000-000000000000", "addedById")]
    [InlineData("&addedThisWeek=True", "addedThisWeek")]
    [InlineData("&addedThisWeek=1", "addedThisWeek")]
    [InlineData("&addedThisWeek=true,false", "addedThisWeek")]
    public async Task Malformed_new_raw_controls_are_ignored_off_and_refused_on_without_side_effects(string query, string field)
    {
        var project = await d.Project(); await Link(ItemType.Project, project.Id, "Legacy"); var scope = project.Id.ToString();
        await Flag(false); var before = State(); var baseline = await Files(scope);
        Assert.Equal(new[] { "projects", "total", "truncated", "types" }, Keys(baseline));
        Assert.Equal(baseline.ToJsonString(), (await Files(scope, query)).ToJsonString()); Assert.Equal(before, State());
        await Flag(true); before = State(); var refused = await Files(scope, query, expect: 400);
        Assert.NotNull(refused["errors"]![field]); Assert.Equal(before, State());
    }

    [Theory]
    [InlineData("America/Halifax", "2026-11-01T15:00:00Z", "2026-10-26", "2026-10-26T03:00:00Z")]
    [InlineData("America/Halifax", "2026-11-02T03:59:59Z", "2026-10-26", "2026-10-26T03:00:00Z")]
    [InlineData("America/Halifax", "2026-11-02T04:00:00Z", "2026-11-02", "2026-11-02T04:00:00Z")]
    [InlineData("Pacific/Auckland", "2026-11-01T11:00:00Z", "2026-11-02", "2026-11-01T11:00:00Z")]
    public async Task Added_week_uses_actual_org_Monday_and_DST_offsets_and_never_includes_future_or_registration_time(string zone, string now, string from, string start)
    {
        var project = await d.Project(); f.Clock.Now = DateTimeOffset.Parse(now); var startUtc = DateTimeOffset.Parse(start); var pm = d.User(TestData.Pm);
        await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/org_time_zone", new { value = zone })).Json();
        var stamps = new[] { startUtc.AddSeconds(-1), startUtc, f.Clock.Now, f.Clock.Now.AddSeconds(1) };
        var links = stamps.Select((stamp, index) => new DocumentLink { ProjectId = project.Id, ItemType = ItemType.Project, ItemId = project.Id,
            Title = "Week " + index, Url = "https://example.test/week/" + index, AddedBy = pm, AddedAt = stamp, LinkType = LinkType.Other }).ToArray();
        // A newly recorded declaration on an older link cannot turn AddedAt into this week's activity.
        links[0].SourceSystem = "Synthetic register"; links[0].DeclaredRevision = "C";
        links[0].ReferenceRegisteredBy = pm; links[0].ReferenceRegisteredAt = f.Clock.Now;
        await f.DbAsync(async db => { db.DocumentLinks.AddRange(links); return await db.SaveChangesAsync(); });
        await Flag(true); var before = State(); var response = await Files(project.Id.ToString(), "&addedThisWeek=true");
        Assert.Equal(new[] { links[1].Id, links[2].Id }.Order(), Links(response).Select(row => row.G("id")).Order());
        Assert.Equal(from, Screen(response).S("weekFrom")); Assert.Equal(DateOnly.Parse(from).AddDays(6).ToString("yyyy-MM-dd"), Screen(response).S("weekThrough"));
        Assert.Equal(zone, Screen(response).S("orgTimeZone")); Assert.Equal(f.Clock.Now, DateTimeOffset.Parse(Screen(response).S("asOf")));
        Assert.Equal(4, (await Files(project.Id.ToString(), "&addedThisWeek=false")).I("total"));
        Assert.Equal(before, State());
    }

    [Fact]
    public async Task Every_new_predicate_runs_before_the_link_cap_and_equal_titles_have_an_ID_tiebreak()
    {
        var project = await d.Project(); var pm = d.User(TestData.Pm); var alex = d.User(TestData.Alex);
        var electrical = await d.Discipline("Electrical"); var task = await d.NewTask(project.Id, TestData.Pm, new { assigneeId = alex }, "Electrical");
        var rows = Enumerable.Range(0, 2001).Select(i => new DocumentLink { ProjectId = project.Id, ItemType = ItemType.Project, ItemId = project.Id,
            Title = "A " + i.ToString("D4"), Url = "https://example.test/cap/" + i, AddedBy = pm, AddedAt = f.Clock.Now.AddDays(-20), LinkType = LinkType.Other }).ToArray();
        var target = new DocumentLink { ProjectId = project.Id, ItemType = ItemType.Task, ItemId = task.G("id"), Title = "Z target", Url = @"\\synthetic\s44\target",
            AddedBy = alex, AddedAt = f.Clock.Now, LinkType = LinkType.NetworkFolder };
        await f.DbAsync(async db => { db.DocumentLinks.AddRange(rows.Append(target)); return await db.SaveChangesAsync(); });
        await Flag(true); var scope = project.Id.ToString(); var before = State(); var capped = await Files(scope);
        Assert.Equal(2000, capped.I("total")); Assert.True(capped["truncated"]!.GetValue<bool>()); Assert.DoesNotContain(Links(capped), row => row.G("id") == target.Id);
        string[] filters = [$"&disciplineId={electrical}", $"&addedById={alex}", "&addedThisWeek=true",
            $"&disciplineId={electrical}&addedById={alex}&addedThisWeek=true&type=Network%20Folder"];
        foreach (var filter in filters)
        {
            var response = await Files(scope, filter); Assert.Equal(target.Id, Assert.Single(Links(response)).G("id"));
            Assert.Equal(1, response.I("total")); Assert.False(response["truncated"]!.GetValue<bool>());
        }
        Assert.Equal(before, State());
        var ties = new[] { Guid.Parse("11111111-1111-1111-1111-111111111111"), Guid.Parse("22222222-2222-2222-2222-222222222222") };
        await f.DbAsync(async db => { db.DocumentLinks.AddRange(ties.Reverse().Select(id => new DocumentLink { Id = id, ProjectId = project.Id,
            ItemType = ItemType.Project, ItemId = project.Id, Title = "Equal title", Url = "https://example.test/tie/" + id, AddedBy = pm, AddedAt = f.Clock.Now })); return await db.SaveChangesAsync(); });
        Assert.Equal(ties, Links(await Files(scope, "&q=Equal%20title")).Select(row => row.G("id")));
    }

    [Fact]
    public async Task Missing_count_is_exact_beyond_its_independent_cap_with_stable_project_key_ID_order()
    {
        var project = await d.Project(); var pd = d.ProjectDiscipline(project.Id, "Civil"); var dt = await d.DeliverableType();
        // Bounded synthetic register history, not evidence of bypassing any user command.
        var rows = Enumerable.Range(0, 2001).Select(i => new Deliverable { ProjectId = project.Id, ProjectDisciplineId = pd, DeliverableTypeId = dt,
            Key = "S44-" + i.ToString("D4"), Name = "Synthetic unlinked " + i, Seq = i + 100, Status = i == 0 ? DeliverableStatus.OnHold : DeliverableStatus.NotStarted }).ToArray();
        await f.DbAsync(async db => { db.Deliverables.AddRange(rows.Reverse()); return await db.SaveChangesAsync(); });
        await Flag(true); var before = State(); var response = await Files(project.Id.ToString(), "&type=Network%20Folder&addedThisWeek=true");
        Assert.Equal(0, response.I("total")); Assert.False(response["truncated"]!.GetValue<bool>());
        Assert.Equal(2001, Missing(response).I("total")); Assert.True(Missing(response)["truncated"]!.GetValue<bool>());
        Assert.Equal(rows.Take(2000).Select(row => row.Id), MissingIds(response)); Assert.Equal(before, State());
    }

    [Fact]
    public async Task Choice_caps_are_scoped_and_independent_of_selected_filters_with_honest_sentinels()
    {
        var project = await d.Project(); var dt = await d.DeliverableType();
        var disciplines = Enumerable.Range(0, 201).Select(i => new Discipline { Code = "S44-" + i, Name = "S44 choice " + i.ToString("D3"), IsActive = i != 0 }).ToArray();
        var authors = Enumerable.Range(0, 201).Select(i => new AppUser { Email = "s44-choice-" + i + "@example.test", DisplayName = "S44 author " + i.ToString("D3"), IsActive = i != 0 }).ToArray();
        var pds = disciplines.Select(discipline => new ProjectDiscipline { ProjectId = project.Id, DisciplineId = discipline.Id }).ToArray();
        var deliverables = pds.Select((pd, i) => new Deliverable { ProjectId = project.Id, ProjectDisciplineId = pd.Id, DeliverableTypeId = dt,
            Key = "S44-C-" + i, Name = "Synthetic choice drawing " + i, Seq = i + 100 }).ToArray();
        var links = deliverables.Select((drawing, i) => new DocumentLink { ProjectId = project.Id, ItemType = ItemType.Deliverable, ItemId = drawing.Id,
            Title = "Choice link " + i, Url = "https://example.test/choice/" + i, AddedBy = authors[i].Id, AddedAt = f.Clock.Now }).ToArray();
        await f.DbAsync(async db => { db.Disciplines.AddRange(disciplines); db.Users.AddRange(authors); db.ProjectDisciplines.AddRange(pds); db.Deliverables.AddRange(deliverables); db.DocumentLinks.AddRange(links); return await db.SaveChangesAsync(); });
        await Flag(true); var before = State(); var response = await Files(project.Id.ToString(), $"&q=no-links-match&addedById={authors[^1].Id}&disciplineId={disciplines[^1].Id}");
        Assert.Empty(Links(response)); var screen = Screen(response);
        Assert.True(screen["authorsTruncated"]!.GetValue<bool>()); Assert.True(screen["disciplinesTruncated"]!.GetValue<bool>());
        Assert.Equal(authors.Take(200).Select(row => row.Id), screen["authors"]!.AsArray().Select(row => row!.G("id")));
        Assert.Equal(disciplines.Take(200).Select(row => row.Id), screen["disciplines"]!.AsArray().Select(row => row!.G("id")));
        Assert.False(screen["authors"]![0]!["isActive"]!.GetValue<bool>()); Assert.False(screen["disciplines"]![0]!["isActive"]!.GetValue<bool>());
        Assert.Equal(before, State());
    }

    [Fact]
    public async Task Disabling_omits_all_screen_context_and_keeps_legacy_fields_colours_URL_actions_and_original_author_wire()
    {
        var project = await d.Project(); var drawing = await Deliverable(project.Id);
        const string unc = @"\\synthetic\s44\drawings";
        await Flag(true); var link = await Link(ItemType.Deliverable, drawing.G("id"), "UNC drawing", reference: new { sourceSystem = "Synthetic register", declaredRevision = "C" }, url: unc);
        var own = await Link(ItemType.Project, project.Id, "Project folder");
        var scope = project.Id.ToString(); var enabled = await Files(scope); Assert.Equal(2, Screen(enabled)["links"]!.AsArray().Count);
        await Flag(false); var before = State(); var dark = await Files(scope, "&disciplineId=bad&addedById=bad&addedThisWeek=bad");
        Assert.Equal(new[] { "projects", "total", "truncated", "types" }, Keys(dark));
        Assert.All(Links(dark), row => Assert.Equal(new[] { "id", "title", "url", "linkType", "isNetworkPath", "addedBy", "addedAt" }, Keys(row)));
        var network = Links(dark).Single(row => row.G("id") == link.G("id")); Assert.Equal(unc, network.S("url")); Assert.True(network["isNetworkPath"]!.GetValue<bool>());
        Assert.Equal(dark.ToJsonString(), Legacy(enabled).ToJsonString()); Assert.Equal(before, State());
        await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.visual_system", new { value = true })).Json();
        await (await f.As(TestData.Pm).Patch($"/api/v1/projects/{project.Id}", new { identityColour = IdentityColour.Cyan }, d.Version(project.Id))).Json();
        var coloured = await Files(scope); Assert.Equal(IdentityColour.Cyan, coloured["projects"]![0]!.S("identityColour"));
        await Flag(true); var restored = await Files(scope);
        Assert.Equal("Different", Context(restored, link.G("id"))["referenceContext"]!["comparison"]!.S("state"));
        Assert.NotNull(Links(restored).Single(row => row.G("id") == own.G("id"))["addedByUser"]);
    }
}
