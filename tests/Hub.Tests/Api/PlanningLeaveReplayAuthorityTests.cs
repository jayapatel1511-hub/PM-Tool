using System.Net;
using System.Text;
using System.Text.Json;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

// ISOLATED CHECKPOINT 35 DRAFT: authored, uncompiled and UNRUN. Root owns integration and provider execution.
public sealed class PlanningLeaveReplayAuthorityTests : IDisposable
{
    readonly HubFactory f = new();
    static readonly DateOnly Day = new(2026, 11, 2);
    public PlanningLeaveReplayAuthorityTests() => f.Clock.SetDate(2026, 11, 2);
    public void Dispose() => f.Dispose();

    string State() => f.Db(db => JsonSerializer.Serialize(new {
        Users = db.Users.AsNoTracking().OrderBy(row => row.Id).ToArray(),
        Roles = db.UserRoles.AsNoTracking().OrderBy(row => row.Id).ToArray(),
        Leaves = db.ApprovedLeaves.AsNoTracking().OrderBy(row => row.Id).ToArray(),
        Overrides = db.AvailabilityOverrides.AsNoTracking().OrderBy(row => row.Id).ToArray(),
        Schedules = db.PersonSchedules.AsNoTracking().OrderBy(row => row.Id).ToArray(),
        CapacityVersions = db.PersonDateVersions.AsNoTracking().OrderBy(row => row.PersonId).ThenBy(row => row.WorkDate).ToArray(),
        Imports = db.LeaveImportStates.AsNoTracking().OrderBy(row => row.SourceSystem).ToArray(),
        Receipts = db.Idempotency.AsNoTracking().OrderBy(row => row.Id).ToArray(),
        Activity = db.ActivityLog.AsNoTracking().OrderBy(row => row.Id).ToArray(),
        Outbox = db.Outbox.AsNoTracking().OrderBy(row => row.Id).ToArray(),
        Notices = db.Notifications.AsNoTracking().OrderBy(row => row.Id).ToArray(),
        Emails = db.Emails.AsNoTracking().OrderBy(row => row.Id).ToArray(),
    }, JsonOpts.Web));

