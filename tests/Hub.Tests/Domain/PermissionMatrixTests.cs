using Hub.Domain;

namespace Hub.Tests.Domain;

/// §8.5 as the acceptance test for the authorisation layer: every matrix row is checked for every role.
public sealed class PermissionMatrixTests
{
    static readonly Guid Me = Guid.NewGuid(), Other = Guid.NewGuid(), Civil = Guid.NewGuid(), Electrical = Guid.NewGuid();

    static Actor A(params string[] roles) => new(Me, true, roles.ToHashSet());
    static readonly Actor Admin = A(SystemRole.Admin), Exec = A(SystemRole.Executive), Sup = A(SystemRole.Supervisor),
        SysPm = A(SystemRole.ProjectManager), Std = A(), RO = A(SystemRole.ReadOnly);

    static ProjectContext Ctx(string role, string status = ProjectStatus.Active, string visibility = Visibility.Open, bool viewerComments = true) => role switch
    {
        "PM" => new(Guid.NewGuid(), status, visibility, Me, viewerComments, new HashSet<string> { ProjectRole.PM }, new HashSet<Guid>(), null),
        "DL" => new(Guid.NewGuid(), status, visibility, Other, viewerComments, new HashSet<string> { ProjectRole.TeamMember }, new HashSet<Guid> { Civil }, Civil),
        "TM" => new(Guid.NewGuid(), status, visibility, Other, viewerComments, new HashSet<string> { ProjectRole.TeamMember }, new HashSet<Guid>(), Civil),
        "Reviewer" => new(Guid.NewGuid(), status, visibility, Other, viewerComments, new HashSet<string> { ProjectRole.Reviewer }, new HashSet<Guid>(), null),
        "Viewer" => new(Guid.NewGuid(), status, visibility, Other, viewerComments, new HashSet<string> { ProjectRole.Viewer }, new HashSet<Guid>(), null),
        _ => new(Guid.NewGuid(), status, visibility, Other, viewerComments, new HashSet<string>(), new HashSet<Guid>(), null),
    };

    static TaskFacts Task(Guid? assignee = null, Guid? reviewer = null, Guid? creator = null, string status = TaskStatuses.InProgress,
        Guid? discipline = null, bool collaborator = false, bool deps = false, bool window = false) =>
        new(discipline ?? Civil, assignee, reviewer, creator ?? Other, collaborator ? new HashSet<Guid> { Me } : new HashSet<Guid>(), status, deps, window);

    // ---------- §8.5.1 system level ----------

    [Theory]
    [InlineData("Admin", true, true, true, true)]
    [InlineData("Executive", false, true, true, true)]
    [InlineData("Supervisor", false, true, true, true)]
    [InlineData("ProjectManager", true, true, true, false)]
    [InlineData("Standard", false, false, false, false)]
    [InlineData("ReadOnly", false, false, false, false)]
    public void System_level_rows(string role, bool createProject, bool portfolio, bool workload, bool staff)
    {
        var a = role switch { "Admin" => Admin, "Executive" => Exec, "Supervisor" => Sup, "ProjectManager" => SysPm, "ReadOnly" => RO, _ => Std };
        Assert.Equal(createProject, Permissions.CreateProject(a).Ok);
        Assert.Equal(portfolio, Permissions.ViewPortfolio(a).Ok);
        Assert.Equal(workload, Permissions.ViewWorkload(a).Ok);
        Assert.Equal(staff, Permissions.ViewStaff(a).Ok);
        Assert.Equal(role == "Admin", Permissions.Administer(a).Ok);
        Assert.Equal(role is "Admin" or "Executive", Permissions.ViewAllStaff(a));
    }

    [Fact]
    public void Supervisors_act_on_direct_reports_only()
    {
        Assert.True(Permissions.ActOnStaff(Sup, Me).Ok);
        Assert.False(Permissions.ActOnStaff(Sup, Other).Ok);
        Assert.True(Permissions.ActOnStaff(Admin, Other).Ok);
        Assert.False(Permissions.ActOnStaff(Exec, Me).Ok);
        Assert.True(Permissions.StaffOnProject(Sup, Ctx("none"), Me).Ok);
        Assert.False(Permissions.StaffOnProject(Sup, Ctx("none", ProjectStatus.Complete), Me).Ok);
    }

