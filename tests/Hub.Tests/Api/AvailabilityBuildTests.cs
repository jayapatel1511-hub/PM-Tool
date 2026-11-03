using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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

    sealed record ManagementIntent(Guid PersonId, string Source, Func<string, Task<HttpResponseMessage>> Send, Action<JsonNode> AssertSaved);

    async Task<ManagementIntent> ValidManagementIntent(string operation)
    {
        var person = await Person(); var sam = f.As(TestData.Sam);
        if (operation == "manual")
        {
            var initial = await (await sam.Post("/api/v1/planning/leave/manual", new { personId = person.Id, sourceSystem = person.Source,
                externalId = "synthetic-readonly-manual", fromDate = W, throughDate = W, status = "Approved", rowVersion = 0 })).Json();
            var id = initial.G("id"); var version = initial.I("rowVersion");
            var body = new { personId = person.Id, sourceSystem = person.Source, externalId = "synthetic-readonly-manual",
                fromDate = W, throughDate = W.AddDays(1), status = "Approved", rowVersion = version };
            return new(person.Id, person.Source, who => f.As(who).Post("/api/v1/planning/leave/manual", body), result => {
                Assert.Equal(["id", "rowVersion"], Keys(result)); Assert.Equal(id, result.G("id")); Assert.Equal(version + 1, result.I("rowVersion"));
                Assert.Equal(W.AddDays(1), f.Db(db => db.ApprovedLeaves.Single(row => row.Id == id).ThroughDate));
            });
        }
        if (operation == "schedule")
        {
            var path = $"/api/v1/planning/schedules/{person.Id}";
            var initial = await (await sam.Put(path, new { fromDate = W, weeklyHours = 40m, workingDays = new[] { 1, 2, 3, 4, 5 }, rowVersion = 0 })).Json();
            var id = initial.G("id"); var version = initial.I("rowVersion");
            var body = new { fromDate = W, weeklyHours = 35m, workingDays = new[] { 1, 2, 3, 4, 5 }, rowVersion = version };
            return new(person.Id, person.Source, who => f.As(who).Put(path, body), result => {
                Assert.Equal(["id", "rowVersion"], Keys(result)); Assert.Equal(id, result.G("id")); Assert.Equal(version + 1, result.I("rowVersion"));
                Assert.Equal(35m, f.Db(db => db.PersonSchedules.Single(row => row.Id == id).WeeklyHours));
            });
        }
        Assert.Equal("reconcile", operation);
        await Apply(await Preview(Csv(person.Source)));
        var leave = f.Db(db => db.ApprovedLeaves.Single(row => row.PersonId == person.Id && row.ExternalId == "L-1").Id);
        // Explicit legacy-unverified source setup, as in the existing reconciliation contract test; commands below are real HTTP.
        await f.DbAsync(async db => {
            db.AvailabilityOverrides.AddRange(new PersonAvailabilityOverride { PersonId = person.Id, WorkDate = W.AddDays(1), AvailableHours = 0, Category = AvailabilityCategory.Unavailable, RowVersion = 1 },
                new PersonAvailabilityOverride { PersonId = person.Id, WorkDate = W.AddDays(2), AvailableHours = 0, Category = AvailabilityCategory.Unavailable, RowVersion = 1 });
            return await db.SaveChangesAsync();
        });
        var firstVersion = f.Db(db => db.AvailabilityOverrides.Single(row => row.PersonId == person.Id && row.WorkDate == W.AddDays(1)).RowVersion);
        var first = await (await sam.Post("/api/v1/planning/leave/reconcile", new { personId = person.Id, leaveId = leave,
            dates = new[] { new { workDate = W.AddDays(1), rowVersion = firstVersion } } })).Json();
        Assert.Equal(1, first.I("count"));
        var observed = f.Db(db => db.AvailabilityOverrides.Single(row => row.PersonId == person.Id && row.WorkDate == W.AddDays(2)));
        Assert.Null(observed.ReconciledLeaveId);
        var reconcile = new { personId = person.Id, leaveId = leave, dates = new[] { new { workDate = observed.WorkDate, rowVersion = observed.RowVersion } } };
        return new(person.Id, person.Source, who => f.As(who).Post("/api/v1/planning/leave/reconcile", reconcile), result => {
            Assert.Equal(["count"], Keys(result)); Assert.Equal(1, result.I("count"));
            Assert.Equal(leave, f.Db(db => db.AvailabilityOverrides.Single(row => row.Id == observed.Id).ReconciledLeaveId));
            Assert.Equal(observed.RowVersion + 1, f.Db(db => db.AvailabilityOverrides.Single(row => row.Id == observed.Id).RowVersion));
        });
    }

    string ManagementState(ManagementIntent intent) => f.Db(db => JsonSerializer.Serialize(new {
        Person = db.Users.AsNoTracking().Where(row => row.Id == intent.PersonId).ToArray(),
        Leaves = db.ApprovedLeaves.AsNoTracking().Where(row => row.PersonId == intent.PersonId).OrderBy(row => row.Id).ToArray(),
        Schedules = db.PersonSchedules.AsNoTracking().Where(row => row.PersonId == intent.PersonId).OrderBy(row => row.Id).ToArray(),
        Overrides = db.AvailabilityOverrides.AsNoTracking().Where(row => row.PersonId == intent.PersonId).OrderBy(row => row.Id).ToArray(),
        CapacityVersions = db.PersonDateVersions.AsNoTracking().Where(row => row.PersonId == intent.PersonId).OrderBy(row => row.WorkDate).ToArray(),
        ImportState = db.LeaveImportStates.AsNoTracking().Where(row => row.SourceSystem == intent.Source).ToArray(),
        Activity = db.ActivityLog.AsNoTracking().OrderBy(row => row.Id).ToArray(), Outbox = db.Outbox.AsNoTracking().OrderBy(row => row.Id).ToArray(),
        Notices = db.Notifications.AsNoTracking().OrderBy(row => row.Id).ToArray(), Emails = db.Emails.AsNoTracking().OrderBy(row => row.Id).ToArray()
    }));

    [Theory]
    [InlineData("manual")] [InlineData("reconcile")] [InlineData("schedule")]
    public async Task Management_commands_refuse_read_only_supervisor_without_writes_and_restore_supervisor_success(string operation)
    {
        var intent = await ValidManagementIntent(operation); var samId = d.User(TestData.Sam);
        Assert.Equal(samId, f.Db(db => db.Users.Single(user => user.Id == intent.PersonId).SupervisorId));
        // Resolve real personas before the no-write snapshots, so JIT identity setup is not mistaken for a command effect.
        foreach (var who in new[] { TestData.Lena, TestData.Alex })
        {
            var me = await (await f.As(who).GetAsync("/api/v1/me")).Json(); Assert.False(me["capabilities"]!["admin"]!.GetValue<bool>());
            if (who == TestData.Lena) Assert.Contains(SystemRole.Supervisor, me["systemRoles"]!.AsArray().Select(role => role!.GetValue<string>()));
            var before = ManagementState(intent); var refused = await (await intent.Send(who)).Json(403);
            Assert.Equal("forbidden", refused.S("code")); Assert.Equal(global::Hub.Api.Text.Get("perm.supervised_staff"), refused.S("detail"));
            Assert.Equal(before, ManagementState(intent));
        }
        var original = await (await f.As(TestData.Sam).GetAsync("/api/v1/me")).Json();
        Assert.False(original["capabilities"]!["readOnly"]!.GetValue<bool>());
        Assert.DoesNotContain(SystemRole.ReadOnly, original["systemRoles"]!.AsArray().Select(role => role!.GetValue<string>()));
        await (await f.As(TestData.Admin).Post($"/api/v1/admin/users/{samId}/roles", new { role = SystemRole.ReadOnly })).Json(204);
        try
        {
            var current = await (await f.As(TestData.Sam).GetAsync("/api/v1/me")).Json();
            var roles = current["systemRoles"]!.AsArray().Select(role => role!.GetValue<string>()).ToHashSet();
            Assert.Contains(SystemRole.Supervisor, roles); Assert.Contains(SystemRole.ReadOnly, roles); Assert.DoesNotContain(SystemRole.Admin, roles);
            Assert.True(current["capabilities"]!["readOnly"]!.GetValue<bool>());
            var actor = new Actor(samId, true, roles); Assert.Equal("perm.read_only", Permissions.ActOnStaff(actor, samId).Why);
            var before = ManagementState(intent); var refused = await (await intent.Send(TestData.Sam)).Json(403);
            Assert.Equal("forbidden", refused.S("code")); Assert.Equal(global::Hub.Api.Text.Get("perm.read_only"), refused.S("detail"));
            Assert.Equal(before, ManagementState(intent));
        }
        finally { await (await f.As(TestData.Admin).DeleteAsync($"/api/v1/admin/users/{samId}/roles/{SystemRole.ReadOnly}")).Json(204); }
        var restored = await (await f.As(TestData.Sam).GetAsync("/api/v1/me")).Json(); Assert.False(restored["capabilities"]!["readOnly"]!.GetValue<bool>());
        intent.AssertSaved(await (await intent.Send(TestData.Sam)).Json());
    }

    [Theory]
    [InlineData("manual")] [InlineData("reconcile")] [InlineData("schedule")]
    public async Task Management_commands_preserve_canonical_admin_with_stored_read_only_role(string operation)
    {
        var intent = await ValidManagementIntent(operation); var adminId = d.User(TestData.Admin);
        var original = await (await f.As(TestData.Admin).GetAsync("/api/v1/me")).Json();
        Assert.DoesNotContain(SystemRole.ReadOnly, original["systemRoles"]!.AsArray().Select(role => role!.GetValue<string>()));
        await (await f.As(TestData.Admin).Post($"/api/v1/admin/users/{adminId}/roles", new { role = SystemRole.ReadOnly })).Json(204);
        try
        {
            var current = await (await f.As(TestData.Admin).GetAsync("/api/v1/me")).Json();
            var roles = current["systemRoles"]!.AsArray().Select(role => role!.GetValue<string>()).ToHashSet();
            Assert.Contains(SystemRole.Admin, roles); Assert.Contains(SystemRole.ReadOnly, roles); Assert.True(current["capabilities"]!["admin"]!.GetValue<bool>());
            Assert.False(current["capabilities"]!["readOnly"]!.GetValue<bool>());
            var actor = new Actor(adminId, true, roles); Assert.False(actor.ReadOnly); Assert.True(Permissions.ActOnStaff(actor, d.User(TestData.Sam)));
            intent.AssertSaved(await (await intent.Send(TestData.Admin)).Json());
        }
        finally { await (await f.As(TestData.Admin).DeleteAsync($"/api/v1/admin/users/{adminId}/roles/{SystemRole.ReadOnly}")).Json(204); }
        Assert.False((await (await f.As(TestData.Admin).GetAsync("/api/v1/me")).Json())["capabilities"]!["readOnly"]!.GetValue<bool>());
    }

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
    public async Task Week_preview_and_apply_keep_their_property_names_order_and_types() // the typed records must serialise to the bytes the dialog already reads
    {
        var p = await Person(); var c = f.As(TestData.Sam);
        var draft = await (await c.Post("/api/v1/planning/entries", new { personId = p.Id, hoursPerWeek = 8, startWeek = W, endWeek = W, label = "Synthetic names", sourceCategory = "Proposal", confidence = "Possible" })).Json(201);
        var next = W.AddDays(7); var preview = await (await c.GetAsync($"/api/v1/planning/week-preview?operation=Copy&week={next:yyyy-MM-dd}")).Json();
        Assert.Equal(["operation", "week", "snapshot", "entries"], preview.AsObject().Select(x => x.Key));
        Assert.Equal("Copy", preview.S("operation")); Assert.Equal($"{next:yyyy-MM-dd}", preview.S("week"));
        var candidate = preview["entries"]!.AsArray().Single(e => e!.G("id") == draft.G("id"))!;
        Assert.Equal(["id", "rowVersion", "personId", "personName", "label", "hoursPerWeek", "confidence", "visibility", "warnings"], candidate.AsObject().Select(x => x.Key));
        Assert.Equal(JsonValueKind.Array, candidate["warnings"]!.GetValueKind());
        var applied = await (await c.Post("/api/v1/planning/week-apply", new { operation = "Copy", week = next, snapshot = preview["snapshot"], entries = new[] { new { id = candidate.G("id"), rowVersion = candidate.I("rowVersion") } } })).Json();
        Assert.Equal(["count", "items", "week"], applied.AsObject().Select(x => x.Key));
        Assert.Equal(1, applied["count"]!.GetValue<int>()); Assert.Equal($"{next:yyyy-MM-dd}", applied.S("week"));
        Assert.Equal(["id", "personId", "rowVersion"], applied["items"]![0]!.AsObject().Select(x => x.Key));
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
    public async Task Calendar_subscription_and_feed_keep_their_property_names_order_status_and_headers() // the typed records must serialise to the bytes the Sync dialog already reads
    {
        var p = await Person(); var c = f.As(p.Email); const string route = "/api/v1/me/calendar-subscription";
        var off = await (await c.GetAsync(route)).Json();
        Assert.Equal(["enabled", "rowVersion"], off.AsObject().Select(x => x.Key));
        Assert.Equal((JsonValueKind.False, JsonValueKind.Number, 0), (off["enabled"]!.GetValueKind(), off["rowVersion"]!.GetValueKind(), off.I("rowVersion")));

        var rotate = await c.Post(route, new { rowVersion = 0 }); Assert.Equal(HttpStatusCode.OK, rotate.StatusCode); Assert.True(rotate.Headers.CacheControl!.NoStore); // the token is shown once and never cached
        var link = await rotate.Json(); Assert.Equal(["url", "rowVersion"], link.AsObject().Select(x => x.Key));
        Assert.Matches(@"^https?://[^/]+/api/v1/calendar-feed\.ics\?token=[0-9a-f]{64}$", link.S("url")); Assert.Equal(JsonValueKind.Number, link["rowVersion"]!.GetValueKind());
        var on = await (await c.GetAsync(route)).Json(); Assert.Equal(["enabled", "rowVersion"], on.AsObject().Select(x => x.Key));
        Assert.Equal((JsonValueKind.True, link.I("rowVersion")), (on["enabled"]!.GetValueKind(), on.I("rowVersion")));
        Assert.Equal(HttpStatusCode.Conflict, (await c.Post(route, new { rowVersion = 0 })).StatusCode); // a stale rotate or revoke changes nothing
        Assert.Equal(HttpStatusCode.Conflict, (await c.DeleteAsync($"{route}?rowVersion=0")).StatusCode);

        using var anonymous = f.CreateClient(); var feed = await anonymous.GetAsync(link.S("url"));
        Assert.Equal(HttpStatusCode.OK, feed.StatusCode); Assert.Equal("text/calendar; charset=utf-8", feed.Content.Headers.ContentType!.ToString());
        Assert.True(feed.Headers.CacheControl!.NoStore && feed.Headers.CacheControl.Private); Assert.Equal("no-referrer", Assert.Single(feed.Headers.GetValues("Referrer-Policy")));
        Assert.StartsWith("BEGIN:VCALENDAR", await feed.Content.ReadAsStringAsync());
        var token = link.S("url")[^64..];
        foreach (var bad in new[] { "", $"?token={token[1..]}", $"?token=g{token[1..]}", $"?token={new string('0', 64)}" }) { // missing, short, not hex, unknown
            var refused = await anonymous.GetAsync("/api/v1/calendar-feed.ics" + bad);
            Assert.Equal(HttpStatusCode.Unauthorized, refused.StatusCode); Assert.True(refused.Headers.CacheControl!.NoStore); Assert.Equal("no-referrer", Assert.Single(refused.Headers.GetValues("Referrer-Policy")));
        }

        var revoked = await c.DeleteAsync($"{route}?rowVersion={link.I("rowVersion")}");
        Assert.Equal(HttpStatusCode.NoContent, revoked.StatusCode); Assert.Equal("", await revoked.Content.ReadAsStringAsync());
        var after = await (await c.GetAsync(route)).Json(); Assert.Equal(["enabled", "rowVersion"], after.AsObject().Select(x => x.Key));
        Assert.False(after["enabled"]!.GetValue<bool>()); Assert.True(after.I("rowVersion") > link.I("rowVersion"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(link.S("url"))).StatusCode);
        var other = await Person(); Assert.Equal(HttpStatusCode.NotFound, (await f.As(other.Email).DeleteAsync($"{route}?rowVersion=0")).StatusCode); // nothing to revoke
    }

    static string[] Keys(JsonNode? node) => [.. node!.AsObject().Select(x => x.Key)];
    static bool IsNull(JsonNode? node, string key) => node!.AsObject().TryGetPropertyValue(key, out var value) && value is null; // written as null, not left out
    async Task Flag(string name, bool on) => (await f.As(TestData.Admin).Put($"/api/v1/admin/settings/flag.{name}", new { value = on })).EnsureSuccessStatusCode();

    [Fact]
    public async Task Availability_read_keeps_its_property_names_order_and_nulls() // 040-S15: the typed records must serialise to the bytes the Vacation page already reads
    {
        var p = await Person(); var project = await d.Project(); var holiday = "Synthetic typed-contract holiday " + Guid.NewGuid().ToString("N")[..6];
        await f.DbAsync(async db => {
            db.ProjectMembers.Add(new ProjectMember { ProjectId = project.Id, UserId = p.Id, Roles = [ProjectRole.TeamMember], AddedAt = f.Clock.GetUtcNow() });
            var office = new Office { Name = "Synthetic typed-contract office", Code = Guid.NewGuid().ToString("N") }; db.Offices.Add(office); (await db.Users.SingleAsync(u => u.Id == p.Id)).OfficeId = office.Id;
            db.Holidays.Add(new Holiday { Date = W.AddDays(4), OfficeId = office.Id, Name = holiday });
            db.AvailabilityOverrides.Add(new PersonAvailabilityOverride { PersonId = p.Id, WorkDate = W.AddDays(1), AvailableHours = 0, Category = AvailabilityCategory.Unavailable, RowVersion = 1 });
            return await db.SaveChangesAsync();
        });
        var dated = await d.NewTask(project.Id, extra: new { assigneeId = p.Id, dueDate = W }); await d.NewTask(project.Id, extra: new { assigneeId = p.Id });
        await Apply(await Preview(Csv(p.Source, hours: "4")));
        await (await f.As(TestData.Sam).Put($"/api/v1/planning/schedules/{p.Id}", new { fromDate = W, weeklyHours = 40, workingDays = new[] { 1, 2, 3, 4, 5 }, rowVersion = 0 })).Json();

        var view = await View(p.Id);
        Assert.Equal(["from", "through", "canImport", "people", "holidays", "coverage", "imports"], Keys(view));
        Assert.Equal(($"{W:yyyy-MM-dd}", $"{W.AddDays(6):yyyy-MM-dd}", false), (view.S("from"), view.S("through"), view["canImport"]!.GetValue<bool>()));
        Assert.True(IsNull(view, "imports")); // the key is written, with null for a caller who is not an administrator
        var person = view["people"]![0]!;
        Assert.Equal(["id", "displayName", "canManage", "weeklyHours", "days", "schedules", "leave", "unverified"], Keys(person));
        Assert.Equal((JsonValueKind.True, JsonValueKind.Number, 40m), (person["canManage"]!.GetValueKind(), person["weeklyHours"]!.GetValueKind(), person["weeklyHours"]!.GetValue<decimal>()));
        Assert.Equal(7, person["days"]!.AsArray().Count);
        var day = person["days"]![0]!; Assert.Equal(["date", "normal", "available", "leave", "holiday", "provisional"], Keys(day));
        Assert.Equal(($"{W:yyyy-MM-dd}", JsonValueKind.Number, JsonValueKind.False), (day.S("date"), day["normal"]!.GetValueKind(), day["provisional"]!.GetValueKind()));
        Assert.Equal(JsonValueKind.True, person["days"]![1]!["provisional"]!.GetValueKind()); // the legacy unavailable day stays provisional
        var schedule = person["schedules"]![0]!; Assert.Equal(["fromDate", "weeklyHours", "workingDays", "rowVersion"], Keys(schedule)); // no weekdayHours with the flag off
        Assert.Equal([1, 2, 3, 4, 5], schedule["workingDays"]!.AsArray().Select(x => x!.GetValue<int>()));
        var leave = person["leave"]![0]!;
        Assert.Equal(["id", "fromDate", "throughDate", "hoursPerDay", "status", "sourceKind", "sourceUpdatedAt", "importedAt", "rowVersion", "sourceSystem", "externalId", "canEdit"], Keys(leave));
        Assert.Equal((p.Source, "L-1", 4m, "HRImport", false), (leave.S("sourceSystem"), leave.S("externalId"), leave["hoursPerDay"]!.GetValue<decimal>(), leave.S("sourceKind"), leave["canEdit"]!.GetValue<bool>()));
        var unverified = person["unverified"]![0]!; Assert.Equal(["workDate", "availableHours", "rowVersion"], Keys(unverified)); Assert.Equal($"{W.AddDays(1):yyyy-MM-dd}", unverified.S("workDate"));
        var holidayRow = view["holidays"]!.AsArray().Single(h => h!.S("name") == holiday)!; Assert.Equal(["date", "name", "officeId"], Keys(holidayRow)); Assert.Equal(JsonValueKind.String, holidayRow["officeId"]!.GetValueKind());
        var coverage = view["coverage"]!.AsArray(); Assert.Equal(2, coverage.Count);
        var concern = coverage.Single(c => c!.G("id") == dated.G("id"))!; Assert.Equal(["personId", "id", "key", "name", "type", "projectId", "dueDate", "role", "concern"], Keys(concern));
        Assert.Equal(("Task", "Assignee", "PartialAbsence", $"{W:yyyy-MM-dd}"), (concern.S("type"), concern.S("role"), concern.S("concern"), concern.S("dueDate")));
        var undated = coverage.Single(c => c!.G("id") != dated.G("id"))!; Assert.Equal("Unknown", undated.S("concern")); Assert.True(IsNull(undated, "dueDate")); // the due date is written as null

        var admin = await View(p.Id, TestData.Admin); // the administrator also sees who may import and when each source last did
        Assert.True(admin["canImport"]!.GetValue<bool>()); Assert.Equal(JsonValueKind.Array, admin["imports"]!.GetValueKind());
        var mark = admin["imports"]!.AsArray().Single(x => x!.S("sourceSystem") == p.Source)!; Assert.Equal(["sourceSystem", "importedAt", "sourceUpdatedAt"], Keys(mark));
        var own = await View(p.Id, p.Email); var ownPerson = own["people"]![0]!; Assert.True(IsNull(ownPerson, "schedules")); // schedules, the source and the reference are for those who manage the person
        Assert.Equal(["id", "displayName", "canManage", "weeklyHours", "days", "schedules", "leave", "unverified"], Keys(ownPerson));
        var hidden = ownPerson["leave"]![0]!; Assert.True(IsNull(hidden, "sourceSystem") && IsNull(hidden, "externalId"));
    }

    [Fact, Trait("flag-on", "capacity_polish")]
    public async Task Availability_schedules_write_weekday_hours_only_with_the_flag_on_and_null_when_the_row_has_none()
    {
        var p = await Person(); var sam = f.As(TestData.Sam);
        await (await sam.Put($"/api/v1/planning/schedules/{p.Id}", new { fromDate = W, weeklyHours = 40, workingDays = new[] { 1, 2, 3, 4, 5 }, rowVersion = 0 })).Json();
        await Flag("capacity_polish", true);
        try {
            await (await sam.Put($"/api/v1/planning/schedules/{p.Id}", new { fromDate = W.AddDays(7), weeklyHours = 20, workingDays = new[] { 1, 2, 3 }, weekdayHours = new[] { 0m, 8m, 8m, 4m, 0m, 0m, 0m }, rowVersion = 0 })).Json();
            var rows = (await View(p.Id))["people"]![0]!["schedules"]!.AsArray();
            Assert.Equal(["fromDate", "weeklyHours", "workingDays", "rowVersion", "weekdayHours"], Keys(rows[0])); Assert.True(IsNull(rows[0], "weekdayHours")); // a row with no vector writes null
            Assert.Equal(["fromDate", "weeklyHours", "workingDays", "rowVersion", "weekdayHours"], Keys(rows[1]));
            Assert.Equal([0m, 8m, 8m, 4m, 0m, 0m, 0m], rows[1]!["weekdayHours"]!.AsArray().Select(x => x!.GetValue<decimal>()));
        } finally { await Flag("capacity_polish", false); }
        Assert.All((await View(p.Id))["people"]![0]!["schedules"]!.AsArray(), row => Assert.Equal(["fromDate", "weeklyHours", "workingDays", "rowVersion"], Keys(row)));
    }

    [Fact]
    public async Task Hr_identities_and_leave_commands_keep_their_property_names_order_and_status() // 040-S15: the typed records must serialise to the bytes the import, reconcile and schedule dialogs already read
    {
        var p = await Person(); var admin = f.As(TestData.Admin); var sam = f.As(TestData.Sam);
        var identities = await (await admin.GetAsync("/api/v1/planning/hr-identities")).Json(); var identity = identities.AsArray().Single(x => x!.G("id") == p.Id)!;
        Assert.Equal(["id", "displayName", "hrSourceSystem", "hrPersonKey", "rowVersion"], Keys(identity)); Assert.Equal((p.Source, "employee-1"), (identity.S("hrSourceSystem"), identity.S("hrPersonKey")));
        Assert.Equal(HttpStatusCode.Forbidden, (await sam.GetAsync("/api/v1/planning/hr-identities")).StatusCode);
        var renamed = await admin.Put($"/api/v1/planning/hr-identities/{p.Id}", new { sourceSystem = p.Source, personKey = "employee-2", rowVersion = identity.I("rowVersion") });
        Assert.Equal(HttpStatusCode.OK, renamed.StatusCode); var saved = await renamed.Json(); Assert.Equal(["rowVersion"], Keys(saved)); Assert.True(saved.I("rowVersion") > identity.I("rowVersion"));
        Assert.Equal(HttpStatusCode.Conflict, (await admin.Put($"/api/v1/planning/hr-identities/{p.Id}", new { sourceSystem = p.Source, personKey = "employee-1", rowVersion = identity.I("rowVersion") })).StatusCode);
        await f.DbAsync(async db => { (await db.Users.SingleAsync(u => u.Id == p.Id)).HrPersonKey = "employee-1"; return await db.SaveChangesAsync(); });

        var preview = await Preview(Csv(p.Source, hours: "4"));
        Assert.Equal(["previewId", "rows", "errors", "canApply", "creates", "updates", "cancellations", "unchanged", "manualMatches", "affectedWeeks"], Keys(preview));
        Assert.Equal((JsonValueKind.String, JsonValueKind.True, 1, 0, 0, 0, 0), (preview["previewId"]!.GetValueKind(), preview["canApply"]!.GetValueKind(), preview.I("creates"), preview.I("updates"), preview.I("cancellations"), preview.I("unchanged"), preview.I("manualMatches")));
        Assert.Equal(["sourceSystem", "externalId", "personKey", "personId", "personVersion", "fromDate", "throughDate", "hoursPerDay", "status", "sourceUpdatedAt", "rowVersion"], Keys(preview["rows"]![0]));
        Assert.Equal(["personId", "fromWeek", "throughWeek"], Keys(preview["affectedWeeks"]![0])); Assert.Single(preview["affectedWeeks"]!.AsArray()); Assert.Empty(preview["errors"]!.AsArray());
        var unmapped = await Preview(Csv(p.Source) + "\nunknown,L-2,employee-1,Approved,2026-09-14,2026-09-16,,2026-09-13T10:00:00Z");
        Assert.True(IsNull(unmapped, "previewId")); Assert.False(unmapped["canApply"]!.GetValue<bool>());
        Assert.Equal(["row", "error"], Keys(unmapped["errors"]![0])); Assert.Equal((3, "availability.unmapped"), (unmapped["errors"]![0]!.I("row"), unmapped["errors"]![0]!.S("error")));
        var applied = await Apply(preview); Assert.Equal(["applied", "unchanged", "importedAt"], Keys(applied)); Assert.Equal((1, 0, JsonValueKind.String), (applied.I("applied"), applied.I("unchanged"), applied["importedAt"]!.GetValueKind()));
        Assert.Equal(["applied", "unchanged", "importedAt"], Keys(await Apply(await Preview(Csv(p.Source, hours: "4"))))); // an unchanged row is counted and not applied

        var manual = await sam.Post("/api/v1/planning/leave/manual", new { personId = p.Id, sourceSystem = p.Source, externalId = "M-1", fromDate = W.AddDays(3), throughDate = W.AddDays(3), status = "Approved", rowVersion = 0 });
        Assert.Equal(HttpStatusCode.OK, manual.StatusCode); var made = await manual.Json(); Assert.Equal(["id", "rowVersion"], Keys(made)); Assert.Equal(1, made.I("rowVersion"));
        var again = await (await sam.Post("/api/v1/planning/leave/manual", new { personId = p.Id, sourceSystem = p.Source, externalId = "M-1", fromDate = W.AddDays(3), throughDate = W.AddDays(4), status = "Approved", rowVersion = made.I("rowVersion") })).Json();
        Assert.Equal((made.G("id"), made.I("rowVersion") + 1), (again.G("id"), again.I("rowVersion"))); // the same record, one version on
        Assert.Equal(HttpStatusCode.Conflict, (await sam.Post("/api/v1/planning/leave/manual", new { personId = p.Id, sourceSystem = p.Source, externalId = "M-1", fromDate = W, throughDate = W, status = "Approved", rowVersion = made.I("rowVersion") })).StatusCode);

        var leave = (await View(p.Id))["people"]![0]!["leave"]!.AsArray().Single(x => x!.S("externalId") == "L-1")!.G("id");
        await f.DbAsync(async db => { db.AvailabilityOverrides.Add(new PersonAvailabilityOverride { PersonId = p.Id, WorkDate = W.AddDays(1), AvailableHours = 0, Category = AvailabilityCategory.Unavailable, RowVersion = 1 }); return await db.SaveChangesAsync(); });
        var reconciled = await sam.Post("/api/v1/planning/leave/reconcile", new { personId = p.Id, leaveId = leave, dates = new[] { new { workDate = W.AddDays(1), rowVersion = 1 } } });
        Assert.Equal(HttpStatusCode.OK, reconciled.StatusCode); var count = await reconciled.Json(); Assert.Equal(["count"], Keys(count)); Assert.Equal(1, count.I("count"));

        var schedule = await sam.Put($"/api/v1/planning/schedules/{p.Id}", new { fromDate = W, weeklyHours = 40, workingDays = new[] { 1, 2, 3, 4, 5 }, rowVersion = 0 });
        Assert.Equal(HttpStatusCode.OK, schedule.StatusCode); var row = await schedule.Json(); Assert.Equal(["id", "rowVersion"], Keys(row)); Assert.Equal(1, row.I("rowVersion"));
        Assert.Equal(HttpStatusCode.Conflict, (await sam.Put($"/api/v1/planning/schedules/{p.Id}", new { fromDate = W, weeklyHours = 30, workingDays = new[] { 1, 2, 3, 4, 5 }, rowVersion = 0 })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await sam.Put($"/api/v1/planning/schedules/{Guid.NewGuid()}", new { fromDate = W, weeklyHours = 30, workingDays = new[] { 1, 2, 3, 4, 5 }, rowVersion = 0 })).StatusCode);
    }

    [Fact]
    public async Task Availability_export_keeps_its_columns_and_rows_for_days_and_for_leave() // 040-S15: the export reads the typed view, so what it writes must not move
    {
        var p = await Person(); await Apply(await Preview(Csv(p.Source, hours: "4"))); var sam = f.As(TestData.Sam);
        var range = $"?from={W:yyyy-MM-dd}&through={W.AddDays(6):yyyy-MM-dd}&personId={p.Id}&format=csv";
        var leave = (await sam.GetStringAsync("/api/v1/planning/availability/export" + range + "&kind=leave")).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal("Person,From,Through,Absent hours per day (blank = full day),Status,Type,Source updated,Imported", leave[0]);
        Assert.Equal(2, leave.Length); Assert.Contains(",2026-09-14,2026-09-16,4.00,Approved,HRImport,2026-09-13T10:00:00+00:00,", leave[1]);
        var days = (await sam.GetStringAsync("/api/v1/planning/availability/export" + range)).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal("Person,Date,Contracted hours,Available hours,Leave hours,Holiday hours,Provisional", days[0]);
        Assert.Equal(8, days.Length); Assert.EndsWith(",2026-09-14,8.0,4.00,4.00,0,No", days[1]); Assert.EndsWith(",2026-09-19,0,0,0,0,No", days[6]);
        var xlsx = await sam.GetAsync("/api/v1/planning/availability/export" + range.Replace("csv", "xlsx")); Assert.Equal(HttpStatusCode.OK, xlsx.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await sam.GetAsync($"/api/v1/planning/availability/export?from={W:yyyy-MM-dd}&through={W.AddDays(-1):yyyy-MM-dd}&format=csv")).StatusCode);
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
