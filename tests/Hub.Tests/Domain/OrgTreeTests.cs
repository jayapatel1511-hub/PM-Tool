using Hub.Domain;
using Hub.Api.Infrastructure;

namespace Hub.Tests.Domain;

public class OrgTreeTests
{
    [Fact]
    public void Refuses_parent_cycles_and_invalid_level_order()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        Assert.True(OrgTree.CreatesCycle(a, b, new Dictionary<Guid, Guid?> { [b] = a }));
        Assert.False(OrgTree.CreatesCycle(a, null, new Dictionary<Guid, Guid?>()));
        Assert.True(OrgTree.IsLevelOrderValid(2, 1));
        Assert.False(OrgTree.IsLevelOrderValid(1, 2));
    }

    [Fact]
    public void Leader_scope_includes_unit_subtree_once()
    {
        var leader = Guid.NewGuid(); var one = Guid.NewGuid(); var two = Guid.NewGuid(); var outsider = Guid.NewGuid();
        var root = Guid.NewGuid(); var child = Guid.NewGuid();
        var people = new[] { new OrgTree.Person(one, null, root), new OrgTree.Person(two, null, child), new OrgTree.Person(outsider, null, null) };
        var units = new[] { new OrgTree.Unit(root, null, 0, leader), new OrgTree.Unit(child, root, 1, null) };
        var scope = OrgTree.PeopleScope(new OrgTree.Person(leader, null, null), people, units, false);
        Assert.Equal(new[] { one, two }.OrderBy(x => x), scope.OrderBy(x => x));
    }

    [Fact]
    public void Indirect_scope_is_empty_when_reporting_cycle_exists()
    {
        var leader = Guid.NewGuid(); var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var people = new[] { new OrgTree.Person(a, b, null), new OrgTree.Person(b, a, null) };
        var scope = OrgTree.PeopleScope(new OrgTree.Person(leader, a, null), people, [], true);
        Assert.Empty(scope);
    }

    [Fact]
    public void Viewer_on_reporting_cycle_gets_no_indirect_scope()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
        var people = new[] { new OrgTree.Person(a, b, null), new OrgTree.Person(b, a, null), new OrgTree.Person(c, b, null) };
        var scope = OrgTree.PeopleScope(people[0], people, [], true);
        Assert.Contains(b, scope); Assert.DoesNotContain(c, scope); Assert.DoesNotContain(a, scope);
    }

    [Fact]
    public void Ordinary_manager_chain_returns_direct_and_indirect_reports()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid(); var d = Guid.NewGuid();
        var people = new[] { new OrgTree.Person(a, null, null), new OrgTree.Person(b, a, null), new OrgTree.Person(c, b, null), new OrgTree.Person(d, c, null) };
        Assert.Equal(new[] { b }, OrgTree.PeopleScope(people[0], people, [], false));
        Assert.Equal(new[] { b, c, d }.OrderBy(x => x), OrgTree.PeopleScope(people[0], people, [], true).OrderBy(x => x));
    }

    [Fact]
    public void Inactive_manager_does_not_bridge_to_indirect_reports()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid(); var d = Guid.NewGuid();
        var people = new[] { new OrgTree.Person(a, null, null), new OrgTree.Person(b, a, null), new OrgTree.Person(c, b, null, false), new OrgTree.Person(d, c, null) };
        Assert.Equal(new[] { b }, OrgTree.PeopleScope(people[0], people, [], true));
    }

    [Fact]
    public void All_staff_scope_contains_active_people_only()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
        var people = new[] { new OrgTree.Person(a, null, null), new OrgTree.Person(b, a, null), new OrgTree.Person(c, a, null, false) };
        Assert.Equal(new[] { a, b }.OrderBy(x => x), OrgTree.PeopleScope(people[0], people, [], false, allStaff: true).OrderBy(x => x));
    }
}
