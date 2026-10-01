using System.Net;
using Hub.Api.Data;
using Hub.Api.Features;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;

namespace Hub.Tests.Api;

/// Cross-packet FR-MDC-02 sweep (spec-parts/12 and the "Repeat each core command as Read Only ... repeat on Complete,
/// Archived, Cancelled and On Hold projects" check in specs/025–033): Read Only vetoes mutations, Archived and Cancelled
/// projects are read-only, Complete is PM-only, On Hold keeps the normal rules, and reads keep working throughout.
[Collection("api")]
public sealed class CoordinationLifecycleSweepTests(HubFactory f)
{
    readonly TestData data = new(f);
    const string Reason = "Lifecycle sweep reason";
    static readonly DateOnly Day = new(2026, 9, 14); // TestClock date, a Monday

    sealed record World(Project P, Guid Civil, Guid Electrical, Guid Deliverable, Guid Revision, Guid Milestone, Guid Task, Guid ElectricalTask, Guid Issue);

    /// Actor is a non-PM who is authorised on an Active project, so the Complete case exercises the PM-only rule.
    sealed record Command(string Actor, int Success, Func<World, string> Path, Func<World, object> Body, Func<World, string> Read);

    int Version<T>(Guid id) where T : Audited => f.Db(db => db.Set<T>().AsNoTracking().Single(x => x.Id == id).RowVersion);

    Dictionary<string, Command> Commands() => new()
    {
        ["025 handoff create"] = new(TestData.Marc, 201, w => $"/api/v1/projects/{w.P.Id}/handoffs",
            w => new HandoffEndpoints.DraftBody(Guid.NewGuid(), "Sweep handoff", w.Deliverable, Version<Deliverable>(w.Deliverable), "A",
                "https://example.test/sweep-A.pdf", w.Electrical, data.User(TestData.Alex), data.User(TestData.Omar), w.ElectricalTask, null,
                "Set the service alignment", "Covers the corridor", Day.AddDays(2), Day.AddDays(4), null, Reason),
            w => $"/api/v1/projects/{w.P.Id}/handoffs"),
        ["026 review package create"] = new(TestData.Marc, 200, w => $"/api/v1/projects/{w.P.Id}/reviews",
            w => new ReviewEndpoints.CreateBody(Guid.NewGuid(), "Sweep review", "Check the corridor", w.Civil, data.User(TestData.Marc), [w.Revision],
                [new(w.Electrical, data.User(TestData.Omar), Day.AddDays(4))], false, Reason),
            w => $"/api/v1/projects/{w.P.Id}/reviews"),
        ["027 source revision register"] = new(TestData.Marc, 200, w => $"/api/v1/projects/{w.P.Id}/source-revisions",
            w => new ChangeEndpoints.RegisterBody(Guid.NewGuid(), null, null, w.Civil, data.User(TestData.Alex), "External", "sweep-" + Guid.NewGuid().ToString("N")[..6],
                "Sweep external source", "A", "https://example.test/external-A.pdf", "Client", "Whole site", null, null, null, Reason, null, null),
            w => $"/api/v1/projects/{w.P.Id}/changes"),
        ["028 submission package create"] = new(TestData.Marc, 200, w => $"/api/v1/projects/{w.P.Id}/submissions",
            w => new SubmissionEndpoints.CreateBody(Guid.NewGuid(), "Sweep package", "Permit review", "Municipality", data.User(TestData.Marc),
                w.Milestone, Day.AddDays(30), [new(w.Revision)], [], null, Reason),
            w => $"/api/v1/projects/{w.P.Id}/submissions"),
        ["029 allocation propose"] = new(TestData.Marc, 200, w => $"/api/v1/projects/{w.P.Id}/allocations",
            w => new AllocationEndpoints.CreateBody(Guid.NewGuid(), data.User(TestData.Alex), AllocationPurpose.Production, Day.AddDays(21), Day.AddDays(21), 8,
                [], [new("Task", w.Task, Day.AddDays(21))], Reason),
            w => $"/api/v1/projects/{w.P.Id}/allocations"),
        ["031 design basis create"] = new(TestData.Marc, 200, w => $"/api/v1/projects/{w.P.Id}/design-basis",
            w => new DesignBasisEndpoints.CreateBody(Guid.NewGuid(), BasisKind.Assumption, "Sweep assumption " + Guid.NewGuid().ToString("N")[..6],
                data.User(TestData.Alex), w.Civil, null, new("Area A", "Assumed level", null, null, null, null, null, null, Day.AddDays(7), null), Reason),
            w => $"/api/v1/projects/{w.P.Id}/design-basis"),
        ["032 readiness constraint create"] = new(TestData.Alex, 200, w => $"/api/v1/projects/{w.P.Id}/readiness/Task/{w.Task}/constraints",
            w => new ReadinessEndpoints.ConstraintBody(Guid.NewGuid(), Version<WorkTask>(w.Task), "Handoff", "Obtain accepted input", data.User(TestData.Pm),
                Day.AddDays(7), "https://example.test/constraint"),
            w => $"/api/v1/projects/{w.P.Id}/readiness/Task/{w.Task}/constraints"),
        ["032 weekly promise propose"] = new(TestData.Alex, 200, w => $"/api/v1/projects/{w.P.Id}/weekly-commitments/Task/{w.Task}",
            w => new WeeklyCommitmentsEndpoints.ProposeBody(Guid.NewGuid(), Version<WorkTask>(w.Task), Day.AddDays(7), Day.AddDays(9), "Layout output", "Layout checked", Reason),
            w => $"/api/v1/projects/{w.P.Id}/weekly-commitments"),
        ["033 issue location add"] = new(TestData.Alex, 201, w => $"/api/v1/issues/{w.Issue}/locations",
            w => new { kind = "SiteArea", siteArea = "North", rowVersion = Version<Issue>(w.Issue) },
            w => $"/api/v1/issues/{w.Issue}/locations"),
    };

