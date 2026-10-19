using System.Net;
using System.Text.Json.Nodes;
using Hub.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Packet 042 S08: the optional project stamp extends the existing pulse without changing its base stamp.
public sealed class LiveStampTests : IClassFixture<HubFactory>
{
    readonly HubFactory f;
    readonly TestData d;

    public LiveStampTests(HubFactory factory)
    {
        f = factory;
        d = new(factory);
    }

    async Task SetFlag(bool on) =>
        await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.live_project_stamp", new { value = on }).Result.Json();

    async Task<JsonNode> Pulse(string user, Guid? project = null)
    {
        var suffix = project is null ? "" : $"?projectId={project}";
        return await f.As(user).GetAsync("/api/v1/me/notifications/pulse" + suffix).Result.Json();
    }

    [Fact]
    [Trait("flag-off", "live_project_stamp")]
    public async Task Flag_off_keeps_the_request_and_response_unchanged()
    {
        await SetFlag(false);
        var p = await d.Project();
        var withoutProject = await Pulse(TestData.Alex);
        var withProject = await Pulse(TestData.Alex, p.Id);

        Assert.Equal(withoutProject.S("stamp"), withProject.S("stamp"));
        Assert.DoesNotContain("projectStamp", withProject.AsObject().Select(x => x.Key));
    }

    [Fact]
    [Trait("flag-on", "live_project_stamp")]
    public async Task Visible_project_stamp_changes_while_base_stamp_matches_the_unparameterized_pulse()
    {
        await SetFlag(true);
        var p = await d.Project();
        var first = await Pulse(TestData.Alex, p.Id);
        Assert.Equal((await Pulse(TestData.Alex)).S("stamp"), first.S("stamp"));
        var firstProjectStamp = first.S("projectStamp");

        f.Clock.Now = f.Clock.Now.AddMinutes(1);
        await d.NewTask(p.Id, TestData.Pm);
        var afterActivity = await Pulse(TestData.Alex, p.Id);
        Assert.Equal((await Pulse(TestData.Alex)).S("stamp"), afterActivity.S("stamp"));
        Assert.NotEqual(firstProjectStamp, afterActivity.S("projectStamp"));

        var later = f.Clock.Now.AddMinutes(1);
        await f.DbAsync(async db =>
        {
            var state = await db.ProjectStates.SingleAsync(x => x.ProjectId == p.Id);
            state.EvaluatedAt = later;
            return await db.SaveChangesAsync();
        });
        var afterEvaluation = await Pulse(TestData.Alex, p.Id);
        Assert.Equal((await Pulse(TestData.Alex)).S("stamp"), afterEvaluation.S("stamp"));
        Assert.NotEqual(afterActivity.S("projectStamp"), afterEvaluation.S("projectStamp"));
    }

    [Fact]
    [Trait("flag-on", "live_project_stamp")]
    public async Task Restricted_and_unknown_projects_have_identical_base_pulse_without_project_stamp()
    {
        await SetFlag(true);
        var restricted = await d.Project();
        var unknown = Guid.NewGuid();
        await f.DbAsync(async db =>
        {
            var project = await db.Projects.SingleAsync(x => x.Id == restricted.Id);
            project.Visibility = Hub.Domain.Visibility.Restricted;
            var member = await db.ProjectMembers.SingleAsync(x => x.ProjectId == restricted.Id && x.UserId == d.User(TestData.Alex));
            member.RemovedAt = f.Clock.Now;
            return await db.SaveChangesAsync();
        });

        var hidden = await Pulse(TestData.Alex, restricted.Id);
        var missing = await Pulse(TestData.Alex, unknown);
        Assert.Equal(hidden.S("stamp"), missing.S("stamp"));
        Assert.DoesNotContain("projectStamp", hidden.AsObject().Select(x => x.Key));
        Assert.DoesNotContain("projectStamp", missing.AsObject().Select(x => x.Key));
    }
}
