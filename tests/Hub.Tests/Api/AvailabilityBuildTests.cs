using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
namespace Hub.Tests.Api;

[Collection("api")]
public sealed class AvailabilityBuildTests(HubFactory f)
{
    readonly TestData d = new(f);
    static readonly DateOnly W = new(2026, 9, 14);
    async Task<(Guid Id, string Email, string Source)> Person()
    {
        var email = $"synthetic-leave-{Guid.NewGuid():N}@hub.test"; var source = "HR-" + Guid.NewGuid().ToString("N");
        var id = (await (await f.As(email).GetAsync("/api/v1/me")).Json()).G("id");
        await f.DbAsync(async db => { var user = await db.Users.SingleAsync(u => u.Id == id); user.SupervisorId = d.User(TestData.Sam); user.WeeklyCapacityHours = 40; user.HrSourceSystem = source; user.HrPersonKey = "employee-1"; return await db.SaveChangesAsync(); });
        return (id, email, source);
    }
    async Task<JsonNode> Preview(string csv) => await (await f.As(TestData.Admin).Post("/api/v1/planning/leave/import-preview", new { csv })).Json();
    static string Csv(string source, string id = "L-1", string status = "Approved", string time = "2026-09-13T10:00:00Z", string hours = "") => "source_system,external_id,person_key,status,start_date,end_date,hours_per_day,source_updated_at\n" + $"{source},{id},employee-1,{status},2026-09-14,2026-09-16,{hours},{time}";
    async Task<JsonNode> Apply(JsonNode preview, string? key = null) {
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/v1/planning/leave/import") { Content = JsonContent.Create(new { previewId = preview["previewId"], rows = preview["rows"] }) };
        if (key != null) req.Headers.Add("Idempotency-Key", key);
        return await (await f.As(TestData.Admin).SendAsync(req)).Json();
    }
    async Task<JsonNode> View(Guid person, string as_ = TestData.Sam) => await (await f.As(as_).GetAsync($"/api/v1/planning/availability?from={W:yyyy-MM-dd}&through={W.AddDays(6):yyyy-MM-dd}&personId={person}")).Json();

    [Fact]
    public async Task HR_import_is_idempotent_explicit_cancellation_restores_capacity_and_no_missing_row_cancels()
    {
        var p = await Person(); var preview = await Preview(Csv(p.Source)); Assert.True(preview["canApply"]!.GetValue<bool>());
        var key = Guid.NewGuid().ToString(); var applied = await Apply(preview, key); var replay = await Apply(preview, key); Assert.Equal(applied.ToJsonString(), replay.ToJsonString());
        Assert.Equal(1, f.Db(db => db.ApprovedLeaves.Count(l => l.PersonId == p.Id)));
        var view = await View(p.Id); Assert.Equal(16m, view["people"]![0]!["days"]!.AsArray().Sum(x => x!["available"]!.GetValue<decimal>()));
        await Apply(await Preview(Csv(p.Source, "L-2", hours: "4"))); Assert.Equal(2, f.Db(db => db.ApprovedLeaves.Count(l => l.PersonId == p.Id && l.Status == "Approved")));
        await Apply(await Preview(Csv(p.Source, status: "Cancelled", time: "2026-09-14T10:00:00Z")));
        await Apply(await Preview(Csv(p.Source, "L-2", "Cancelled", "2026-09-14T10:00:00Z", "4")));
        var restored = await View(p.Id); Assert.Equal(40m, restored["people"]![0]!["days"]!.AsArray().Sum(x => x!["available"]!.GetValue<decimal>()));
        Assert.True(f.Db(db => db.ActivityLog.Any(l => l.ItemType == "ApprovedLeave" && l.ItemId != null)));
    }