    [Fact]
    public void Templates_need_admin_or_the_template_editor_flag()
    {
        Assert.True(Permissions.ManageTemplates(Admin, false).Ok);
        Assert.True(Permissions.ManageTemplates(Std, true).Ok);
        Assert.False(Permissions.ManageTemplates(SysPm, false).Ok);
    }

    // ---------- §8.7 visibility ----------

    [Fact]
    public void Restricted_projects_are_visible_to_members_admins_and_executives_only() // AC-PERM-05
    {
        var restricted = Ctx("none", visibility: Visibility.Restricted);
        Assert.False(Permissions.CanView(Std, restricted));
        Assert.False(Permissions.CanView(Sup, restricted));
        Assert.True(Permissions.CanView(Exec, restricted));
        Assert.True(Permissions.CanView(Admin, restricted));
        Assert.True(Permissions.CanView(Std, Ctx("TM", visibility: Visibility.Restricted)));
        Assert.True(Permissions.CanView(Std, Ctx("none")));
    }

    // ---------- §8.5.2 project level ----------

    [Theory]
    [InlineData("PM", true)] [InlineData("DL", false)] [InlineData("TM", false)] [InlineData("Reviewer", false)] [InlineData("Viewer", false)]
    public void PM_only_rows(string role, bool expected)
    {
        var c = Ctx(role);
        Assert.Equal(expected, Permissions.EditProject(Std, c).Ok);
        Assert.Equal(expected, Permissions.ManageTeam(Std, c).Ok);
        Assert.Equal(expected, Permissions.HealthOverride(Std, c).Ok);
        Assert.Equal(expected, Permissions.ManageMilestones(Std, c).Ok); // AC-PERM-01 for the DL
        Assert.Equal(expected, Permissions.ChangeProjectStatus(Std, c, ProjectStatus.OnHold).Ok);
    }

    [Theory]
    [InlineData("PM", true, true)] [InlineData("DL", true, false)] [InlineData("TM", false, false)] [InlineData("Reviewer", false, false)] [InlineData("Viewer", false, false)]
    public void Create_deliverable_in_own_discipline(string role, bool civil, bool electrical) // AC-PERM-02
    {
        Assert.Equal(civil, Permissions.CreateDeliverable(Std, Ctx(role), Civil).Ok);
        Assert.Equal(electrical, Permissions.CreateDeliverable(Std, Ctx(role), Electrical).Ok);
    }

    [Fact]
    public void Deliverable_owner_edits_and_moves_status_except_issue()
    {
        var owned = new DeliverableFacts(Electrical, Me, null);
        Assert.True(Permissions.EditDeliverable(Std, Ctx("TM"), owned).Ok);
        Assert.True(Permissions.DeliverableTransition(Std, Ctx("TM"), owned, DeliverableStatus.NotStarted, DeliverableStatus.InProgress).Ok);
        Assert.False(Permissions.DeliverableTransition(Std, Ctx("TM"), owned, DeliverableStatus.ReadyToIssue, DeliverableStatus.Issued).Ok);
        Assert.False(Permissions.DeliverableTransition(Std, Ctx("TM"), owned, DeliverableStatus.InProgress, DeliverableStatus.Cancelled).Ok);
        var reviewed = new DeliverableFacts(Electrical, Other, Me);
        Assert.True(Permissions.DeliverableTransition(Std, Ctx("Reviewer"), reviewed, DeliverableStatus.InReview, DeliverableStatus.ReadyToIssue).Ok);
        Assert.False(Permissions.DeliverableTransition(Std, Ctx("Reviewer"), reviewed, DeliverableStatus.InProgress, DeliverableStatus.InReview).Ok);
        Assert.True(Permissions.DeleteDeliverable(Std, Ctx("DL"), new DeliverableFacts(Civil, null, null)).Ok);
        Assert.False(Permissions.DeleteDeliverable(Std, Ctx("DL"), new DeliverableFacts(Civil, null, null, HasCompletedTasks: true)).Ok);
    }

