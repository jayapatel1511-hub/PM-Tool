using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hub.Tests.Api;

[Collection("api")]
public sealed class PlanningContractTests(HubFactory f)
{
    readonly TestData d = new(f);
    static readonly DateOnly W = new(2026, 9, 14);
    async Task<(Guid Id, string Email)> Person()
    {
        var email = $"synthetic-pln-{Guid.NewGuid():N}@hub.test";
        await (await f.As(email).GetAsync("/api/v1/me")).Json();
        var sam = d.User(TestData.Sam);
        return (await f.DbAsync(async db => { var u = await db.Users.SingleAsync(x => x.Email == email); u.SupervisorId = sam; u.WeeklyCapacityHours = 40; await db.SaveChangesAsync(); return u.Id; }), email);
    }
    async Task<JsonNode> Entry(Guid person, decimal hours = 8, string confidence = "Expected", string visibility = "Published", Guid? project = null, string? label = null, string actor = TestData.Sam)
        => await (await f.As(actor).Post("/api/v1/planning/entries", new { personId = person, hoursPerWeek = hours, startWeek = W, endWeek = W.AddDays(7), label = label ?? "Synthetic " + Guid.NewGuid().ToString("N"), sourceCategory = project == null ? "Proposal" : "MajorProject", projectId = project, confidence, visibility, notes = "Synthetic notes" })).Json(201);
    async Task<JsonNode> Grid(Guid person, string actor = TestData.Sam, string extra = "")
        => await (await f.As(actor).GetAsync($"/api/v1/planning/grid?personId={person}&from={W:yyyy-MM-dd}&weeks=2{extra}")).Json();
    static decimal N(JsonNode n, string field) => n[field]!.GetValue<decimal>();
    static JsonNode CellOf(JsonNode grid) => grid["people"]![0]!["cells"]![0]!;
    static async Task<string> ExportText(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        if (response.Content.Headers.ContentType?.MediaType == "text/csv") return await response.Content.ReadAsStringAsync();
        using var zip = new ZipArchive(new MemoryStream(await response.Content.ReadAsByteArrayAsync()));
        var parts = new List<string>();
        foreach (var entry in zip.Entries.Where(e => e.FullName.EndsWith(".xml", StringComparison.Ordinal))) { using var reader = new StreamReader(entry.Open()); parts.Add(await reader.ReadToEndAsync()); }
        return string.Join("\n", parts);
    }

    [Fact]
    public async Task Exact_bands_negative_remaining_possible_and_filters_preserve_accounting()
    {
        var (id, _) = await Person();
        await Entry(id, 30, "Confirmed", label: "Synthetic exact confirmed");
        await Entry(id, 13, "Expected", label: "Synthetic exact expected");
        await Entry(id, 8, "Possible", label: "Synthetic exact possible");
        var before = await Grid(id); var c = CellOf(before);
        Assert.Equal(40, N(c, "capacity")); Assert.Equal(30, N(c, "confirmed")); Assert.Equal(13, N(c, "expected"));
        Assert.Equal(8, N(c, "possible")); Assert.Equal(-3, N(c, "remaining")); Assert.True(c["overPlanned"]!.GetValue<bool>());
        foreach (var filter in new[] { "&confidence=Possible", "&source=Proposal", "&visibility=Published", "&q=exact%20possible" })
        {
            var filtered = await Grid(id, extra: filter); Assert.Equal(c.ToJsonString(), CellOf(filtered).ToJsonString());
            Assert.Equal(3, filtered["people"]![0]!["entries"]!.AsArray().Count);
        }
        var detail = await (await f.As(TestData.Sam).GetAsync($"/api/v1/planning/cells/{id}/{W:yyyy-MM-dd}")).Json();
        Assert.Equal(N(c, "remaining"), N(detail, "remaining")); Assert.Equal(N(c, "capacity"), N(detail["capacity"]!, "capacity"));
        foreach (var field in new[] { "confirmed", "expected", "possible" }) Assert.Equal(N(c, field), N(detail["bands"]!, field));
    }

