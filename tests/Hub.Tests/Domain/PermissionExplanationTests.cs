using System.Reflection;
using Hub.Domain;

namespace Hub.Tests.Domain;

/// Current enforcement examples, not a second grant matrix. The complete-coverage gate stays blocked honestly.
public sealed class PermissionExplanationTests
{
    [Fact]
    public void Every_public_allow_bool_and_correction_tuple_has_one_actual_method_binding()
    {
        var methods = typeof(Permissions).GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.ReturnType == typeof(Allow) || method.ReturnType == typeof(bool) || method.ReturnType == typeof((Allow, bool)))
            .Select(method => new PermissionActionBinding(method.Name, method.ToString()!)).OrderBy(row => row.Key).ToArray();
        Assert.Equal(methods, PermissionExplanationCatalog.Bindings.OrderBy(row => row.Key).ToArray());
        Assert.Equal(methods.Length, methods.Select(row => row.Key).Distinct().Count());
    }

    [Fact]
    public void Discipline_lead_examples_call_current_own_other_scope_and_milestone_rules()
    {
        var me = Guid.Parse("00000000-0000-0000-0000-000000005601");
        var civil = Guid.Parse("00000000-0000-0000-0000-000000005604");
        var electrical = Guid.Parse("00000000-0000-0000-0000-000000005605");
        var actor = new Actor(me, true, new HashSet<string>());
        var project = new ProjectContext(Guid.NewGuid(), ProjectStatus.Active, Visibility.Open, Guid.NewGuid(), true,
            new HashSet<string> { ProjectRole.TeamMember }, new HashSet<Guid> { civil }, civil);
        var own = PermissionExplanationCatalog.Evaluate("derived-DisciplineLead", nameof(Permissions.CreateTask), "same-discipline");
        var other = PermissionExplanationCatalog.Evaluate("derived-DisciplineLead", nameof(Permissions.CreateTask), "other-discipline");
        Assert.True(own.Outcome.Allowed); Assert.False(other.Outcome.Allowed);
        Assert.Equal(Permissions.CreateTask(actor, project, civil).Ok, own.Outcome.Allowed);
        Assert.Equal(Permissions.CreateTask(actor, project, electrical).Why, other.Outcome.ReasonCode);
        Assert.False(PermissionExplanationCatalog.Evaluate("derived-DisciplineLead", nameof(Permissions.ManageMilestones), "same-discipline").Outcome.Allowed);
    }

    [Fact]
    public void Current_overrides_privacy_boolean_reasons_and_resolved_template_policy_stay_canonical()
    {
        Assert.False(PermissionExplanationCatalog.Evaluate("project-PM", nameof(Permissions.EditProject), "read-only").Outcome.Allowed);
        Assert.True(PermissionExplanationCatalog.Evaluate("system-Admin", nameof(Permissions.EditProject), "read-only").Outcome.Allowed);
        Assert.False(PermissionExplanationCatalog.Evaluate("baseline-Standard", nameof(Permissions.CanView), "restricted-outsider").Outcome.Allowed);
        Assert.False(PermissionExplanationCatalog.Evaluate("system-Admin", nameof(Permissions.SeePlanningEntry), "private-draft").Outcome.Allowed);
        var boolean = PermissionExplanationCatalog.Evaluate("system-Executive", nameof(Permissions.ViewAllStaff), "same-discipline");
        Assert.True(boolean.Outcome.Allowed); Assert.Null(boolean.Outcome.ReasonCode);
        Assert.Equal("Condition", PermissionExplanationCatalog.Evaluate("project-PM", nameof(Permissions.IsPM), "same-discipline").ResultKind);
        Assert.True(PermissionExplanationCatalog.Evaluate("system-ProjectManager", nameof(Permissions.ManageTemplates), "same-discipline").Outcome.Allowed);
        Assert.True(PermissionExplanationCatalog.Evaluate("system-ProjectManager", nameof(Permissions.ManageTemplates), "template-editor").Outcome.Allowed);
        Assert.False(PermissionExplanationCatalog.Evaluate("system-ProjectManager", nameof(Permissions.ManageTemplates), "read-only").Outcome.Allowed);
        Assert.False(PermissionExplanationCatalog.Evaluate("baseline-Standard", nameof(Permissions.ManageTemplates), "same-discipline").Outcome.Allowed);
        Assert.True(PermissionExplanationCatalog.Evaluate("baseline-Standard", nameof(Permissions.ManageTemplates), "template-editor").Outcome.Allowed);
        Assert.True(PermissionExplanationCatalog.Evaluate("system-Admin", nameof(Permissions.ManageTemplates), "read-only").Outcome.Allowed);
        Assert.DoesNotContain("system-pm-template-policy", PermissionExplanationCatalog.CompletionGaps);
        Assert.Contains("compound-and-transport-guards", PermissionExplanationCatalog.CompletionGaps);
    }

    [Fact]
    public void Contribution_examples_require_own_actual_membership_even_for_admin_and_pm()
    {
        foreach (var role in new[] { "baseline-Standard", "project-PM", "system-Admin" })
        {
            var own = PermissionExplanationCatalog.Evaluate(role, nameof(Permissions.MarkTaskContribution), "own-item");
            Assert.True(own.Outcome.Allowed);
            Assert.Contains("adminRole.fact.ownTaskCollaborator", own.FactKeys);
            var other = PermissionExplanationCatalog.Evaluate(role, nameof(Permissions.MarkTaskContribution), "same-discipline");
            Assert.False(other.Outcome.Allowed);
            Assert.Contains("adminRole.fact.notTaskCollaborator", other.FactKeys);
        }
        Assert.False(PermissionExplanationCatalog.Evaluate("project-PM", nameof(Permissions.MarkTaskContribution), "inactive-person").Outcome.Allowed);
        Assert.False(PermissionExplanationCatalog.Evaluate("baseline-Standard", nameof(Permissions.MarkTaskContribution), "archived-project").Outcome.Allowed);
    }

    [Fact]
    public void Public_examples_are_bounded_labelled_and_never_publish_synthetic_identifiers()
    {
        var roles = PermissionExplanationCatalog.Read("derived-DisciplineLead", "same-discipline");
        Assert.Single(roles); Assert.Equal(PermissionExplanationCatalog.Bindings.Count, roles[0].Examples.Count);
        Assert.All(roles[0].Examples, example => { Assert.NotEmpty(example.FactKeys); Assert.StartsWith("adminRole.action.", example.ActionLabelKey); });
        Assert.DoesNotContain(PermissionExplanationCatalog.ScenarioChoices, choice => choice.Key is "inactive-person" or "restricted-outsider");
        Assert.Empty(PermissionExplanationCatalog.Read("missing-role", "same-discipline"));
    }
}
