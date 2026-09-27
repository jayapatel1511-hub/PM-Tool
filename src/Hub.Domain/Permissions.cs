namespace Hub.Domain;

/// The signed-in person's organisation-wide standing (§8.2).
public sealed record Actor(Guid Id, bool IsActive, IReadOnlySet<string> Roles)
{
    public bool Has(string r) => Roles.Contains(r);
    public bool Admin => Has(SystemRole.Admin);
    public bool Executive => Has(SystemRole.Executive);
    public bool Supervisor => Has(SystemRole.Supervisor);
    public bool SystemPM => Has(SystemRole.ProjectManager);
    /// Read Only removes every write the other roles would grant (AC-PERM-04); Admin is never read-only.
    public bool ReadOnly => Has(SystemRole.ReadOnly) && !Admin;
}

/// The actor's standing on one project (§8.3). Discipline Lead is derived from project disciplines (§12.2).
public sealed record ProjectContext(
    Guid ProjectId, string Status, string Visibility, Guid PrimaryPmId, bool AllowViewerComments,
    IReadOnlySet<string> MemberRoles, IReadOnlySet<Guid> LeadDisciplineIds, Guid? PrimaryDisciplineId)
{
    public bool IsMember => MemberRoles.Count > 0 || LeadDisciplineIds.Count > 0;
    public bool Has(string role) => MemberRoles.Contains(role);
}

/// Result of a permission check with the reason shown on hover and in 403 bodies (§8.9).
public readonly record struct Allow(bool Ok, string? Why = null, string? Arg = null)
{
    public static readonly Allow Yes = new(true);
    public static Allow No(string why, string? arg = null) => new(false, why, arg);
    public static implicit operator bool(Allow a) => a.Ok;
}

/// Facts about a task the matrix needs (§8.5.2, §8.6).
public sealed record TaskFacts(Guid DisciplineId, Guid? AssigneeId, Guid? ReviewerId, Guid? CreatedBy, IReadOnlySet<Guid> Collaborators,
    string Status, bool HasDependencies = false, bool CreatorWindowOpen = false, Guid? AssigneeSupervisorId = null);

public sealed record DeliverableFacts(Guid DisciplineId, Guid? OwnerId, Guid? ReviewerId, bool HasCompletedTasks = false);

/// Owner-style items: decisions, risks, issues, meeting actions, calendar events, time entries.
public sealed record OwnedFacts(Guid? OwnerId, Guid? CreatedBy, Guid? DisciplineId = null);

/// The permission matrix of §8.5 as pure functions of (actor, roles, memberships, item) (§8.9 principle 3).
public static class Permissions
{
    // Shared packet 026/027 gates. Management does not confer a named technical signature.
    public static Allow CoordinationWrite(Actor a, ProjectContext p) => !a.IsActive || !CanView(a, p)
        ? Allow.No("perm.not_member") : Writable(a, p);
    public static Allow CoordinateReview(Actor a, ProjectContext p, Guid discipline, Guid? coordinator = null)
    {
        var gate = CoordinationWrite(a, p); if (!gate) return gate;
        return IsPM(a, p) || IsDL(p, discipline) || (coordinator == a.Id && p.IsMember) ? Allow.Yes : Allow.No("perm.pm_or_dl");
    }
    public static Allow PublishSource(Actor a, ProjectContext p, Guid discipline, Guid? owner)
    {
        var gate = CoordinationWrite(a, p); if (!gate) return gate;
        return IsPM(a, p) || IsDL(p, discipline) || (owner == a.Id && p.IsMember) ? Allow.Yes : Allow.No("perm.pm_or_dl");
    }
    public static Allow NamedCoordinationAction(Actor a, ProjectContext p, Guid owner)
    {
        var gate = CoordinationWrite(a, p); if (!gate) return gate;
        return a.Id == owner && p.IsMember ? Allow.Yes : Allow.No("perm.owner");
    }
    public static Allow ManageCoordination(Actor a, ProjectContext p, Guid discipline)
    {
        var gate = CoordinationWrite(a, p); if (!gate) return gate;
        return IsPM(a, p) || IsDL(p, discipline) ? Allow.Yes : Allow.No("perm.pm_or_dl");
    }

