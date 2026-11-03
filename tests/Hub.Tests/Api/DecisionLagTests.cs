using System.Text.Json;
using Hub.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

public sealed class DecisionLagTests : IDisposable
{
    readonly HubFactory f = new(); readonly TestData d;
    public DecisionLagTests() { d = new(f); f.Clock.Now = new(2026, 11, 14, 14, 0, 0, TimeSpan.Zero); }
    public void Dispose() => f.Dispose();
    async Task Flag(bool value) => await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.register_screens", new { value })).Json();
    async Task<Guid> Party(Guid project, string? organisation) => (await (await f.As(TestData.Pm).Post($"/api/v1/projects/{project}/external-parties", new { name = "Synthetic contact", organisation, isClient = true })).Json(201)).G("id");
    async Task<Guid> Decision(Guid project, int days, Guid? party = null)
    {
        var id = (await (await f.As(TestData.Pm).Post($"/api/v1/projects/{project}/decisions", new { subject = "Synthetic lag", description = "Synthetic context", ownerUserId = party is null ? (Guid?)d.User(TestData.Alex) : null, ownerExternalPartyId = party,
            dateRequested = "2026-11-01", requiredByDate = "2026-11-20", impactLevel = "Low", impactDescription = "Synthetic impact" })).Json(201)).G("id");
        var version = f.Db(db => db.Decisions.Single(row => row.Id == id).RowVersion);
        await (await f.As(TestData.Pm).Post($"/api/v1/decisions/{id}/transition", new { toStatus = "Decided", decisionText = "Synthetic recorded outcome", decisionDate = new DateOnly(2026, 11, 1).AddDays(days), rowVersion = version })).Json(); return id;
    }
    string State() => f.Db(db => JsonSerializer.Serialize(new { Rows = db.Decisions.AsNoTracking().OrderBy(row => row.Id).Select(row => new { row.Id, row.RowVersion, row.Status, row.DecisionDate, row.OwnerUserId, row.OwnerExternalPartyId }).ToArray(), Activity = db.ActivityLog.Count(), Notifications = db.Notifications.Count(), Receipts = db.Idempotency.Count() }));

    [Fact]
    public async Task AC_SCR_69_Client_four_ten_twelve_is_ten_by_real_organisation_never_recorder_or_contact()
    {
        await Flag(true); var p = await d.Project(); var first = await Party(p.Id, " Synthetic client organisation "); var second = await Party(p.Id, "Synthetic client organisation");
        var reopened = await Decision(p.Id, 4, first); await Decision(p.Id, 10, second); await Decision(p.Id, 12, first);
        await Decision(p.Id, 4); await Decision(p.Id, 11); await Decision(p.Id, 0, await Party(p.Id, "Internal"));
        var before = State(); var read = await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/decisions/screen")).Json(); Assert.Equal(before, State());
        var lag = read["lag"]!; Assert.Equal(6, lag.I("total")); Assert.Equal(6, lag.I("attributed")); Assert.Equal(0, lag.I("invalidDateCount")); Assert.Equal(0, lag.I("unattributedCount"));
        var groups = lag["groups"]!.AsArray(); var client = Assert.Single(groups.Where(group => group!.S("organisation") == "Synthetic client organisation"))!;
        Assert.Equal(10m, client["medianDays"]!.GetValue<decimal>()); Assert.Equal(3, client.I("decidedCount"));
        Assert.Equal(7.5m, groups.Single(group => group!.S("ownerKind") == "Internal")!["medianDays"]!.GetValue<decimal>());
        Assert.Equal(0m, groups.Single(group => group!.S("ownerKind") == "External" && group.S("organisation") == "Internal")!["medianDays"]!.GetValue<decimal>());
        Assert.DoesNotContain("Synthetic contact", read.ToJsonString()); Assert.DoesNotContain(d.User(TestData.Pm).ToString(), read.ToJsonString());
        foreach (var query in new[] { "ownerId=" + d.User(TestData.Alex), "status=Decided", "q=Synthetic", "decidedById=" + d.User(TestData.Pm) })
            await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/decisions/screen?{query}")).Json(400);
        Assert.Equal(before, State());
        await (await f.As(TestData.Pm).Post($"/api/v1/decisions/{reopened}/transition", new { toStatus = "Pending", reason = "Synthetic reconsideration", rowVersion = f.Db(db => db.Decisions.Single(row => row.Id == reopened).RowVersion) })).Json();
        var afterReopen = await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/decisions/screen")).Json();
        var remaining = afterReopen["lag"]!["groups"]!.AsArray().Single(group => group!.S("organisation") == "Synthetic client organisation")!;
        Assert.Equal(2, remaining.I("decidedCount")); Assert.Equal(11m, remaining["medianDays"]!.GetValue<decimal>());
        await (await f.As(TestData.Pm).Post($"/api/v1/decisions/{reopened}/transition", new { toStatus = "Decided", decisionText = "Synthetic replacement outcome", decisionDate = "2026-11-14", rowVersion = f.Db(db => db.Decisions.Single(row => row.Id == reopened).RowVersion) })).Json();
        var partyVersion = f.Db(db => db.ExternalParties.Single(row => row.Id == first).RowVersion);
        await (await f.As(TestData.Pm).Patch($"/api/v1/external-parties/{first}", new { organisation = "Synthetic renamed organisation" }, partyVersion)).Json();
        var renamed = await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/decisions/screen")).Json();
        var current = renamed["lag"]!["groups"]!.AsArray().Single(group => group!.S("organisation") == "Synthetic renamed organisation")!;
        Assert.Equal(2, current.I("decidedCount")); Assert.Equal(12.5m, current["medianDays"]!.GetValue<decimal>());
    }

