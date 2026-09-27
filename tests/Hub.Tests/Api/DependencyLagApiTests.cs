using System.Net;
using System.Text.Json.Nodes;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 021: explicit deliverable dependencies with lag (US1, US2) and office holiday calendars with working days (US3).
[Collection("api")]
public sealed class DependencyLagApiTests(HubFactory f)
{
    readonly TestData d = new(f);

    async Task<JsonNode> Deliverable(Guid projectId, string name, string discipline, string? start = null, string? due = null) =>
        await f.As(TestData.Pm).Post($"/api/v1/projects/{projectId}/deliverables", new
        {
            name, projectDisciplineId = d.ProjectDiscipline(projectId, discipline), deliverableTypeId = await d.DeliverableType(), startDate = start, dueDate = due,
        }).Result.Json(201);

    Task<JsonNode> Detail(JsonNode del) => f.As(TestData.Pm).GetAsync($"/api/v1/deliverables/{del.S("id")}").Result.Json();

    [Fact]
    public async Task Leads_link_deliverables_directly_with_a_lag_and_loops_are_refused() // US1, US2, FR-001..FR-003, D-03
    {
        var p = await d.Project();
        var geo = await Deliverable(p.Id, "Geotechnical Investigation Report", "Electrical", due: "2026-09-30");
        var civil = await Deliverable(p.Id, "85% Civil Drawing Package", "Civil", start: "2026-09-10", due: "2026-11-30");
        var marc = f.As(TestData.Marc); // Civil lead: the successor's discipline
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).Post($"/api/v1/deliverables/{civil.S("id")}/dependencies", new { predecessorDeliverableId = geo.G("id") })).StatusCode);
        var link = await marc.Post($"/api/v1/deliverables/{civil.S("id")}/dependencies", new { predecessorDeliverableId = geo.G("id"), lagDays = 10, note = "Client review" }).Result.Json(201);
        Assert.Equal(HttpStatusCode.Conflict, (await marc.Post($"/api/v1/deliverables/{civil.S("id")}/dependencies", new { predecessorDeliverableId = geo.G("id") })).StatusCode);
        var loop = await marc.Post($"/api/v1/deliverables/{civil.S("id")}/dependencies", new { successorDeliverableId = geo.G("id") }); // civil → geo would close the loop
        Assert.Equal(HttpStatusCode.Conflict, loop.StatusCode);
        var path = (await loop.Json(409))["cyclePath"]!.AsArray().Select(x => x!.GetValue<string>()).ToList();
        Assert.Contains(geo.S("key"), path);
        Assert.Contains(civil.S("key"), path);
        Assert.Equal(HttpStatusCode.BadRequest, (await marc.Post($"/api/v1/deliverables/{civil.S("id")}/dependencies", new { predecessorDeliverableId = geo.G("id"), successorDeliverableId = geo.G("id") })).StatusCode);
        var other = await d.Project();
        var elsewhere = await Deliverable(other.Id, "Other project package", "Civil");
        Assert.Equal(422, (int)(await marc.Post($"/api/v1/deliverables/{civil.S("id")}/dependencies", new { predecessorDeliverableId = elsewhere.G("id") })).StatusCode);

        var succ = await Detail(civil);
        var pred = await Detail(geo);
        var explicitPred = succ["explicitPredecessors"]!.AsArray().Single()!;
        Assert.Equal((geo.S("key"), 10, "Client review"), (explicitPred.S("key"), explicitPred.I("lagDays"), explicitPred.S("note")));
        Assert.Equal(civil.S("key"), pred["explicitSuccessors"]!.AsArray().Single()!.S("key"));
        Assert.Empty(succ["derivedPredecessors"]!.AsArray()); // explicit and derived stay apart
        Assert.True(succ["permissions"]!["linkDeliverables"]!.GetValue<bool>());

        await f.Evaluate(p.Id); // the successor has started: blocked by the unissued predecessor, which is Blocking Others
        var rows = (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/deliverables").Result.Json()).AsArray();
        var cRow = rows.Single(x => x!.S("id") == civil.S("id"))!;
        var gRow = rows.Single(x => x!.S("id") == geo.S("id"))!;
        Assert.True(cRow["state"]!["isBlocked"]!.GetValue<bool>());
        Assert.Contains(geo.S("key"), cRow["state"]!["blockedBy"]!.ToJsonString());
        Assert.Equal(1, gRow["state"]!["blockingCount"]!.GetValue<int>());

        Assert.Equal(HttpStatusCode.NoContent, (await marc.DeleteAsync($"/api/v1/deliverable-dependencies/{link.S("id")}")).StatusCode);
        Assert.Empty((await Detail(civil))["explicitPredecessors"]!.AsArray());
        Assert.Equal(2, await f.DbAsync(db => db.ActivityLog.CountAsync(a => a.ProjectId == p.Id && a.Categories.Contains("dependency"))));
    }

    [Fact]
    public async Task An_office_holiday_counts_once_working_days_are_on() // US3-1..2, FR-004, FR-005
    {
        var admin = f.As(TestData.Admin);
        var p = await d.Project();
        var office = await d.Office();
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Pm).Post("/api/v1/admin/holidays", new { officeId = office, date = "2026-10-12", name = "Thanksgiving" })).StatusCode);
        var holiday = await admin.Post("/api/v1/admin/holidays", new { officeId = office, date = "2026-10-12", name = "Thanksgiving" }).Result.Json(201);
        Assert.Equal(HttpStatusCode.Conflict, (await admin.Post("/api/v1/admin/holidays", new { officeId = office, date = "2026-10-12", name = "Again" })).StatusCode);
        var list = (await admin.GetAsync($"/api/v1/admin/holidays?officeId={office}&year=2026").Result.Json()).AsArray();
        Assert.Contains(list, x => x!.S("name") == "Thanksgiving" && x.S("date") == "2026-10-12");

        var task = await d.NewTask(p.Id, TestData.Marc, new { name = "Pavement memo", dueDate = "2026-10-13" }); // due Tuesday
        var saved = f.Clock.Now;
        await admin.Put("/api/v1/admin/settings/task_due_soon_days", new { value = 1 }).Result.Json();
        await admin.Put("/api/v1/admin/settings/working_days_enabled", new { value = true }).Result.Json();
        try
        {
            f.Clock.SetDate(2026, 10, 9); // Friday
            Task<bool> DueSoon() => f.DbAsync(db => db.TaskStates.Where(x => x.TaskId == task.G("id")).Select(x => x.IsDueSoon).FirstAsync());
            await f.Evaluate(p.Id);
            Assert.True(await DueSoon()); // Friday → Tuesday is one working day: the weekend and the holiday are skipped
            (await admin.DeleteAsync($"/api/v1/admin/holidays/{holiday.S("id")}")).EnsureSuccessStatusCode();
            await f.Evaluate(p.Id);
            Assert.False(await DueSoon()); // without the holiday Monday counts: two working days
        }
        finally
        {
            f.Clock.Now = saved;
            await admin.Put("/api/v1/admin/settings/working_days_enabled", new { value = false }).Result.Json();
            await admin.Put("/api/v1/admin/settings/task_due_soon_days", new { value = 5 }).Result.Json(); // the default
            await f.DbAsync(db => db.Holidays.Where(h => h.OfficeId == office && h.Date == new DateOnly(2026, 10, 12)).ExecuteDeleteAsync());
        }
    }
}