    [Theory]
    [InlineData("PM", true, true)] [InlineData("DL", true, false)] [InlineData("TM", true, false)] [InlineData("Reviewer", false, false)] [InlineData("Viewer", false, false)]
    public void Create_task(string role, bool civil, bool electrical)
    {
        Assert.Equal(civil, Permissions.CreateTask(Std, Ctx(role), Civil).Ok);
        Assert.Equal(electrical, Permissions.CreateTask(Std, Ctx(role), Electrical).Ok);
    }

    [Fact]
    public void Team_member_updates_own_task_but_cannot_reassign() // AC-PERM-03
    {
        var mine = Task(assignee: Me, discipline: Electrical, status: TaskStatuses.NotStarted);
        Assert.True(Permissions.TaskTransition(Std, Ctx("TM"), mine, TaskStatuses.NotStarted, TaskStatuses.InProgress).Ok);
        Assert.True(Permissions.EditTask(Std, Ctx("TM"), mine).Ok);
        Assert.False(Permissions.AssignTask(Std, Ctx("TM"), mine).Ok);
        Assert.False(Permissions.TaskTransition(Std, Ctx("TM"), mine, TaskStatuses.NotStarted, TaskStatuses.Cancelled).Ok);
        Assert.False(Permissions.EditTask(Std, Ctx("TM"), Task(assignee: Other, discipline: Electrical)).Ok);
    }

    [Fact]
    public void Collaborator_updates_progress_but_not_due_date_or_assignee() // AC-TSK-11, T-15
    {
        var t = Task(assignee: Other, collaborator: true, discipline: Electrical);
        Assert.True(Permissions.EditTask(Std, Ctx("TM"), t).Ok);
        Assert.False(Permissions.ChangeDueDate(Std, Ctx("TM"), t).Ok);
        Assert.False(Permissions.AssignTask(Std, Ctx("TM"), t).Ok);
        Assert.True(Permissions.ManualBlock(Std, Ctx("TM"), t).Ok);
        Assert.True(Permissions.ChangeDueDate(Std, Ctx("TM"), Task(assignee: Me, discipline: Electrical)).Ok);
        Assert.True(Permissions.DueChangeNeedsReason(Std, Ctx("TM"), Task(assignee: Me, discipline: Electrical)));
        Assert.False(Permissions.DueChangeNeedsReason(Std, Ctx("DL"), Task(assignee: Me)));
    }

    [Fact]
    public void Reviewer_takes_review_steps_and_can_reopen()
    {
        var t = Task(assignee: Other, reviewer: Me, discipline: Electrical, status: TaskStatuses.InReview);
        Assert.True(Permissions.TaskTransition(Std, Ctx("Reviewer"), t, TaskStatuses.ReadyForReview, TaskStatuses.InReview).Ok);
        Assert.True(Permissions.TaskTransition(Std, Ctx("Reviewer"), t, TaskStatuses.InReview, TaskStatuses.Complete).Ok);
        Assert.True(Permissions.TaskTransition(Std, Ctx("Reviewer"), t, TaskStatuses.InReview, TaskStatuses.RevisionRequired).Ok);
        Assert.True(Permissions.TaskTransition(Std, Ctx("Reviewer"), t, TaskStatuses.Complete, TaskStatuses.InProgress).Ok);
        Assert.False(Permissions.TaskTransition(Std, Ctx("Reviewer"), t, TaskStatuses.NotStarted, TaskStatuses.InProgress).Ok);
        Assert.False(Permissions.TaskTransition(Std, Ctx("TM"), Task(assignee: Me, discipline: Electrical), TaskStatuses.Complete, TaskStatuses.InProgress).Ok);
        Assert.False(Permissions.TaskTransition(Std, Ctx("TM"), Task(assignee: Me, discipline: Electrical), TaskStatuses.ReadyForReview, TaskStatuses.InReview).Ok);
    }

    [Fact]
    public void Only_the_pm_restores_a_cancelled_task()
    {
        var t = Task(status: TaskStatuses.Cancelled);
        Assert.True(Permissions.TaskTransition(Std, Ctx("PM"), t, TaskStatuses.Cancelled, TaskStatuses.NotStarted).Ok);
        Assert.False(Permissions.TaskTransition(Std, Ctx("DL"), t, TaskStatuses.Cancelled, TaskStatuses.NotStarted).Ok);
    }

