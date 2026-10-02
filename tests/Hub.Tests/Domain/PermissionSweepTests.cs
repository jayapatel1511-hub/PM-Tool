using Hub.Domain;
using static Hub.Domain.TaskStatuses;

namespace Hub.Tests.Domain;

/// The §8.5 matrix swept across personas and project states: who may do each thing on an Active project, and the gates
/// that override it (Read Only accounts AC-PERM-04, Archived/Cancelled projects P-06, Complete projects P-05).
public sealed class PermissionSweepTests
{
    [Fact]
    public void Appointed_basis_approver_loses_authority_when_project_membership_is_removed()
    {
        var actor = new Actor(Guid.NewGuid(), true, new HashSet<string>());
        var project = new ProjectContext(Guid.NewGuid(), ProjectStatus.Active, Visibility.Open, Guid.NewGuid(), true, new HashSet<string>(), new HashSet<Guid>(), null);
        Assert.False(Permissions.ConfirmBasis(actor, project, Guid.NewGuid(), actor.Id).Ok);
        Assert.True(Permissions.ConfirmBasis(actor, project with { MemberRoles = new HashSet<string> { ProjectRole.Reviewer } }, Guid.NewGuid(), actor.Id).Ok);
    }

    static readonly Guid Civ = Guid.NewGuid(), Elec = Guid.NewGuid(), Pid = Guid.NewGuid();
    sealed record Persona(string Name, Actor Actor, string[] ProjectRoles, Guid[] Leads, Guid? PrimaryDiscipline, bool PrimaryPm = false);

    static readonly Dictionary<string, Guid> Ids = new[] { "admin", "pm", "pm2", "dl", "dlOther", "tm", "assignee", "collab", "reviewer", "viewer", "outsider", "readOnly", "supervisor" }
        .ToDictionary(n => n, _ => Guid.NewGuid());

    static Persona P(string name, string[]? sys = null, string[]? roles = null, Guid[]? leads = null, Guid? primary = null, bool primaryPm = false) =>
        new(name, new Actor(Ids[name], true, new HashSet<string>(sys ?? [])), roles ?? [], leads ?? [], primary, primaryPm);

    static readonly Persona[] People =
    [
        P("admin", [SystemRole.Admin]),
        P("pm", [SystemRole.ProjectManager], [ProjectRole.PM], primaryPm: true),
        P("pm2", [], [ProjectRole.PM]),
        P("dl", [], [ProjectRole.TeamMember], [Civ], Civ),
        P("dlOther", [], [ProjectRole.TeamMember], [Elec], Elec),
        P("tm", [], [ProjectRole.TeamMember], primary: Civ),
        P("assignee", [], [ProjectRole.TeamMember], primary: Civ),
        P("collab", [], [ProjectRole.TeamMember], primary: Elec),
        P("reviewer", [], [ProjectRole.Reviewer]),
        P("viewer", [], [ProjectRole.Viewer]),
        P("outsider", [SystemRole.Supervisor]),
        P("readOnly", [SystemRole.ReadOnly], [ProjectRole.PM], [Civ], Civ),
        P("supervisor", [SystemRole.Supervisor], [ProjectRole.Viewer]),
    ];

    static ProjectContext Ctx(Persona x, string status, string visibility = Visibility.Open, bool viewerComments = true) =>
        new(Pid, status, visibility, x.PrimaryPm ? x.Actor.Id : Ids["pm"], viewerComments, new HashSet<string>(x.ProjectRoles), new HashSet<Guid>(x.Leads), x.PrimaryDiscipline);

    static readonly TaskFacts Task = new(Civ, Ids["assignee"], Ids["reviewer"], Ids["tm"], new HashSet<Guid> { Ids["collab"] }, InProgress);
    static readonly DeliverableFacts Del = new(Civ, Ids["assignee"], Ids["reviewer"]);
    static readonly OwnedFacts Owned = new(Ids["assignee"], Ids["tm"], Civ);
    static readonly DateTimeOffset Now = new(2026, 9, 25, 12, 0, 0, TimeSpan.Zero);

