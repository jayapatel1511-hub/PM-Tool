using Hub.Api.Data;
using Hub.Api.Features;

namespace Hub.Tests.Domain;

public sealed class TaskPickupRulesTests
{
    [Fact]
    [Trait("flag-off", "task_pickup")]
    public void Same_team_without_org_structure_requires_the_same_non_null_manager()
    {
        var manager = Guid.NewGuid();
        Assert.True(TaskPickupRules.SameTeam(false, null, null, manager, manager, true, true));
        Assert.False(TaskPickupRules.SameTeam(false, null, null, null, null, true, true));
        Assert.False(TaskPickupRules.SameTeam(false, null, null, manager, Guid.NewGuid(), true, true));
    }

    [Fact]
    [Trait("flag-on", "task_pickup")]
    public void Org_structure_uses_home_unit_and_does_not_fallback_to_manager()
    {
        var manager = Guid.NewGuid();
        var unit = Guid.NewGuid();
        Assert.True(TaskPickupRules.SameTeam(true, unit, unit, manager, Guid.NewGuid(), true, true));
        Assert.True(TaskPickupRules.SameTeam(true, null, null, manager, manager, true, true));
        Assert.False(TaskPickupRules.SameTeam(true, unit, null, manager, manager, true, true));
        Assert.False(TaskPickupRules.SameTeam(true, unit, Guid.NewGuid(), manager, manager, true, true));
        Assert.False(TaskPickupRules.SameTeam(true, unit, unit, manager, manager, false, true));
    }

    [Fact]
    public void Legacy_overload_never_groups_unassigned_or_inactive_people()
    {
        var manager = Guid.NewGuid();
        var assignee = new AppUser { SupervisorId = manager, IsActive = true };
        var picker = new AppUser { SupervisorId = manager, IsActive = false };
        Assert.False(TaskPickupRules.SameTeam(assignee, picker));
        assignee.SupervisorId = null; picker.IsActive = true;
        Assert.False(TaskPickupRules.SameTeam(assignee, picker));
    }
}