    [Fact]
    public void Dependencies_by_pm_either_end_lead_or_successor_assignee() // D-10
    {
        var civil = Task(discipline: Civil);
        var elec = Task(discipline: Electrical);
        Assert.True(Permissions.ManageDependency(Std, Ctx("DL"), civil, elec).Ok);
        Assert.True(Permissions.ManageDependency(Std, Ctx("DL"), elec, civil).Ok);
        Assert.False(Permissions.ManageDependency(Std, Ctx("DL"), elec, Task(discipline: Electrical)).Ok);
        // FR-DEP-09 (packet 021): deliverable links — the PM, or the lead of either end.
        Assert.True(Permissions.ManageDeliverableDependency(Std, Ctx("PM"), Electrical, Electrical).Ok);
        Assert.True(Permissions.ManageDeliverableDependency(Std, Ctx("DL"), Civil, Electrical).Ok);
        Assert.True(Permissions.ManageDeliverableDependency(Std, Ctx("DL"), Electrical, Civil).Ok);
        Assert.False(Permissions.ManageDeliverableDependency(Std, Ctx("DL"), Electrical, Electrical).Ok);
        Assert.False(Permissions.ManageDeliverableDependency(Std, Ctx("TM"), Civil, Civil).Ok);
        Assert.False(Permissions.ManageDeliverableDependency(RO, Ctx("PM"), Civil, Civil).Ok);
        Assert.True(Permissions.ManageDependency(Std, Ctx("TM"), elec, Task(assignee: Me, discipline: Electrical)).Ok);
        Assert.False(Permissions.ManageDependency(Std, Ctx("TM"), Task(assignee: Me, discipline: Electrical), elec).Ok);
    }

    [Fact]
    public void Delete_task_rules()
    {
        Assert.True(Permissions.DeleteTask(Std, Ctx("PM"), Task(status: TaskStatuses.InProgress)).Ok);
        Assert.True(Permissions.DeleteTask(Std, Ctx("DL"), Task(status: TaskStatuses.NotStarted)).Ok);
        Assert.False(Permissions.DeleteTask(Std, Ctx("DL"), Task(status: TaskStatuses.InProgress)).Ok);
        Assert.True(Permissions.DeleteTask(Std, Ctx("TM"), Task(creator: Me, discipline: Electrical, status: TaskStatuses.NotStarted)).Ok);
        Assert.False(Permissions.DeleteTask(Std, Ctx("TM"), Task(creator: Me, discipline: Electrical, status: TaskStatuses.NotStarted, deps: true)).Ok);
    }

    [Theory]
    [InlineData("PM", true, true, true)] [InlineData("DL", true, true, true)] [InlineData("TM", true, false, true)]
    [InlineData("Reviewer", true, false, false)] [InlineData("Viewer", true, false, false)]
    public void Comments_registers_and_coordination(string role, bool comment, bool coordination, bool raise)
    {
        Assert.Equal(comment, Permissions.Comment(Std, Ctx(role)).Ok);
        Assert.Equal(coordination, Permissions.RunCoordination(Std, Ctx(role)).Ok);
        Assert.Equal(raise, Permissions.RaiseRegisterItem(Std, Ctx(role)).Ok);
    }

    [Fact]
    public void Viewer_comments_follow_the_project_setting() // AC-COM-05
    {
        Assert.False(Permissions.Comment(Std, Ctx("Viewer", viewerComments: false)).Ok);
        Assert.False(Permissions.Comment(Std, Ctx("none")).Ok);
    }

    [Fact]
    public void Decision_outcome_by_pm_or_owner()
    {
        Assert.True(Permissions.DecideOrDefer(Std, Ctx("TM"), new OwnedFacts(Me, Other)).Ok);
        Assert.False(Permissions.DecideOrDefer(Std, Ctx("TM"), new OwnedFacts(Other, Me)).Ok);
        Assert.True(Permissions.EditRegisterItem(Std, Ctx("TM"), new OwnedFacts(Other, Me)).Ok);
        Assert.True(Permissions.DecideOrDefer(Std, Ctx("PM"), new OwnedFacts(Other, Other)).Ok);
    }