    public static Allow CreateSubmission(Actor a, ProjectContext p)
    {
        var gate = CoordinationWrite(a, p); if (!gate) return gate;
        return IsPM(a, p) || IsAnyDL(p) ? Allow.Yes : Allow.No("perm.pm_or_dl");
    }
    public static Allow CreateBasis(Actor a, ProjectContext p)
    {
        var gate = CoordinationWrite(a, p); if (!gate) return gate;
        return IsPM(a, p) || IsAnyDL(p) ? Allow.Yes : Allow.No("perm.pm_or_dl");
    }
    public static Allow ProposeAllocation(Actor a, ProjectContext p)
    {
        var gate = CoordinationWrite(a, p); if (!gate) return gate;
        return IsPM(a, p) || IsAnyDL(p) ? Allow.Yes : Allow.No("perm.pm_or_dl");
    }
    public static Allow ConfirmAllocation(Actor a, ProjectContext p, Guid? personSupervisorId)
    {
        var gate = CoordinationWrite(a, p); if (!gate) return gate;
        return ActOnStaff(a, personSupervisorId);
    }
    public static Allow CoordinateSubmission(Actor a, ProjectContext p, Guid coordinator)
    {
        var gate = CoordinationWrite(a, p); if (!gate) return gate;
        return a.Id == coordinator && p.IsMember ? Allow.Yes : Allow.No("perm.owner");
    }
    public static Allow SignSubmissionCheck(Actor a, ProjectContext p, Guid owner)
        => NamedCoordinationAction(a, p, owner);
    public static Allow AuthoriseSubmission(Actor a, ProjectContext p)
    {
        var gate = CoordinationWrite(a, p); if (!gate) return gate;
        return p.PrimaryPmId == a.Id || p.Has(ProjectRole.PM) ? Allow.Yes : Allow.No("perm.pm");
    }

    // Packet 025: management rights never imply permission to sign another person's receipt.
    static Allow HandoffGate(Actor a, ProjectContext p) => !a.IsActive || !CanView(a, p)
        ? Allow.No("perm.not_member") : Writable(a, p);

    public static Allow CreateHandoff(Actor a, ProjectContext p, Guid sendingDiscipline, Guid receivingDiscipline, Guid? sourceOwner)
    {
        var gate = HandoffGate(a, p); if (!gate) return gate;
        return IsPM(a, p) || IsDL(p, sendingDiscipline) || IsDL(p, receivingDiscipline)
            || (a.Id == sourceOwner && p.IsMember && p.PrimaryDisciplineId == sendingDiscipline)
            ? Allow.Yes : Allow.No("handoff.create_permission");
    }

    public static Allow AssignHandoff(Actor a, ProjectContext p, HandoffFacts h)
    {
        var gate = HandoffGate(a, p); if (!gate) return gate;
        return IsPM(a, p) || IsDL(p, h.SendingDisciplineId) || IsDL(p, h.ReceivingDisciplineId)
            ? Allow.Yes : Allow.No("perm.pm_or_dl");
    }

    public static Allow EditHandoff(Actor a, ProjectContext p, HandoffFacts h)
    {
        var gate = HandoffGate(a, p); if (!gate) return gate;
        if (!HandoffRules.Editable(h.Status)) return Allow.No("handoff.fixed");
        return a.Id == h.SendingOwnerId || AssignHandoff(a, p, h) ? Allow.Yes : Allow.No("handoff.sender");
    }

    public static Allow HandoffTransition(Actor a, ProjectContext p, HandoffFacts h, string to, bool allowSelfReview)
    {
        var gate = HandoffGate(a, p); if (!gate) return gate;
        if (!HandoffRules.Step(h.Status, to)) return Allow.No("handoff.illegal_transition");
        if (to == HandoffStatus.Cancelled) return AssignHandoff(a, p, h);
        if (to == HandoffStatus.Submitted) return a.Id == h.SendingOwnerId ? Allow.Yes : Allow.No("handoff.sender");
        if (a.Id != h.ReceivingOwnerId) return Allow.No("handoff.receiver");
        if (to is HandoffStatus.Accepted or HandoffStatus.Incorporated && !allowSelfReview && HandoffRules.SelfReceipt(h))
            return Allow.No("handoff.self_receipt");
        return Allow.Yes;
    }

    // ---------- System level (§8.5.1) ----------