    /// Every project-scoped write, evaluated for one persona and project state.
    static readonly Dictionary<string, Func<Actor, ProjectContext, Allow>> Writes = new()
    {
        ["EditProject"] = Permissions.EditProject, ["ManageTeam"] = Permissions.ManageTeam, ["HealthOverride"] = Permissions.HealthOverride,
        ["ManageMilestones"] = Permissions.ManageMilestones, ["RestoreItem"] = Permissions.RestoreItem, ["ManageExternalParties"] = Permissions.ManageExternalParties,
        ["CreateDeliverable"] = (a, p) => Permissions.CreateDeliverable(a, p, Civ),
        ["EditDeliverable"] = (a, p) => Permissions.EditDeliverable(a, p, Del),
        ["DeleteDeliverable"] = (a, p) => Permissions.DeleteDeliverable(a, p, Del),
        ["DeleteDeliverableWithCompletedTasks"] = (a, p) => Permissions.DeleteDeliverable(a, p, Del with { HasCompletedTasks = true }),
        ["DeliverableOwnerMove"] = (a, p) => Permissions.DeliverableTransition(a, p, Del, DeliverableStatus.InProgress, DeliverableStatus.InReview),
        ["DeliverableIssue"] = (a, p) => Permissions.DeliverableTransition(a, p, Del, DeliverableStatus.ReadyToIssue, DeliverableStatus.Issued),
        ["DeliverableReview"] = (a, p) => Permissions.DeliverableTransition(a, p, Del, DeliverableStatus.InReview, DeliverableStatus.ReadyToIssue),
        ["DeliverableCancel"] = (a, p) => Permissions.DeliverableTransition(a, p, Del, DeliverableStatus.InProgress, DeliverableStatus.Cancelled),
        ["CreateTask"] = (a, p) => Permissions.CreateTask(a, p, Civ),
        ["EditTask"] = (a, p) => Permissions.EditTask(a, p, Task),
        ["EditTaskCreatorWindow"] = (a, p) => Permissions.EditTask(a, p, Task with { CreatorWindowOpen = true }),
        ["AssignTask"] = (a, p) => Permissions.AssignTask(a, p, Task),
        ["AssignAtCreation"] = (a, p) => Permissions.AssignTask(a, p, Task, atCreation: true),
        ["SetReviewer"] = (a, p) => Permissions.SetReviewer(a, p, Task),
        ["ChangeDueDate"] = (a, p) => Permissions.ChangeDueDate(a, p, Task),
        ["DueAtCreation"] = (a, p) => Permissions.ChangeDueDate(a, p, Task, atCreation: true),
        ["Start"] = (a, p) => Permissions.TaskTransition(a, p, Task, NotStarted, InProgress),
        ["Withdraw"] = (a, p) => Permissions.TaskTransition(a, p, Task, ReadyForReview, InProgress),
        ["StartReview"] = (a, p) => Permissions.TaskTransition(a, p, Task, ReadyForReview, InReview),
        ["Approve"] = (a, p) => Permissions.TaskTransition(a, p, Task, InReview, Complete),
        ["Reopen"] = (a, p) => Permissions.TaskTransition(a, p, Task, Complete, InProgress),
        ["CancelTask"] = (a, p) => Permissions.TaskTransition(a, p, Task, InProgress, Cancelled),
        ["RestoreCancelled"] = (a, p) => Permissions.TaskTransition(a, p, Task, Cancelled, NotStarted),
        ["ManualBlock"] = (a, p) => Permissions.ManualBlock(a, p, Task),
        ["ManageDependency"] = (a, p) => Permissions.ManageDependency(a, p, Task, Task with { DisciplineId = Elec, AssigneeId = Ids["collab"] }),
        ["DeleteNotStarted"] = (a, p) => Permissions.DeleteTask(a, p, Task with { Status = NotStarted }),
        ["DeleteStarted"] = (a, p) => Permissions.DeleteTask(a, p, Task),
        ["DeleteWithDependencies"] = (a, p) => Permissions.DeleteTask(a, p, Task with { Status = NotStarted, HasDependencies = true }),
        ["RaiseRegisterItem"] = Permissions.RaiseRegisterItem, ["CreateExternalParty"] = Permissions.CreateExternalParty,
        ["EditRegisterItem"] = (a, p) => Permissions.EditRegisterItem(a, p, Owned),
        ["EditRegisterItemNoDiscipline"] = (a, p) => Permissions.EditRegisterItem(a, p, Owned with { DisciplineId = null }),
        ["DecideOrDefer"] = (a, p) => Permissions.DecideOrDefer(a, p, Owned),
        ["RunCoordination"] = Permissions.RunCoordination, ["Snooze"] = Permissions.Snooze,
        ["CreateProjectEvent"] = Permissions.CreateProjectEvent,
    };