    // (who, project status, expected status: 0 = the command's success code)
    static readonly Dictionary<string, (string? Who, string Status, int Expect)> Cases = new()
    {
        ["Active"] = (null, ProjectStatus.Active, 0),
        ["On Hold"] = (null, ProjectStatus.OnHold, 0),
        ["Read Only user"] = (TestData.Rita, ProjectStatus.Active, 403),
        ["Archived"] = (null, ProjectStatus.Archived, 403),
        ["Cancelled"] = (null, ProjectStatus.Cancelled, 403),
        ["Complete, non-PM"] = (null, ProjectStatus.Complete, 403),
        ["Complete, PM"] = (TestData.Pm, ProjectStatus.Complete, 0),
    };

    public static TheoryData<string, string> Sweep()
    {
        var rows = new TheoryData<string, string>();
        foreach (var command in CommandNames) foreach (var c in Cases.Keys) rows.Add(command, c);
        return rows;
    }
    static readonly string[] CommandNames = ["025 handoff create", "026 review package create", "027 source revision register", "028 submission package create",
        "029 allocation propose", "031 design basis create", "032 readiness constraint create", "032 weekly promise propose", "033 issue location add"];

    [Theory]
    [MemberData(nameof(Sweep))]
    public async Task Mutation_respects_role_and_project_lifecycle(string command, string scenario)
    {
        var cmd = Commands()[command];
        var (who, status, expect) = Cases[scenario];
        var w = await New();
        var body = cmd.Body(w);
        await SetStatus(w, status);

        var response = await f.As(who ?? cmd.Actor).Post(cmd.Path(w), body);
        Assert.True((int)response.StatusCode == (expect == 0 ? cmd.Success : expect),
            $"{command} / {scenario}: expected {(expect == 0 ? cmd.Success : expect)}, got {(int)response.StatusCode} {await response.Content.ReadAsStringAsync()}");

        // Reads stay available: lifecycle restrictions and the Read Only role veto writes, not the register or the 030 projection.
        foreach (var reader in who == TestData.Rita ? [TestData.Pm, TestData.Rita] : new[] { TestData.Pm })
            foreach (var read in new[] { cmd.Read(w), $"/api/v1/projects/{w.P.Id}/discipline-coordination" })
            {
                if (reader == TestData.Rita && command == "029 allocation propose" && read == cmd.Read(w)) continue; // FR-CAP-06 scopes allocation lists
                Assert.Equal(HttpStatusCode.OK, (await f.As(reader).GetAsync(read)).StatusCode);
            }
    }

    async Task SetStatus(World w, string status) => await f.DbAsync(async db => {
        (await db.Projects.SingleAsync(p => p.Id == w.P.Id)).Status = status; return await db.SaveChangesAsync(); });

    async Task<World> New()
    {
        var p = await data.Project();
        var root = $"/api/v1/projects/{p.Id}";
        var civil = data.ProjectDiscipline(p.Id, "Civil"); var electrical = data.ProjectDiscipline(p.Id, "Electrical");
        var alex = data.User(TestData.Alex);
        var deliverable = (await (await f.As(TestData.Marc).Post(root + "/deliverables", new { name = "Sweep survey", projectDisciplineId = civil,
            deliverableTypeId = await data.DeliverableType(), ownerId = alex, revision = "A", requiresReview = false,
            transmittalUrl = "https://example.test/sweep-A.pdf" })).Json(201)).G("id");
        var revision = (await (await f.As(TestData.Alex).Post(root + "/source-revisions", new ChangeEndpoints.RegisterBody(Guid.NewGuid(), deliverable,
            Version<Deliverable>(deliverable), civil, alex, "Deliverable", "survey", "Sweep survey", "A", "https://example.test/sweep-A.pdf",
            "Survey team", "Corridor", null, null, null, null, null, null))).Json()).G("id");
        var milestone = (await (await f.As(TestData.Pm).Post(root + "/milestones", new MilestoneEndpoints.CreateBody("Sweep submission",
            MilestoneType.DesignSubmission, Day.AddDays(30), null, null, null, true))).Json(201)).G("id");
        var task = (await data.NewTask(p.Id, TestData.Marc, new { assigneeId = alex, estimatedHours = 8m, startDate = Day, dueDate = Day.AddDays(28) })).G("id");
        var electricalTask = (await data.NewTask(p.Id, TestData.Omar, new { assigneeId = data.User(TestData.Omar), dueDate = Day.AddDays(2) }, "Electrical")).G("id");
        var issue = (await (await f.As(TestData.Alex).Post(root + "/issues", new { title = "Sweep issue", severity = "High", ownerId = alex,
            projectDisciplineId = civil })).Json(201)).G("id");
        return new World(p, civil, electrical, deliverable, revision, milestone, task, electricalTask, issue);
    }
}
