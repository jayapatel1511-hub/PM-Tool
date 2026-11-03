using System.Net;
using System.Text.RegularExpressions;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// AC-SCR-83 partial current examples / AC-SCR-84 dark boundary; no complete matrix or S49 acceptance claim.
public sealed class AdminRoleExplanationTests
{
    [Fact]
    public async Task Current_role_examples_are_admin_only_bounded_translated_readonly_and_dark_off()
    {
        using var f = new HubFactory(); var admin = f.As(TestData.Admin);
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/v1/admin/role-explanations")).StatusCode);
        (await admin.Put("/api/v1/admin/settings/flag.people_screens", new { value = true })).EnsureSuccessStatusCode();
        var before = f.Db(db => (db.ActivityLog.Count(), db.Users.Sum(user => (long)user.RowVersion), db.Notifications.Count(), db.Idempotency.Count()));
        var read = await (await admin.GetAsync("/api/v1/admin/role-explanations?role=derived-DisciplineLead&scenario=other-discipline")).Json();
        Assert.False(read["coverageComplete"]!.GetValue<bool>());
        Assert.DoesNotContain("system-pm-template-policy", read["gaps"]!.AsArray().Select(value => value!.GetValue<string>()));
        var role = Assert.Single(read["roles"]!.AsArray())!;
        Assert.Equal("Discipline Lead", role.S("label"));
        var action = role["examples"]!.AsArray().Single(value => value!.S("actionKey") == nameof(Permissions.CreateTask))!;
        Assert.False(action["allowed"]!.GetValue<bool>()); Assert.Equal("Create a task", action.S("actionLabel"));
        Assert.DoesNotMatch(new Regex("[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}", RegexOptions.IgnoreCase), read.ToJsonString());
        Assert.DoesNotContain("adminRole.", read.ToJsonString());
        Assert.Equal(PermissionExplanationCatalog.Bindings.Count, role["examples"]!.AsArray().Count);
        Assert.Equal(before, f.Db(db => (db.ActivityLog.Count(), db.Users.Sum(user => (long)user.RowVersion), db.Notifications.Count(), db.Idempotency.Count())));
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).GetAsync("/api/v1/admin/role-explanations")).StatusCode);
        foreach (var query in new[] { "role=forged", "scenario=forged", "actorId=forged", "role=baseline-Standard&role=system-Admin" })
            Assert.Equal(HttpStatusCode.BadRequest, (await admin.GetAsync("/api/v1/admin/role-explanations?" + query)).StatusCode);
        // Inactive is now an explicit bounded negative row scenario, not an arbitrary identity input.
        var inactive = await (await admin.GetAsync("/api/v1/admin/role-explanations?role=baseline-Standard&scenario=inactive-person")).Json();
        Assert.Equal("none", inactive.S("selectedMembershipKey"));
        var inactiveOperations = inactive["rows"]!.AsArray().SelectMany(row => row!["operations"]!.AsArray()).ToArray();
        Assert.NotEmpty(inactiveOperations);
        Assert.All(inactiveOperations, operation => Assert.Contains(operation!.S("decisionKind"), new[] { "NotEligible", "PolicyPending" }));
        Assert.Equal(before, f.Db(db => (db.ActivityLog.Count(), db.Users.Sum(user => (long)user.RowVersion), db.Notifications.Count(), db.Idempotency.Count())));
        (await admin.Put("/api/v1/admin/settings/flag.people_screens", new { value = false })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.NotFound, (await admin.GetAsync("/api/v1/admin/role-explanations")).StatusCode);
    }
}