    /// Writes that ignore the Complete-project gate (comments, time and events stay open to members until archived).
    static readonly Dictionary<string, Func<Actor, ProjectContext, Allow>> MemberWrites = new()
    {
        ["Comment"] = Permissions.Comment, ["AddLink"] = Permissions.AddLink, ["EnterTime"] = Permissions.EnterTime,
        ["EditTime"] = (a, p) => Permissions.EditTime(a, p, Ids["tm"]).Allow,
        ["EditEvent"] = (a, p) => Permissions.EditEvent(a, p, Ids["tm"]),
    };

    static readonly string[] Pms = ["admin", "pm", "pm2"];
    static string[] With(params string[] more) => [.. Pms, .. more];
    static readonly string[] Members = With("dl", "dlOther", "tm", "assignee", "collab", "reviewer");

    static readonly Dictionary<string, string[]> Active = new()
    {
        ["EditProject"] = Pms, ["ManageTeam"] = Pms, ["HealthOverride"] = Pms, ["ManageMilestones"] = Pms, ["RestoreItem"] = Pms, ["ManageExternalParties"] = Pms,
        ["CreateDeliverable"] = With("dl"), ["EditDeliverable"] = With("dl", "assignee"), ["DeleteDeliverable"] = With("dl"), ["DeleteDeliverableWithCompletedTasks"] = Pms,
        ["DeliverableOwnerMove"] = With("dl", "assignee"), ["DeliverableIssue"] = With("dl"), ["DeliverableReview"] = With("dl", "assignee", "reviewer"),
        ["DeliverableCancel"] = With("dl"),
        ["CreateTask"] = With("dl", "tm", "assignee"), ["EditTask"] = With("dl", "assignee", "collab"), ["EditTaskCreatorWindow"] = With("dl", "assignee", "collab", "tm"),
        ["AssignTask"] = With("dl"), ["AssignAtCreation"] = With("dl", "tm"), ["SetReviewer"] = With("dl"),
        ["ChangeDueDate"] = With("dl", "assignee"), ["DueAtCreation"] = With("dl", "assignee", "tm"),
        ["Start"] = With("dl", "assignee", "collab"), ["Withdraw"] = With("dl", "assignee"), ["StartReview"] = With("dl", "reviewer"), ["Approve"] = With("dl", "reviewer"),
        ["Reopen"] = With("dl", "reviewer"), ["CancelTask"] = With("dl"), ["RestoreCancelled"] = Pms,
        ["ManualBlock"] = With("dl", "assignee", "collab"), ["ManageDependency"] = With("dl", "dlOther", "collab"),
        ["DeleteNotStarted"] = With("dl", "tm"), ["DeleteStarted"] = Pms, ["DeleteWithDependencies"] = With("dl"),
        ["RaiseRegisterItem"] = With("dl", "dlOther", "tm", "assignee", "collab"), ["CreateExternalParty"] = With("dl", "dlOther", "tm", "assignee", "collab"),
        ["EditRegisterItem"] = With("dl", "assignee", "tm"), ["EditRegisterItemNoDiscipline"] = With("assignee", "tm"), ["DecideOrDefer"] = With("assignee"),
        ["RunCoordination"] = With("dl", "dlOther"), ["Snooze"] = With("dl", "dlOther"), ["CreateProjectEvent"] = Members,
        ["Comment"] = [.. Members, "viewer", "supervisor"], ["AddLink"] = [.. Members, "viewer", "supervisor"], ["EnterTime"] = Members,
        ["EditTime"] = With("tm"), ["EditEvent"] = With("tm"),
    };

    static Allow Run(string fn, Persona x, string status, string visibility = Visibility.Open) =>
        (Writes.TryGetValue(fn, out var w) ? w : MemberWrites[fn])(x.Actor, Ctx(x, status, visibility));

    [Theory]
    [InlineData(ProjectStatus.Active)]
    [InlineData(ProjectStatus.Setup)]
    [InlineData(ProjectStatus.OnHold)]
    public void Live_project_matrix(string status)
    {
        foreach (var (fn, allowed) in Active)
            foreach (var x in People)
                Assert.True(Run(fn, x, status).Ok == allowed.Contains(x.Name), $"{fn} for {x.Name} on a {status} project");
    }

