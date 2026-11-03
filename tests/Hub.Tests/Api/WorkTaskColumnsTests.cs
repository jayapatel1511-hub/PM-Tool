using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

public sealed class WorkTaskColumnsTests : IDisposable
{
    readonly HubFactory f = new();
    readonly TestData data;
    public WorkTaskColumnsTests() => data = new(f);
    public void Dispose() => f.Dispose();
    sealed record Setup(Project Project, Guid Milestone, Guid Deliverable, Guid Revision, Guid Package);
    async Task<JsonNode> Get(string path, string who = TestData.Alex) => await (await f.As(who).GetAsync(path)).Json();
    async Task<JsonNode> Post(string who, string path, object body, int status = 200) => await (await f.As(who).Post(path, body)).Json(status);
    async Task Flag(string flag, bool enabled) => (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/flag.{flag}", new { value = enabled })).EnsureSuccessStatusCode();
    int Version<T>(Guid id) where T : Audited => f.Db(db => db.Set<T>().AsNoTracking().Single(row => row.Id == id).RowVersion);
    static string Root(Setup s) => $"/api/v1/projects/{s.Project.Id}";
    static string PackagePath(Setup s, Guid? id = null) => Root(s) + $"/submissions/{id ?? s.Package}";
    static JsonNode Row(JsonNode page, Guid id) => Assert.Single(page["items"]!.AsArray(), row => row!.G("id") == id)!;
    static void Date(JsonNode row, string? expected)
    {
        Assert.Equal("Known", row["submissionDate"]!.S("state"));
        if (expected is null) Assert.Null(row["submissionDate"]!["targetDate"]);
        else Assert.Equal(expected, row["submissionDate"]!.S("targetDate"));
    }
    async Task<Guid> Document(Project p)
    {
        var civil = data.ProjectDiscipline(p.Id, "Civil");
        return (await Post(TestData.Marc, $"/api/v1/projects/{p.Id}/deliverables", new { name = "Same deliverable label", projectDisciplineId = civil,
            deliverableTypeId = await data.DeliverableType(), ownerId = data.User(TestData.Alex), revision = "A", requiresReview = false }, 201)).G("id");
    }
    async Task<Guid> Revision(Project p, Guid did)
    {
        return (await Post(TestData.Alex, $"/api/v1/projects/{p.Id}/source-revisions", new ChangeEndpoints.RegisterBody(Guid.NewGuid(), did, Version<Deliverable>(did),
            data.ProjectDiscipline(p.Id, "Civil"), data.User(TestData.Alex), "Deliverable", $"columns-{did}", "Same deliverable label", "A", $"https://example.test/{did}/A.pdf",
            "Design team", "Submission", null, null, null, null, null, null))).G("id");
    }
    async Task<Guid> Package(Setup s, DateOnly target, Guid? supersedes = null)
    {
        return (await Post(TestData.Marc, Root(s) + "/submissions", new SubmissionEndpoints.CreateBody(Guid.NewGuid(), "Column date package", "Synthetic permit issue", "Municipality",
            data.User(TestData.Marc), s.Milestone, target, [new(s.Revision)], [], supersedes, null))).G("id");
    }
    async Task<Setup> New(DateOnly? target = null)
    {
        var p = await data.Project();
        var milestone = (await Post(TestData.Pm, $"/api/v1/projects/{p.Id}/milestones", new MilestoneEndpoints.CreateBody("Submission milestone", MilestoneType.DesignSubmission,
            new(2027, 2, 20), null, null, null, true), 201)).G("id");
        var did = await Document(p); var revision = await Revision(p, did);
        var s = new Setup(p, milestone, did, revision, Guid.Empty);
        return s with { Package = await Package(s, target ?? new(2027, 2, 11)) };
    }
    async Task<JsonNode> TaskFor(Setup s, string name = "Column date task", decimal? hours = 0) => await data.NewTask(s.Project.Id,
        extra: new { name, deliverableId = s.Deliverable, assigneeId = data.User(TestData.Alex), reviewerId = data.User(TestData.Marc), estimatedHours = hours, requiresReview = false });
    async Task Issue(Setup s, Guid id)
    {
        await Post(TestData.Marc, PackagePath(s, id) + "/start", new SubmissionEndpoints.StartBody(Guid.NewGuid(), Version<SubmissionPackage>(id), null));
        var detail = await Get(PackagePath(s, id), TestData.Marc);
        Assert.True(detail["readiness"]!["ready"]!.GetValue<bool>());
        await Post(TestData.Pm, PackagePath(s, id) + "/issue", new SubmissionEndpoints.IssueBody(Guid.NewGuid(), Version<SubmissionPackage>(id), 1,
            detail["readiness"]!.S("fingerprint"), "Municipality", $"https://example.test/{id}/transmittal", null));
    }

    [Fact]
    public async Task Three_projects_twelve_normal_tasks_keep_same_named_deliverables_separate_with_column_facts()
    {
        var setups = new[] { await New(), await New(), await New() }; var ids = new List<Guid>();
        foreach (var s in setups) for (var i = 0; i < 4; i++) ids.Add((await TaskFor(s, $"Column task {i + 1}", i == 0 ? 0 : i)).G("id"));
        await Flag("work_screens", true);
        var page = await Get($"/api/v1/tasks?projects={string.Join(',', setups.Select(s => s.Project.Id))}&group=deliverable&sort=key:asc");
        Assert.Equal(12, page.I("totalCount")); Assert.Equal(ids.Order(), page["items"]!.AsArray().Select(row => row!.G("id")).Order());
        var groups = page["groups"]!.AsArray(); Assert.Equal(3, groups.Count(g => g!.S("dimension") == "project"));
        var leaves = groups.Where(g => g!.S("dimension") == "deliverable").ToArray(); Assert.Equal(3, leaves.Length);
        Assert.Equal(3, leaves.Select(g => g!.S("key")).Distinct().Count()); Assert.All(leaves, g => Assert.Equal(4, g!.I("rowCount")));
        foreach (var row in page["items"]!.AsArray())
        {
            Date(row!, "2027-02-11"); Assert.Equal("Same deliverable label", row!.S("deliverableName"));
            Assert.Equal(data.User(TestData.Marc), row.G("reviewerId")); Assert.Equal("Marc Dubois", row.S("reviewerName"));
            Assert.NotNull(row["estimatedHours"]); Assert.NotNull(row["lastActivityAt"]);
        }
        Assert.Equal(3, page["items"]!.AsArray().Count(row => row!["estimatedHours"]!.GetValue<decimal>() == 0));
    }

    [Fact]
    public async Task Earliest_current_manifest_date_changes_after_normal_edit_replacement_and_cancellation()
    {
        var s = await New(); var second = await Package(s, new(2027, 2, 13)); var task = await TaskFor(s); var id = task.G("id");
        await Flag("work_screens", true); var path = Root(s) + "/tasks";
        Date(Row(await Get(path), id), "2027-02-11");
        await Post(TestData.Marc, PackagePath(s) + "/edit", new SubmissionEndpoints.EditBody(Guid.NewGuid(), Version<SubmissionPackage>(s.Package),
            "Column date package", "Synthetic permit issue", "Municipality", s.Milestone, new(2027, 2, 15), "Correct the synthetic target"));
        Date(Row(await Get(path), id), "2027-02-13");
        var other = await Document(s.Project); var otherRevision = await Revision(s.Project, other);
        await Post(TestData.Marc, PackagePath(s, second) + "/manifest", new SubmissionEndpoints.ManifestBody(Guid.NewGuid(), Version<SubmissionPackage>(second),
            [new(otherRevision)], "Replace current scope with another deliverable"));
        Assert.Equal(2, f.Db(db => db.SubmissionPackages.Single(p => p.Id == second).ManifestVersion));
        Date(Row(await Get(path), id), "2027-02-15"); // The old02-13 manifest still exists, but no longer holds this task's deliverable.
        await Post(TestData.Pm, PackagePath(s) + "/cancel", new SubmissionEndpoints.CancelBody(Guid.NewGuid(), Version<SubmissionPackage>(s.Package), "Cancel this synthetic package"));
        Date(Row(await Get(path), id), null);
    }

    [Fact]
    public async Task Normally_issued_superseded_and_cancelled_packages_never_supply_the_date()
    {
        var s = await New(new(2027, 2, 7)); await Issue(s, s.Package);
        var replacement = await Package(s, new(2027, 2, 8), s.Package); await Issue(s, replacement);
        Assert.Equal(SubmissionStatus.Superseded, f.Db(db => db.SubmissionPackages.Single(p => p.Id == s.Package).Status));
        Assert.Equal(SubmissionStatus.Issued, f.Db(db => db.SubmissionPackages.Single(p => p.Id == replacement).Status));
        var cancelled = await Package(s, new(2027, 2, 9));
        await Post(TestData.Pm, PackagePath(s, cancelled) + "/cancel", new SubmissionEndpoints.CancelBody(Guid.NewGuid(), Version<SubmissionPackage>(cancelled), "Cancel synthetic early issue"));
        await Package(s, new(2027, 2, 13)); var task = await TaskFor(s); await Flag("work_screens", true);
        Date(Row(await Get(Root(s) + "/tasks"), task.G("id")), "2027-02-13");
    }

    [Fact]
    public async Task Optional_manifest_membership_counts_but_deleted_and_foreign_import_references_do_not()
    {
        var s = await New(); var task = await TaskFor(s); var id = task.G("id");
        var optional = await Package(s, new(2027, 2, 10)); var foreign = await New(new(2027, 2, 6));
        // Recovery fixture: the current command creates required manifest items only. Required=false is existing stored schema, not a newly invented command.
        await f.DbAsync(async db => { (await db.SubmissionManifestItems.SingleAsync(m => m.PackageId == optional)).Required = false; return await db.SaveChangesAsync(); });
        await Flag("work_screens", true); Date(Row(await Get(Root(s) + "/tasks"), id), "2027-02-10");
        // Explicit damaged-import fixture with real FK targets: another project's package must not become an eligible package for this task.
        await f.DbAsync(async db => { (await db.SubmissionManifestItems.SingleAsync(m => m.PackageId == foreign.Package)).DeliverableId = s.Deliverable; return await db.SaveChangesAsync(); });
        Date(Row(await Get(Root(s) + "/tasks"), id), "2027-02-10");
        await f.DbAsync(async db => { (await db.SubmissionPackages.SingleAsync(p => p.Id == optional)).DeletedAt = f.Clock.Now; return await db.SaveChangesAsync(); });
        Date(Row(await Get(Root(s) + "/tasks"), id), "2027-02-11");
        // Manifest records are not soft-deletable: this is a missing-membership recovery fixture, not a fictitious DeletedAt flag.
        await f.DbAsync(db => db.SubmissionManifestItems.Where(m => m.PackageId == s.Package && m.ManifestVersion == 1).ExecuteDeleteAsync());
        Date(Row(await Get(Root(s) + "/tasks"), id), null);
    }

    [Fact]
    public async Task Known_no_package_null_deliverable_and_unavailable_recorded_reference_remain_distinct_without_get_writes()
    {
        var s = await New(); var damaged = await TaskFor(s, "Damaged reference"); var other = await Document(s.Project);
        var known = await data.NewTask(s.Project.Id, extra: new { name = "No package", deliverableId = other, assigneeId = data.User(TestData.Alex) });
        var none = await data.NewTask(s.Project.Id, extra: new { name = "No deliverable", assigneeId = data.User(TestData.Alex) });
        // Damaged-import recovery only: ordinary deletion refuses referenced deliverables.
        await f.DbAsync(async db => { (await db.Deliverables.SingleAsync(d => d.Id == s.Deliverable)).DeletedAt = f.Clock.Now; return await db.SaveChangesAsync(); });
        await Flag("work_screens", true);
        string Stored() => f.Db(db => JsonSerializer.Serialize(new {
            Tasks = db.Tasks.AsNoTracking().Where(t => t.ProjectId == s.Project.Id).OrderBy(t => t.Id).Select(t => new { t.Id, t.RowVersion, t.Status, t.DeliverableId }).ToArray(),
            Packages = db.SubmissionPackages.AsNoTracking().IgnoreQueryFilters().Where(p => p.ProjectId == s.Project.Id).Select(p => new { p.Id, p.RowVersion, p.ManifestVersion }).ToArray(),
            Activity = db.ActivityLog.Count(log => log.ProjectId == s.Project.Id), State = db.TaskStates.Count(state => db.Tasks.Any(t => t.Id == state.TaskId && t.ProjectId == s.Project.Id))
        }));
        var before = Stored(); var page = await Get(Root(s) + "/tasks?group=deliverable");
        Date(Row(page, known.G("id")), null); Date(Row(page, none.G("id")), null);
        var unknown = Row(page, damaged.G("id"))["submissionDate"]!; Assert.Equal("Unavailable", unknown.S("state")); Assert.Null(unknown["targetDate"]);
        Assert.Equal(before, Stored());
        var hidden = await New(); var alex = data.User(TestData.Alex);
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == hidden.Project.Id)).Visibility = Visibility.Restricted;
            (await db.ProjectMembers.SingleAsync(m => m.ProjectId == hidden.Project.Id && m.UserId == alex)).RemovedAt = f.Clock.Now; return await db.SaveChangesAsync(); });
        var scoped = await Get($"/api/v1/tasks?projects={s.Project.Id},{hidden.Project.Id}"); Assert.DoesNotContain(hidden.Project.ProjectNumber, scoped.ToJsonString());
        await (await f.As(TestData.Alex).GetAsync(Root(hidden) + "/tasks")).Json(404);
    }

    [Fact]
    public async Task Two_hundred_and_one_command_created_tasks_keep_page_membership_and_date_facts_on_three_pages()
    {
        var s = await New(); var ids = new List<Guid>();
        for (var i = 0; i < 201; i++) ids.Add((await TaskFor(s, $"Date paging task {i + 1}", 1)).G("id"));
        await Flag("work_screens", true); var seen = new List<Guid>();
        for (var page = 1; page <= 3; page++)
        {
            var result = await Get($"/api/v1/tasks?projects={s.Project.Id}&group=deliverable&sort=key:asc&pageSize=100&page={page}");
            Assert.Equal(201, result.I("totalCount")); Assert.Equal(page == 3 ? 1 : 100, result["items"]!.AsArray().Count);
            foreach (var row in result["items"]!.AsArray()) { Date(row!, "2027-02-11"); seen.Add(row!.G("id")); }
            Assert.Equal(201, Assert.Single(result["groups"]!.AsArray(), g => g!.S("dimension") == "deliverable")!.I("rowCount"));
        }
        Assert.Equal(ids, seen); Assert.Equal(201, seen.Distinct().Count());
    }

    [Fact]
    public async Task Workspace_saved_columns_preserve_identity_and_scope_while_own_views_remain_private()
    {
        var s = await New(); await Flag("work_screens", true);
        const string columns = "key,name,project,deliverable,reviewer,estimate,lastActivity,submissionDate";
        var saved = await Post(TestData.Alex, "/api/v1/views", new { name = "Five optional work columns", listType = "workspace-tasks", scope = "Personal",
            @params = new Dictionary<string, string> { ["cols"] = columns, ["projects"] = s.Project.Id.ToString(), ["group"] = "deliverable", ["sort"] = "key:asc", ["status"] = "Not Started", ["panel"] = "Task:transient" } }, 201);
        var view = Assert.Single((await Get("/api/v1/views?listType=workspace-tasks"))["views"]!.AsArray(), row => row!.G("id") == saved.G("id"))!;
        Assert.Equal(columns, view["params"]!.S("cols")); Assert.Equal(s.Project.Id.ToString(), view["params"]!.S("projects"));
        Assert.Equal("deliverable", view["params"]!.S("group")); Assert.Equal("key:asc", view["params"]!.S("sort")); Assert.Equal("Not Started", view["params"]!.S("status")); Assert.Null(view["params"]!["panel"]);
        Assert.DoesNotContain((await Get("/api/v1/views?listType=workspace-tasks", TestData.Jill))["views"]!.AsArray(), row => row!.G("id") == saved.G("id"));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Date_projection_is_work_list_only_and_off_wire_remains_unchanged(bool work, bool small)
    {
        var s = await New(); var task = await TaskFor(s); var id = task.G("id");
        await Flag("small_ux", small); var before = (await Get(Root(s) + "/tasks")).ToJsonString(); await Flag("work_screens", work);
        foreach (var path in new[] { Root(s) + "/tasks", $"/api/v1/tasks?projects={s.Project.Id}" })
        {
            var row = Row(await Get(path), id); if (work) Date(row, "2027-02-11"); else Assert.False(row.AsObject().ContainsKey("submissionDate"));
        }
        var mine = await Get("/api/v1/me/work");
        Assert.False(Assert.Single(mine["tasks"]!.AsArray(), row => row!.G("id") == id)!.AsObject().ContainsKey("submissionDate"));
        await Flag("work_screens", false); Assert.Equal(before, (await Get(Root(s) + "/tasks")).ToJsonString());
    }
}