    public static Allow CreateProject(Actor a) => a.ReadOnly ? Allow.No("perm.read_only") : a.Admin || a.SystemPM ? Allow.Yes : Allow.No("perm.create_project");
    public static Allow ViewPortfolio(Actor a) => a.Admin || a.Executive || a.Supervisor || a.SystemPM ? Allow.Yes : Allow.No("perm.portfolio");
    public static Allow ViewWorkload(Actor a) => a.Admin || a.Executive || a.Supervisor || a.SystemPM ? Allow.Yes : Allow.No("perm.workload");
    public static Allow ViewStaff(Actor a) => a.Admin || a.Executive || a.Supervisor ? Allow.Yes : Allow.No("perm.staff");
    public static bool ViewAllStaff(Actor a) => a.Admin || a.Executive;
    public static Allow Administer(Actor a) => a.Admin ? Allow.Yes : Allow.No("perm.admin");
    public static Allow ManageTemplates(Actor a, bool templateEditor) => a.Admin || (templateEditor && !a.ReadOnly) ? Allow.Yes : Allow.No("perm.templates");

    /// Supervisors act on their direct reports only (Q19, ASG-08); Admins on anyone.
    public static Allow ActOnStaff(Actor a, Guid? personSupervisorId) =>
        a.ReadOnly ? Allow.No("perm.read_only") : a.Admin || (a.Supervisor && personSupervisorId == a.Id) ? Allow.Yes : Allow.No("perm.supervisor");

    public static bool ViewPersonWork(Actor a, Guid personId, Guid? personSupervisorId) =>
        a.Id == personId || a.Admin || a.Executive || (a.Supervisor && personSupervisorId == a.Id);

    // ---------- Visibility (§8.7) ----------

    public static bool CanView(Actor a, ProjectContext p) =>
        p.Visibility != Visibility.Restricted || p.IsMember || a.Admin || a.Executive || p.PrimaryPmId == a.Id;

    // ---------- Project level (§8.5.2) ----------

    public static bool IsPM(Actor a, ProjectContext p) => a.Admin || p.PrimaryPmId == a.Id || p.Has(ProjectRole.PM);
    public static bool IsDL(ProjectContext p, Guid disciplineId) => p.LeadDisciplineIds.Contains(disciplineId);
    public static bool IsAnyDL(ProjectContext p) => p.LeadDisciplineIds.Count > 0;

    /// Gate before any project write: Read Only accounts, Archived/Cancelled projects (P-06), and Complete
    /// projects, which stay editable by the PM only (P-05).
    public static Allow Writable(Actor a, ProjectContext p, bool pmOnly = false)
    {
        if (a.ReadOnly) return Allow.No("perm.read_only");
        if (ProjectStatus.IsReadOnly(p.Status)) return Allow.No("perm.project_read_only", p.Status);
        if ((p.Status == ProjectStatus.Complete || pmOnly) && !IsPM(a, p)) return Allow.No(p.Status == ProjectStatus.Complete ? "perm.project_read_only" : "perm.pm", p.Status);
        return Allow.Yes;
    }

    static Allow PmOnly(Actor a, ProjectContext p) => Writable(a, p, pmOnly: true);

    public static Allow EditProject(Actor a, ProjectContext p) => PmOnly(a, p);
    public static Allow ManageTeam(Actor a, ProjectContext p) => PmOnly(a, p);
    public static Allow HealthOverride(Actor a, ProjectContext p) => PmOnly(a, p);
    public static Allow ManageMilestones(Actor a, ProjectContext p) => PmOnly(a, p);

    public static Allow ChangeProjectStatus(Actor a, ProjectContext p, string to)
    {
        if (a.ReadOnly) return Allow.No("perm.read_only");
        if (p.Status == ProjectStatus.Archived) return a.Admin && to == ProjectStatus.Complete ? Allow.Yes : Allow.No("perm.admin");
        if (p.Status == ProjectStatus.Cancelled) return Allow.No("perm.project_read_only", p.Status);
        return IsPM(a, p) ? Allow.Yes : Allow.No("perm.pm");
    }

    /// Supervisors add or remove their direct reports as Team Members on Setup, Active or On Hold projects (ASG-10).
    public static Allow StaffOnProject(Actor a, ProjectContext p, Guid? personSupervisorId)
    {
        if (!ProjectStatus.IsLive(p.Status)) return Allow.No("perm.project_read_only", p.Status);
        if (!CanView(a, p)) return Allow.No("perm.supervisor");
        return ActOnStaff(a, personSupervisorId);
    }

    public static Allow CreateDeliverable(Actor a, ProjectContext p, Guid disciplineId)
    {
        var w = Writable(a, p); if (!w) return w;
        return IsPM(a, p) || IsDL(p, disciplineId) ? Allow.Yes : Allow.No("perm.dl_own");
    }