    [Fact]
    public void Calendar_events_and_task_hours()
    {
        Assert.True(Permissions.CreateProjectEvent(Std, Ctx("Reviewer")).Ok);
        Assert.False(Permissions.CreateProjectEvent(Std, Ctx("Viewer")).Ok);
        Assert.True(Permissions.EditEvent(Std, Ctx("TM"), Me).Ok);
        Assert.False(Permissions.EditEvent(Std, Ctx("TM"), Other).Ok);
        Assert.True(Permissions.EditEvent(Std, Ctx("PM"), Other).Ok);
        Assert.True(Permissions.EnterTime(Std, Ctx("TM")).Ok);
        Assert.False(Permissions.EnterTime(Std, Ctx("Viewer")).Ok);
        Assert.Equal((true, false), (Permissions.EditTime(Std, Ctx("TM"), Me).Allow.Ok, Permissions.EditTime(Std, Ctx("TM"), Me).NeedsReason));
        Assert.False(Permissions.EditTime(Std, Ctx("TM"), Other).Allow.Ok);
        Assert.Equal((true, true), (Permissions.EditTime(Std, Ctx("PM"), Other).Allow.Ok, Permissions.EditTime(Std, Ctx("PM"), Other).NeedsReason));
        Assert.True(Permissions.ViewTimeEntry(Std, Ctx("DL"), Other, Civil, null));
        Assert.False(Permissions.ViewTimeEntry(Std, Ctx("DL"), Other, Electrical, null));
        Assert.True(Permissions.ViewTimeEntry(Sup, Ctx("none"), Other, Electrical, Me));
    }

    // ---------- Status gates (P-05, P-06, AC-PERM-04) ----------

    [Fact]
    public void Read_only_accounts_cannot_write_anything() // AC-PERM-04
    {
        var c = Ctx("PM");
        Assert.False(Permissions.EditProject(RO, c).Ok);
        Assert.False(Permissions.Comment(RO, c).Ok);
        Assert.False(Permissions.CreateTask(RO, c, Civil).Ok);
        Assert.False(Permissions.CreateProject(RO).Ok);
        Assert.Equal("perm.read_only", Permissions.EditTask(RO, c, Task(assignee: Me)).Why);
    }

    [Fact]
    public void Archived_and_cancelled_projects_are_read_only_even_for_the_pm() // P-06
    {
        foreach (var st in new[] { ProjectStatus.Archived, ProjectStatus.Cancelled })
        {
            var c = Ctx("PM", st);
            Assert.False(Permissions.EditProject(Std, c).Ok);
            Assert.False(Permissions.CreateTask(Std, c, Civil).Ok);
            Assert.False(Permissions.Comment(Std, c).Ok);
            Assert.False(Permissions.EnterTime(Std, c).Ok);
        }
        Assert.True(Permissions.ChangeProjectStatus(Admin, Ctx("none", ProjectStatus.Archived), ProjectStatus.Complete).Ok);
        Assert.False(Permissions.ChangeProjectStatus(Std, Ctx("PM", ProjectStatus.Archived), ProjectStatus.Complete).Ok);
    }

    [Fact]
    public void Complete_projects_stay_editable_by_the_pm_only() // P-05
    {
        Assert.True(Permissions.EditTask(Std, Ctx("PM", ProjectStatus.Complete), Task()).Ok);
        Assert.False(Permissions.EditTask(Std, Ctx("TM", ProjectStatus.Complete), Task(assignee: Me)).Ok);
        Assert.False(Permissions.CreateDeliverable(Std, Ctx("DL", ProjectStatus.Complete), Civil).Ok);
    }

    [Fact]
    public void Admins_act_as_pm_everywhere_and_people_hold_the_union_of_roles() // E-19, §8.2
    {
        Assert.True(Permissions.ManageMilestones(Admin, Ctx("none")).Ok);
        var both = new ProjectContext(Guid.NewGuid(), ProjectStatus.Active, Visibility.Open, Me, true,
            new HashSet<string> { ProjectRole.PM, ProjectRole.TeamMember }, new HashSet<Guid> { Civil }, Civil);
        Assert.True(Permissions.CreateDeliverable(Std, both, Electrical).Ok);
        Assert.True(Permissions.RunCoordination(Std, both).Ok);
    }
}
