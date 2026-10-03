using System.Net;
using System.Text.Json.Nodes;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 003: AC-MS-04..06, AC-PERM-01/02, AC-DEL-01, AC-DEL-03..05, M-05, M-09, DL-03, DL-09, DL-10, E-24.
[Collection("api")]
public sealed class MilestonesDeliverablesTests(HubFactory f)
{
    readonly TestData d = new(f);

    async Task<JsonNode> Milestone(Guid projectId, string date, string type = "Design Submission", string name = "60% Design Submission") =>
        await f.As(TestData.Pm).Post($"/api/v1/projects/{projectId}/milestones", new { name, milestoneType = type, date }).Result.Json(201);

    async Task<JsonNode> Deliverable(Guid projectId, string as_ = TestData.Marc, object? extra = null, string discipline = "Civil")
    {
        var body = new Dictionary<string, object?> { ["name"] = "Civil package " + Guid.NewGuid().ToString("N")[..4], ["projectDisciplineId"] = d.ProjectDiscipline(projectId, discipline),
            ["deliverableTypeId"] = await d.DeliverableType() };
        if (extra is not null) foreach (var p in extra.GetType().GetProperties()) body[p.Name] = p.GetValue(extra);
        return await f.As(as_).Post($"/api/v1/projects/{projectId}/deliverables", body).Result.Json(201);
    }