    [Theory]
    [InlineData(ProjectStatus.Archived)]
    [InlineData(ProjectStatus.Cancelled)]
    public void Archived_and_cancelled_projects_refuse_every_write(string status) // P-06
    {
        foreach (var fn in Writes.Keys.Concat(MemberWrites.Keys))
            foreach (var x in People)
            {
                var r = Run(fn, x, status);
                Assert.False(r.Ok, $"{fn} for {x.Name}");
                Assert.Equal(x.Name == "readOnly" ? "perm.read_only" : "perm.project_read_only", r.Why);
            }
    }

    [Fact]
    public void Complete_projects_stay_editable_by_the_pm_only() // P-05
    {
        foreach (var (fn, allowed) in Active.Where(kv => Writes.ContainsKey(kv.Key)))
            foreach (var x in People)
                Assert.True(Run(fn, x, ProjectStatus.Complete).Ok == (allowed.Contains(x.Name) && Pms.Contains(x.Name)), $"{fn} for {x.Name}");
        foreach (var (fn, allowed) in Active.Where(kv => MemberWrites.ContainsKey(kv.Key)))
            foreach (var x in People)
                Assert.True(Run(fn, x, ProjectStatus.Complete).Ok == allowed.Contains(x.Name), $"{fn} for {x.Name}");
    }

    [Fact]
    public void Read_only_accounts_never_write_even_as_pm() // AC-PERM-04
    {
        var ro = People.Single(x => x.Name == "readOnly");
        foreach (var status in new[] { ProjectStatus.Setup, ProjectStatus.Active, ProjectStatus.OnHold, ProjectStatus.Complete })
        {
            var p = Ctx(ro, status);
            foreach (var fn in Writes.Keys.Concat(MemberWrites.Keys)) Assert.Equal("perm.read_only", Run(fn, ro, status).Why);
            Assert.Equal("perm.read_only", Permissions.ChangeProjectStatus(ro.Actor, p, ProjectStatus.OnHold).Why);
            Assert.Equal("perm.read_only", Permissions.DeleteComment(ro.Actor, p, ro.Actor.Id).Why);
            Assert.Equal("perm.read_only", Permissions.EditComment(ro.Actor, ro.Actor.Id, Now, Now).Why);
            Assert.Equal("perm.read_only", Permissions.ManageSavedProjectView(ro.Actor, p).Why);
            Assert.Equal("perm.read_only", Permissions.EditEvent(ro.Actor, null, ro.Actor.Id).Why);
        }
        Assert.Equal("perm.read_only", Permissions.CreateProject(ro.Actor).Why);
        Assert.Equal("perm.read_only", Permissions.ActOnStaff(ro.Actor, ro.Actor.Id).Why);
        Assert.False(Permissions.ManageTemplates(ro.Actor, templateEditor: true));
    }

    [Fact]
    public void Viewer_comments_follow_the_project_switch() // C-01, allow_viewer_comments
    {
        var viewer = People.Single(x => x.Name == "viewer");
        Assert.True(Permissions.Comment(viewer.Actor, Ctx(viewer, ProjectStatus.Active, viewerComments: true)));
        Assert.Equal("perm.comment", Permissions.Comment(viewer.Actor, Ctx(viewer, ProjectStatus.Active, viewerComments: false)).Why);
        var outsider = People.Single(x => x.Name == "outsider");
        Assert.Equal("perm.comment", Permissions.Comment(outsider.Actor, Ctx(outsider, ProjectStatus.Active)).Why);
    }

    [Fact]
    public void Project_status_changes()
    {
        Persona x(string n) => People.Single(p => p.Name == n);
        Assert.True(Permissions.ChangeProjectStatus(x("pm").Actor, Ctx(x("pm"), ProjectStatus.Active), ProjectStatus.OnHold));
        Assert.Equal("perm.pm", Permissions.ChangeProjectStatus(x("dl").Actor, Ctx(x("dl"), ProjectStatus.Active), ProjectStatus.OnHold).Why);
        Assert.True(Permissions.ChangeProjectStatus(x("admin").Actor, Ctx(x("admin"), ProjectStatus.Archived), ProjectStatus.Complete)); // unarchive
        Assert.Equal("perm.admin", Permissions.ChangeProjectStatus(x("pm").Actor, Ctx(x("pm"), ProjectStatus.Archived), ProjectStatus.Complete).Why);
        Assert.Equal("perm.admin", Permissions.ChangeProjectStatus(x("admin").Actor, Ctx(x("admin"), ProjectStatus.Archived), ProjectStatus.Active).Why);
        Assert.Equal("perm.project_read_only", Permissions.ChangeProjectStatus(x("admin").Actor, Ctx(x("admin"), ProjectStatus.Cancelled), ProjectStatus.Active).Why);
    }

