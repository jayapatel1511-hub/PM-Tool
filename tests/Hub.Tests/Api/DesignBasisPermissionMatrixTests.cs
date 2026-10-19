using System.Net;
using Hub.Api.Features;
using Hub.Api.Data;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Hub.Tests.Api;

[Collection("api")]
public sealed class DesignBasisPermissionMatrixTests(HubFactory f, Xunit.Abstractions.ITestOutputHelper output)
{
    readonly TestData d = new(f);

    [Fact] // AC-FIG-08 decision 4: basis create/assign flags and real commands across every role/status.
    public async Task Basis_create_and_assign_flags_match_real_commands_for_every_role_and_status()
    {
        var users = new[] { TestData.Admin, TestData.Pm, TestData.Marc, TestData.Alex, TestData.Rita, TestData.Lena };
        foreach (var status in ProjectStatus.All)
        {
            var p = await d.Project(activate: true);
            var discipline = d.ProjectDiscipline(p.Id, "Civil");
            await f.DbAsync(async db =>
            {
                foreach (var who in users)
                {
                    var id = d.User(who);
                    if (!await db.ProjectMembers.AnyAsync(m => m.ProjectId == p.Id && m.UserId == id && m.RemovedAt == null))
                        db.ProjectMembers.Add(new ProjectMember { ProjectId = p.Id, UserId = id, Roles = [ProjectRole.TeamMember], AddedAt = f.Clock.Now });
                }
                return await db.SaveChangesAsync();
            });
            await f.DbAsync(async db => { (await db.Projects.SingleAsync(x => x.Id == p.Id)).Status = status; return await db.SaveChangesAsync(); });
            foreach (var user in users)
            {
                var project = await (await f.As(user).GetAsync($"/api/v1/projects/{p.Id}")).Json();
                var options = await (await f.As(user).GetAsync($"/api/v1/projects/{p.Id}/changes/options")).Json();
                var isPm = project["permissions"]!["isPm"]!.GetValue<bool>();
                var leads = project["permissions"]!["leadOf"]!.AsArray();
                var primary = f.Db(db => db.ProjectMembers.Where(m => m.ProjectId == p.Id && m.UserId == d.User(user) && m.RemovedAt == null).Select(m => m.PrimaryDisciplineId).FirstOrDefault());
                var legacyCreate = options["canWrite"]!.GetValue<bool>() && (isPm || leads.Any(x => x!.GetValue<Guid>() == discipline) || (primary == discipline && project["myRoles"]!.AsArray().Any(x => x!.GetValue<string>() == ProjectRole.TeamMember)));
                var legacyAssign = isPm || leads.Count > 0;
                var createAllowed = project["permissions"]!["createBasisIn"]!.AsArray().Any(x => x!.GetValue<Guid>() == discipline);
                async Task<HttpResponseMessage> Create(string owner) => await f.As(user).Post($"/api/v1/projects/{p.Id}/design-basis", new DesignBasisEndpoints.CreateBody(Guid.NewGuid(), BasisKind.Criterion,
                    $"Create {user} {status} {owner}", d.User(owner), discipline, null,
                    new DesignBasisEndpoints.VersionInput("Scope", "Statement", 1m, "m", "Synthetic", Guid.NewGuid().ToString(), "https://example.test/basis", "A", new DateOnly(2026, 11, 15), null), "Permission matrix correction"));
                var create = await Create(user);
                Assert.True(createAllowed ? create.IsSuccessStatusCode : create.StatusCode == HttpStatusCode.Forbidden, $"{user} {status} create returned {(int)create.StatusCode}: {await create.Content.ReadAsStringAsync()}");
                if (legacyCreate != createAllowed) output.WriteLine($"{user} {status} decision4.create: client {legacyCreate}, flag {createAllowed}, HTTP {(int)create.StatusCode}");
                var assignAllowed = project["permissions"]!["assignBasisIn"]!.AsArray().Any(x => x!.GetValue<Guid>() == discipline);
                var assign = await Create(user == TestData.Alex ? TestData.Pm : TestData.Alex);
                Assert.True(assignAllowed ? assign.IsSuccessStatusCode : assign.StatusCode == HttpStatusCode.Forbidden, $"{user} {status} name another owner returned {(int)assign.StatusCode}: {await assign.Content.ReadAsStringAsync()}");
                if (legacyAssign != assignAllowed) output.WriteLine($"{user} {status} decision4.assign: client {legacyAssign}, flag {assignAllowed}, HTTP {(int)assign.StatusCode}");
            }
        }
    }
}
