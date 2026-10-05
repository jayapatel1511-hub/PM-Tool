using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

public sealed class ReviewDemoSeedTests
{
    private static readonly string[] TuesdayPersonaEmails =
    [
        "jordan@hub.test", "lena@hub.test", "sam@hub.test", "priya@hub.test", "marc@hub.test",
        "alex@hub.test", "jill@hub.test", "diane@hub.test", "omar@hub.test", "rita@hub.test",
    ];

    [Fact]
    public async Task Review_seed_refuses_to_take_over_an_existing_directory_identity()
    {
        using var f = new HubFactory { Settings = { ["Seed:DevUsers"] = "false" } };
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
        Assert.All(projects, p => Assert.DoesNotContain("Demo", p.Name));
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
        Assert.Equal(13, f.Db(db => db.Users.Count(u => u.Email.EndsWith("@hub.test"))));

        var response = await f.As("taylor@hub.test").GetAsync("/api/v1/projects/REVIEW-101");
        Assert.True(response.IsSuccessStatusCode, await response.Content.ReadAsStringAsync());
        var yagmurProject = await f.As("yagmur@hub.test").GetAsync("/api/v1/projects/DEMO-103");
        Assert.True(yagmurProject.IsSuccessStatusCode, await yagmurProject.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Review_seed_cleans_only_legacy_display_defaults_and_preserves_client_edits()
    {
        using var f = new HubFactory { ReviewDemo = true };
        using var client = f.CreateClient();
        var projectId = f.Db(db => db.Projects.Single(p => p.ExternalId == "DEMO-101").Id);
        var clientId = f.Db(db => db.Projects.Single(p => p.Id == projectId).ClientId);
        await f.DbAsync(async db =>
        {
            var project = await db.Projects.SingleAsync(p => p.Id == projectId);
            project.Name += " (Demo)";
            project.Description = "SYNTHETIC DEMO DATA — fictional client, project and work for application review only.";
            var other = await db.Projects.SingleAsync(p => p.ExternalId == "DEMO-102");
            other.Name = "Reviewer project name";
            other.Description = "Reviewer description";
            var customer = await db.Clients.SingleAsync(c => c.Id == clientId);
            customer.Name = "Reviewer client name";
            customer.ShortName = "Reviewer";
            await db.SaveChangesAsync();
            await ReviewDemoSeed.Seed(db, f.Clock);
            await ReviewDemoSeed.Seed(db, f.Clock);
            return 0;
        });
        Assert.Equal("Community Facility Site Servicing", f.Db(db => db.Projects.Single(p => p.Id == projectId).Name));
        Assert.DoesNotContain("SYNTHETIC", f.Db(db => db.Projects.Single(p => p.Id == projectId).Description)!);
        Assert.Equal("Reviewer project name", f.Db(db => db.Projects.Single(p => p.ExternalId == "DEMO-102").Name));
        Assert.Equal("Reviewer description", f.Db(db => db.Projects.Single(p => p.ExternalId == "DEMO-102").Description));
        Assert.Equal("Reviewer client name", f.Db(db => db.Clients.Single(c => c.Id == clientId).Name));
        Assert.Equal("Reviewer", f.Db(db => db.Clients.Single(c => c.Id == clientId).ShortName));
        Assert.Equal(1, f.Db(db => db.Clients.Count(c => c.Id == clientId)));
        Assert.Equal(3, f.Db(db => db.Projects.Count(p => p.ExternalSource == ReviewDemoSeed.Source)));
    }

    [Fact]
    public async Task Review_seed_adds_all_tuesday_personas_with_roles_and_reporting_lines()
    {
        using var f = new HubFactory { ReviewDemo = true };
        using var client = f.CreateClient();

        var people = f.Db(db => db.Users.Include(u => u.Roles).ToDictionary(u => u.Email));
        Assert.Equal(13, people.Count);
        Assert.All(TuesdayPersonaEmails, email =>
        {
            Assert.True(people.ContainsKey(email), $"Missing synthetic review persona {email}");
            Assert.Equal("dev-" + email, people[email].EntraObjectId);
        });

        Assert.Equal("Lena Brooks", people["lena@hub.test"].DisplayName);
        Assert.Equal("Civil Group Manager", people["sam@hub.test"].JobTitle);
        Assert.Equal("Senior Civil Engineer", people["marc@hub.test"].JobTitle);
        Assert.Equal("Auditor", people["rita@hub.test"].JobTitle);
        Assert.Equal(people["lena@hub.test"].Id, people["sam@hub.test"].SupervisorId);
        Assert.Equal(people["lena@hub.test"].Id, people["priya@hub.test"].SupervisorId);
        Assert.Equal(people["sam@hub.test"].Id, people["marc@hub.test"].SupervisorId);
        Assert.Equal(people["sam@hub.test"].Id, people["alex@hub.test"].SupervisorId);
        Assert.Equal(people["sam@hub.test"].Id, people["jill@hub.test"].SupervisorId);
        Assert.Equal(people["lena@hub.test"].Id, people["diane@hub.test"].SupervisorId);
        Assert.Equal(people["lena@hub.test"].Id, people["omar@hub.test"].SupervisorId);
        Assert.Null(people["jordan@hub.test"].SupervisorId);
        Assert.Null(people["rita@hub.test"].SupervisorId);
        Assert.Contains(people["jordan@hub.test"].Roles, r => r.Role == SystemRole.Admin);
        Assert.Contains(people["lena@hub.test"].Roles, r => r.Role == SystemRole.Executive);
        Assert.Contains(people["lena@hub.test"].Roles, r => r.Role == SystemRole.Supervisor);
        Assert.Contains(people["sam@hub.test"].Roles, r => r.Role == SystemRole.Supervisor);
        Assert.Contains(people["priya@hub.test"].Roles, r => r.Role == SystemRole.ProjectManager);
        Assert.Contains(people["marc@hub.test"].Roles, r => r.Role == SystemRole.ProjectManager);
        Assert.Contains(people["marc@hub.test"].Roles, r => r.Role == SystemRole.Supervisor);
        Assert.Contains(people["rita@hub.test"].Roles, r => r.Role == SystemRole.ReadOnly);
    }

    [Fact]
    public async Task Review_seed_preserves_edits_to_a_tuesday_persona_and_does_not_duplicate_it()
    {
        using var f = new HubFactory { ReviewDemo = true };
        using var client = f.CreateClient();

        await f.DbAsync(async db =>
        {
            var marc = await db.Users.Include(u => u.Roles).SingleAsync(u => u.Email == "marc@hub.test");
            var lena = await db.Users.SingleAsync(u => u.Email == "lena@hub.test");
            marc.DisplayName = "Marc — reviewer alias";
            marc.JobTitle = "Temporary review lead";
            marc.IsActive = false;
            marc.SupervisorId = lena.Id;
            // Mutate the existing assignments in place so the test exercises reviewer role edits
            // without making assumptions about join-row deletion ordering.
            marc.Roles[0].Role = SystemRole.ReadOnly;
            db.UserRoles.Remove(marc.Roles[1]);
            await db.SaveChangesAsync();
            return 0;
        });
        await f.DbAsync(async db =>
        {
            await ReviewDemoSeed.Seed(db, f.Clock);
            await ReviewDemoSeed.Seed(db, f.Clock);
            return 0;
        });

        var marcAfter = f.Db(db => db.Users.Include(u => u.Roles).Single(u => u.Email == "marc@hub.test"));
        Assert.Equal("Marc — reviewer alias", marcAfter.DisplayName);
        Assert.Equal("Temporary review lead", marcAfter.JobTitle);
        Assert.False(marcAfter.IsActive);
        Assert.Equal(f.Db(db => db.Users.Single(u => u.Email == "lena@hub.test").Id), marcAfter.SupervisorId);
        Assert.NotEmpty(marcAfter.Roles);
        Assert.All(marcAfter.Roles, role => Assert.Equal(SystemRole.ReadOnly, role.Role));
        Assert.Equal(1, f.Db(db => db.Users.Count(u => u.Email == "marc@hub.test")));
        Assert.Equal(13, f.Db(db => db.Users.Count(u => u.Email.EndsWith("@hub.test"))));
    }

    [Fact]
    public async Task Review_seed_refuses_reserved_identity_collision_transactionally()
    {
        using var f = new HubFactory { Settings = { ["Seed:DevUsers"] = "false" } };
        using var client = f.CreateClient();
        await f.DbAsync(async db =>
        {
            db.Users.Add(new AppUser
            {
                DisplayName = "Existing directory person", Email = "jordan@hub.test",
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
        Assert.Equal(1, f.Db(db => db.Users.Count(u => u.Email.EndsWith("@hub.test"))));
        Assert.Equal("Existing directory person", f.Db(db => db.Users.Single(u => u.Email == "jordan@hub.test").DisplayName));
        Assert.Equal(0, f.Db(db => db.Projects.Count(p => p.ExternalSource == ReviewDemoSeed.Source)));
    }
}