    public static Allow EditDeliverable(Actor a, ProjectContext p, DeliverableFacts d)
    {
        var w = Writable(a, p); if (!w) return w;
        return IsPM(a, p) || IsDL(p, d.DisciplineId) || d.OwnerId == a.Id ? Allow.Yes : Allow.No("perm.dl_own");
    }

    /// Owners change status except to Issued/Accepted; reviewers take In Review → Revision Required / Ready to Issue (§8.6).
    public static Allow DeliverableTransition(Actor a, ProjectContext p, DeliverableFacts d, string from, string to)
    {
        var w = Writable(a, p); if (!w) return w;
        if (IsPM(a, p) || IsDL(p, d.DisciplineId)) return Allow.Yes;
        if (to == DeliverableStatus.Cancelled) return Allow.No("perm.pm_or_dl");
        if (d.ReviewerId == a.Id && from == DeliverableStatus.InReview && to is DeliverableStatus.RevisionRequired or DeliverableStatus.ReadyToIssue) return Allow.Yes;
        if (d.OwnerId == a.Id && to is not (DeliverableStatus.Issued or DeliverableStatus.Accepted)) return Allow.Yes;
        return Allow.No("perm.transition", to);
    }

    public static Allow DeleteDeliverable(Actor a, ProjectContext p, DeliverableFacts d)
    {
        var w = Writable(a, p); if (!w) return w;
        return IsPM(a, p) || (IsDL(p, d.DisciplineId) && !d.HasCompletedTasks) ? Allow.Yes : Allow.No("perm.pm_or_dl");
    }

    /// Team Members create tasks in disciplines they belong to (Recommendation in §8.3).
    public static Allow CreateTask(Actor a, ProjectContext p, Guid disciplineId)
    {
        var w = Writable(a, p); if (!w) return w;
        if (IsPM(a, p) || IsDL(p, disciplineId)) return Allow.Yes;
        return p.Has(ProjectRole.TeamMember) && p.PrimaryDisciplineId == disciplineId ? Allow.Yes : Allow.No("perm.dl_own");
    }

    static bool Owns(Actor a, TaskFacts t) => t.AssigneeId == a.Id || t.Collaborators.Contains(a.Id);

    public static Allow EditTask(Actor a, ProjectContext p, TaskFacts t)
    {
        var w = Writable(a, p); if (!w) return w;
        return IsPM(a, p) || IsDL(p, t.DisciplineId) || Owns(a, t) || (t.CreatedBy == a.Id && t.CreatorWindowOpen) ? Allow.Yes : Allow.No("perm.task_edit");
    }

    /// Supervisors also reassign tasks their direct reports own (§8.5.1 "Reassign tasks across projects for supervised staff").
    public static Allow AssignTask(Actor a, ProjectContext p, TaskFacts t, bool atCreation = false)
    {
        var w = Writable(a, p); if (!w) return w;
        return IsPM(a, p) || IsDL(p, t.DisciplineId) || (atCreation && t.CreatedBy == a.Id)
            || (a.Supervisor && t.AssigneeSupervisorId == a.Id && CanView(a, p)) ? Allow.Yes : Allow.No("perm.task_assign");
    }

    /// Collaborators cannot change the due date (T-15, AC-TSK-11); assignees can, with a reason (T-16).
    public static Allow ChangeDueDate(Actor a, ProjectContext p, TaskFacts t, bool atCreation = false)
    {
        var w = Writable(a, p); if (!w) return w;
        return IsPM(a, p) || IsDL(p, t.DisciplineId) || t.AssigneeId == a.Id || (atCreation && t.CreatedBy == a.Id) ? Allow.Yes : Allow.No("perm.task_due");
    }

    public static bool DueChangeNeedsReason(Actor a, ProjectContext p, TaskFacts t) => !(IsPM(a, p) || IsDL(p, t.DisciplineId));

    /// Who may take each canonical step (T-10 to T-14 and the §8.5.2 "Change task status" row).
    public static Allow TaskTransition(Actor a, ProjectContext p, TaskFacts t, string from, string to)
    {
        var w = Writable(a, p); if (!w) return w;
        var manager = IsPM(a, p) || IsDL(p, t.DisciplineId);
        var assignee = t.AssigneeId == a.Id;
        var collaborator = t.Collaborators.Contains(a.Id);
        var reviewer = t.ReviewerId == a.Id;
        var ok = (from, to) switch
        {
            (TaskStatuses.Cancelled, TaskStatuses.NotStarted) => IsPM(a, p),
            (_, TaskStatuses.Cancelled) => manager,
            (TaskStatuses.Complete, TaskStatuses.InProgress) => manager || reviewer,
            (TaskStatuses.ReadyForReview, TaskStatuses.InReview) or (TaskStatuses.InReview, TaskStatuses.Complete)
                or (TaskStatuses.InReview, TaskStatuses.RevisionRequired) => manager || reviewer,
            (TaskStatuses.ReadyForReview, TaskStatuses.InProgress) => manager || assignee,
            _ => manager || assignee || collaborator,
        };
        return ok ? Allow.Yes : Allow.No("perm.transition", to);
    }