    [Fact]
    public async Task Missing_invalid_future_foreign_and_reopened_data_remain_disjoint_unknowns_and_reads_do_not_write()
    {
        await Flag(true); var p = await d.Project(); var blank = await Party(p.Id, null);
        await Decision(p.Id, 2, blank); var invalid = await Decision(p.Id, 2, blank); var missing = await Decision(p.Id, 3); var future = await Decision(p.Id, 4);
        var other = await d.Project(); var foreign = await Party(other.Id, "Must not leak"); var corrupt = await Decision(p.Id, 1);
        await f.DbAsync(async db => { var rows = await db.Decisions.Where(row => new[] { invalid, missing, future, corrupt }.Contains(row.Id)).ToArrayAsync();
            rows.Single(row => row.Id == invalid).DecisionDate = new(2026, 10, 31); rows.Single(row => row.Id == missing).DecisionDate = null; rows.Single(row => row.Id == future).DecisionDate = new(2026, 11, 15);
            var c = rows.Single(row => row.Id == corrupt); c.OwnerUserId = null; c.OwnerExternalPartyId = foreign; return await db.SaveChangesAsync(); });
        var before = State(); var read = await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/decisions/screen")).Json(); Assert.Equal(before, State());
        Assert.Equal(5, read["lag"]!.I("total")); Assert.Equal(3, read["lag"]!.I("invalidDateCount")); Assert.Equal(2, read["lag"]!.I("unattributedCount")); Assert.Equal(0, read["lag"]!.I("attributed")); Assert.Empty(read["lag"]!["groups"]!.AsArray()); Assert.DoesNotContain("Must not leak", read.ToJsonString());
        await Flag(false); await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/decisions/screen")).Json(404);
    }

    [Fact]
    public async Task Hidden_project_summary_is_not_disclosed_to_a_nonmember()
    {
        await Flag(true); var hidden = await d.Project(tweak: body => body["members"] = Array.Empty<object>());
        await f.DbAsync(async db => { var project = await db.Projects.SingleAsync(row => row.Id == hidden.Id); project.Visibility = "Restricted"; return await db.SaveChangesAsync(); });
        await (await f.As(TestData.Alex).GetAsync($"/api/v1/projects/{hidden.Id}/decisions/screen")).Json(404);
    }

    [Fact]
    public async Task Future_date_exclusion_uses_one_organisation_today_even_when_UTC_is_already_tomorrow()
    {
        await Flag(true); var p = await d.Project(); var id = await Decision(p.Id, 4);
        await (await f.As(TestData.Admin).Put("/api/v1/admin/settings/org_time_zone", new { value = "America/Halifax" })).Json();
        f.Clock.Now = new(2026, 11, 15, 2, 0, 0, TimeSpan.Zero); // Halifax is still November14.
        await f.DbAsync(async db => { var row = await db.Decisions.SingleAsync(row => row.Id == id); row.DecisionDate = new(2026, 11, 15); return await db.SaveChangesAsync(); });
        var before = State(); var read = await (await f.As(TestData.Pm).GetAsync($"/api/v1/projects/{p.Id}/decisions/screen")).Json();
        Assert.Equal(1, read["lag"]!.I("invalidDateCount")); Assert.Equal(0, read["lag"]!.I("attributed")); Assert.Equal(before, State());
    }
}
