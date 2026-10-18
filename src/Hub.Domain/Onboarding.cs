namespace Hub.Domain;

/// The small, deterministic getting-started catalogue (§51.2). The API supplies the
/// completion facts for the current person; this type deliberately carries no project data.
public sealed record OnboardingFacts(
    bool ChangedTaskStatus = false, bool Commented = false, bool PlannedSelf = false,
    bool CreatedProject = false, bool AddedMilestone = false, bool AssignedTask = false, bool ActivatedProject = false,
    bool AddedDeliverable = false, bool ProposedAllocationOrRole = false,
    bool PlannedDirectReport = false, bool ConfirmedAllocation = false, bool RecordedAway = false,
    bool StarredProject = false, bool FollowedProject = false, bool SetSupervisors = false,
    bool ImportedLeave = false, bool ImportedProjects = false, bool CanPlanSelf = true);
public sealed record OnboardingStep(string Key, string Label, string? Route, bool Done = false);
public sealed record OnboardingGroup(string Role, IReadOnlyList<OnboardingStep> Steps);

public static class Onboarding
{
    public static IReadOnlyList<OnboardingStep> StepsFor(IEnumerable<string> roles, OnboardingFacts f)
    {
        var r = roles.ToHashSet();
        if (r.Contains(SystemRole.ReadOnly)) return [];
        var steps = new List<OnboardingStep>
        {
            new("change-status", "Move one of your tasks to a new status", "/my-work", f.ChangedTaskStatus),
            new("comment", "Comment on an item", "/my-work", f.Commented),
            new("self-plan", "Add your own plan in My Week", "/planner", f.PlannedSelf),
        };
        if (r.Contains(SystemRole.ProjectManager) || r.Contains(SystemRole.Admin))
            steps.AddRange([new("create-project", "Create a project", "/projects", f.CreatedProject), new("add-milestone", "Add a milestone", "/projects", f.AddedMilestone), new("assign-task", "Assign a task", "/my-work", f.AssignedTask), new("activate-project", "Activate a project", "/projects", f.ActivatedProject)]);
        if (r.Contains("DisciplineLead"))
            steps.AddRange([new("add-deliverable", "Add a deliverable", "/projects", f.AddedDeliverable), new("propose-allocation-or-role", "Propose an allocation or open role", "/planner", f.ProposedAllocationOrRole)]);
        if (r.Contains(SystemRole.Supervisor))
            steps.AddRange([new("plan-direct-report", "Plan a week for a direct report", "/planner", f.PlannedDirectReport), new("confirm-allocation", "Confirm an allocation", "/planner", f.ConfirmedAllocation), new("record-away", "Record time away for a direct report", "/vacation", f.RecordedAway)]);
        if (r.Contains(SystemRole.Executive))
            steps.AddRange([new("star-project", "Star a project", "/projects", f.StarredProject), new("follow-project", "Follow a project", "/projects", f.FollowedProject)]);
        if (r.Contains(SystemRole.Admin))
            steps.AddRange([new("set-supervisors", "Set Supervisors", "/admin/users", f.SetSupervisors), new("import-leave", "Import approved leave", "/vacation", f.ImportedLeave), new("import-projects", "Import the project list", "/projects", f.ImportedProjects)]);
        return steps.Where(step => step.Key != "self-plan" || f.CanPlanSelf).ToArray();
    }

    public static IReadOnlyList<OnboardingGroup> Groups(IEnumerable<string> roles, OnboardingFacts facts)
    {
        var steps = StepsFor(roles, facts);
        string Group(string key) => key switch {
            "change-status" or "comment" or "self-plan" => "Everyone",
            "create-project" or "add-milestone" or "assign-task" or "activate-project" => SystemRole.ProjectManager,
            "add-deliverable" or "propose-allocation-or-role" => "DisciplineLead",
            "plan-direct-report" or "confirm-allocation" or "record-away" => SystemRole.Supervisor,
            "star-project" or "follow-project" => SystemRole.Executive, _ => SystemRole.Admin };
        return steps.GroupBy(s => Group(s.Key)).Select(g => new OnboardingGroup(g.Key, g.ToArray())).ToArray();
    }
}