    [Fact]
    public async Task A_milestone_s_discipline_must_belong_to_its_project()
    {
        var p = await d.Project();
        var other = await d.Project();
        var m = await Milestone(p.Id, "2027-03-01");
        var refused = await f.As(TestData.Pm).Patch($"/api/v1/milestones/{m.S("id")}", new { projectDisciplineId = d.ProjectDiscipline(other.Id, "Civil") }, m.I("rowVersion"));
        Assert.Equal(HttpStatusCode.BadRequest, refused.StatusCode);
        Assert.Null(await f.DbAsync(db => db.Milestones.Where(x => x.Id == m.G("id")).Select(x => x.ProjectDisciplineId).SingleAsync()));
        (await f.As(TestData.Pm).Patch($"/api/v1/milestones/{m.S("id")}", new { projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil") }, m.I("rowVersion"))).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Milestone_date_change_keeps_original_logs_notifies_and_cascades() // AC-MS-04, AC-MS-05
    {
        var p = await d.Project();
        var m = await Milestone(p.Id, "2027-01-29");
        var del = await Deliverable(p.Id, extra: new { milestoneId = m.G("id") });
        var preview = await f.As(TestData.Pm).Post($"/api/v1/milestones/{m.S("id")}/change-date", new { newDate = "2027-02-12", cascadeDeliverables = true, dryRun = true }).Result.Json();
        Assert.Equal(14, preview["milestone"]!.I("delta"));
        Assert.Equal("2027-02-12", preview["deliverables"]![0]!.S("newDue"));
        var noReason = await f.As(TestData.Pm).Post($"/api/v1/milestones/{m.S("id")}/change-date", new { newDate = "2027-02-12", rowVersion = m.I("rowVersion") });
        Assert.Equal(HttpStatusCode.BadRequest, noReason.StatusCode);
        await f.As(TestData.Pm).Post($"/api/v1/milestones/{m.S("id")}/change-date", new { newDate = "2027-02-12", reason = "Client extension", cascadeDeliverables = true, rowVersion = m.I("rowVersion") }).Result.Json();
        var (ms, dl, notified, logs) = await f.DbAsync(async db => (
            await db.Milestones.FirstAsync(x => x.Id == m.G("id")),
            await db.Deliverables.FirstAsync(x => x.Id == del.G("id")),
            await db.Notifications.Where(n => n.ItemId == m.G("id") && n.EventType == NotificationEvents.MilestoneDateChanged).Select(n => n.UserId).ToListAsync(),
            await db.ActivityLog.Where(a => a.ItemId == del.G("id") && a.Action == "Cascade").CountAsync()));
        Assert.Equal(new DateOnly(2027, 1, 29), ms.OriginalDate);
        Assert.Equal(new DateOnly(2027, 2, 12), ms.Date);
        Assert.Equal(new DateOnly(2027, 2, 12), dl.DueDate);
        Assert.Contains(d.User(TestData.Marc), notified);
        Assert.Contains(d.User(TestData.Omar), notified);
        Assert.Equal(1, logs);
        var dlAttempt = await f.As(TestData.Marc).Post($"/api/v1/milestones/{m.S("id")}/change-date", new { newDate = "2027-03-01", reason = "Trying it", rowVersion = ms.RowVersion });
        Assert.Contains("Project Manager", (await dlAttempt.Json(403)).S("detail")); // AC-PERM-01
    }

    [Fact]
    public async Task Completing_with_unissued_deliverables_needs_confirmation() // AC-MS-06, M-05, M-06, M-09
    {
        var p = await d.Project();
        var far = await Milestone(p.Id, "2027-06-01");
        var early = await f.As(TestData.Pm).Post($"/api/v1/milestones/{far.S("id")}/complete", new { rowVersion = far.I("rowVersion") });
        Assert.Equal("too_early", (await early.Json(422)).S("code"));
        var m = await Milestone(p.Id, "2026-09-16", name: "30% Submission");
        var del = await Deliverable(p.Id, extra: new { milestoneId = m.G("id") });
        var need = await f.As(TestData.Pm).Post($"/api/v1/milestones/{m.S("id")}/complete", new { rowVersion = m.I("rowVersion") });
        var body = await need.Json(409);
        Assert.Equal("confirm_open_deliverables", body.S("code"));
        Assert.Equal(del.S("key"), body["deliverables"]![0]!.S("key"));
        var done = await f.As(TestData.Pm).Post($"/api/v1/milestones/{m.S("id")}/complete", new { confirm = true, rowVersion = m.I("rowVersion") }).Result.Json();
        var saved = await f.DbAsync(db => db.Milestones.FirstAsync(x => x.Id == m.G("id")));
        Assert.True(saved.IsComplete);
        Assert.Equal(new DateOnly(2026, 9, 14), saved.CompletedDate);
        Assert.Equal("Not Started", f.Db(db => db.Deliverables.First(x => x.Id == del.G("id")).Status));
        Assert.NotNull(done);
    }

    [Fact]
    public async Task Deliverable_defaults_and_discipline_permissions() // AC-DEL-01, AC-PERM-02, DL-01, DL-03
    {
        var p = await d.Project();
        var m = await Milestone(p.Id, "2027-01-29");
        var created = await Deliverable(p.Id, extra: new { milestoneId = m.G("id") });
        Assert.Equal("2027-01-29", created.S("dueDate"));
        var row = await f.DbAsync(db => db.Deliverables.FirstAsync(x => x.Id == created.G("id")));
        Assert.Equal(d.User(TestData.Marc), row.OwnerId); // owner defaults to the discipline lead
        var elec = await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Lighting", projectDisciplineId = d.ProjectDiscipline(p.Id, "Electrical"), deliverableTypeId = await d.DeliverableType() });
        Assert.Equal(HttpStatusCode.Forbidden, elec.StatusCode);
        var late = await Deliverable(p.Id, extra: new { milestoneId = m.G("id"), dueDate = "2027-02-05" });
        Assert.Single(late["warnings"]!.AsArray());
        var badDates = await f.As(TestData.Marc).Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Bad", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), deliverableTypeId = await d.DeliverableType(), startDate = "2027-03-01", dueDate = "2027-02-01" });
        Assert.Equal(HttpStatusCode.BadRequest, badDates.StatusCode);
    }

    [Fact]
    public async Task Lifecycle_guards_review_comment_issue_and_reissue() // AC-DEL-03, AC-DEL-04, AC-DEL-05, E-24
    {
        var p = await d.Project();
        var del = await Deliverable(p.Id);
        var id = del.S("id");
        async Task<int> V() => await f.DbAsync(db => db.Deliverables.Where(x => x.Id == del.G("id")).Select(x => x.RowVersion).FirstAsync());
        var marc = f.As(TestData.Marc);
        await marc.Post($"/api/v1/deliverables/{id}/transition", new { toStatus = "In Progress", rowVersion = await V() }).Result.Json();
        var guard = await marc.Post($"/api/v1/deliverables/{id}/transition", new { toStatus = "Ready to Issue", rowVersion = await V() });
        Assert.Contains("requires review", (await guard.Json(422)).S("detail"));
        var noReviewer = await marc.Post($"/api/v1/deliverables/{id}/transition", new { toStatus = "In Review", rowVersion = await V() });
        Assert.Equal(HttpStatusCode.BadRequest, noReviewer.StatusCode);
        (await marc.Patch($"/api/v1/deliverables/{id}", new { reviewerId = d.User(TestData.Diane) }, await V())).EnsureSuccessStatusCode();
        await marc.Post($"/api/v1/deliverables/{id}/transition", new { toStatus = "In Review", rowVersion = await V() }).Result.Json();
        var diane = f.As(TestData.Diane);
        Assert.Equal(HttpStatusCode.BadRequest, (await diane.Post($"/api/v1/deliverables/{id}/transition", new { toStatus = "Revision Required", rowVersion = await V() })).StatusCode);
        await diane.Post($"/api/v1/deliverables/{id}/transition", new { toStatus = "Revision Required", comment = "Profiles do not match plan", rowVersion = await V() }).Result.Json();
        var review = await f.DbAsync(db => db.Comments.FirstAsync(c => c.ItemId == del.G("id") && c.CommentKind == CommentKind.Review));
        Assert.Equal("Profiles do not match plan", review.Body);
        Assert.True(await f.DbAsync(db => db.Notifications.AnyAsync(n => n.ItemId == del.G("id") && n.UserId == d.User(TestData.Marc) && n.EventType == NotificationEvents.ReviewOutcome)));
        await marc.Post($"/api/v1/deliverables/{id}/transition", new { toStatus = "In Progress", rowVersion = await V() }).Result.Json();
        await marc.Post($"/api/v1/deliverables/{id}/transition", new { toStatus = "In Review", rowVersion = await V() }).Result.Json();
        await diane.Post($"/api/v1/deliverables/{id}/transition", new { toStatus = "Ready to Issue", rowVersion = await V() }).Result.Json();

        await f.DbAsync(async db =>
        {
            var pr = await db.Projects.FirstAsync(x => x.Id == p.Id);
            db.Tasks.Add(new Hub.Api.Data.WorkTask { ProjectId = p.Id, Seq = 9000, Key = pr.ProjectNumber + "-T9000", Name = "File record copy", ProjectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), DeliverableId = del.G("id") });
            return await db.SaveChangesAsync();
        });
        var confirm = await marc.Post($"/api/v1/deliverables/{id}/issue", new { revision = "Rev A", issuedTo = "City of X", rowVersion = await V() });
        Assert.Equal("confirm_open_tasks", (await confirm.Json(409)).S("code"));
        await marc.Post($"/api/v1/deliverables/{id}/issue", new { revision = "Rev A", issuedTo = "City of X", confirmOpenTasks = true, rowVersion = await V() }).Result.Json();
        await marc.Post($"/api/v1/deliverables/{id}/transition", new { toStatus = "Revision Required", comment = "Client comments received", rowVersion = await V() }).Result.Json();
        var afterReturn = await f.DbAsync(db => db.Deliverables.FirstAsync(x => x.Id == del.G("id")));
        Assert.Equal("Rev A", afterReturn.Revision); // E-24: issued date and revision kept
        Assert.NotNull(afterReturn.IssuedDate);
        Assert.Equal(1, await f.DbAsync(db => db.DeliverableIssues.CountAsync(i => i.DeliverableId == del.G("id"))));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await marc.DeleteAsync($"/api/v1/deliverables/{id}")).StatusCode); // DL-09
    }

    [Fact]
    public async Task Changing_discipline_moves_tasks_after_confirmation() // DL-10
    {
        var p = await d.Project();
        var del = await Deliverable(p.Id, as_: TestData.Pm);
        await f.DbAsync(async db =>
        {
            var pr = await db.Projects.FirstAsync(x => x.Id == p.Id);
            db.Tasks.Add(new Hub.Api.Data.WorkTask { ProjectId = p.Id, Seq = 9001, Key = pr.ProjectNumber + "-T9001", Name = "Grading", ProjectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"), DeliverableId = del.G("id") });
            return await db.SaveChangesAsync();
        });
        var elec = d.ProjectDiscipline(p.Id, "Electrical");
        var ask = await f.As(TestData.Pm).Patch($"/api/v1/deliverables/{del.S("id")}", new { projectDisciplineId = elec }, del.I("rowVersion"));
        Assert.Equal("confirm_move_tasks", (await ask.Json(409)).S("code"));
        (await f.As(TestData.Pm).Patch($"/api/v1/deliverables/{del.S("id")}", new { projectDisciplineId = elec, confirmMoveTasks = true }, del.I("rowVersion"))).EnsureSuccessStatusCode();
        Assert.Equal(elec, f.Db(db => db.Tasks.First(t => t.DeliverableId == del.G("id")).ProjectDisciplineId));
    }
}