    [Fact]
    public async Task Invalid_or_unmapped_rows_block_preview_and_stale_apply_changes_nothing()
    {
        var p = await Person(); var preview = await Preview(Csv(p.Source));
        var bad = await Preview(Csv(p.Source) + "\nunknown,L-2,employee-1,Approved,2026-09-14,2026-09-16,,2026-09-13T10:00:00Z"); Assert.False(bad["canApply"]!.GetValue<bool>());
        await Apply(preview);
        var stale = await f.As(TestData.Admin).Post("/api/v1/planning/leave/import", new { previewId = preview["previewId"], rows = preview["rows"] }); Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(1, f.Db(db => db.ApprovedLeaves.Count(l => l.PersonId == p.Id)));
        Assert.False((await Preview(Csv(p.Source, time: "2026-09-12T10:00:00Z")))["canApply"]!.GetValue<bool>());
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(p.Email).Post("/api/v1/planning/leave/import-preview", new { csv = Csv(p.Source) })).StatusCode);
    }

    [Fact]
    public async Task Import_receipt_rejects_tampering_and_changed_mapping_and_freshness_never_moves_backwards()
    {
        var p = await Person(); var preview = await Preview(Csv(p.Source));
        var edited = preview.DeepClone(); edited["rows"]![0]!["hoursPerDay"] = 2;
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Admin).Post("/api/v1/planning/leave/import", new { previewId = edited["previewId"], rows = edited["rows"] })).StatusCode);
        Assert.Empty(f.Db(db => db.ApprovedLeaves.Where(l => l.PersonId == p.Id).ToArray()));
        var userVersion = f.Db(db => db.Users.Single(u => u.Id == p.Id).RowVersion);
        await (await f.As(TestData.Admin).Put($"/api/v1/planning/hr-identities/{p.Id}", new { sourceSystem = p.Source, personKey = "changed-key", rowVersion = userVersion })).Json();
        Assert.Equal(HttpStatusCode.Conflict, (await f.As(TestData.Admin).Post("/api/v1/planning/leave/import", new { previewId = preview["previewId"], rows = preview["rows"] })).StatusCode);
        await f.DbAsync(async db => { (await db.Users.SingleAsync(u => u.Id == p.Id)).HrPersonKey = "employee-1"; return await db.SaveChangesAsync(); });
        await Apply(await Preview(Csv(p.Source, time: "2026-09-14T10:00:00Z")));
        await Apply(await Preview(Csv(p.Source, "older-separate-record", time: "2026-09-13T10:00:00Z")));
        Assert.Equal(DateTimeOffset.Parse("2026-09-14T10:00:00Z"), f.Db(db => db.LeaveImportStates.Single(s => s.SourceSystem == p.Source).SourceUpdatedAt));
    }

    [Fact]
    public async Task Identical_rows_deduplicate_partial_conflicts_block_and_manual_reference_reconciles_in_place()
    {
        var p = await Person(); var csv = Csv(p.Source, hours: "4");
        var duplicate = await Preview(csv + "\n" + csv.Split('\n')[1]); Assert.Single(duplicate["rows"]!.AsArray());
        await Apply(duplicate);
        var overlap = await Preview(Csv(p.Source, "partial-overlap", hours: "2")); Assert.False(overlap["canApply"]!.GetValue<bool>()); Assert.Null(overlap["previewId"]);
        Assert.False((await Preview(Csv(p.Source, "too-many-hours", hours: "9")))["canApply"]!.GetValue<bool>());
        var manual = await (await f.As(TestData.Sam).Post("/api/v1/planning/leave/manual", new { personId = p.Id, sourceSystem = p.Source, externalId = "manual-reference", fromDate = W, throughDate = W.AddDays(2), status = "Approved", rowVersion = 0 })).Json();
        await Apply(await Preview(Csv(p.Source, "manual-reference")));
        var row = f.Db(db => db.ApprovedLeaves.Single(l => l.Id == manual.G("id"))); Assert.Equal("HRImport", row.SourceKind);
        Assert.Equal(DateTimeOffset.Parse("2026-09-13T10:00:00Z"), row.SourceUpdatedAt);
    }

    [Fact]
    public async Task Weekend_schedule_drives_allocation_spreading_and_holidays_invalidate_confirmation_dates()
    {
        var p = await Person(); var project = await d.Project(); var saturday = W.AddDays(5);
        await f.DbAsync(async db => { db.ProjectMembers.Add(new ProjectMember { ProjectId = project.Id, UserId = p.Id, Roles = [ProjectRole.TeamMember], AddedAt = f.Clock.GetUtcNow() }); return await db.SaveChangesAsync(); });
        await (await f.As(TestData.Sam).Put($"/api/v1/planning/schedules/{p.Id}", new { fromDate = W, weeklyHours = 16, workingDays = new[] { 0, 6 }, rowVersion = 0 })).Json();
        var task = await d.NewTask(project.Id, extra: new { assigneeId = p.Id, estimatedHours = 4, startDate = saturday, dueDate = saturday });
        var root = $"/api/v1/projects/{project.Id}/allocations";
        var allocation = await (await f.As(TestData.Pm).Post(root, new AllocationEndpoints.CreateBody(Guid.NewGuid(), p.Id, AllocationPurpose.Production, saturday, saturday.AddDays(1), 8, [], [new("Task", task.G("id"), saturday)], null))).Json();
        var preview = await (await f.As(TestData.Sam).GetAsync($"{root}/{allocation.G("id")}/confirmation-preview")).Json();
        Assert.All(preview["days"]!.AsArray(), day => Assert.Equal(8m, day!["availableHours"]!.GetValue<decimal>()));
        var versions = f.Db(db => db.PersonDateVersions.Single(v => v.PersonId == p.Id && v.WorkDate == saturday).RowVersion);
        var holiday = await (await f.As(TestData.Admin).Post("/api/v1/admin/holidays", new { date = saturday, name = "Synthetic weekend holiday" })).Json(201);
        Assert.True(f.Db(db => db.PersonDateVersions.Single(v => v.PersonId == p.Id && v.WorkDate == saturday).RowVersion) > versions);
        Assert.Equal(0m, (await View(p.Id))["people"]![0]!["days"]![5]!["available"]!.GetValue<decimal>());
        await (await f.As(TestData.Admin).DeleteAsync($"/api/v1/admin/holidays/{holiday.G("id")}")).Json(204);
        Assert.Equal(16m, (await View(p.Id))["people"]![0]!["days"]!.AsArray().Sum(day => day!["available"]!.GetValue<decimal>()));
    }

    [Fact]
    public async Task Holiday_leave_overlap_and_effective_schedule_count_once_in_planner_and_workload()
    {
        var p = await Person(); await Apply(await Preview(Csv(p.Source, hours: "4")));
        await f.DbAsync(async db => { var office = new Office { Name = "Synthetic isolated leave office", Code = Guid.NewGuid().ToString("N") }; db.Offices.Add(office); (await db.Users.SingleAsync(u => u.Id == p.Id)).OfficeId = office.Id; db.Holidays.Add(new Holiday { Date = W, OfficeId = office.Id, Name = "Synthetic office holiday" }); return await db.SaveChangesAsync(); });
        var view = await View(p.Id); var days = view["people"]![0]!["days"]!.AsArray(); Assert.Equal(24m, days.Sum(x => x!["available"]!.GetValue<decimal>())); Assert.Equal(8m, days.Sum(x => x!["leave"]!.GetValue<decimal>())); Assert.Equal(8m, days.Sum(x => x!["holiday"]!.GetValue<decimal>()));
        var grid = await (await f.As(TestData.Sam).GetAsync($"/api/v1/planning/grid?from={W:yyyy-MM-dd}&weeks=1&personId={p.Id}")).Json(); Assert.Equal(24m, grid["people"]![0]!["cells"]![0]!["capacity"]!.GetValue<decimal>());
        await (await f.As(TestData.Sam).Put($"/api/v1/planning/schedules/{p.Id}", new { fromDate = W.AddDays(3), weeklyHours = 20, workingDays = new[] { 1, 2, 3, 4, 5 }, rowVersion = 0 })).Json();
        Assert.Equal(16m, (await View(p.Id))["people"]![0]!["days"]!.AsArray().Sum(x => x!["available"]!.GetValue<decimal>()));
    }

    [Fact]
    public async Task Partial_leave_preserves_extra_hours_and_legacy_nonworking_absence_stays_provisional()
    {
        var p = await Person(); await Apply(await Preview(Csv(p.Source, hours: "4")));
        await f.DbAsync(async db => {
            db.AvailabilityOverrides.AddRange(new PersonAvailabilityOverride { PersonId = p.Id, WorkDate = W.AddDays(1), AvailableHours = 10, Category = AvailabilityCategory.Additional, RowVersion = 1 },
                new PersonAvailabilityOverride { PersonId = p.Id, WorkDate = W.AddDays(5), AvailableHours = 0, Category = AvailabilityCategory.Unavailable, RowVersion = 1 });
            return await db.SaveChangesAsync();
        });
        var days = await f.DbAsync(db => PersonCapacity.Load(db, [p.Id], W, W.AddDays(6), new OrgSettings()));
        Assert.Equal(6, days[(p.Id, W.AddDays(1))].Available); Assert.Equal(4, days[(p.Id, W.AddDays(1))].Leave); Assert.Equal(2, days[(p.Id, W.AddDays(1))].Additional);
        Assert.True(days[(p.Id, W.AddDays(5))].Provisional);
    }

    [Fact]
    public async Task Own_team_availability_excludes_other_teams_and_HR_identifiers()
    {
        var p = await Person(); var other = await Person(); await Apply(await Preview(Csv(p.Source)));
        await f.DbAsync(async db => { (await db.Users.SingleAsync(u => u.Id == other.Id)).SupervisorId = d.User(TestData.Lena); return await db.SaveChangesAsync(); });
        var view = await View(p.Id, p.Email); Assert.Null(view["people"]![0]!["leave"]![0]!["externalId"]); Assert.False(view["people"]![0]!["canManage"]!.GetValue<bool>());
        Assert.Empty((await View(other.Id, p.Email))["people"]!.AsArray());
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(p.Email).Post("/api/v1/planning/leave/manual", new { personId = p.Id, sourceSystem = p.Source, externalId = "new", fromDate = W, throughDate = W, status = "Approved", rowVersion = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await f.As(TestData.Sam).Post("/api/v1/planning/leave/manual", new { personId = p.Id, sourceSystem = p.Source, externalId = "L-1", fromDate = W, throughDate = W, status = "Cancelled", rowVersion = f.Db(db => db.ApprovedLeaves.Single(l => l.PersonId == p.Id).RowVersion) })).StatusCode);
    }

    [Fact]
    public async Task Copy_and_publish_keep_confidence_private_drafts_atomic_versions_and_one_summary()
    {
        var p = await Person(); var c = f.As(TestData.Sam);
        var draft = await (await c.Post("/api/v1/planning/entries", new { personId = p.Id, hoursPerWeek = 8, startWeek = W, endWeek = W, label = "Synthetic proposal", sourceCategory = "Proposal", confidence = "Possible" })).Json(201);
        var next = W.AddDays(7); var preview = await (await c.GetAsync($"/api/v1/planning/week-preview?operation=Copy&week={next:yyyy-MM-dd}")).Json();
        var candidate = preview["entries"]!.AsArray().Single(e => e!.G("id") == draft.G("id"))!;
        var copy = await (await c.Post("/api/v1/planning/week-apply", new { operation = "Copy", week = next, snapshot = preview["snapshot"], entries = new[] { new { id = candidate.G("id"), rowVersion = candidate.I("rowVersion") } } })).Json();
        var id = copy["items"]![0]!.G("id"); Assert.Equal("Draft", f.Db(db => db.PlanningEntries.Single(e => e.Id == id).Visibility)); Assert.Equal("Possible", f.Db(db => db.PlanningEntries.Single(e => e.Id == id).Confidence));
        var publish = await (await c.GetAsync($"/api/v1/planning/week-preview?operation=Publish&week={next:yyyy-MM-dd}")).Json();
        var selected = publish["entries"]!.AsArray().Single(e => e!.G("id") == id)!;
        var body = new { operation = "Publish", week = next, snapshot = publish["snapshot"], entries = new[] { new { id, rowVersion = selected.I("rowVersion") } } };
        await (await c.Post("/api/v1/planning/week-apply", body)).Json(); Assert.Equal("Published", f.Db(db => db.PlanningEntries.Single(e => e.Id == id).Visibility));
        Assert.Equal(1, f.Db(db => db.Notifications.Count(n => n.UserId == p.Id && n.ItemId == id)));
        Assert.Equal(HttpStatusCode.Conflict, (await c.Post("/api/v1/planning/week-apply", body)).StatusCode);
    }

    [Fact]
    public async Task Calendar_feed_rotates_revokes_and_cancels_without_private_titles()
    {
        var p = await Person(); await Apply(await Preview(Csv(p.Source))); var c = f.As(p.Email);
        var sub = await (await c.Post("/api/v1/me/calendar-subscription", new { rowVersion = 0 })).Json(); var url = sub.S("url");
        using var anonymous = f.CreateClient(); var feed = await anonymous.GetStringAsync(url); Assert.Contains("Approved leave", feed); Assert.DoesNotContain(p.Source, feed);
        var uid = feed.Split("\r\n").Single(l => l.StartsWith("UID:")); Assert.Contains(uid, await anonymous.GetStringAsync(url));
        await Apply(await Preview(Csv(p.Source, status: "Cancelled", time: "2026-09-14T10:00:00Z")));
        var cancelled = await anonymous.GetStringAsync(url); Assert.Contains(uid, cancelled); Assert.Contains("STATUS:CANCELLED", cancelled); Assert.DoesNotContain("Approved leave", cancelled);
        var rotated = await (await c.Post("/api/v1/me/calendar-subscription", new { rowVersion = sub.I("rowVersion") })).Json(); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(url)).StatusCode);
        await (await c.DeleteAsync($"/api/v1/me/calendar-subscription?rowVersion={rotated.I("rowVersion")}")).Json(204); Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(rotated.S("url"))).StatusCode);
        Assert.DoesNotContain("token", f.Db(db => string.Join(" ", db.ActivityLog.Where(l => l.ItemType == "CalendarSubscription").Select(l => l.Changes))), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Coverage_exports_and_calendar_recheck_project_permissions()
    {
        var p = await Person(); var project = await d.Project();
        await f.DbAsync(async db => { db.ProjectMembers.Add(new ProjectMember { ProjectId = project.Id, UserId = p.Id, Roles = [ProjectRole.TeamMember], AddedAt = f.Clock.GetUtcNow() }); return await db.SaveChangesAsync(); });
        const string title = "Synthetic private calendar work";
        var task = await d.NewTask(project.Id, extra: new { name = title, assigneeId = p.Id, dueDate = W });
        await Apply(await Preview(Csv(p.Source)));
        Assert.Contains((await View(p.Id))["coverage"]!.AsArray(), item => item!.G("id") == task.G("id"));
        var csv = await f.As(p.Email).GetStringAsync($"/api/v1/planning/availability/export?from={W:yyyy-MM-dd}&through={W.AddDays(6):yyyy-MM-dd}&personId={p.Id}&format=csv&kind=leave");
        Assert.DoesNotContain(p.Source, csv); Assert.Contains("Approved", csv);
        var xlsx = await f.As(TestData.Sam).GetAsync($"/api/v1/planning/availability/export?from={W:yyyy-MM-dd}&through={W.AddDays(6):yyyy-MM-dd}&personId={p.Id}&format=xlsx");
        Assert.Equal(HttpStatusCode.OK, xlsx.StatusCode);
        var sub = await (await f.As(p.Email).Post("/api/v1/me/calendar-subscription", new { rowVersion = 0 })).Json();
        using var anonymous = f.CreateClient(); var first = await anonymous.GetStringAsync(sub.S("url")); Assert.Contains(title, first);
        await f.DbAsync(async db => { (await db.Projects.SingleAsync(x => x.Id == project.Id)).Visibility = Visibility.Restricted; (await db.ProjectMembers.SingleAsync(m => m.ProjectId == project.Id && m.UserId == p.Id)).RemovedAt = f.Clock.GetUtcNow(); return await db.SaveChangesAsync(); });
        var cancelled = await anonymous.GetStringAsync(sub.S("url")); Assert.DoesNotContain(title, cancelled); Assert.Contains("STATUS:CANCELLED", cancelled);
        Assert.DoesNotContain((await View(p.Id, p.Email))["coverage"]!.AsArray(), item => item!.G("id") == task.G("id"));
        await f.DbAsync(async db => { (await db.Users.SingleAsync(u => u.Id == p.Id)).IsActive = false; return await db.SaveChangesAsync(); });
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(sub.S("url"))).StatusCode);
    }
}
