using System.Net;
using System.Text.Json.Nodes;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// S37 (FR-SCR-65, AC-SCR-65, 053-D13): with flag.register_screens on, a milestone or deliverable date move records an organisation
/// reason category beside its free-text reason, on the move's own activity entry; "Why dates moved" counts it. A first date is not a move.
public sealed class DateReasonTests : IDisposable
{
    readonly HubFactory f = new(); readonly TestData d;
    public DateReasonTests() { d = new(f); }
    public void Dispose() => f.Dispose();
    async Task Flag(bool on) => await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.register_screens", new { value = on })).Json();
    Guid Category(string name) => f.Db(db => db.DateReasonCategories.Single(c => c.Name == name).Id);
    async Task<JsonNode> Milestone(Guid project, string date = "2026-10-05") =>
        await (await f.As(TestData.Pm).Post($"/api/v1/projects/{project}/milestones", new { name = "Synthetic submission", milestoneType = "Design Submission", date })).Json(201);
    int Version(Guid id) => f.Db(db => db.Milestones.Single(m => m.Id == id).RowVersion);

    [Fact]
    public async Task A_milestone_move_holds_its_category_and_reason_and_why_dates_moved_counts_it()
    {
        await Flag(true); var p = await d.Project(); var m = await Milestone(p.Id); var id = m.G("id");
        var url = $"/api/v1/milestones/{id}/change-date";
        // The seven 053-D13 defaults are seeded; a move without one is refused, an unknown one too.
        Assert.Equal(7, f.Db(db => db.DateReasonCategories.Count(c => c.IsActive)));
        var missing = await f.As(TestData.Pm).Post(url, new { newDate = "2026-10-12", reason = "Waiting on survey", rowVersion = Version(id) });
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode); Assert.Contains("dateReasonCategoryId", await missing.Content.ReadAsStringAsync());
        var unknown = await f.As(TestData.Pm).Post(url, new { newDate = "2026-10-12", reason = "Waiting on survey", rowVersion = Version(id), dateReasonCategoryId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.BadRequest, unknown.StatusCode);

        var waiting = Category("Waiting on input");
        await (await f.As(TestData.Pm).Post(url, new { newDate = "2026-10-12", reason = "Waiting on survey", rowVersion = Version(id), dateReasonCategoryId = waiting })).Json();
        var entry = f.Db(db => db.ActivityLog.AsNoTracking().Where(a => a.ItemId == id && a.Categories.Contains("date")).OrderByDescending(a => a.OccurredAt).First());
        Assert.Equal("Waiting on survey", entry.Reason); Assert.Contains("dateReason:" + waiting.ToString("D"), entry.Categories);

        var summary = await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/milestones/date-reasons")).Json();
        Assert.Equal(1, summary.I("moves")); Assert.Equal(0, summary.I("notRecorded"));
        var row = Assert.Single(summary["reasons"]!.AsArray())!;
        Assert.Equal("Waiting on input", row.S("name")); Assert.Equal(1, row.I("count"));
        // The count opens the activity list filtered to exactly its entries.
        var filtered = await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/activity?category={Uri.EscapeDataString(row.S("activityCategory"))}")).Json();
        Assert.Equal(id, Assert.Single(filtered["items"]!.AsArray())!.G("itemId"));
    }

    [Fact]
    public async Task Deliverable_moves_need_a_category_but_first_dates_and_the_flag_off_do_not()
    {
        var p = await d.Project();
        var created = await (await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/deliverables", new { name = "Synthetic drawing", projectDisciplineId = d.ProjectDiscipline(p.Id, "Civil"),
            deliverableTypeId = await d.DeliverableType(), requiresReview = false })).Json(201);
        var did = created.G("id"); int V() => f.Db(db => db.Deliverables.Single(x => x.Id == did).RowVersion);
        // Flag off: unchanged behaviour.
        await (await f.As(TestData.Pm).Patch($"/api/v1/deliverables/{did}", new { dueDate = "2026-10-01" }, V())).Json();
        await (await f.As(TestData.Pm).Patch($"/api/v1/deliverables/{did}", new { dueDate = "2026-10-02" }, V())).Json();
        await Flag(true);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Pm).Patch($"/api/v1/deliverables/{did}", new { dueDate = "2026-10-09" }, V())).StatusCode);
        await (await f.As(TestData.Pm).Patch($"/api/v1/deliverables/{did}", new { dueDate = "2026-10-09", dateReasonCategoryId = Category("Client request") }, V())).Json();
        // A first start date is not a move; neither is an unchanged date.
        await (await f.As(TestData.Pm).Patch($"/api/v1/deliverables/{did}", new { startDate = "2026-09-20", dueDate = "2026-10-09" }, V())).Json();
        // A bulk shift is a move of each deliverable.
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/deliverables/bulk", new { ids = new[] { did }, operation = "shiftDueDates", @params = new { days = 2 }, reason = "Client asked for two days" })).StatusCode);
        await (await f.As(TestData.Pm).Post($"/api/v1/projects/{p.Id}/deliverables/bulk", new { ids = new[] { did }, operation = "shiftDueDates", @params = new { days = 2, dateReasonCategoryId = Category("Client request") }, reason = "Client asked for two days" })).Json();

        var summary = await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/milestones/date-reasons")).Json();
        Assert.Equal(3, summary.I("moves")); Assert.Equal(1, summary.I("notRecorded")); // the flag-off move has none
        Assert.Equal(2, Assert.Single(summary["reasons"]!.AsArray())!.I("count"));

        // With every category deactivated a move is never blocked.
        await f.DbAsync(async db => { foreach (var c in db.DateReasonCategories) c.IsActive = false; return await db.SaveChangesAsync(); });
        await (await f.As(TestData.Pm).Patch($"/api/v1/deliverables/{did}", new { dueDate = "2026-10-20" }, V())).Json();
    }
}