    [Theory]
    [InlineData("manual")]
    [InlineData("reconcile")]
    public async Task Effective_read_only_supervisor_cannot_replay_previously_authorized_leave_success(string operation)
    {
        // Each theory case owns a disposable HubFactory database; seeded real personas are configured through APIs.
        var supervisor = await (await f.As(TestData.Sam).GetAsync("/api/v1/me")).Json();
        var samId = supervisor.G("id");
        Assert.Contains(SystemRole.Supervisor, supervisor["systemRoles"]!.AsArray().Select(role => role!.GetValue<string>()));
        Assert.False(supervisor["capabilities"]!["readOnly"]!.GetValue<bool>());
        var personId = (await (await f.As(TestData.Alex).GetAsync("/api/v1/me")).Json()).G("id");
        var personVersion = f.Db(db => db.Users.AsNoTracking().Single(row => row.Id == personId).RowVersion);
        await (await f.As(TestData.Admin).Patch($"/api/v1/admin/users/{personId}",
            new { supervisorId = samId, weeklyCapacityHours = 40m }, personVersion)).Json();
        Assert.Equal(samId, f.Db(db => db.Users.Single(row => row.Id == personId).SupervisorId));

        const string manualPath = "/api/v1/planning/leave/manual";
        var source = "Synthetic replay HR " + Guid.NewGuid().ToString("N");
        var external = "Synthetic approved reference " + Guid.NewGuid().ToString("N");
        string Body(object value) => JsonSerializer.Serialize(value, JsonOpts.Web);
        var path = "/api/v1/planning/leave/" + operation;
        string originalBody; string freshBody; Guid leaveId;
        DateOnly? reconciledDate = null;
        if (operation == "manual")
        {
            originalBody = Body(new { personId, sourceSystem = source, externalId = external,
                fromDate = Day, throughDate = Day, status = "Approved", rowVersion = 0 });
            freshBody = ""; leaveId = Guid.Empty; // Filled from the real first successful response below.
        }
        else
        {
            Assert.Equal("reconcile", operation);
            var leave = await (await f.As(TestData.Sam).Post(manualPath, new { personId, sourceSystem = source,
                externalId = external, fromDate = Day, throughDate = Day.AddDays(1), status = "Approved", rowVersion = 0 })).Json();
            leaveId = leave.G("id");
            // Supported capacity API supplies the actual unreconciled rows; no direct storage/provenance fixture.
            var first = await (await f.As(TestData.Sam).Put($"/api/v1/users/{personId}/availability/{Day:yyyy-MM-dd}",
                new AllocationEndpoints.AvailabilityBody(0, 0, AvailabilityCategory.Unavailable))).Json();
            var second = await (await f.As(TestData.Sam).Put($"/api/v1/users/{personId}/availability/{Day.AddDays(1):yyyy-MM-dd}",
                new AllocationEndpoints.AvailabilityBody(0, 0, AvailabilityCategory.Unavailable))).Json();
            originalBody = Body(new { personId, leaveId, dates = new[] { new { workDate = Day, rowVersion = first.I("rowVersion") } } });
            freshBody = Body(new { personId, leaveId, dates = new[] { new { workDate = Day.AddDays(1), rowVersion = second.I("rowVersion") } } });
            reconciledDate = Day;
        }
        async Task<HttpResponseMessage> Send(string body, string key)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            request.Headers.Add("Idempotency-Key", key);
            return await f.As(TestData.Sam).SendAsync(request);
        }
        var oldKey = Guid.NewGuid().ToString();
        using var committed = await Send(originalBody, oldKey);
        var committedBytes = await committed.Content.ReadAsStringAsync(); var result = await committed.Json();
        if (operation == "manual")
        {
            leaveId = result.G("id");
            freshBody = Body(new { personId, sourceSystem = source, externalId = external, fromDate = Day,
                throughDate = Day.AddDays(1), status = "Approved", rowVersion = result.I("rowVersion") });
        }
        else Assert.Equal(1, result.I("count"));
        var originalLeave = f.Db(db => db.ApprovedLeaves.AsNoTracking().Single(row => row.Id == leaveId));
        Assert.Equal("ManualHR", originalLeave.SourceKind); Assert.Equal(samId, originalLeave.CreatedBy);
        Assert.Equal(source, originalLeave.SourceSystem); Assert.Equal(external, originalLeave.ExternalId);
        if (reconciledDate is { } date)
        {
            var linked = f.Db(db => db.AvailabilityOverrides.AsNoTracking().Single(row => row.PersonId == personId && row.WorkDate == date));
            Assert.Equal(leaveId, linked.ReconciledLeaveId); Assert.Equal(samId, linked.UpdatedBy);
            Assert.Null(f.Db(db => db.AvailabilityOverrides.Single(row => row.PersonId == personId && row.WorkDate == Day.AddDays(1)).ReconciledLeaveId));
        }
        var receipt = f.Db(db => db.Idempotency.AsNoTracking().Single(row => row.UserId == samId && row.Key == oldKey));
        Assert.Equal(200, receipt.Status); Assert.StartsWith(path + "|", receipt.Path);
        Assert.Equal(committedBytes, Encoding.UTF8.GetString(receipt.Body));
        var authorizedState = State();
        using (var authorizedReplay = await Send(originalBody, oldKey))
        {
            Assert.Equal(HttpStatusCode.OK, authorizedReplay.StatusCode);
            Assert.True(authorizedReplay.Headers.Contains("Idempotent-Replayed"));
            Assert.Equal(committedBytes, await authorizedReplay.Content.ReadAsStringAsync());
        }
        Assert.Equal(authorizedState, State());

        await (await f.As(TestData.Admin).Post($"/api/v1/admin/users/{samId}/roles", new { role = SystemRole.ReadOnly })).Json(204);
        var current = await (await f.As(TestData.Sam).GetAsync("/api/v1/me")).Json();
        var roles = current["systemRoles"]!.AsArray().Select(role => role!.GetValue<string>()).ToHashSet();
        Assert.Contains(SystemRole.Supervisor, roles); Assert.Contains(SystemRole.ReadOnly, roles); Assert.DoesNotContain(SystemRole.Admin, roles);
        Assert.True(current["capabilities"]!["readOnly"]!.GetValue<bool>());
        Assert.Equal(samId, f.Db(db => db.Users.Single(row => row.Id == personId).SupervisorId));
        var before = State(); var freshKey = Guid.NewGuid().ToString();
        foreach (var attempt in new[] { (Body: freshBody, Key: freshKey), (Body: originalBody, Key: oldKey) })
        {
            using var refused = await Send(attempt.Body, attempt.Key);
            var error = await refused.Json(403);
            Assert.False(refused.Headers.Contains("Idempotent-Replayed"));
            Assert.Equal("forbidden", error.S("code")); Assert.Equal(global::Hub.Api.Text.Get("perm.read_only"), error.S("detail"));
            Assert.Equal(before, State());
        }
        Assert.False(f.Db(db => db.Idempotency.Any(row => row.UserId == samId && row.Key == freshKey)));
        Assert.Equal(JsonSerializer.Serialize(originalLeave, JsonOpts.Web), f.Db(db => JsonSerializer.Serialize(
            db.ApprovedLeaves.AsNoTracking().Single(row => row.Id == leaveId), JsonOpts.Web)));
        var retained = f.Db(db => db.Idempotency.AsNoTracking().Single(row => row.Id == receipt.Id));
        Assert.Equal(JsonSerializer.Serialize(receipt, JsonOpts.Web), JsonSerializer.Serialize(retained, JsonOpts.Web));
    }
}