    [Fact]
    public void Restricted_projects_are_visible_to_members_admins_executives_and_the_pm() // §8.7
    {
        var exec = new Actor(Guid.NewGuid(), true, new HashSet<string> { SystemRole.Executive });
        var stranger = new Actor(Guid.NewGuid(), true, new HashSet<string> { SystemRole.ProjectManager });
        ProjectContext R(Guid pmId, params string[] roles) => new(Pid, ProjectStatus.Active, Visibility.Restricted, pmId, true, new HashSet<string>(roles), new HashSet<Guid>(), null);
        Assert.True(Permissions.CanView(exec, R(Ids["pm"])));
        Assert.False(Permissions.CanView(stranger, R(Ids["pm"])));
        Assert.True(Permissions.CanView(stranger, R(stranger.Id)));
        Assert.True(Permissions.CanView(stranger, R(Ids["pm"], ProjectRole.Viewer)));
        Assert.True(Permissions.CanView(stranger, new ProjectContext(Pid, ProjectStatus.Active, Visibility.Open, Ids["pm"], true, new HashSet<string>(), new HashSet<Guid>(), null)));
        Assert.Equal("perm.supervisor", Permissions.StaffOnProject(stranger, R(Ids["pm"]), null).Why);
    }

    [Fact]
    public void Supervisors_staff_their_direct_reports_on_live_projects() // ASG-10, Q19
    {
        var sup = People.Single(p => p.Name == "supervisor");
        var ctx = Ctx(sup, ProjectStatus.Active);
        Assert.True(Permissions.StaffOnProject(sup.Actor, ctx, sup.Actor.Id));
        Assert.Equal("perm.supervisor", Permissions.StaffOnProject(sup.Actor, ctx, Guid.NewGuid()).Why);
        Assert.Equal("perm.project_read_only", Permissions.StaffOnProject(sup.Actor, Ctx(sup, ProjectStatus.Complete), sup.Actor.Id).Why);
        Assert.True(Permissions.StaffOnProject(People[0].Actor, Ctx(People[0], ProjectStatus.OnHold), null)); // Admin
        Assert.True(Permissions.ViewPersonWork(sup.Actor, Guid.NewGuid(), sup.Actor.Id));
        Assert.False(Permissions.ViewPersonWork(sup.Actor, Guid.NewGuid(), Guid.NewGuid()));
        Assert.True(Permissions.ViewPersonWork(sup.Actor, sup.Actor.Id, null));
    }

    [Fact]
    public void Comments_are_deleted_by_authors_or_the_pm_and_edited_within_15_minutes() // C-02
    {
        var tm = People.Single(p => p.Name == "tm");
        var pm = People.Single(p => p.Name == "pm");
        Assert.True(Permissions.DeleteComment(tm.Actor, Ctx(tm, ProjectStatus.Active), tm.Actor.Id));
        Assert.True(Permissions.DeleteComment(pm.Actor, Ctx(pm, ProjectStatus.Active), tm.Actor.Id));
        Assert.Equal("perm.pm", Permissions.DeleteComment(tm.Actor, Ctx(tm, ProjectStatus.Active), pm.Actor.Id).Why);
        Assert.True(Permissions.EditComment(tm.Actor, tm.Actor.Id, Now.AddMinutes(-15), Now));
        Assert.Equal("perm.comment_edit_window", Permissions.EditComment(tm.Actor, tm.Actor.Id, Now.AddMinutes(-16), Now).Why);
        Assert.Equal("perm.comment_edit_window", Permissions.EditComment(pm.Actor, tm.Actor.Id, Now, Now).Why);
    }