    [Fact]
    public async Task Approved_coverage_and_person_calendar_reconcile_grid_and_explanation()
    {
        var (id, email) = await Person(); var project = await d.Project();
        var allocation = await f.DbAsync(async db => {
            var a = new ResourceAllocation { ProjectId = project.Id, PersonId = id, PlannedHours = 20, FromDate = W, ThroughDate = W.AddDays(4), Status = "Confirmed", CreatedBy = d.User(TestData.Pm), ConfirmedBy = d.User(TestData.Sam), ConfirmedAt = f.Clock.GetUtcNow() };
            db.Allocations.Add(a); db.AllocationDayOverrides.Add(new() { AllocationId = a.Id, WorkDate = W, Hours = 12 });
            await db.SaveChangesAsync(); return a.Id;
        });
        var first = await Entry(id, 16, "Confirmed", project: project.Id);
        var second = await Entry(id, 14, "Expected", project: project.Id);
        var grid = await Grid(id); var cell = CellOf(grid);
        Assert.Equal(20, N(cell, "approved")); Assert.Equal(20, N(cell, "confirmed")); Assert.Equal(10, N(cell, "expected")); Assert.Equal(10, N(cell, "remaining"));
        var explain = await (await f.As(TestData.Sam).GetAsync($"/api/v1/planning/cells/{id}/{W:yyyy-MM-dd}")).Json();
        var rows = explain["entries"]!.AsArray();
        Assert.Equal(16, N(rows.Single(e => e!.G("id") == first.G("id"))!, "coveredHours"));
        Assert.Equal(4, N(rows.Single(e => e!.G("id") == second.G("id"))!, "coveredHours"));
        Assert.Equal(10, N(rows.Single(e => e!.G("id") == second.G("id"))!, "countedHours"));
        var own = await Grid(id, email);
        Assert.False(own["people"]![0]!["approvedAllocations"]![0]!["canOpen"]!.GetValue<bool>());
        Assert.Equal(allocation, own["people"]![0]!["approvedAllocations"]![0]!.G("allocationId"));
        // A draft filter never removes the approved allocation input.
        var filtered = await Grid(id, extra: "&confidence=Expected"); Assert.Equal(cell.ToJsonString(), CellOf(filtered).ToJsonString());
    }

    [Fact]
    public async Task Own_entries_stay_self_entered_and_manager_entries_are_read_only_to_subject()
    {
        var (id, email) = await Person();
        var own = await Entry(id, visibility: "Confirmed", actor: email);
        Assert.Equal("Self", own.S("ownerKind")); Assert.True(own["canEdit"]!.GetValue<bool>()); Assert.False(own["canChangeVisibility"]!.GetValue<bool>());
        var path = $"/api/v1/planning/entries/{own.G("id")}";
        var changed = await (await f.As(email).Patch(path, new { rowVersion = own.I("rowVersion"), label = "Synthetic self update" })).Json();
        Assert.Equal("Synthetic self update", changed.S("label"));
        await (await f.As(email).Post(path + "/visibility", new { rowVersion = changed.I("rowVersion"), visibility = "Draft" })).Json(400);
        var manager = await Entry(id);
        await (await f.As(email).Patch($"/api/v1/planning/entries/{manager.G("id")}", new { rowVersion = manager.I("rowVersion"), label = "No" })).Json(403);
        foreach (var format in new[] { "csv", "xlsx" }) await (await f.As(email).GetAsync($"/api/v1/planning/entries/export?format={format}")).Json(403);
    }

