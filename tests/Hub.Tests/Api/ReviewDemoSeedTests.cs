using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

public sealed class ReviewDemoSeedTests
{
    [Fact]
    public async Task Review_seed_refuses_to_take_over_an_existing_directory_identity()
    {
        using var f = new HubFactory();
        using var client = f.CreateClient();
        await f.DbAsync(async db =>
        {
            db.Users.Add(new AppUser
            {
                DisplayName = "Existing directory person", Email = "taylor@hub.test",
                EntraObjectId = Guid.NewGuid().ToString(), IsActive = true,
            });
            await db.SaveChangesAsync();
            return 0;
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => f.DbAsync(async db =>
        {
            await ReviewDemoSeed.Seed(db, f.Clock);
            return 0;
        }));
        Assert.Equal(0, f.Db(db => db.Projects.Count(p => p.ExternalSource == ReviewDemoSeed.Source)));
    }

    [Fact]
    public async Task Opt_in_review_seed_keeps_personas_projects_and_reviewer_edits_on_restart()
    {
        using var f = new HubFactory { ReviewDemo = true };
        using var client = f.CreateClient(); // Starts the app and applies migrations plus the opt-in seed.

        var people = f.Db(db => db.Users.Include(u => u.Roles)
            .Where(u => new[] { "taylor@hub.test", "jay@hub.test", "yagmur@hub.test" }.Contains(u.Email))
            .ToDictionary(u => u.Email));
        Assert.Equal(3, people.Count);
        Assert.All(people.Values, u => Assert.Equal("dev-" + u.Email, u.EntraObjectId));
        Assert.Equal(people["taylor@hub.test"].Id, people["yagmur@hub.test"].SupervisorId);
        Assert.Null(people["jay@hub.test"].SupervisorId);
        Assert.All(people.Values, u => Assert.Contains(u.Roles, r => r.Role == SystemRole.ProjectManager));
        Assert.Contains(people["taylor@hub.test"].Roles, r => r.Role == SystemRole.Supervisor);
        Assert.Contains(people["jay@hub.test"].Roles, r => r.Role == SystemRole.Supervisor);
        Assert.DoesNotContain(people["yagmur@hub.test"].Roles, r => r.Role == SystemRole.Supervisor);

        var projects = f.Db(db => db.Projects.Where(p => p.ExternalSource == ReviewDemoSeed.Source).OrderBy(p => p.ProjectNumber).ToList());
        Assert.Equal(3, projects.Count);
        Assert.Equal(new[] { people["taylor@hub.test"].Id, people["jay@hub.test"].Id, people["yagmur@hub.test"].Id },
            projects.Select(p => p.ProjectManagerId));
        Assert.All(projects, p => Assert.Contains("Demo", p.Name));
        Assert.Equal(6, f.Db(db => db.Tasks.Count(t => projects.Select(p => p.Id).Contains(t.ProjectId))));
        var staff = await (await f.As("taylor@hub.test").GetAsync("/api/v1/staff")).Json();
        Assert.Equal(1, staff["tiles"]!["staff"]!.GetValue<int>());
        Assert.Equal("Yagmur", staff["people"]![0]!["displayName"]!.GetValue<string>());

        var first = projects[0];
        await f.DbAsync(async db =>
        {
            var project = await db.Projects.FirstAsync(p => p.Id == first.Id);
            project.Name = "Edited by reviewer (Demo)";
            project.ProjectNumber = "REVIEW-101";
            var report = await db.Users.FirstAsync(u => u.Email == "yagmur@hub.test");
            report.SupervisorId = null;
            report.EntraObjectId = null; // Earlier seed versions left this vulnerable to email matching.
            await db.SaveChangesAsync();
            await ReviewDemoSeed.Seed(db, f.Clock);
            return 0;
        });
        Assert.Equal("Edited by reviewer (Demo)", f.Db(db => db.Projects.Single(p => p.Id == first.Id).Name));
        Assert.Equal("REVIEW-101", f.Db(db => db.Projects.Single(p => p.Id == first.Id).ProjectNumber));
        Assert.Null(f.Db(db => db.Users.Single(u => u.Email == "yagmur@hub.test").SupervisorId));
        Assert.Equal("dev-yagmur@hub.test", f.Db(db => db.Users.Single(u => u.Email == "yagmur@hub.test").EntraObjectId));
        Assert.Equal(3, f.Db(db => db.Projects.Count(p => p.ExternalSource == ReviewDemoSeed.Source)));
        Assert.Equal(3, f.Db(db => db.Users.Count(u => u.Email == "taylor@hub.test" || u.Email == "jay@hub.test" || u.Email == "yagmur@hub.test")));

        var response = await f.As("taylor@hub.test").GetAsync("/api/v1/projects/REVIEW-101");
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var yagmurProject = await f.As("yagmur@hub.test").GetAsync("/api/v1/projects/DEMO-103");
        Assert.True(yagmurProject.IsSuccessStatusCode, await yagmurProject.Content.ReadAsStringAsync());
    }
}
