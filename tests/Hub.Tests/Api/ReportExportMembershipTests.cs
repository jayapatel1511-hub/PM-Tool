using System.Net;
using Hub.Domain;

namespace Hub.Tests.Api;

/// §8.5.1 "Export reports": a Standard User exports only projects they belong to (Jay, 2026-11-03); the screen and
/// every other role keep all visible projects. S21 of the Admin role explainer reads the same rule.
[Collection("api")]
public sealed class ReportExportMembershipTests(HubFactory f)
{
    readonly TestData d = new(f);

    async Task<string> Deliverable(Guid project, string name)
    {
        await (await f.As(TestData.Pm).Post($"/api/v1/projects/{project}/deliverables", new { name, projectDisciplineId = d.ProjectDiscipline(project, "Civil"),
            deliverableTypeId = await d.DeliverableType(), requiresReview = false })).Json(201);
        return name;
    }

    [Fact]
    public async Task Standard_user_exports_member_projects_only_while_the_screen_and_other_roles_keep_visible_projects()
    {
        var member = await d.Project(); // alex is a Civil team member
        var other = await d.Project(tweak: body => body["members"] = Array.Empty<object>());
        var mine = await Deliverable(member.Id, "Export member " + Guid.NewGuid().ToString("N")[..6]);
        var notMine = await Deliverable(other.Id, "Export other " + Guid.NewGuid().ToString("N")[..6]);
        var both = $"projectId={member.Id},{other.Id}";

        var screen = await (await f.As(TestData.Alex).GetAsync($"/api/v1/reports/deliverable-status-by-project?{both}")).Content.ReadAsStringAsync();
        Assert.Contains(mine, screen);
        Assert.Contains(notMine, screen);

        var csv = await f.As(TestData.Alex).GetAsync($"/api/v1/reports/deliverable-status-by-project/export?format=csv&{both}");
        Assert.Equal(HttpStatusCode.OK, csv.StatusCode);
        var text = await csv.Content.ReadAsStringAsync();
        Assert.Contains(mine, text);
        Assert.DoesNotContain(notMine, text);

        var unscoped = await (await f.As(TestData.Alex).GetAsync("/api/v1/reports/deliverable-status-by-project/export?format=csv")).Content.ReadAsStringAsync();
        Assert.DoesNotContain(notMine, unscoped);

        foreach (var role in new[] { TestData.Pm, TestData.Rita, TestData.Lena })
        {
            var all = await (await f.As(role).GetAsync($"/api/v1/reports/deliverable-status-by-project/export?format=csv&{both}")).Content.ReadAsStringAsync();
            Assert.Contains(notMine, all);
        }

        // A single-project report on a project the Standard User only views is refused, with the reason.
        Assert.Equal(HttpStatusCode.Forbidden, (await f.As(TestData.Alex).GetAsync($"/api/v1/reports/project-activity-log/export?format=csv&projectId={other.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.As(TestData.Alex).GetAsync($"/api/v1/reports/project-activity-log/export?format=csv&projectId={member.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await f.As(TestData.Alex).GetAsync($"/api/v1/reports/project-activity-log?projectId={other.Id}")).StatusCode);
    }

    [Fact]
    public void Explainer_reads_the_same_rule()
    {
        PermissionOperationExplanation Op(string role, string membership) =>
            Assert.Single(Assert.Single(PermissionExplanationCatalog.ReadRows(role, "same-discipline", membership, "S21.export")).Operations);
        Assert.Equal("NotEligible", Op("baseline-Standard", "none").DecisionKind);
        Assert.Equal("Conditional", Op("baseline-Standard", ProjectRole.Viewer).DecisionKind);
        foreach (var role in SystemRole.All) Assert.Equal("Conditional", Op("system-" + role, "none").DecisionKind);
    }
}
