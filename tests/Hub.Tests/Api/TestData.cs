using System.Text.Json.Nodes;
using Hub.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Builders for integration tests. Seeded people (Seed.DevUsers): jordan (Admin), lena (Executive, Supervisor
/// of sam, priya, omar, diane), sam (Supervisor of alex, jill, marc), priya (PM), marc (PM, Supervisor), alex,
/// jill, diane, omar, rita (Read Only).
public sealed class TestData(HubFactory f)
{
    public const string Admin = "jordan@hub.test", Pm = "priya@hub.test", Marc = "marc@hub.test", Alex = "alex@hub.test",
        Jill = "jill@hub.test", Sam = "sam@hub.test", Diane = "diane@hub.test", Omar = "omar@hub.test", Lena = "lena@hub.test", Rita = "rita@hub.test";

    JsonNode? reference;
    public async Task<JsonNode> Ref() => reference ??= await f.As(Admin).GetAsync("/api/v1/reference").Result.Json();
    public async Task<Guid> Discipline(string name) => (await Ref())["disciplines"]!.AsArray().First(d => d!["name"]!.GetValue<string>() == name)!.G("id");
    public async Task<Guid> Client() => (await Ref())["clients"]!.AsArray().First()!.G("id");
    public async Task<Guid> Office() => (await Ref())["offices"]!.AsArray().First()!.G("id");
    public async Task<Guid> DeliverableType(string name = "Drawing Package") => (await Ref())["deliverableTypes"]!.AsArray().First(d => d!["name"]!.GetValue<string>() == name)!.G("id");
    public Guid User(string email) => f.Db(db => db.Users.First(u => u.Email == email).Id);

    public static string Number() => "P" + Guid.NewGuid().ToString("N")[..7].ToUpperInvariant();

    /// A project owned by priya with Civil (lead marc) and Electrical (lead omar), and alex as Civil team member.
    public async Task<Project> Project(bool activate = true, string pm = Pm, Action<Dictionary<string, object?>>? tweak = null)
    {
        var body = new Dictionary<string, object?>
        {
            ["projectNumber"] = Number(), ["name"] = "Test " + Guid.NewGuid().ToString("N")[..6], ["clientId"] = await Client(), ["officeId"] = await Office(),
            ["disciplines"] = new[] { new { disciplineId = await Discipline("Civil"), leadUserId = (Guid?)User(Marc) }, new { disciplineId = await Discipline("Electrical"), leadUserId = (Guid?)User(Omar) } },
            ["members"] = new[] { new { userId = User(Alex), roles = new[] { "TeamMember" }, disciplineId = (Guid?)await Discipline("Civil") } },
        };
        tweak?.Invoke(body);
        var created = await f.As(pm).Post("/api/v1/projects", body).Result.Json(201);
        var id = created.G("id");
        if (activate)
        {
            var p = await f.As(pm).GetAsync($"/api/v1/projects/{id}").Result.Json();
            await f.As(pm).Post($"/api/v1/projects/{id}/transition", new { toStatus = "Active", rowVersion = p.I("rowVersion") }).Result.Json();
        }
        return f.Db(db => db.Projects.AsNoTracking().First(p => p.Id == id));
    }

    public Guid ProjectDiscipline(Guid projectId, string name) =>
        f.Db(db => db.ProjectDisciplines.Include(d => d.Discipline).First(d => d.ProjectId == projectId && d.Discipline!.Name == name).Id);

    public int Version(Guid projectId) => f.Db(db => db.Projects.AsNoTracking().First(p => p.Id == projectId).RowVersion);

    /// Creates a task through the API; `extra` properties are merged into the body.
    public async Task<JsonNode> NewTask(Guid projectId, string as_ = Marc, object? extra = null, string discipline = "Civil")
    {
        var body = new Dictionary<string, object?> { ["name"] = "Task " + Guid.NewGuid().ToString("N")[..4], ["projectDisciplineId"] = ProjectDiscipline(projectId, discipline) };
        if (extra is not null) foreach (var p in extra.GetType().GetProperties()) body[p.Name] = p.GetValue(extra);
        return await f.As(as_).Post($"/api/v1/projects/{projectId}/tasks", body).Result.Json(201);
    }

    public Task<int> TaskVersion(JsonNode t) => f.DbAsync(db => db.Tasks.Where(x => x.Id == t.G("id")).Select(x => x.RowVersion).FirstAsync());

    public async Task<JsonNode> Move(string as_, JsonNode t, string to, object? extra = null, int expect = 200)
    {
        var body = new Dictionary<string, object?> { ["toStatus"] = to, ["rowVersion"] = await TaskVersion(t) };
        if (extra is not null) foreach (var p in extra.GetType().GetProperties()) body[p.Name] = p.GetValue(extra);
        return await f.As(as_).Post($"/api/v1/tasks/{t.S("id")}/transition", body).Result.Json(expect);
    }
}