    [Fact]
    public async Task Move_unlink_archive_and_correction_preserve_owner_and_audit_reasons()
    {
        var (id, _) = await Person(); var (target, _) = await Person(); var project = await d.Project();
        var row = await Entry(id, project: project.Id); var path = $"/api/v1/planning/entries/{row.G("id")}";
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == project.Id)).Status = "Archived"; return await db.SaveChangesAsync(); });
        row = await (await f.As(TestData.Sam).Patch(path, new { rowVersion = row.I("rowVersion"), label = "Synthetic archived context" })).Json();
        Assert.Contains("ProjectNotActive", row["warnings"]!.AsArray().Select(x => x!.GetValue<string>()));
        row = await (await f.As(TestData.Sam).Patch(path, new { rowVersion = row.I("rowVersion"), personId = target, sourceCategory = "OtherProject", projectId = (Guid?)null, projectDisciplineId = (Guid?)null, notes = (string?)null })).Json();
        Assert.Equal(target, row.G("personId")); Assert.Null(row["projectId"]); Assert.Null(row["notes"]); Assert.Equal(d.User(TestData.Sam), row.G("ownerId"));
        await (await f.As(TestData.Admin).Patch(path, new { rowVersion = row.I("rowVersion"), label = "No reason" })).Json(400);
        row = await (await f.As(TestData.Admin).Patch(path, new { rowVersion = row.I("rowVersion"), label = "Synthetic correction", reason = "Corrected synthetic fixture" })).Json();
        Assert.True(f.Db(db => db.ActivityLog.Any(a => a.ItemId == row.G("id") && a.Action == "DataCorrection" && a.Reason == "Corrected synthetic fixture" && a.ProjectId == null)));
        Assert.Equal(d.User(TestData.Sam), row.G("ownerId"));
    }

    [Fact]
    public async Task Publish_withdraw_and_delete_notices_are_atomic_and_hide_withdrawn_details()
    {
        var (id, _) = await Person(); var row = await Entry(id, visibility: "Draft", label: "Synthetic PRIVATE WITHDRAW LABEL");
        var entryId = row.G("id"); var path = $"/api/v1/planning/entries/{entryId}";
        Assert.Equal(0, f.Db(db => db.Notifications.Count(n => n.ItemId == entryId)));
        row = await (await f.As(TestData.Sam).Post(path + "/visibility", new { rowVersion = row.I("rowVersion"), visibility = "Published" })).Json();
        Assert.Equal(1, f.Db(db => db.Notifications.Count(n => n.ItemId == entryId && n.UserId == id)));
        Assert.Equal(0, f.Db(db => db.Notifications.Count(n => n.ItemId == entryId && n.UserId == d.User(TestData.Sam))));
        row = await (await f.As(TestData.Sam).Post(path + "/visibility", new { rowVersion = row.I("rowVersion"), visibility = "Draft" })).Json();
        Assert.All(f.Db(db => db.Notifications.Where(n => n.ItemId == entryId).AsNoTracking().ToList()), n => { Assert.DoesNotContain("PRIVATE WITHDRAW LABEL", n.Title); Assert.Null(n.Body); Assert.Equal("/my-work", n.LinkPath); });
        await (await f.As(TestData.Sam).DeleteAsync(path + $"?rowVersion={row.I("rowVersion")}")).Json(204);
        Assert.True(f.Db(db => db.PlanningEntries.IgnoreQueryFilters().Single(e => e.Id == entryId).DeletedAt != null));
        Assert.True(f.Db(db => db.ActivityLog.Count(a => a.ItemId == entryId && a.ItemType == "PlanningEntry") >= 4));
    }

    [Fact]
    public async Task Concurrent_create_receipts_create_one_entry_and_recheck_current_authority()
    {
        var (id, _) = await Person(); var key = Guid.NewGuid().ToString();
        var body = new { personId = id, hoursPerWeek = 4, startWeek = W, endWeek = W, label = "Synthetic concurrent receipt", sourceCategory = "Other" };
        async Task<HttpResponseMessage> Send() { var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/planning/entries") { Content = JsonContent.Create(body) }; request.Headers.Add("Idempotency-Key", key); return await f.As(TestData.Sam).SendAsync(request); }
        var responses = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => Send()));
        var ids = new List<Guid>(); foreach (var res in responses) ids.Add((await res.Json(201)).G("id"));
        Assert.Single(ids.Distinct()); Assert.Equal(1, f.Db(db => db.PlanningEntries.Count(e => e.Id == ids[0])));
        await f.DbAsync(async db => { (await db.Users.SingleAsync(u => u.Id == id)).SupervisorId = d.User(TestData.Lena); return await db.SaveChangesAsync(); });
        await (await Send()).Json(404);
    }

    [Fact]
    public async Task Time_away_matches_029_versions_and_clear_preserves_additional_and_noops()
    {
        var (first, _) = await Person(); var (second, _) = await Person();
        for (var version = 0; version < 2; version++)
        {
            var legacy = await (await f.As(TestData.Sam).Put($"/api/v1/users/{first}/availability/{W:yyyy-MM-dd}", new { expectedRowVersion = version, availableHours = 4, category = "Reduced" })).Json();
            var plan = await (await f.As(TestData.Sam).Post("/api/v1/planning/time-away", new { personId = second, from = W, through = W, action = "Record", availableHours = 4, category = "Reduced", versions = new[] { new { workDate = W, rowVersion = version } } })).Json();
            Assert.Equal(legacy.I("rowVersion"), plan["days"]![0]!.I("rowVersion"));
            Assert.Equal(N(legacy, "availableHours"), N(plan["days"]![0]!, "availableHours"));
            Assert.Equal(f.Db(db => db.PersonDateVersions.Single(v => v.PersonId == first && v.WorkDate == W).RowVersion), plan["days"]![0]!.I("dateVersion"));
        }
        var saturday = W.AddDays(5); var friday = W.AddDays(4);
        await (await f.As(TestData.Sam).Put($"/api/v1/users/{second}/availability/{saturday:yyyy-MM-dd}", new { expectedRowVersion = 0, availableHours = 0, category = "Unavailable" })).Json();
        await (await f.As(TestData.Sam).Put($"/api/v1/users/{second}/availability/{friday:yyyy-MM-dd}", new { expectedRowVersion = 0, availableHours = 10, category = "Additional" })).Json();
        var cleared = await (await f.As(TestData.Sam).Post("/api/v1/planning/time-away", new { personId = second, from = W, through = saturday, action = "Clear", versions = new[] { new { workDate = W, rowVersion = 2 }, new { workDate = saturday, rowVersion = 1 } } })).Json();
        Assert.Equal(2, cleared["cleared"]!.AsArray().Count);
        Assert.Equal(1, f.Db(db => db.AvailabilityOverrides.Count(v => v.PersonId == second)));
        Assert.Equal(1, f.Db(db => db.PersonDateVersions.Single(v => v.PersonId == second && v.WorkDate == friday).RowVersion));
        var before = f.Db(db => db.PersonDateVersions.Where(v => v.PersonId == second).Sum(v => v.RowVersion));
        var noOp = await (await f.As(TestData.Sam).Post("/api/v1/planning/time-away", new { personId = second, from = W, through = saturday, action = "Clear" })).Json();
        Assert.Empty(noOp["cleared"]!.AsArray()); Assert.Equal(before, f.Db(db => db.PersonDateVersions.Where(v => v.PersonId == second).Sum(v => v.RowVersion)));
    }

    [Fact]
    public async Task Stale_range_and_missing_versions_cannot_partially_write()
    {
        var (id, _) = await Person();
        await (await f.As(TestData.Sam).Put($"/api/v1/users/{id}/availability/{W:yyyy-MM-dd}", new { expectedRowVersion = 0, availableHours = 4, category = "Reduced" })).Json();
        var before = f.Db(db => JsonSerializer.Serialize(db.AvailabilityOverrides.Where(x => x.PersonId == id).AsNoTracking().ToList()));
        foreach (var versions in new object[] { Array.Empty<object>(), new[] { new { workDate = W, rowVersion = 0 }, new { workDate = W.AddDays(1), rowVersion = 0 } } })
        {
            var result = await (await f.As(TestData.Sam).Post("/api/v1/planning/time-away", new { personId = id, from = W, through = W.AddDays(1), action = "Record", availableHours = 0, category = "Unavailable", versions })).Json(409);
            Assert.Contains(W.ToString("yyyy-MM-dd"), result["staleDates"]!.ToJsonString());
            Assert.Equal(before, f.Db(db => JsonSerializer.Serialize(db.AvailabilityOverrides.Where(x => x.PersonId == id).AsNoTracking().ToList())));
            Assert.False(f.Db(db => db.PersonDateVersions.Any(v => v.PersonId == id && v.WorkDate == W.AddDays(1))));
        }
    }

    [Fact]
    public async Task Private_drafts_change_no_nonowner_counts_totals_exports_history_search_or_digest()
    {
        var (id, email) = await Person(); var marker = "SYNTHETICPRIVATE" + Guid.NewGuid().ToString("N");
        var project = await d.Project();
        await f.DbAsync(async db => { db.ProjectMembers.Add(new() { ProjectId = project.Id, UserId = id, Roles = ["TeamMember"] }); return await db.SaveChangesAsync(); });
        var viewers = new[] { email, TestData.Lena, TestData.Admin, TestData.Pm };
        var baseline = new Dictionary<string, string>(); foreach (var viewer in viewers) baseline[viewer] = (await Grid(id, viewer)).ToJsonString();
        var row = await Entry(id, visibility: "Draft", label: marker); var entryId = row.G("id");
        await f.DbAsync(async db => { var e = await db.PlanningEntries.SingleAsync(e => e.Id == entryId); e.EndWeek = W.AddDays(70); e.LastValidatedAt = f.Clock.GetUtcNow().AddDays(-40); return await db.SaveChangesAsync(); });
        foreach (var viewer in viewers)
        {
            Assert.Equal(baseline[viewer], (await Grid(id, viewer)).ToJsonString());
            foreach (var route in new[] { $"/planning/entries?personId={id}", $"/planning/cells/{id}/{W:yyyy-MM-dd}", $"/search?q={marker}&type=planning", "/me/notifications" })
            {
                var response = await (await f.As(viewer).GetAsync("/api/v1" + route)).Json();
                if (route.StartsWith("/search")) response.AsObject().Remove("q");
                Assert.DoesNotContain(marker, response.ToJsonString()); Assert.DoesNotContain(entryId.ToString(), response.ToJsonString());
            }
            await (await f.As(viewer).GetAsync($"/api/v1/planning/entries/{entryId}")).Json(404);
            await (await f.As(viewer).GetAsync($"/api/v1/planning/entries/{entryId}/activity")).Json(404);
            if (viewer != email) foreach (var format in new[] { "csv", "xlsx" }) foreach (var kind in new[] { "entries", "grid" })
            { var text = await ExportText(await f.As(viewer).GetAsync($"/api/v1/planning/{kind}/export?format={format}&personId={id}")); Assert.DoesNotContain(marker, text); Assert.DoesNotContain(entryId.ToString(), text); }
            var digest = await f.DbAsync(db => Digest.Build(db, d.User(viewer), "Synthetic", W.AddDays(35), f.Clock.GetUtcNow().AddDays(-1), f.Clock.GetUtcNow().AddDays(35), new OrgSettings(), "http://localhost:5080"));
            Assert.DoesNotContain(marker, digest?.Body ?? "");
        }
        foreach (var viewer in new[] { TestData.Marc, TestData.Rita })
        {
            await (await f.As(viewer).GetAsync($"/api/v1/planning/cells/{id}/{W:yyyy-MM-dd}")).Json(viewer == TestData.Rita ? 403 : 404);
            await (await f.As(viewer).GetAsync($"/api/v1/planning/entries/{entryId}")).Json(404);
        }
        var ownerDigest = await f.DbAsync(db => Digest.Build(db, d.User(TestData.Sam), "Synthetic", W.AddDays(35), f.Clock.GetUtcNow().AddDays(-1), f.Clock.GetUtcNow().AddDays(35), new OrgSettings(), "http://localhost:5080"));
        Assert.Contains(marker, ownerDigest!.Body);
        var owner = await Grid(id); Assert.Contains(marker, owner.ToJsonString()); Assert.DoesNotContain(marker, (await Grid(id, extra: "&includeMyDrafts=false")).ToJsonString());
        var beforeAccess = f.Db(db => db.ActivityLog.Count(a => a.ItemType == "PlanningDraftAccess"));
        var correction = await Grid(id, TestData.Admin, "&draftAccessReason=Synthetic%20privacy%20review"); Assert.Contains(marker, correction.ToJsonString());
        Assert.Equal(beforeAccess + 1, f.Db(db => db.ActivityLog.Count(a => a.ItemType == "PlanningDraftAccess")));
        var audit = f.Db(db => db.ActivityLog.Where(a => a.ItemType == "PlanningDraftAccess").OrderByDescending(a => a.OccurredAt).ThenByDescending(a => a.Id).First());
        Assert.Equal(d.User(TestData.Admin), audit.ActorUserId); Assert.Contains(id.ToString(), audit.Snapshot!); Assert.Equal("Synthetic privacy review", audit.Reason);
        var org = await (await f.As(TestData.Admin).GetAsync("/api/v1/admin/activity?itemType=PlanningEntry")).Json(); Assert.DoesNotContain(marker, org.ToJsonString());
    }

    [Fact]
    public async Task Restricted_published_rows_and_revoked_project_access_are_filtered_before_bands()
    {
        var (id, email) = await Person(); var project = await d.Project();
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == project.Id)).Visibility = "Restricted"; db.ProjectMembers.Add(new() { ProjectId = project.Id, UserId = id, Roles = ["TeamMember"] }); return await db.SaveChangesAsync(); });
        var row = await Entry(id, project: project.Id, actor: TestData.Admin, label: "Synthetic restricted plan");
        Assert.DoesNotContain(row.G("id").ToString(), (await Grid(id)).ToJsonString());
        Assert.Equal(0, N(CellOf(await Grid(id)), "expected"));
        await (await f.As(TestData.Sam).GetAsync($"/api/v1/planning/entries/{row.G("id")}")).Json(404);
        Assert.Contains("Synthetic restricted plan", (await Grid(id, email)).ToJsonString());
        await f.DbAsync(async db => { (await db.ProjectMembers.SingleAsync(m => m.ProjectId == project.Id && m.UserId == id)).RemovedAt = f.Clock.GetUtcNow(); return await db.SaveChangesAsync(); });
        Assert.DoesNotContain("Synthetic restricted plan", (await Grid(id, email)).ToJsonString());
        await (await f.As(TestData.Admin).Post("/api/v1/planning/entries", new { personId = id, hoursPerWeek = 4, startWeek = W, endWeek = W, label = "Synthetic invalid link", sourceCategory = "MajorProject", projectId = project.Id })).Json(400);
    }
    [Fact]
    public async Task One_week_grid_and_cell_use_the_same_under_planned_lookahead()
    {
        var (id, _) = await Person();
        var grid = await (await f.As(TestData.Admin).GetAsync($"/api/v1/planning/grid?personId={id}&from={W:yyyy-MM-dd}&weeks=1")).Json();
        var detail = await (await f.As(TestData.Admin).GetAsync($"/api/v1/planning/cells/{id}/{W:yyyy-MM-dd}")).Json();
        Assert.True(CellOf(grid)["underPlanned"]!.GetValue<bool>());
        Assert.Equal(CellOf(grid)["underPlanned"]!.GetValue<bool>(), detail["indicators"]!.AsArray().Single(i => i!.S("code") == "UnderPlanned")!["active"]!.GetValue<bool>());
        Assert.Single(grid["weeks"]!.AsArray()); Assert.Single(grid["people"]![0]!["cells"]!.AsArray());
    }

    [Fact]
    public async Task Personal_grid_does_not_grant_workload_export_permission()
    {
        var (id, email) = await Person();
        await (await f.As(email).GetAsync($"/api/v1/planning/grid?personId={id}")).Json();
        foreach (var kind in new[] { "grid", "entries" })
            foreach (var format in new[] { "csv", "xlsx" })
                await (await f.As(email).GetAsync($"/api/v1/planning/{kind}/export?personId={id}&format={format}")).Json(403);
    }

    [Fact]
    public async Task Notification_reads_and_queued_email_recheck_current_project_access()
    {
        var (id, email) = await Person(); var project = await d.Project();
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(p => p.Id == project.Id)).Visibility = "Restricted"; db.ProjectMembers.Add(new() { ProjectId = project.Id, UserId = id, Roles = ["TeamMember"] }); return await db.SaveChangesAsync(); });
        var entry = await Entry(id, project: project.Id, actor: TestData.Admin, label: "Synthetic restricted email sentinel");
        var entryId = entry.G("id");
        var notice = f.Db(db => db.Notifications.Single(n => n.UserId == id && n.ItemId == entryId).Id);
        Assert.Contains("Synthetic restricted email sentinel", (await (await f.As(email).GetAsync("/api/v1/me/notifications")).Json()).ToJsonString());
        var beforeCount = (await (await f.As(email).GetAsync("/api/v1/me/notifications/unread-count")).Json()).I("notifications");
        var queuedId = Guid.NewGuid();
        await f.DbAsync(async db => {
            (await db.ProjectMembers.SingleAsync(m => m.ProjectId == project.Id && m.UserId == id)).RemovedAt = f.Clock.GetUtcNow();
            db.Emails.Add(new EmailMessage { Id = queuedId, UserId = id, ToAddress = email, Subject = "Synthetic restricted email sentinel", BodyText = "Synthetic restricted email sentinel", RequiredPlanningEntryIds = [entryId], CreatedAt = DateTimeOffset.MinValue });
            return await db.SaveChangesAsync();
        });
        var centre = await (await f.As(email).GetAsync("/api/v1/me/notifications")).Json(); Assert.DoesNotContain("Synthetic restricted email sentinel", centre.ToJsonString());
        var count = await (await f.As(email).GetAsync("/api/v1/me/notifications/unread-count")).Json(); Assert.Equal(beforeCount - 1, count.I("notifications"));
        var pulse = await (await f.As(email).GetAsync("/api/v1/me/notifications/pulse")).Json(); Assert.StartsWith(count.I("notifications") + ":", pulse.S("stamp"));
        Assert.Equal(0, (await (await f.As(email).Post("/api/v1/me/notifications/read", new { ids = new[] { notice } })).Json()).I("marked"));
        using (var scope = f.Services.CreateScope()) await new EmailJob().Run(scope.ServiceProvider, CancellationToken.None);
        var queued = f.Db(db => db.Emails.Single(e => e.Id == queuedId)); Assert.Null(queued.SentAt); Assert.NotNull(queued.SuppressedAt); Assert.Equal("PlanningAccessRemoved", queued.LastError);
    }

    [Fact]
    public async Task Withdrawal_removes_details_from_pending_email_and_inactive_recipient_is_suppressed()
    {
        var (id, email) = await Person();
        await f.DbAsync(async db => { db.NotificationPreferences.Add(new() { UserId = id, EventType = "PlanningEntryChanged", InApp = true, Email = true }); return await db.SaveChangesAsync(); });
        var entry = await Entry(id, label: "Synthetic withdrawal email sentinel"); var entryId = entry.G("id");
        var oldEmail = f.Db(db => db.Emails.Single(e => e.RequiredPlanningEntryIds.Contains(entryId)).Id);
        await (await f.As(TestData.Sam).Post($"/api/v1/planning/entries/{entryId}/visibility", new { visibility = "Draft", rowVersion = entry.I("rowVersion") })).Json();
        var withdrawn = f.Db(db => db.Emails.Single(e => e.Id == oldEmail)); Assert.NotNull(withdrawn.SuppressedAt); Assert.Equal(f.Clock.GetUtcNow(), withdrawn.SuppressedAt); Assert.DoesNotContain("sentinel", withdrawn.BodyText); Assert.DoesNotContain("sentinel", withdrawn.Subject); Assert.Null(withdrawn.BodyHtml);
        var generic = Guid.NewGuid();
        await f.DbAsync(async db => { (await db.Users.SingleAsync(u => u.Id == id)).IsActive = false; db.Emails.Add(new() { Id = generic, UserId = id, ToAddress = email, Subject = "Synthetic generic withdrawal", Kind = "PlanningWithdrawal", CreatedAt = DateTimeOffset.MinValue }); return await db.SaveChangesAsync(); });
        using (var scope = f.Services.CreateScope()) await new EmailJob().Run(scope.ServiceProvider, CancellationToken.None); var result = f.Db(db => db.Emails.Single(e => e.Id == generic)); Assert.Null(result.SentAt); Assert.NotNull(result.SuppressedAt);
    }

    [Fact]
    public async Task Admin_owned_manager_replay_case_insensitive_time_away_and_correction_access_are_current()
    {
        var (id, _) = await Person();
        var entry = await Entry(id, visibility: "Draft", actor: TestData.Admin);
        async Task<HttpResponseMessage> Send(string path, string json, string key) { var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json") }; request.Headers.Add("Idempotency-Key", key); return await f.As(TestData.Admin).SendAsync(request); }
        var key = Guid.NewGuid().ToString(); var path = $"/api/v1/planning/entries/{entry.G("id")}/still-valid";
        var body = JsonSerializer.Serialize(new { RowVersion = entry.I("rowVersion") }); var first = await (await Send(path, body, key)).Json(); var replay = await Send(path, body, key); Assert.Equal(first.G("id"), (await replay.Json()).G("id")); Assert.True(replay.Headers.Contains("Idempotent-Replayed"));
        var timeBody = JsonSerializer.Serialize(new { PersonId = id, From = W.AddDays(3), Through = W.AddDays(3), Action = "Record", AvailableHours = 0, Category = "Unavailable", Versions = new[] { new { WorkDate = W.AddDays(3), RowVersion = 0 } } });
        var timeKey = Guid.NewGuid().ToString(); await (await Send("/api/v1/planning/time-away", timeBody, timeKey)).Json(); await (await Send("/api/v1/planning/time-away", timeBody, timeKey)).Json();
        var other = await Entry(id, visibility: "Draft"); var correctionBody = JsonSerializer.Serialize(new { RowVersion = other.I("rowVersion"), Reason = "Synthetic correction access" });
        var before = f.Db(db => db.ActivityLog.Count(a => a.ItemType == "PlanningDraftAccess"));
        var correctionKey = Guid.NewGuid().ToString(); var correctionPath = $"/api/v1/planning/entries/{other.G("id")}/still-valid";
        await (await Send(correctionPath, correctionBody, correctionKey)).Json(); await (await Send(correctionPath, correctionBody, correctionKey)).Json();
        Assert.Equal(before + 2, f.Db(db => db.ActivityLog.Count(a => a.ItemType == "PlanningDraftAccess")));
    }

}