    [Fact]
    public void Time_entries_are_corrected_by_the_pm_with_a_reason_and_seen_by_leads_and_supervisors() // §36.8
    {
        Persona x(string n) => People.Single(p => p.Name == n);
        Assert.Equal((Allow.Yes, false), Permissions.EditTime(x("tm").Actor, Ctx(x("tm"), ProjectStatus.Active), Ids["tm"]));
        Assert.Equal((Allow.Yes, true), Permissions.EditTime(x("pm").Actor, Ctx(x("pm"), ProjectStatus.Active), Ids["tm"]));
        Assert.Equal("perm.time_entry", Permissions.EditTime(x("dl").Actor, Ctx(x("dl"), ProjectStatus.Active), Ids["tm"]).Allow.Why);
        bool View(string n, Guid? supervisor = null, string vis = Visibility.Open) => Permissions.ViewTimeEntry(x(n).Actor, Ctx(x(n), ProjectStatus.Active, vis), Ids["tm"], Civ, supervisor);
        Assert.True(View("tm"));
        Assert.True(View("pm"));
        Assert.True(View("dl"));
        Assert.False(View("dlOther"));
        Assert.True(View("supervisor", Ids["supervisor"]));
        Assert.False(View("supervisor", Guid.NewGuid()));
        Assert.False(View("outsider", Ids["outsider"], Visibility.Restricted));
        Assert.True(Permissions.EditEvent(x("tm").Actor, null, Ids["tm"]));
        Assert.Equal("perm.owner", Permissions.EditEvent(x("pm").Actor, null, Ids["tm"]).Why);
    }

    [Fact]
    public void System_level_rights() // §8.5.1
    {
        Actor A(params string[] r) => new(Guid.NewGuid(), true, new HashSet<string>(r));
        var none = A();
        Assert.True(Permissions.CreateProject(A(SystemRole.ProjectManager)));
        Assert.Equal("perm.create_project", Permissions.CreateProject(none).Why);
        foreach (var r in new[] { SystemRole.Admin, SystemRole.Executive, SystemRole.Supervisor, SystemRole.ProjectManager })
        {
            Assert.True(Permissions.ViewPortfolio(A(r)));
            Assert.True(Permissions.ViewWorkload(A(r)));
        }
        Assert.False(Permissions.ViewPortfolio(none));
        Assert.False(Permissions.ViewWorkload(A(SystemRole.ReadOnly)));
        Assert.True(Permissions.ViewStaff(A(SystemRole.Supervisor)));
        Assert.False(Permissions.ViewStaff(A(SystemRole.ProjectManager)));
        Assert.True(Permissions.ViewAllStaff(A(SystemRole.Executive)));
        Assert.False(Permissions.ViewAllStaff(A(SystemRole.Supervisor)));
        Assert.True(Permissions.Administer(A(SystemRole.Admin)));
        Assert.Equal("perm.admin", Permissions.Administer(A(SystemRole.Executive)).Why);
        Assert.True(Permissions.ManageTemplates(none, templateEditor: true));
        Assert.False(Permissions.ManageTemplates(none, templateEditor: false));
        Assert.True(Permissions.ManageTemplates(A(SystemRole.Admin, SystemRole.ReadOnly), templateEditor: false)); // Admin is never read-only
        Assert.True(Permissions.ActOnStaff(A(SystemRole.Admin), null));
    }

    [Fact]
    public void Due_change_reasons_apply_to_everyone_but_the_pm_and_lead() // T-16
    {
        foreach (var x in People.Where(p => p.Name != "readOnly"))
            Assert.True(Permissions.DueChangeNeedsReason(x.Actor, Ctx(x, ProjectStatus.Active), Task) == !(Pms.Contains(x.Name) || x.Name == "dl"), x.Name);
    }

    [Fact]
    public void The_team_arranges_the_board_but_viewers_and_outsiders_do_not() // §13.4, packet 019
    {
        foreach (var x in People)
            Assert.True(Permissions.ArrangeBoard(x.Actor, Ctx(x, ProjectStatus.Active)).Ok == Members.Contains(x.Name), x.Name);
        var pm = People.Single(x => x.Name == "pm");
        Assert.Equal("perm.project_read_only", Permissions.ArrangeBoard(pm.Actor, Ctx(pm, ProjectStatus.Archived)).Why);
    }

    [Fact]
    public void Saved_project_views_are_shared_by_the_pm_and_leads() // §18.4
    {
        foreach (var x in People)
            Assert.True(Permissions.ManageSavedProjectView(x.Actor, Ctx(x, ProjectStatus.Active)).Ok == With("dl", "dlOther").Contains(x.Name), x.Name);
    }
}
