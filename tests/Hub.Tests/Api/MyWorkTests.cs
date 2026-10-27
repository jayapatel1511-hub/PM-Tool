using System.Text.Json.Nodes;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// §55.3 My Work → No date, packet 051 S07 (FR-BKL-06, AC-BKL-04): the view is the person's Backlog across projects, read
/// through the same task list as Today, Upcoming and Overdue, so it follows the same project access and the same flag.
[Collection("api")]
public sealed class MyWorkTests(HubFactory f)
{
    readonly TestData d = new(f);
    DateOnly Today => DateOnly.FromDateTime(f.Clock.Now.UtcDateTime);
    string In(int days) => Today.AddDays(days).ToString("yyyy-MM-dd");

    async Task Backlog(bool on) => (await f.As(TestData.Admin).Put("/api/v1/admin/settings/flag.backlog", new { value = on })).EnsureSuccessStatusCode();

    [Fact, Trait("flag-off", "backlog"), Trait("flag-on", "backlog")]
    [Trait("Spec", "AC-BKL-04")]
    public async Task No_date_lists_the_persons_backlog_across_projects_and_leaves_restricted_projects_out()
    {
        var tester = "nodate." + Guid.NewGuid().ToString("N")[..6] + "@hub.test";
        (await f.As(tester).GetAsync("/api/v1/me")).EnsureSuccessStatusCode();
        var me = d.User(tester);
        var alex = d.User(TestData.Alex);
        var pm = f.As(TestData.Pm);

        async Task<Guid> Add(Guid projectId, object? extra = null, string? status = null)
        {
            var id = (await d.NewTask(projectId, TestData.Pm, extra)).G("id");
            if (status is not null) await f.DbAsync(db => db.Tasks.Where(x => x.Id == id).ExecuteUpdateAsync(x => x.SetProperty(y => y.Status, status)));
            return id;
        }
        async Task<Guid> Project()
        {
            var p = await d.Project();
            await pm.Post($"/api/v1/projects/{p.Id}/members", new { userId = me, roles = new[] { ProjectRole.TeamMember } }).Result.Json(201);
            return p.Id;
        }

        var a = await Project(); var b = await Project(); var c = await Project(); var hidden = await Project();
        var backlog = new List<Guid>
        {
            await Add(a, new { assigneeId = me }), await Add(a, new { assigneeId = me }, TaskStatuses.OnHold),
            await Add(b, new { assigneeId = me }), await Add(c, new { assigneeId = me }),
        };
        // A collaborator's undated task is the person's work too (My Tasks is assignee or collaborator).
        var shared = await Add(b, new { assigneeId = alex });
        (await pm.Post($"/api/v1/tasks/{shared}/collaborators", new { userId = me })).EnsureSuccessStatusCode();
        backlog.Add(shared);
        // Not the person's Backlog: dated, started, someone else's, Cancelled, Complete.
        var dated = new List<Guid> { await Add(a, new { assigneeId = me, dueDate = In(3) }), await Add(b, new { assigneeId = me, startDate = In(10) }), await Add(c, new { assigneeId = me, dueDate = In(-2) }) };
        var others = new List<Guid>
        {
            await Add(a, new { assigneeId = alex }), await Add(b, new { assigneeId = me }, TaskStatuses.InProgress),
            await Add(b, new { assigneeId = me }, TaskStatuses.Cancelled), await Add(c, new { assigneeId = me }, TaskStatuses.Complete),
        };
        var restricted = await Add(hidden, new { assigneeId = me });
        await f.DbAsync(async db =>
        {
            (await db.Projects.SingleAsync(x => x.Id == hidden)).Visibility = Visibility.Restricted;
            (await db.ProjectMembers.SingleAsync(m => m.ProjectId == hidden && m.UserId == me)).RemovedAt = f.Clock.GetUtcNow();
            return await db.SaveChangesAsync();
        });
        foreach (var id in new[] { a, b, c }) await f.Evaluate(id);

        var person = f.As(tester);
        async Task<List<Guid>> Ids(string q) => (await person.GetAsync($"/api/v1/tasks?pageSize=200&mine=true&{q}").Result.Json())["items"]!.AsArray().Select(x => x!.G("id")).ToList();
        async Task<JsonNode> Work() => await person.GetAsync("/api/v1/me/work").Result.Json();

        // Off (FR-BKL-09): My Work rows carry no backlog field, so nothing in the page can show a No date view.
        Assert.All((await Work())["tasks"]!.AsArray(), x => Assert.Null(x!["backlog"]));

        await Backlog(true);
        try
        {
            // AC-BKL-04: every Backlog item of the person, on all three projects, and nothing else.
            Assert.Equal(backlog.Order(), (await Ids("backlog=true")).Order());
            var tasks = (await Work())["tasks"]!.AsArray();
            Assert.Equal(backlog.Order(), tasks.Where(x => x!["backlog"]!.GetValue<bool>()).Select(x => x!.G("id")).Order());
            Assert.Equal(3, (await Ids("backlog=true")).Select(id => tasks.Single(x => x!.G("id") == id)!.G("projectId")).Distinct().Count());
            Assert.DoesNotContain(restricted, await Ids("backlog=true")); // Restricted project: not a member any more, so not visible, as in every other view
            Assert.DoesNotContain(tasks, x => x!.G("id") == restricted);
            Assert.Equal(backlog.Take(2).Order(), (await Ids($"backlog=true&projects={a}")).Order()); // the workspace scope still narrows it

            // None of them is in Today, Upcoming or Overdue, and the dated work still is.
            var today = await Ids($"dueFrom={In(0)}&dueTo={In(0)}&open=true");
            var upcoming = await Ids($"dueFrom={In(1)}&open=true");
            var overdue = await Ids("overdue=true");
            Assert.Empty(backlog.Concat(others).Intersect(today.Concat(upcoming).Concat(overdue)));
            Assert.Contains(dated[0], upcoming);
            Assert.Contains(dated[2], overdue);
        }
        finally { await Backlog(false); }
    }
}