    public static Allow SetReviewer(Actor a, ProjectContext p, TaskFacts t, bool atCreation = false) => AssignTask(a, p, t with { AssigneeSupervisorId = null }, atCreation);

    public static Allow ManualBlock(Actor a, ProjectContext p, TaskFacts t)
    {
        var w = Writable(a, p); if (!w) return w;
        return IsPM(a, p) || IsDL(p, t.DisciplineId) || Owns(a, t) ? Allow.Yes : Allow.No("perm.task_edit");
    }

    /// D-10: PM, a DL with either end in their discipline, or the successor's assignee.
    public static Allow ManageDependency(Actor a, ProjectContext p, TaskFacts predecessor, TaskFacts successor)
    {
        var w = Writable(a, p); if (!w) return w;
        return IsPM(a, p) || IsDL(p, predecessor.DisciplineId) || IsDL(p, successor.DisciplineId) || successor.AssigneeId == a.Id
            ? Allow.Yes : Allow.No("perm.dl_own");
    }

    /// FR-DEP-09 (packet 021): the PM or the lead of either deliverable's discipline links deliverables directly.
    public static Allow ManageDeliverableDependency(Actor a, ProjectContext p, Guid predecessorDisciplineId, Guid successorDisciplineId)
    {
        var w = Writable(a, p); if (!w) return w;
        return IsPM(a, p) || IsDL(p, predecessorDisciplineId) || IsDL(p, successorDisciplineId) ? Allow.Yes : Allow.No("perm.dl_own");
    }

    public static Allow DeleteTask(Actor a, ProjectContext p, TaskFacts t)
    {
        var w = Writable(a, p); if (!w) return w;
        if (IsPM(a, p)) return Allow.Yes;
        if (IsDL(p, t.DisciplineId)) return t.Status == TaskStatuses.NotStarted ? Allow.Yes : Allow.No("perm.pm");
        return t.CreatedBy == a.Id && t.Status == TaskStatuses.NotStarted && !t.HasDependencies ? Allow.Yes : Allow.No("perm.pm_or_dl");
    }

    public static Allow RestoreItem(Actor a, ProjectContext p) => PmOnly(a, p);

    public static Allow Comment(Actor a, ProjectContext p)
    {
        if (a.ReadOnly) return Allow.No("perm.read_only");
        if (ProjectStatus.IsReadOnly(p.Status)) return Allow.No("perm.project_read_only", p.Status);
        if (IsPM(a, p) || IsAnyDL(p) || p.Has(ProjectRole.TeamMember) || p.Has(ProjectRole.Reviewer)) return Allow.Yes;
        return p.Has(ProjectRole.Viewer) && p.AllowViewerComments ? Allow.Yes : Allow.No("perm.comment");
    }

    public static Allow AddLink(Actor a, ProjectContext p) => Comment(a, p);

    public static Allow DeleteComment(Actor a, ProjectContext p, Guid authorId) =>
        a.ReadOnly ? Allow.No("perm.read_only") : authorId == a.Id || IsPM(a, p) ? Allow.Yes : Allow.No("perm.pm");

    /// C-02: authors edit within 15 minutes of posting.
    public static Allow EditComment(Actor a, Guid authorId, DateTimeOffset createdAt, DateTimeOffset now) =>
        a.ReadOnly ? Allow.No("perm.read_only") : authorId == a.Id && now - createdAt <= TimeSpan.FromMinutes(15) ? Allow.Yes : Allow.No("perm.comment_edit_window");

    /// Decisions, risks, issues and meeting actions: PM, DL and Team Members raise; owners and requesters edit their own (§8.5.2).
    public static Allow RaiseRegisterItem(Actor a, ProjectContext p)
    {
        var w = Writable(a, p); if (!w) return w;
        return IsPM(a, p) || IsAnyDL(p) || p.Has(ProjectRole.TeamMember) ? Allow.Yes : Allow.No("perm.not_member");
    }

