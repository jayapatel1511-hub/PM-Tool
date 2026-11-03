using System.Net;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// AC-SCR-82 columns only / AC-SCR-84. Deactivation is deliberately not implemented by phase1.
public sealed class AdminUserScreenTests
{
    sealed record Graph(Guid Supervisor, Guid Mixed, Guid Inactive, Guid Direct, Guid Discipline);
    static async Task<Graph> Seed(HubFactory f)
    {
        return await f.DbAsync<Graph>(async db =>
        {
            var discipline = new Discipline { Name = "Screen retired discipline", Code = "SRD", IsActive = false };
            var level = new OrgLevel { Name = "Screen level", SortOrder = 200 };
            var s = new AppUser { DisplayName = "Screen supervisor", Email = "screen-supervisor@test.invalid", WeeklyCapacityHours = 0, HomeDisciplineId = discipline.Id };
            var mixed = new AppUser { DisplayName = "Screen mixed", Email = "screen-mixed@test.invalid" };
            var inactive = new AppUser { DisplayName = "Screen inactive supervisor", Email = "screen-inactive@test.invalid", IsActive = false };
            var unit = new OrgUnit { Name = "Screen unit", LevelId = level.Id, LeaderId = s.Id };
            var child = new OrgUnit { Name = "Screen child", LevelId = level.Id, ParentId = unit.Id };
            var closed = new OrgUnit { Name = "Screen closed", LevelId = level.Id, LeaderId = s.Id, ClosedOn = new(2026, 1, 1) };
            var direct = new AppUser { DisplayName = "Screen direct", Email = "screen-direct@test.invalid", SupervisorId = s.Id, UnitId = child.Id };
            db.AddRange(discipline, level, s, mixed, inactive, unit, child, closed, direct,
                new AppUser { DisplayName = "Screen indirect", Email = "screen-indirect@test.invalid", SupervisorId = direct.Id },
                new AppUser { DisplayName = "Screen unit member", Email = "screen-unit@test.invalid", UnitId = child.Id },
                new AppUser { DisplayName = "Screen closed member", Email = "screen-closed@test.invalid", UnitId = closed.Id },
                new AppUser { DisplayName = "Screen outside", Email = "screen-outside@test.invalid" },
                new AppUser { DisplayName = "Screen inactive report", Email = "screen-inactive-report@test.invalid", SupervisorId = s.Id, IsActive = false },
                new AppUser { DisplayName = "Screen inactive manager report", Email = "screen-inactive-manager-report@test.invalid", SupervisorId = inactive.Id },
                new PersonSchedule { PersonId = s.Id, FromDate = new(2026, 1, 1), WeeklyHours = 60 });
            foreach (var id in new[] { s.Id, mixed.Id, inactive.Id }) db.UserRoles.Add(new() { UserId = id, Role = SystemRole.Supervisor, GrantedAt = f.Clock.GetUtcNow() });
            db.UserRoles.Add(new() { UserId = mixed.Id, Role = SystemRole.Admin, GrantedAt = f.Clock.GetUtcNow() });
            // Insert the leader and units before linking that leader back to the unit; the final graph is unchanged.
            await db.SaveChangesAsync();
            s.UnitId = unit.Id;
            await db.SaveChangesAsync(); return new(s.Id, mixed.Id, inactive.Id, direct.Id, discipline.Id);
        });
    }
    static OrgSettings Settings(bool structure, bool indirect = false, bool on = true) => new()
    {
        DefaultWeeklyCapacityHours = 41, IncludeIndirectReports = indirect,
        Flags = new Dictionary<string, bool> { ["people_screens"] = on, ["org_structure"] = structure },
    };
    static Task<Dictionary<Guid, AdminUserScreen>> Read(HubFactory f, Graph g, OrgSettings settings) => f.DbAsync(async db =>
    {
        await using var snapshot = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead);
        return await AdminUserScreenReads.Read(db, [g.Supervisor, g.Mixed, g.Inactive, g.Direct], settings, f.Clock);
    });

    [Fact]
    public async Task Structure_off_is_direct_reports_even_for_mixed_admin_and_capacity_is_only_base_configuration()
    {
        using var f = new HubFactory(); var g = await Seed(f);
        var before = f.Db(db => db.ActivityLog.Count());
        var rows = await Read(f, g, Settings(false, true));
        Assert.Equal(1, rows[g.Supervisor].PeopleScopeCount); Assert.Equal("DirectReports", rows[g.Supervisor].ScopeBasis);
        Assert.False(rows[g.Supervisor].ScopeIncludesSelf); Assert.Equal(0, rows[g.Mixed].PeopleScopeCount);
        Assert.Equal(0m, rows[g.Supervisor].BaseWeeklyCapacityHours); Assert.Equal("Override", rows[g.Supervisor].CapacityBasis);
        Assert.Equal(41m, rows[g.Mixed].BaseWeeklyCapacityHours); Assert.Equal("OrganisationDefault", rows[g.Mixed].CapacityBasis);
        Assert.Equal(g.Discipline, rows[g.Supervisor].HomeDiscipline!.Id); Assert.Equal("Inactive", rows[g.Supervisor].HomeDisciplineState);
        Assert.Null(rows[g.Direct].PeopleScopeCount); Assert.Equal("NotApplicable", rows[g.Direct].ScopeBasis);
        Assert.Equal("NotSet", rows[g.Direct].HomeDisciplineState); Assert.Null(rows[g.Direct].HomeDiscipline);
        Assert.True(rows[g.Inactive].ConfiguredInactive); Assert.Equal(1, rows[g.Inactive].PeopleScopeCount);
        Assert.Equal(before, f.Db(db => db.ActivityLog.Count()));
        Assert.Empty(await Read(f, g, Settings(true, true, false)));
    }

    [Theory]
    [InlineData(false, 4)] [InlineData(true, 5)]
    public async Task Structure_on_uses_target_scope_with_overlap_self_and_existing_closed_unit_behavior(bool indirect, int expected)
    {
        using var f = new HubFactory(); var g = await Seed(f);
        var rows = await Read(f, g, Settings(true, indirect));
        Assert.Equal(expected, rows[g.Supervisor].PeopleScopeCount); Assert.True(rows[g.Supervisor].ScopeIncludesSelf);
        Assert.Equal("OrgPeopleScope", rows[g.Supervisor].ScopeBasis);
        Assert.Equal(f.Db(db => db.Users.Count(user => user.IsActive)), rows[g.Mixed].PeopleScopeCount);
        Assert.Equal(1, rows[g.Inactive].PeopleScopeCount); Assert.False(rows[g.Inactive].ScopeIncludesSelf);
        var characterization = await f.DbAsync(db => PeopleScope.Load(db, new(g.Supervisor, true, new HashSet<string> { SystemRole.Supervisor }), indirect));
        Assert.Equal(characterization.Count, rows[g.Supervisor].PeopleScopeCount);
        await f.DbAsync(async db => { db.Users.Add(new() { DisplayName = "Fresh direct", Email = "fresh-direct@test.invalid", SupervisorId = g.Supervisor }); return await db.SaveChangesAsync(); });
        var current = await Read(f, g, Settings(true, indirect)); Assert.Equal(expected + 1, current[g.Supervisor].PeopleScopeCount);
    }

    [Fact]
    public async Task Http_screen_is_admin_only_coherent_and_absent_off_without_removing_old_fields()
    {
        using var f = new HubFactory(); var g = await Seed(f); var admin = f.As(TestData.Admin);
        (await admin.Put("/api/v1/admin/settings/flag.people_screens", new { value = true })).EnsureSuccessStatusCode();
        var response = await (await admin.GetAsync("/api/v1/admin/users?q=Screen")).Json();
        var row = response.AsArray().Single(value => value!.G("id") == g.Supervisor)!;
        Assert.Equal(row.I("rowVersion"), row["screen"]!.I("userRowVersion")); Assert.Equal(g.Supervisor, row["screen"]!.G("userId"));
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).GetAsync("/api/v1/admin/users")).StatusCode);
        (await admin.Put("/api/v1/admin/settings/flag.people_screens", new { value = false })).EnsureSuccessStatusCode();
        var dark = await (await admin.GetAsync("/api/v1/admin/users?q=Screen")).Json();
        Assert.All(dark.AsArray(), value => { Assert.Null(value!["screen"]); Assert.Contains("weeklyCapacityHours", value.AsObject().Select(pair => pair.Key)); Assert.Contains("isTemplateEditor", value.AsObject().Select(pair => pair.Key)); });
    }
}