    public static Allow EditRegisterItem(Actor a, ProjectContext p, OwnedFacts i)
    {
        var w = Writable(a, p); if (!w) return w;
        return IsPM(a, p) || i.OwnerId == a.Id || i.CreatedBy == a.Id || (i.DisciplineId is { } d && IsDL(p, d)) ? Allow.Yes : Allow.No("perm.owner");
    }

    /// Recording an outcome: PM or the decision owner (§8.5.2 "Record a decision outcome").
    public static Allow DecideOrDefer(Actor a, ProjectContext p, OwnedFacts d)
    {
        var w = Writable(a, p); if (!w) return w;
        return IsPM(a, p) || d.OwnerId == a.Id ? Allow.Yes : Allow.No("perm.owner");
    }

    public static Allow RunCoordination(Actor a, ProjectContext p)
    {
        var w = Writable(a, p); if (!w) return w;
        return IsPM(a, p) || IsAnyDL(p) ? Allow.Yes : Allow.No("perm.pm_or_dl");
    }

    public static Allow Snooze(Actor a, ProjectContext p) => RunCoordination(a, p);

    public static Allow ManageSavedProjectView(Actor a, ProjectContext p) => a.ReadOnly ? Allow.No("perm.read_only") : IsPM(a, p) || IsAnyDL(p) ? Allow.Yes : Allow.No("perm.pm_or_dl");

    /// §13.4: the team arranges cards on the project board; the order is shared by the project's members.
    public static Allow ArrangeBoard(Actor a, ProjectContext p)
    {
        var w = Writable(a, p); if (!w) return w;
        return IsPM(a, p) || IsAnyDL(p) || p.Has(ProjectRole.TeamMember) || p.Has(ProjectRole.Reviewer) ? Allow.Yes : Allow.No("perm.not_member");
    }

    public static Allow ManageExternalParties(Actor a, ProjectContext p) => PmOnly(a, p);
    public static Allow CreateExternalParty(Actor a, ProjectContext p) => RaiseRegisterItem(a, p);

    // ---------- Calendar events (§36.5) ----------

    public static Allow CreateProjectEvent(Actor a, ProjectContext p)
    {
        var w = Writable(a, p); if (!w) return w;
        return IsPM(a, p) || IsAnyDL(p) || p.Has(ProjectRole.TeamMember) || p.Has(ProjectRole.Reviewer) ? Allow.Yes : Allow.No("perm.not_member");
    }

    public static Allow EditEvent(Actor a, ProjectContext? p, Guid ownerId)
    {
        if (a.ReadOnly) return Allow.No("perm.read_only");
        if (p is not null && ProjectStatus.IsReadOnly(p.Status)) return Allow.No("perm.project_read_only", p.Status);
        return ownerId == a.Id || (p is not null && IsPM(a, p)) ? Allow.Yes : Allow.No("perm.owner");
    }

    // ---------- Task hours (§36.8, FR-VIS-10) ----------

    public static Allow EnterTime(Actor a, ProjectContext p)
    {
        if (a.ReadOnly) return Allow.No("perm.read_only");
        if (ProjectStatus.IsReadOnly(p.Status)) return Allow.No("perm.project_read_only", p.Status);
        return IsPM(a, p) || IsAnyDL(p) || p.Has(ProjectRole.TeamMember) || p.Has(ProjectRole.Reviewer) ? Allow.Yes : Allow.No("perm.not_member");
    }

    /// Owners edit their own entries; a PM corrects anyone's with a reason (returned as the second value).
    public static (Allow Allow, bool NeedsReason) EditTime(Actor a, ProjectContext p, Guid entryOwnerId)
    {
        if (a.ReadOnly) return (Allow.No("perm.read_only"), false);
        if (ProjectStatus.IsReadOnly(p.Status)) return (Allow.No("perm.project_read_only", p.Status), false);
        if (entryOwnerId == a.Id) return (Allow.Yes, false);
        return IsPM(a, p) ? (Allow.Yes, true) : (Allow.No("perm.time_entry"), false);
    }

    /// PMs see all project entries, DLs their discipline's, Supervisors their direct reports', everyone their own.
    public static bool ViewTimeEntry(Actor a, ProjectContext p, Guid entryOwnerId, Guid taskDisciplineId, Guid? ownerSupervisorId) =>
        CanView(a, p) && (entryOwnerId == a.Id || IsPM(a, p) || IsDL(p, taskDisciplineId) || (a.Supervisor && ownerSupervisorId == a.Id));
}
