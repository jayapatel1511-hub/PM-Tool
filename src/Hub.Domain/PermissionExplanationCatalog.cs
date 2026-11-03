using System.Reflection;

namespace Hub.Domain;

public sealed record PermissionExampleOutcome(bool Allowed, string? ReasonCode = null, string? ReasonArgument = null, bool? RequiresReason = null);
public sealed record PermissionExample(string ActionKey, string ActionLabelKey, string ResultKind, string ScenarioKey, string ScenarioLabelKey,
    IReadOnlyList<string> FactKeys, PermissionExampleOutcome Outcome);
public sealed record PermissionRoleExamples(string Key, string LabelKey, string ScopeKind, IReadOnlyList<PermissionExample> Examples);
public sealed record PermissionActionBinding(string Key, string Signature);
public sealed record PermissionExampleChoice(string Key, string LabelKey);


public sealed record PermissionMembershipChoice(string Key, string LabelKey);
public sealed record PermissionOperationChoice(string Key, string RowId, string LabelKey);
public sealed record PermissionRowRequirement(string Key, string State);
public sealed record PermissionRowFactor(string ActionKey, string ActionLabelKey, string ResultKind, PermissionExampleOutcome Outcome);
public sealed record PermissionOperationExplanation(string Key, string LabelKey, string DecisionKind, IReadOnlyList<string> FactKeys,
    IReadOnlyList<PermissionRowFactor> Factors, IReadOnlyList<PermissionRowRequirement> Requirements);
public sealed record PermissionMatrixRowExplanation(string RowId, string MatrixRowKey, string LabelKey, IReadOnlyList<PermissionOperationExplanation> Operations);

/// Read-only conditional examples. Delegates call enforcement; this catalog never grants authority or copies grant results.
public static class PermissionExplanationCatalog
{
    static readonly Guid Me = Guid.Parse("00000000-0000-0000-0000-000000005601"), Other = Guid.Parse("00000000-0000-0000-0000-000000005602"),
        Third = Guid.Parse("00000000-0000-0000-0000-000000005603"), Civil = Guid.Parse("00000000-0000-0000-0000-000000005604"),
        Electrical = Guid.Parse("00000000-0000-0000-0000-000000005605");
    sealed record Persona(string Key, string LabelKey, string ScopeKind, string? System = null, string? Project = null, bool Lead = false);
    sealed record Scenario(string Key, bool Own = false, bool Reviewer = false, bool OtherDiscipline = false,
        bool ReadOnly = false, string ProjectStatus = "Active", bool RestrictedOutsider = false, bool TemplateEditor = false,
        bool AtCreation = false, bool DirectReport = false, bool PrivateDraft = false, bool Inactive = false, bool SelfReview = false,
        bool PrivateDraftOwner = false, bool Correction = false, bool Collaborator = false, bool RestrictedProject = false,
        string? TaskState = null, string? DeliverableState = null, string? HandoffState = null, bool ClosedItem = false, bool ExpiredComment = false,
        bool DesignatedVerifier = false, bool PackageCoordinator = false, string? DeliverablePreviousState = null, bool RecordedHoldReason = false,
        bool SelfPlanningEntry = false);
    sealed record Membership(string Key, string? Role = null, bool Lead = false);
    sealed class Case(Persona persona, Scenario scenario, Membership? membership = null)
    {
        public Scenario Scenario => scenario;
        public Actor Actor => new(Me, !scenario.Inactive, new[] { persona.System, scenario.ReadOnly ? SystemRole.ReadOnly : null }.OfType<string>().ToHashSet());
        // An explicit membership never derives from a system role. The null branch only preserves legacy factor examples.
        string? MemberRole => membership is null ? scenario.RestrictedOutsider ? null : persona.Project ?? ProjectRole.TeamMember : membership.Role;
        bool Leads => membership is null ? persona.Lead && !scenario.RestrictedOutsider : membership.Lead;
        public ProjectContext Project => new(Third, scenario.ProjectStatus,
            scenario.RestrictedOutsider || scenario.RestrictedProject ? Visibility.Restricted : Visibility.Open,
            MemberRole == ProjectRole.PM ? Me : Other, true,
            MemberRole is { } role ? new HashSet<string> { role } : new HashSet<string>(),
            Leads ? new HashSet<Guid> { Civil } : new HashSet<Guid>(), Civil);
        public Guid Discipline => scenario.OtherDiscipline ? Electrical : Civil;
        public Guid Owner => scenario.Own || scenario.SelfReview ? Me : Other;
        public Guid Reviewer => scenario.Reviewer || scenario.SelfReview ? Me : Third;
        public Guid Verifier => scenario.DesignatedVerifier || scenario.SelfReview ? Me : Third;
        public Guid Coordinator => scenario.PackageCoordinator ? Me : Third;
        public Guid? Supervisor => scenario.DirectReport ? Me : Third;
        public TaskFacts Task => new(Discipline, Owner, Reviewer, scenario.AtCreation ? Me : Third,
            OwnActualCollaborator ? new HashSet<Guid> { Me } : new HashSet<Guid>(),
            scenario.TaskState ?? (scenario.AtCreation ? TaskStatuses.NotStarted : scenario.Reviewer ? TaskStatuses.ReadyForReview : TaskStatuses.InProgress),
            CreatorWindowOpen: scenario.AtCreation, AssigneeSupervisorId: Supervisor);
        public string DeliverableState => scenario.DeliverableState ?? (scenario.Reviewer ? DeliverableStatus.InReview : DeliverableStatus.InProgress);
        public string? DeliverablePreviousState => scenario.DeliverablePreviousState;
        public DeliverableFacts Deliverable => new(Discipline, Owner, Reviewer, scenario.ClosedItem);
        public OwnedFacts Owned => new(Owner, scenario.AtCreation ? Me : Third, Discipline);
        public HandoffFacts Handoff => new(Discipline, Third, scenario.SelfReview ? Me : Owner, Reviewer,
            scenario.HandoffState ?? (scenario.Reviewer || scenario.SelfReview ? HandoffStatus.Submitted : HandoffStatus.Draft));
        public PlanningEntryFacts Planning => new(scenario.Own ? Me : Other,
            scenario.SelfPlanningEntry ? Owner : scenario.PrivateDraftOwner || scenario.DirectReport ? Me : Owner, Supervisor,
            scenario.Own || scenario.SelfPlanningEntry ? PlanningVisibility.Confirmed : scenario.PrivateDraft ? PlanningVisibility.Draft : PlanningVisibility.Published,
            scenario.Own || scenario.SelfPlanningEntry,
            Permissions.ActOnStaff(Actor, Supervisor).Ok);
        public bool TemplateEditor => scenario.TemplateEditor;
        public bool AtCreation => scenario.AtCreation;
        // Legacy own-item factor examples declared actual collaboration. Explicit row membership keeps ownership separate.
        public bool OwnActualCollaborator => scenario.Collaborator || (membership is null && scenario.Own);
        public bool NamedReviewer => scenario.Reviewer;
        public IReadOnlyList<string> Facts => ["adminRole.fact.syntheticConditions", "adminRole.fact.additionalGuards",
            scenario.OtherDiscipline ? "adminRole.fact.otherDiscipline" : "adminRole.fact.sameDiscipline",
            Owner == Me ? "adminRole.fact.ownItem" : "adminRole.fact.otherOwner",
            OwnActualCollaborator ? "adminRole.fact.ownTaskCollaborator" : "adminRole.fact.notTaskCollaborator",
            scenario.Reviewer || scenario.SelfReview ? "adminRole.fact.namedReviewer" : "adminRole.fact.otherReviewer",
            "adminRole.fact.project." + scenario.ProjectStatus,
            Project.IsMember ? "adminRole.fact.projectMember" : "adminRole.fact.membership.None",
            scenario.DirectReport ? "adminRole.fact.directReport" : "adminRole.fact.otherReport",
            scenario.Own || scenario.SelfPlanningEntry ? "adminRole.fact.confirmedSelfPlanning" : scenario.PrivateDraft ? "adminRole.fact.privateDraft" : "adminRole.fact.publishedPlanning",
            scenario.TemplateEditor ? "adminRole.fact.templateEditor" : "adminRole.fact.noTemplateEditor",
            scenario.AtCreation ? "adminRole.fact.creatorWindow" : "adminRole.fact.noCreatorWindow",
            scenario.Inactive ? "adminRole.fact.inactive" : "adminRole.fact.active",
            "adminRole.fact.membership." + (MemberRole ?? "None"),
            "adminRole.fact.viewerComments",
            scenario.DirectReport ? "adminRole.fact.planningManagerOwner" : "adminRole.fact.planningGenericOwner",
            scenario.SelfReview ? "adminRole.fact.selfHandoff" : scenario.Reviewer ? "adminRole.fact.handoffAccept" : "adminRole.fact.handoffSubmit",
            scenario.Reviewer ? "adminRole.fact.reviewSteps" : "adminRole.fact.productionSteps",
            scenario.ExpiredComment ? "adminRole.fact.expiredComment" : "adminRole.fact.commentAge",
            persona.System is { } system ? "adminRole.fact.systemRole." + system : "adminRole.fact.systemRole.Standard",
            Leads ? "adminRole.fact.leadsSelectedDiscipline" : "adminRole.fact.notSelectedDisciplineLead",
            Actor.ReadOnly ? "adminRole.fact.effectiveReadOnly" : "adminRole.fact.effectiveWritable",
            Actor.Has(SystemRole.ReadOnly) ? "adminRole.fact.storedReadOnly" : "adminRole.fact.noStoredReadOnly",
            scenario.Correction ? "adminRole.fact.correctionRequested" : "adminRole.fact.normalAccess",
            "adminRole.fact.task." + Task.Status, "adminRole.fact.deliverable." + DeliverableState,
            "adminRole.fact.handoff." + Handoff.Status, "adminRole.fact.noLiveEntity",
            "adminRole.fact.reviewRequired", "adminRole.fact.selfReviewDisabled"];
    }
    sealed record Binding(string Key, Func<Case, PermissionExampleOutcome> Evaluate, string ResultKind = "Permission");
    static PermissionExampleOutcome A(Allow allow) => new(allow.Ok, allow.Why, allow.Arg);
    static PermissionExampleOutcome B(bool allowed) => new(allowed);
    static readonly Binding[] Entries =
    [
        new(nameof(Permissions.CoordinationWrite), c => A(Permissions.CoordinationWrite(c.Actor, c.Project))),
        new(nameof(Permissions.RecordExternalReview), c => A(Permissions.RecordExternalReview(c.Actor, c.Project))),
        new(nameof(Permissions.CoordinateReview), c => A(Permissions.CoordinateReview(c.Actor, c.Project, c.Discipline, c.Owner))),
        new(nameof(Permissions.PublishSource), c => A(Permissions.PublishSource(c.Actor, c.Project, c.Discipline, c.Owner))),
        new(nameof(Permissions.NamedCoordinationAction), c => A(Permissions.NamedCoordinationAction(c.Actor, c.Project, c.Owner))),
        new(nameof(Permissions.ManageCoordination), c => A(Permissions.ManageCoordination(c.Actor, c.Project, c.Discipline))),
        new(nameof(Permissions.CreateSubmission), c => A(Permissions.CreateSubmission(c.Actor, c.Project))),
        new(nameof(Permissions.CreateBasis), c => A(Permissions.CreateBasis(c.Actor, c.Project, c.Discipline, c.Owner, c.Reviewer))),
        new(nameof(Permissions.ConfirmBasis), c => A(Permissions.ConfirmBasis(c.Actor, c.Project, c.Discipline, c.Reviewer))),
        new(nameof(Permissions.ProposeAllocation), c => A(Permissions.ProposeAllocation(c.Actor, c.Project))),
        new(nameof(Permissions.ManageOpenRole), c => A(Permissions.ManageOpenRole(c.Actor, c.Project, c.Owner))),
        new(nameof(Permissions.ConfirmAllocation), c => A(Permissions.ConfirmAllocation(c.Actor, c.Project, c.Supervisor))),
        new(nameof(Permissions.CoordinateSubmission), c => A(Permissions.CoordinateSubmission(c.Actor, c.Project, c.Owner))),
        new(nameof(Permissions.SignSubmissionCheck), c => A(Permissions.SignSubmissionCheck(c.Actor, c.Project, c.Owner))),
        new(nameof(Permissions.AuthoriseSubmission), c => A(Permissions.AuthoriseSubmission(c.Actor, c.Project))),
        new(nameof(Permissions.CreateHandoff), c => A(Permissions.CreateHandoff(c.Actor, c.Project, c.Discipline, Third, c.Owner))),
        new(nameof(Permissions.AssignHandoff), c => A(Permissions.AssignHandoff(c.Actor, c.Project, c.Handoff))),
        new(nameof(Permissions.EditHandoff), c => A(Permissions.EditHandoff(c.Actor, c.Project, c.Handoff))),
        new(nameof(Permissions.HandoffTransition), c => A(Permissions.HandoffTransition(c.Actor, c.Project, c.Handoff,
            c.Handoff.Status == HandoffStatus.Draft ? HandoffStatus.Submitted : HandoffStatus.Accepted, false))),
        new(nameof(Permissions.CreateProject), c => A(Permissions.CreateProject(c.Actor))),
        new(nameof(Permissions.ViewPortfolio), c => A(Permissions.ViewPortfolio(c.Actor))),
        new(nameof(Permissions.ViewWorkload), c => A(Permissions.ViewWorkload(c.Actor))),
        new(nameof(Permissions.ViewStaff), c => A(Permissions.ViewStaff(c.Actor))),
        new(nameof(Permissions.ViewAllStaff), c => B(Permissions.ViewAllStaff(c.Actor))),
        new(nameof(Permissions.Administer), c => A(Permissions.Administer(c.Actor))),
        new(nameof(Permissions.ManageTemplates), c => A(Permissions.ManageTemplates(c.Actor, c.TemplateEditor))),
        new(nameof(Permissions.ActOnStaff), c => A(Permissions.ActOnStaff(c.Actor, c.Supervisor))),
        new(nameof(Permissions.CreatePlanningEntry), c => A(Permissions.CreatePlanningEntry(c.Actor, c.Owned.OwnerId!.Value, c.Supervisor))),
        new(nameof(Permissions.ManagePlanningEntry), c => A(Permissions.ManagePlanningEntry(c.Actor, c.Planning))),
        new(nameof(Permissions.SeePlanningEntry), c => A(Permissions.SeePlanningEntry(c.Actor, c.Planning, c.Actor.Admin && c.Scenario.Correction))),
        new(nameof(Permissions.RecordTimeAway), c => A(Permissions.RecordTimeAway(c.Actor, c.Supervisor, c.Owner))),
        new(nameof(Permissions.ViewPersonWork), c => B(Permissions.ViewPersonWork(c.Actor, c.Owner, c.Supervisor))),
        new(nameof(Permissions.CanView), c => B(Permissions.CanView(c.Actor, c.Project))),
        new(nameof(Permissions.IsPM), c => B(Permissions.IsPM(c.Actor, c.Project)), "Condition"),
        new(nameof(Permissions.IsDL), c => B(Permissions.IsDL(c.Project, c.Discipline)), "Condition"),
        new(nameof(Permissions.IsAnyDL), c => B(Permissions.IsAnyDL(c.Project)), "Condition"),
        new(nameof(Permissions.Writable), c => A(Permissions.Writable(c.Actor, c.Project))),
        new(nameof(Permissions.EditProject), c => A(Permissions.EditProject(c.Actor, c.Project))),
        new(nameof(Permissions.ManageTeam), c => A(Permissions.ManageTeam(c.Actor, c.Project))),
        new(nameof(Permissions.SetMemberResponsibility), c => A(Permissions.SetMemberResponsibility(c.Actor, c.Project, Permissions.IsDL(c.Project, c.Discipline)))),
        new(nameof(Permissions.HealthOverride), c => A(Permissions.HealthOverride(c.Actor, c.Project))),
        new(nameof(Permissions.ManageMilestones), c => A(Permissions.ManageMilestones(c.Actor, c.Project))),
        new(nameof(Permissions.Rebaseline), c => A(Permissions.Rebaseline(c.Actor, c.Project, c.Scenario.ClosedItem))),
        new(nameof(Permissions.ChangeProjectStatus), c => A(Permissions.ChangeProjectStatus(c.Actor, c.Project, ProjectStatus.OnHold))),
        new(nameof(Permissions.StaffOnProject), c => A(Permissions.StaffOnProject(c.Actor, c.Project, c.Supervisor))),
        new(nameof(Permissions.CreateDeliverable), c => A(Permissions.CreateDeliverable(c.Actor, c.Project, c.Discipline))),
        new(nameof(Permissions.EditDeliverable), c => A(Permissions.EditDeliverable(c.Actor, c.Project, c.Deliverable))),
        new(nameof(Permissions.DeliverableTransition), c => A(Permissions.DeliverableTransition(c.Actor, c.Project, c.Deliverable,
            c.NamedReviewer ? DeliverableStatus.InReview : DeliverableStatus.InProgress, c.NamedReviewer ? DeliverableStatus.ReadyToIssue : DeliverableStatus.InReview))),
        new(nameof(Permissions.DeleteDeliverable), c => A(Permissions.DeleteDeliverable(c.Actor, c.Project, c.Deliverable))),
        new(nameof(Permissions.CreateTask), c => A(Permissions.CreateTask(c.Actor, c.Project, c.Discipline))),
        new(nameof(Permissions.MarkTaskContribution), c => A(Permissions.MarkTaskContribution(c.Actor, c.Project, c.OwnActualCollaborator))),
        new(nameof(Permissions.EditTask), c => A(Permissions.EditTask(c.Actor, c.Project, c.Task))),
        new(nameof(Permissions.AssignTask), c => A(Permissions.AssignTask(c.Actor, c.Project, c.Task, c.AtCreation))),
        new(nameof(Permissions.ChangeDueDate), c => A(Permissions.ChangeDueDate(c.Actor, c.Project, c.Task, c.AtCreation))),
        new(nameof(Permissions.DueChangeNeedsReason), c => B(Permissions.DueChangeNeedsReason(c.Actor, c.Project, c.Task)), "Condition"),
        new(nameof(Permissions.TaskTransition), c => A(Permissions.TaskTransition(c.Actor, c.Project, c.Task, c.Task.Status,
            c.NamedReviewer ? TaskStatuses.InReview : TaskStatuses.ReadyForReview))),
        new(nameof(Permissions.SetReviewer), c => A(Permissions.SetReviewer(c.Actor, c.Project, c.Task, c.AtCreation))),
        new(nameof(Permissions.ManualBlock), c => A(Permissions.ManualBlock(c.Actor, c.Project, c.Task))),
        new(nameof(Permissions.ManageDependency), c => A(Permissions.ManageDependency(c.Actor, c.Project, c.Task, c.Task with { DisciplineId = Electrical }))),
        new(nameof(Permissions.ManageDeliverableDependency), c => A(Permissions.ManageDeliverableDependency(c.Actor, c.Project, c.Discipline, Electrical))),
        new(nameof(Permissions.DeleteTask), c => A(Permissions.DeleteTask(c.Actor, c.Project, c.Task))),
        new(nameof(Permissions.RestoreItem), c => A(Permissions.RestoreItem(c.Actor, c.Project))),
        new(nameof(Permissions.Comment), c => A(Permissions.Comment(c.Actor, c.Project))),
        new(nameof(Permissions.AddLink), c => A(Permissions.AddLink(c.Actor, c.Project))),
        new(nameof(Permissions.DeleteComment), c => A(Permissions.DeleteComment(c.Actor, c.Project, c.Owner))),
        new(nameof(Permissions.EditComment), c => A(Permissions.EditComment(c.Actor, c.Owner, new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero), new(2026, 1, 1, 12, c.Scenario.ExpiredComment ? 16 : 5, 0, TimeSpan.Zero)))),
        new(nameof(Permissions.RaiseRegisterItem), c => A(Permissions.RaiseRegisterItem(c.Actor, c.Project))),
        new(nameof(Permissions.EditRegisterItem), c => A(Permissions.EditRegisterItem(c.Actor, c.Project, c.Owned))),
        new(nameof(Permissions.DecideOrDefer), c => A(Permissions.DecideOrDefer(c.Actor, c.Project, c.Owned))),
        new(nameof(Permissions.RunCoordination), c => A(Permissions.RunCoordination(c.Actor, c.Project))),
        new(nameof(Permissions.Snooze), c => A(Permissions.Snooze(c.Actor, c.Project))),
        new(nameof(Permissions.ReviewDates), c => A(Permissions.ReviewDates(c.Actor, c.Project))),
        new(nameof(Permissions.ManageSavedProjectView), c => A(Permissions.ManageSavedProjectView(c.Actor, c.Project))),
        new(nameof(Permissions.ArrangeBoard), c => A(Permissions.ArrangeBoard(c.Actor, c.Project))),
        new(nameof(Permissions.ManageExternalParties), c => A(Permissions.ManageExternalParties(c.Actor, c.Project))),
        new(nameof(Permissions.CreateExternalParty), c => A(Permissions.CreateExternalParty(c.Actor, c.Project))),
        new(nameof(Permissions.CreateProjectEvent), c => A(Permissions.CreateProjectEvent(c.Actor, c.Project))),
        new(nameof(Permissions.EditEvent), c => A(Permissions.EditEvent(c.Actor, c.Project, c.Owner))),
        new(nameof(Permissions.EnterTime), c => A(Permissions.EnterTime(c.Actor, c.Project))),
        new(nameof(Permissions.EditTime), c => { var result = Permissions.EditTime(c.Actor, c.Project, c.Owner); return A(result.Allow) with { RequiresReason = result.NeedsReason }; }),
        new(nameof(Permissions.ViewTimeEntry), c => B(Permissions.ViewTimeEntry(c.Actor, c.Project, c.Owner, c.Discipline, c.Supervisor))),
    ];
    static readonly Scenario[] Scenarios = [new("same-discipline"), new("other-discipline", OtherDiscipline: true), new("own-item", Own: true),
        new("named-reviewer", Reviewer: true), new("read-only", ReadOnly: true), new("complete-project", ProjectStatus: ProjectStatus.Complete),
        new("archived-project", ProjectStatus: ProjectStatus.Archived), new("restricted-outsider", RestrictedOutsider: true),
        new("template-editor", TemplateEditor: true), new("creator-window", AtCreation: true), new("direct-report", DirectReport: true),
        new("private-draft", PrivateDraft: true), new("inactive-person", Inactive: true), new("self-review", SelfReview: true),
        new("restricted-project", RestrictedProject: true), new("setup-project", ProjectStatus: ProjectStatus.Setup),
        new("on-hold-project", ProjectStatus: ProjectStatus.OnHold), new("cancelled-project", ProjectStatus: ProjectStatus.Cancelled),
        new("completed-task", TaskState: TaskStatuses.Complete), new("unstarted-task", TaskState: TaskStatuses.NotStarted),
        new("ready-to-issue", DeliverableState: DeliverableStatus.ReadyToIssue), new("issued-deliverable", DeliverableState: DeliverableStatus.Issued),
        new("accepted-handoff", Reviewer: true, HandoffState: HandoffStatus.Accepted),
        new("private-draft-owned", DirectReport: true, PrivateDraft: true, PrivateDraftOwner: true), new("admin-correction", PrivateDraft: true, Correction: true),
        new("task-collaborator", Collaborator: true), new("task-in-review", Reviewer: true, TaskState: TaskStatuses.InReview),
        new("task-revision-required", Own: true, TaskState: TaskStatuses.RevisionRequired), new("closed-item", ClosedItem: true), new("expired-comment", Own: true, ExpiredComment: true),
        new("designated-verifier", DesignatedVerifier: true), new("package-coordinator", PackageCoordinator: true),
        new("direct-report-self-entry", DirectReport: true, SelfPlanningEntry: true),
        new("on-hold-deliverable", Own: true, DeliverableState: DeliverableStatus.OnHold,
            DeliverablePreviousState: DeliverableStatus.RevisionRequired, RecordedHoldReason: true)];
    static IEnumerable<Persona> Personas => new[] { new Persona("baseline-Standard", "adminRole.role.baseline.Standard", "System") }
        .Concat(SystemRole.All.Select(role => new Persona("system-" + role, "adminRole.role.system." + role, "System", System: role)))
        .Concat(ProjectRole.All.Select(role => new Persona("project-" + role, "adminRole.role.project." + role, "Project", Project: role)))
        .Append(new("derived-DisciplineLead", "adminRole.role.derived.DisciplineLead", "Discipline", Lead: true));
    public static IReadOnlyList<PermissionActionBinding> Bindings => Entries.Select(entry => new PermissionActionBinding(entry.Key,
        typeof(Permissions).GetMethods(BindingFlags.Public | BindingFlags.Static).Single(method => method.Name == entry.Key).ToString()!)).ToArray();
    // Access/inactive negative scenarios remain available to verification, not as apparent product grants before compound coverage is complete.
    static bool PublicScenario(Scenario scenario) => !scenario.Inactive && !scenario.RestrictedOutsider;
    public static IReadOnlyList<PermissionExampleChoice> RoleChoices => Personas.Select(persona => new PermissionExampleChoice(persona.Key, persona.LabelKey)).ToArray();
    public static IReadOnlyList<PermissionExampleChoice> ScenarioChoices => Scenarios.Where(PublicScenario).Select(scenario => new PermissionExampleChoice(scenario.Key, "adminRole.scenario." + scenario.Key)).ToArray();
    public static IReadOnlyList<PermissionRoleExamples> Read(string roleKey, string scenarioKey) => Personas.Where(persona => persona.Key == roleKey)
        .Select(persona => new PermissionRoleExamples(persona.Key, persona.LabelKey, persona.ScopeKind,
            Entries.SelectMany(entry => Scenarios.Where(scenario => PublicScenario(scenario) && scenario.Key == scenarioKey)
                .Select(scenario => Example(entry, persona, scenario))).ToArray())).ToArray();
    static PermissionExample Example(Binding entry, Persona persona, Scenario scenario) => new(entry.Key, "adminRole.action." + entry.Key, entry.ResultKind,
        scenario.Key, "adminRole.scenario." + scenario.Key, new Case(persona, scenario).Facts, entry.Evaluate(new(persona, scenario)));
    public static PermissionExample Evaluate(string roleKey, string actionKey, string scenarioKey) => Example(Entries.Single(entry => entry.Key == actionKey),
        Personas.Single(persona => persona.Key == roleKey), Scenarios.Single(scenario => scenario.Key == scenarioKey));
    // Strict completion is intentionally blocked until actual compound/transport guard bindings and policy comparison are accepted.
    public static IReadOnlyList<string> CompletionGaps => ["compound-and-transport-guards"];

    // The row adapter is a bounded explanation of synthetic facts, never a live enforcement/impersonation endpoint.
    // Required factors may rule an example out. Passing factors NEVER produce an operation-level "Allowed".
    sealed record OperationDefinition(string Key, string LabelKey, string[] Factors, string Mode = "All",
        string? TargetKind = null, string? Target = null, string? RequiredStatus = null, string NamedActor = "Owner",
        string[]? ExtraRequirements = null, bool? ProjectScope = null, bool LegacyUnavailable = false, bool EntryPrivacy = false, string[]? ExcludedRequirements = null);
    sealed record RowDefinition(string Id, string MatrixRowKey, string[] Requirements, OperationDefinition[] Operations,
        bool ProjectScope = false, bool PolicyPending = false);
    static readonly Membership[] Memberships =
    [
        new("none"), new(ProjectRole.PM, ProjectRole.PM), new(ProjectRole.TeamMember, ProjectRole.TeamMember),
        new(ProjectRole.Reviewer, ProjectRole.Reviewer), new(ProjectRole.Viewer, ProjectRole.Viewer),
        new("DisciplineLead", ProjectRole.TeamMember, Lead: true)
    ];
    public static IReadOnlyList<PermissionExampleChoice> SystemRoleChoices => Personas.Where(persona => persona.ScopeKind == "System")
        .Select(persona => new PermissionExampleChoice(persona.Key, persona.LabelKey)).ToArray();
    public static IReadOnlyList<PermissionMembershipChoice> MembershipChoices => Memberships
        .Select(item => new PermissionMembershipChoice(item.Key, "adminRole.membership." + item.Key)).ToArray();
    // Negative transport examples are safe here because the row decision honours the active/source boundary.
    public static IReadOnlyList<PermissionExampleChoice> RowScenarioChoices => Scenarios.Where(scenario => !scenario.RestrictedOutsider)
        .Select(scenario => new PermissionExampleChoice(scenario.Key, "adminRole.scenario." + scenario.Key)).ToArray();
    public static IReadOnlyList<PermissionOperationChoice> OperationChoices =>
        new[] { new PermissionOperationChoice("all", "", "adminRole.operation.all") }.Concat(Rows.SelectMany(row => row.Operations
            .Select(operation => new PermissionOperationChoice(operation.Key, row.Id, operation.LabelKey)))).ToArray();
    public static IReadOnlyList<PermissionMatrixRowExplanation> ReadRows(string roleKey, string scenarioKey,
        string membershipKey = "none", string operationKey = "all")
    {
        var persona = Personas.Single(item => item.Key == roleKey);
        var scenario = Scenarios.Single(item => item.Key == scenarioKey);
        var membership = Memberships.Single(item => item.Key == membershipKey);
        if (!OperationChoices.Any(item => item.Key == operationKey)) throw new ArgumentException("Unknown operation.", nameof(operationKey));
        var facts = new Case(persona, scenario, membership);
        return Rows.Where(row => operationKey == "all" || row.Operations.Any(operation => operation.Key == operationKey))
            .Select(row => new PermissionMatrixRowExplanation(row.Id, row.MatrixRowKey, "adminRole.row." + row.Id,
                row.Operations.Where(operation => operationKey == "all" || operation.Key == operationKey)
                    .Select(operation => Explain(row, operation, facts)).ToArray())).ToArray();
    }
    public static string LegacyDecision(PermissionExample example) => example.ResultKind == "Condition" || example.Outcome.Allowed
        ? "Conditional" : "NotEligible";

    static PermissionOperationExplanation Explain(RowDefinition row, OperationDefinition operation, Case facts)
    {
        var evaluated = Factors(operation, facts);
        var permissions = evaluated.Where(factor => factor.ResultKind == "CanonicalPermissionFactor").ToArray();
        var factorDenied = operation.Mode switch
        {
            "All" => permissions.Any(factor => !factor.Outcome.Allowed),
            "Any" => permissions.Length > 0 && permissions.All(factor => !factor.Outcome.Allowed),
            _ => false // Context factors have an unmodelled alternative/current-source branch. Never overstate their denial.
        };
        var privacyDenied = operation.EntryPrivacy && evaluated.Any(factor => factor.ActionKey == nameof(Permissions.SeePlanningEntry)
            && factor.Outcome.ReasonCode == "planning.private");
        var workflowDenied = evaluated.Any(factor => factor.ResultKind == "CanonicalWorkflowFactor" && !factor.Outcome.Allowed);
        var projectDenied = (operation.ProjectScope ?? row.ProjectScope) && !Permissions.CanView(facts.Actor, facts.Project);
        var source = operation.TargetKind switch
        {
            "Project" => facts.Project.Status, "Task" => facts.Task.Status,
            "Deliverable" => facts.DeliverableState, "Handoff" => facts.Handoff.Status, _ => null
        };
        var sourceDenied = operation.RequiredStatus is { } required && source != required;
        // These checks reuse the actual shared status helper or facts, not a parallel grant matrix.
        var commentSourceDenied = row.Id == "W30" && ProjectStatus.IsReadOnly(facts.Project.Status);
        var correctionRequested = facts.Scenario.Correction || operation.Key.EndsWith(".correction-read", StringComparison.Ordinal);
        var correctionDenied = correctionRequested && !facts.Actor.Admin
            && (row.Requirements.Contains("correction") || operation.ExtraRequirements?.Contains("correction") == true);
        var notEligible = !facts.Actor.IsActive || factorDenied || privacyDenied || workflowDenied || projectDenied || sourceDenied
            || commentSourceDenied || correctionDenied || operation.LegacyUnavailable;
        var decision = row.PolicyPending ? "PolicyPending" : notEligible ? "NotEligible" : "Conditional";
        var requirements = row.Requirements.Except(operation.ExcludedRequirements ?? [], StringComparer.Ordinal).Concat(operation.ExtraRequirements ?? [])
            .Prepend("identity").Distinct(StringComparer.Ordinal)
            .Select(key => new PermissionRowRequirement("adminRole.requirement." + key,
                key == "identity" && !facts.Actor.IsActive || key == "project-scope" && projectDenied
                    || key == "comment-source" && commentSourceDenied || key == "correction" && correctionDenied
                    || key == "private-source" && privacyDenied
                    || key == "legacy-timeaway" && operation.LegacyUnavailable ? "NotMetByExample" : "RequiresLiveCheck")).ToList();
        requirements.Add(new("adminRole.requirement.canonical-factors",
            operation.Mode == "Context" ? "RequiresLiveCheck" : factorDenied ? "NotMetByExample" : "MetByExample"));
        if (privacyDenied && !row.Requirements.Contains("private-source") && operation.ExtraRequirements?.Contains("private-source") != true)
            requirements.Add(new("adminRole.requirement.private-source", "NotMetByExample"));
        if (workflowDenied) requirements.Add(new("adminRole.requirement.workflow-path", "NotMetByExample"));
        if (sourceDenied) requirements.Add(new("adminRole.requirement.selected-source-state", "NotMetByExample"));
        if (projectDenied && !row.Requirements.Contains("project-scope"))
            requirements.Add(new("adminRole.requirement.project-scope", "NotMetByExample"));
        return new(operation.Key, operation.LabelKey, decision, RowFacts(row, operation, facts), evaluated, requirements.ToArray());
    }


    static IReadOnlyList<string> RowFacts(RowDefinition row, OperationDefinition operation, Case facts)
    {
        // Legacy examples keep their broad declared factor context. Row examples show only pertinent facts.
        var keys = new HashSet<string>(StringComparer.Ordinal)
        {
            "adminRole.fact.syntheticConditions", "adminRole.fact.additionalGuards", "adminRole.fact.active", "adminRole.fact.inactive",
            "adminRole.fact.effectiveReadOnly", "adminRole.fact.effectiveWritable", "adminRole.fact.storedReadOnly", "adminRole.fact.noStoredReadOnly",
            "adminRole.fact.leadsSelectedDiscipline", "adminRole.fact.notSelectedDisciplineLead"
        };
        var project = operation.ProjectScope ?? row.ProjectScope;
        var planning = row.Id[0] == 'P' && row.Id != "P10" || row.Id is "S05" or "S06" or "S07" or "S08" or "S09";
        var planningEntry = row.Id is "S06" or "S07" or "S08" or "S09" or "P03" or "P04" or "P05" or "P06" or "P07";
        var task = row.Id is "W15" or "W19" or "W20" or "W21" or "W22" or "W23" or "W24" or "W25" or "W26" or "W27" or "W28";
        var deliverable = row.Id is "W11" or "W12" or "W13" or "W14";
        var handoff = row.Id is "C01" or "C02" or "C03";
        var personScope = planning || row.Requirements.Contains("staff-scope") || row.Requirements.Contains("people-read")
            || row.Id is "S12" or "W17" or "W18";
        void Pick(params string[] values) { foreach (var value in values) keys.Add("adminRole.fact." + value); }
        if (project || row.Requirements.Contains("project-scope") || operation.TargetKind == "Project")
            Pick("projectMember", "sameDiscipline", "otherDiscipline");
        if (personScope) Pick("directReport", "otherReport", "ownItem", "otherOwner");
        if (planningEntry) Pick("privateDraft", "publishedPlanning", "confirmedSelfPlanning", "planningManagerOwner", "planningGenericOwner",
            "correctionRequested", "normalAccess");
        if (task) Pick("ownItem", "otherOwner", "ownTaskCollaborator", "notTaskCollaborator", "creatorWindow", "noCreatorWindow",
            "namedReviewer", "otherReviewer", "reviewRequired");
        if (deliverable || row.Id[0] == 'C' && row.Id != "C18")
        {
            Pick("sameDiscipline", "otherDiscipline");
            if (operation.NamedActor != "Coordinator") Pick("ownItem", "otherOwner");
            if (operation.NamedActor is not ("Verifier" or "Coordinator")) Pick("namedReviewer", "otherReviewer");
        }
        if (deliverable) Pick("reviewRequired");
        if (handoff) Pick("selfHandoff", "handoffAccept", "handoffSubmit", "selfReviewDisabled");
        if (row.Id == "S17") Pick("templateEditor", "noTemplateEditor");
        if (row.Id is "W16" or "W29" or "W30") Pick("ownItem", "otherOwner");
        if (row.Id == "W29") Pick("viewerComments");
        if (row.Id == "W30") Pick("commentAge", "expiredComment");
        var result = facts.Facts.Where(key => keys.Contains(key)
            || key.StartsWith("adminRole.fact.systemRole.", StringComparison.Ordinal)
            || key.StartsWith("adminRole.fact.membership.", StringComparison.Ordinal)
            || (project || row.Requirements.Contains("project-scope") || operation.TargetKind == "Project")
                && key.StartsWith("adminRole.fact.project.", StringComparison.Ordinal)
            || task && key.StartsWith("adminRole.fact.task.", StringComparison.Ordinal)
            || deliverable && key.StartsWith("adminRole.fact.deliverable.", StringComparison.Ordinal)
            || handoff && key.StartsWith("adminRole.fact.handoff.", StringComparison.Ordinal)).ToList();
        result.AddRange(["adminRole.fact.explicitMembership", "adminRole.fact.noLiveEntity", "adminRole.fact.noLiveDecision"]);
        if (operation.Factors.Contains(nameof(Permissions.NamedCoordinationAction)) || operation.Factors.Contains(nameof(Permissions.CoordinateReview))
            || operation.Factors.Contains(nameof(Permissions.CoordinateSubmission)) || operation.Factors.Contains(nameof(Permissions.SignSubmissionCheck)))
            result.Add(operation.NamedActor switch
            {
                "Reviewer" => "adminRole.fact.selectedNamedReviewer", "Verifier" => "adminRole.fact.selectedNamedVerifier",
                "Coordinator" => "adminRole.fact.selectedPackageCoordinator", _ => "adminRole.fact.selectedNamedOwner"
            });
        if (operation.NamedActor == "Verifier")
            result.Add(facts.Verifier == facts.Actor.Id ? "adminRole.fact.actorIsDesignatedVerifier" : "adminRole.fact.otherDesignatedVerifier");
        if (operation.NamedActor == "Coordinator")
            result.Add(facts.Coordinator == facts.Actor.Id ? "adminRole.fact.actorIsPackageCoordinator" : "adminRole.fact.otherPackageCoordinator");
        if (deliverable && facts.DeliverablePreviousState is { } previous)
            result.Add("adminRole.fact.deliverablePrevious." + previous);
        if (deliverable && facts.DeliverableState == DeliverableStatus.OnHold && facts.Scenario.RecordedHoldReason)
            result.Add("adminRole.fact.recordedHoldReason");
        if (operation.TargetKind == "Deliverable" && operation.Target == DeliverableStatus.OnHold)
            result.Add("adminRole.fact.onHoldRetainsPreviousStatus");
        if (operation.TargetKind is { } kind && operation.Target is { } target)
            result.Add("adminRole.fact.target." + kind + "." + target);
        if (row.Requirements.Contains("baseline"))
            result.Add(facts.Scenario.ClosedItem ? "adminRole.fact.closedItem" : "adminRole.fact.openItem");
        if (row.Id == "S22") result.Add("adminRole.fact.ownPersonalState");
        if (facts.Scenario.Correction || operation.Key.EndsWith(".correction-read", StringComparison.Ordinal))
            if (row.Requirements.Contains("correction") || operation.ExtraRequirements?.Contains("correction") == true)
                result.Add("adminRole.fact.correctionRequested");
        return result.Distinct(StringComparer.Ordinal).ToArray();
    }

    static IReadOnlyList<PermissionRowFactor> Factors(OperationDefinition operation, Case facts)
    {
        var result = new List<PermissionRowFactor>();
        void Add(string key, PermissionExampleOutcome outcome, string kind = "CanonicalPermissionFactor") =>
            result.Add(new(key, key.StartsWith("Workflow.", StringComparison.Ordinal) ? "adminRole.factor." + key : "adminRole.action." + key, kind, outcome));
        foreach (var key in operation.Factors)
        {
            var binding = Entries.Single(item => item.Key == key);
            if (key == nameof(Permissions.ChangeProjectStatus) && operation.TargetKind == "Project")
                Add(key, A(Permissions.ChangeProjectStatus(facts.Actor, facts.Project, operation.Target!)));
            else if (key == nameof(Permissions.TaskTransition) && operation.TargetKind == "Task")
            {
                var path = Workflow.TaskPath(facts.Task.Status, operation.Target!, requiresReview: true);
                Add("Workflow.TaskPath", B(path is not null), "CanonicalWorkflowFactor");
                // A checkbox path is evaluated at every real intermediate source state.
                var from = facts.Task.Status;
                foreach (var next in path ?? new[] { operation.Target! })
                {
                    Add(key, A(Permissions.TaskTransition(facts.Actor, facts.Project, facts.Task with { Status = from }, from, next)));
                    from = next;
                }
            }
            else if (key == nameof(Permissions.DeliverableTransition) && operation.TargetKind == "Deliverable")
            {
                var issuing = operation.Target == DeliverableStatus.Issued;
                var manager = Permissions.IsPM(facts.Actor, facts.Project) || Permissions.IsDL(facts.Project, facts.Discipline);
                Add(issuing ? "Workflow.DeliverableIssueAllowedFrom" : "Workflow.DeliverableStep",
                    B(issuing ? Workflow.DeliverableIssueAllowedFrom(facts.DeliverableState, manager, requiresReview: true)
                        : Workflow.DeliverableStep(facts.DeliverableState, operation.Target!, requiresReview: true, previous: facts.DeliverablePreviousState)),
                    "CanonicalWorkflowFactor");
                var outcome = A(Permissions.DeliverableTransition(facts.Actor, facts.Project, facts.Deliverable,
                    issuing ? DeliverableStatus.ReadyToIssue : facts.DeliverableState, operation.Target!));
                if (operation.Target == DeliverableStatus.OnHold)
                    outcome = outcome with { RequiresReason = Workflow.DeliverableNeedsReason(operation.Target!) };
                Add(key, outcome);
            }
            else if (key == nameof(Permissions.HandoffTransition) && operation.TargetKind == "Handoff")
                Add(key, A(Permissions.HandoffTransition(facts.Actor, facts.Project, facts.Handoff, operation.Target!, allowSelfReview: false)));
            else if (key == nameof(Permissions.ManageCoordination) && (operation.Key is "C16.record-not-met" or "C16.snapshot"))
                Add(key, A(Permissions.ManageCoordination(facts.Actor, facts.Project, Guid.Empty)));
            else if (key == nameof(Permissions.CoordinateReview) && operation.NamedActor == "Coordinator")
                Add(key, A(Permissions.CoordinateReview(facts.Actor, facts.Project, facts.Discipline, facts.Coordinator)));
            else if (key == nameof(Permissions.CoordinateSubmission) && operation.NamedActor == "Coordinator")
                Add(key, A(Permissions.CoordinateSubmission(facts.Actor, facts.Project, facts.Coordinator)));
            else if (key == nameof(Permissions.Writable) && operation.Key == "W32.reopen")
                Add(key, A(Permissions.Writable(facts.Actor, facts.Project, pmOnly: true)));
            else if (key == nameof(Permissions.NamedCoordinationAction))
                Add(key, A(Permissions.NamedCoordinationAction(facts.Actor, facts.Project,
                    operation.NamedActor switch
                    {
                        "Reviewer" => facts.Reviewer, "Verifier" => facts.Verifier, "Coordinator" => facts.Coordinator, _ => facts.Owner
                    })));
            else if (key == nameof(Permissions.SeePlanningEntry))
                Add(key, A(Permissions.SeePlanningEntry(facts.Actor, facts.Planning,
                    facts.Actor.Admin && (facts.Scenario.Correction || operation.Key.EndsWith(".correction-read", StringComparison.Ordinal)))));
            else
                Add(key, binding.Evaluate(facts), binding.ResultKind == "Condition" ? "CanonicalConditionFactor" : "CanonicalPermissionFactor");
        }
        if (operation.TargetKind == "Project")
            Add("Workflow.ProjectAllowed", B(Workflow.ProjectAllowed(facts.Project.Status, operation.Target!)), "CanonicalWorkflowFactor");
        return result.ToArray();
    }

    // Exact83 matrix keys are maintained compile-time metadata. No manifest or copied grant table is read at runtime.
    static readonly RowDefinition[] Rows =
    [
        new("S01", "8.5.1:Sign in and use the application", ["identity"],
        [
            new("S01.sign-in", "adminRole.operation.S01.sign-in", [], Mode: "Context"),
        ], ProjectScope: false),
        new("S02", "8.5.1:View project list and any project (subject to 8.7)", ["project-scope", "list-parity"],
        [
            new("S02.operation-1", "adminRole.operation.S02.operation-1", [nameof(Permissions.CanView)], Mode: "Context"),
            new("S02.operation-2", "adminRole.operation.S02.operation-2", [nameof(Permissions.CanView)]),
        ], ProjectScope: false),
        new("S03", "8.5.1:Create a project", ["new-project", "replay"],
        [
            new("S03.operation-1", "adminRole.operation.S03.operation-1", [nameof(Permissions.CreateProject)]),
        ], ProjectScope: false),
        new("S04", "8.5.1:View Portfolio Dashboard", ["portfolio-scope", "list-parity"],
        [
            new("S04.operation-1", "adminRole.operation.S04.operation-1", [nameof(Permissions.ViewPortfolio)]),
            new("S04.operation-2", "adminRole.operation.S04.operation-2", [nameof(Permissions.ViewPortfolio)]),
        ], ProjectScope: false),
        new("S05", "8.5.1:View Weekly Resource Planner (published and confirmed allocations)", ["people-read", "source-kinds", "private-source", "list-parity"],
        [
            new("S05.view-planner", "adminRole.operation.S05.view-planner", [nameof(Permissions.ViewWorkload)], Mode: "Context"),
            new("S05.view-approved", "adminRole.operation.S05.view-approved", [nameof(Permissions.SeePlanningEntry)], Mode: "Context"),
        ], ProjectScope: false),
        new("S06", "8.5.1:View Private Draft allocations", ["private-source", "correction", "list-parity"],
        [
            new("S06.view-draft", "adminRole.operation.S06.view-draft", [nameof(Permissions.SeePlanningEntry)], Mode: "Context", EntryPrivacy: true),
            new("S06.correction-read", "adminRole.operation.S06.correction-read", [nameof(Permissions.Administer), nameof(Permissions.SeePlanningEntry)], ExtraRequirements: ["correction"], EntryPrivacy: true),
        ], ProjectScope: false),
        new("S07", "8.5.1:Create / edit / delete manager allocations for a person", ["planning-owner", "entry-source", "version", "replay"],
        [
            new("S07.create", "adminRole.operation.S07.create", [nameof(Permissions.CreatePlanningEntry)]),
            new("S07.edit", "adminRole.operation.S07.edit", [nameof(Permissions.ManagePlanningEntry)]),
            new("S07.delete", "adminRole.operation.S07.delete", [nameof(Permissions.ManagePlanningEntry)]),
        ], ProjectScope: false),
        new("S08", "8.5.1:Publish / confirm / unpublish a manager allocation", ["planning-owner", "entry-transition", "version"],
        [
            new("S08.publish-entry", "adminRole.operation.S08.publish-entry", [nameof(Permissions.ManagePlanningEntry)]),
            new("S08.confirm-allocation", "adminRole.operation.S08.confirm-allocation", [nameof(Permissions.ConfirmAllocation)]),
            new("S08.unpublish-entry", "adminRole.operation.S08.unpublish-entry", [nameof(Permissions.ManagePlanningEntry)]),
        ], ProjectScope: false),
        new("S09", "8.5.1:Create / edit / delete own employee allocations", ["self-entry", "entry-source", "version", "replay"],
        [
            new("S09.create", "adminRole.operation.S09.create", [nameof(Permissions.CreatePlanningEntry)]),
            new("S09.edit", "adminRole.operation.S09.edit", [nameof(Permissions.ManagePlanningEntry)]),
            new("S09.delete", "adminRole.operation.S09.delete", [nameof(Permissions.ManagePlanningEntry)]),
        ], ProjectScope: false),
        new("S10", "8.5.1:Record / correct a manual copy of HR-approved leave (with HR reference)", ["hr-source", "staff-scope", "version", "replay"],
        [
            new("S10.manual", "adminRole.operation.S10.manual", [nameof(Permissions.ActOnStaff)]),
            new("S10.correct", "adminRole.operation.S10.correct", [nameof(Permissions.ActOnStaff)]),
            new("S10.reconcile", "adminRole.operation.S10.reconcile", [nameof(Permissions.ActOnStaff)]),
        ], ProjectScope: false),
        new("S11", "8.5.1:Preview / apply HR-approved leave CSV; configure office holidays", ["hr-import", "holidays", "replay"],
        [
            new("S11.preview", "adminRole.operation.S11.preview", [nameof(Permissions.Administer)]),
            new("S11.apply", "adminRole.operation.S11.apply", [nameof(Permissions.Administer)]),
            new("S11.holidays", "adminRole.operation.S11.holidays", [nameof(Permissions.Administer)]),
        ], ProjectScope: false),
        new("S12", "8.5.1:View Vacation & Coverage availability", ["availability-scope", "private-fields", "project-scope"],
        [
            new("S12.availability", "adminRole.operation.S12.availability", [], Mode: "Context"),
            new("S12.coverage", "adminRole.operation.S12.coverage", [], Mode: "Context"),
        ], ProjectScope: false),
        new("S13", "8.5.1:Set a person's weekly capacity and working days", ["staff-scope", "capacity-schedule", "version"],
        [
            new("S13.capacity", "adminRole.operation.S13.capacity", [nameof(Permissions.ActOnStaff)]),
            new("S13.schedule", "adminRole.operation.S13.schedule", [nameof(Permissions.ActOnStaff)]),
        ], ProjectScope: false),
        new("S14", "8.5.1:Reassign tasks across projects for supervised staff", ["staff-scope", "task-target", "project-scope", "atomic"],
        [
            new("S14.operation-1", "adminRole.operation.S14.operation-1", [nameof(Permissions.ActOnStaff), nameof(Permissions.AssignTask)]),
        ], ProjectScope: false),
        new("S15", "8.5.1:Add or remove supervised staff on a project team (as Team Member)", ["staff-scope", "team-role", "version"],
        [
            new("S15.add-report", "adminRole.operation.S15.add-report", [nameof(Permissions.ManageTeam), nameof(Permissions.StaffOnProject)], Mode: "Any"),
            new("S15.remove-report", "adminRole.operation.S15.remove-report", [nameof(Permissions.ManageTeam), nameof(Permissions.StaffOnProject)], Mode: "Any"),
        ], ProjectScope: false),
        new("S16", "8.5.1:Manage users, system roles, offices, disciplines, clients, deliverable types, phases", ["admin-child", "active-reference", "version"],
        [
            new("S16.users", "adminRole.operation.S16.users", [nameof(Permissions.Administer)]),
            new("S16.system-roles", "adminRole.operation.S16.system-roles", [nameof(Permissions.Administer)]),
            new("S16.offices", "adminRole.operation.S16.offices", [nameof(Permissions.Administer)]),
            new("S16.disciplines", "adminRole.operation.S16.disciplines", [nameof(Permissions.Administer)]),
            new("S16.clients", "adminRole.operation.S16.clients", [nameof(Permissions.Administer)]),
            new("S16.deliverable-types", "adminRole.operation.S16.deliverable-types", [nameof(Permissions.Administer)]),
            new("S16.phases", "adminRole.operation.S16.phases", [nameof(Permissions.Administer)]),
        ], ProjectScope: false),
        new("S17", "8.5.1:Manage project templates", ["template", "version", "replay"],
        [
            new("S17.create", "adminRole.operation.S17.create", [nameof(Permissions.ManageTemplates)]),
            new("S17.edit", "adminRole.operation.S17.edit", [nameof(Permissions.ManageTemplates)]),
            new("S17.publish", "adminRole.operation.S17.publish", [nameof(Permissions.ManageTemplates)]),
            new("S17.new-draft", "adminRole.operation.S17.new-draft", [nameof(Permissions.ManageTemplates)]),
            new("S17.retire", "adminRole.operation.S17.retire", [nameof(Permissions.ManageTemplates)]),
            new("S17.discard", "adminRole.operation.S17.discard", [nameof(Permissions.ManageTemplates)]),
        ], ProjectScope: false),
        new("S18", "8.5.1:Manage organisation thresholds and notification defaults", ["org-settings"],
        [
            new("S18.thresholds", "adminRole.operation.S18.thresholds", [nameof(Permissions.Administer)]),
            new("S18.notification-defaults", "adminRole.operation.S18.notification-defaults", [nameof(Permissions.Administer)]),
        ], ProjectScope: false),
        new("S19", "8.5.1:Unarchive a project", ["project-transition", "reason", "version"],
        [
            new("S19.unarchive", "adminRole.operation.S19.unarchive", [nameof(Permissions.ChangeProjectStatus)], TargetKind: "Project", Target: ProjectStatus.Complete, RequiredStatus: ProjectStatus.Archived),
        ], ProjectScope: false),
        new("S20", "8.5.1:View all activity history", ["activity-fields", "private-fields", "list-parity"],
        [
            new("S20.project-history", "adminRole.operation.S20.project-history", [], Mode: "Context", ProjectScope: true),
            new("S20.audit", "adminRole.operation.S20.audit", [nameof(Permissions.Administer)]),
            new("S20.export", "adminRole.operation.S20.export", [], Mode: "Context", ProjectScope: true),
        ], ProjectScope: false),
        new("S21", "8.5.1:Export reports", ["report-policy", "report-definition", "export-parity"],
        [
            new("S21.export", "adminRole.operation.S21.export", [], Mode: "Context"),
        ], ProjectScope: false, PolicyPending: true),
        new("S22", "8.5.1:Manage own notification preferences, project follow levels, and saved views", ["personal-owner", "personal-flags", "view", "replay"],
        [
            new("S22.notification-preferences", "adminRole.operation.S22.notification-preferences", [], Mode: "Context"),
            new("S22.follow", "adminRole.operation.S22.follow", [], Mode: "Context"),
            new("S22.personal-view", "adminRole.operation.S22.personal-view", [], Mode: "Context"),
            new("S22.shared-view", "adminRole.operation.S22.shared-view", [nameof(Permissions.ManageSavedProjectView)], ProjectScope: true),
        ], ProjectScope: false),
        new("W01", "8.5.2:View project, dashboard, all registers", ["project-scope", "source-kinds", "list-parity"],
        [
            new("W01.operation-1", "adminRole.operation.W01.operation-1", [nameof(Permissions.CanView)]),
            new("W01.operation-2", "adminRole.operation.W01.operation-2", [nameof(Permissions.CanView)]),
            new("W01.operation-3", "adminRole.operation.W01.operation-3", [nameof(Permissions.CanView)]),
        ], ProjectScope: true),
        new("W02", "8.5.2:Edit project information (name, client, description, dates, phase, links)", ["project-fields", "reason", "version"],
        [
            new("W02.operation-1", "adminRole.operation.W02.operation-1", [nameof(Permissions.EditProject)]),
        ], ProjectScope: true),
        new("W03", "8.5.2:Change project status (Active, On Hold, Complete, Cancelled)", ["project-transition", "child-records", "reason", "version"],
        [
            new("W03.active", "adminRole.operation.W03.active", [nameof(Permissions.ChangeProjectStatus)], TargetKind: "Project", Target: ProjectStatus.Active),
            new("W03.onhold", "adminRole.operation.W03.onhold", [nameof(Permissions.ChangeProjectStatus)], TargetKind: "Project", Target: ProjectStatus.OnHold),
            new("W03.complete", "adminRole.operation.W03.complete", [nameof(Permissions.ChangeProjectStatus)], TargetKind: "Project", Target: ProjectStatus.Complete),
            new("W03.cancelled", "adminRole.operation.W03.cancelled", [nameof(Permissions.ChangeProjectStatus)], TargetKind: "Project", Target: ProjectStatus.Cancelled),
        ], ProjectScope: true),
        new("W04", "8.5.2:Archive project", ["project-transition", "version"],
        [
            new("W04.archive", "adminRole.operation.W04.archive", [nameof(Permissions.ChangeProjectStatus)], TargetKind: "Project", Target: ProjectStatus.Archived, RequiredStatus: ProjectStatus.Complete),
        ], ProjectScope: true),
        new("W05", "8.5.2:Manage project team and roles (Supervisors may also add or remove their direct reports as Team Members, §8.5.1)", ["team-role", "active-reference", "version"],
        [
            new("W05.add-member", "adminRole.operation.W05.add-member", [nameof(Permissions.ManageTeam), nameof(Permissions.StaffOnProject)], Mode: "Any"),
            new("W05.remove-member", "adminRole.operation.W05.remove-member", [nameof(Permissions.ManageTeam), nameof(Permissions.StaffOnProject)], Mode: "Any"),
            new("W05.edit-role", "adminRole.operation.W05.edit-role", [nameof(Permissions.ManageTeam)]),
        ], ProjectScope: true),
        new("W06", "8.5.2:Add/remove disciplines, set Discipline Lead", ["discipline-reference", "child-records", "version"],
        [
            new("W06.add-discipline", "adminRole.operation.W06.add-discipline", [nameof(Permissions.ManageTeam)]),
            new("W06.remove-discipline", "adminRole.operation.W06.remove-discipline", [nameof(Permissions.ManageTeam)]),
            new("W06.discipline-lead", "adminRole.operation.W06.discipline-lead", [nameof(Permissions.ManageTeam)]),
        ], ProjectScope: true),
        new("W07", "8.5.2:Set/clear health override", ["health-override", "version"],
        [
            new("W07.set", "adminRole.operation.W07.set", [nameof(Permissions.HealthOverride)]),
            new("W07.clear", "adminRole.operation.W07.clear", [nameof(Permissions.HealthOverride)]),
        ], ProjectScope: true),
        new("W08", "8.5.2:Create / edit / delete milestone; change milestone date", ["milestone", "child-records", "reason", "version"],
        [
            new("W08.create", "adminRole.operation.W08.create", [nameof(Permissions.ManageMilestones)]),
            new("W08.edit", "adminRole.operation.W08.edit", [nameof(Permissions.ManageMilestones)]),
            new("W08.delete", "adminRole.operation.W08.delete", [nameof(Permissions.ManageMilestones)]),
            new("W08.change-date", "adminRole.operation.W08.change-date", [nameof(Permissions.ManageMilestones)]),
            new("W08.cancel", "adminRole.operation.W08.cancel", [nameof(Permissions.ManageMilestones)], ExtraRequirements: ["milestone-cancel"]),
        ], ProjectScope: true),
        new("W09", "8.5.2:Re-baseline a milestone or deliverable, or return it to its original dates, with a reason (§50.5)", ["baseline", "reason", "version"],
        [
            new("W09.milestone-set", "adminRole.operation.W09.milestone-set", [nameof(Permissions.Rebaseline)]),
            new("W09.milestone-clear", "adminRole.operation.W09.milestone-clear", [nameof(Permissions.Rebaseline)]),
            new("W09.deliverable-set", "adminRole.operation.W09.deliverable-set", [nameof(Permissions.Rebaseline)]),
            new("W09.deliverable-clear", "adminRole.operation.W09.deliverable-clear", [nameof(Permissions.Rebaseline)]),
        ], ProjectScope: true),
        new("W10", "8.5.2:Mark milestone Complete", ["milestone-complete", "version"],
        [
            new("W10.operation-1", "adminRole.operation.W10.operation-1", [nameof(Permissions.ManageMilestones)]),
            new("W10.reopen", "adminRole.operation.W10.reopen", [nameof(Permissions.ManageMilestones)], ExtraRequirements: ["milestone-reopen", "reason"]),
        ], ProjectScope: true),
        new("W11", "8.5.2:Create deliverable", ["deliverable-fields", "active-reference"],
        [
            new("W11.operation-1", "adminRole.operation.W11.operation-1", [nameof(Permissions.CreateDeliverable)]),
        ], ProjectScope: true),
        new("W12", "8.5.2:Edit deliverable (fields, owner, dates)", ["deliverable-fields", "reason", "version"],
        [
            new("W12.operation-1", "adminRole.operation.W12.operation-1", [nameof(Permissions.EditDeliverable)]),
            new("W12.operation-2", "adminRole.operation.W12.operation-2", [nameof(Permissions.EditDeliverable)]),
            new("W12.operation-3", "adminRole.operation.W12.operation-3", [nameof(Permissions.EditDeliverable)]),
            new("W12.operation-4", "adminRole.operation.W12.operation-4", [nameof(Permissions.EditDeliverable)]),
        ], ProjectScope: true),
        new("W13", "8.5.2:Change deliverable status", ["deliverable-transition", "review-source", "version"],
        [
            new("W13.inprogress", "adminRole.operation.W13.inprogress", [nameof(Permissions.DeliverableTransition)], TargetKind: "Deliverable", Target: DeliverableStatus.InProgress),
            new("W13.inreview", "adminRole.operation.W13.inreview", [nameof(Permissions.DeliverableTransition)], TargetKind: "Deliverable", Target: DeliverableStatus.InReview),
            new("W13.revisionrequired", "adminRole.operation.W13.revisionrequired", [nameof(Permissions.DeliverableTransition)], TargetKind: "Deliverable", Target: DeliverableStatus.RevisionRequired),
            new("W13.readytoissue", "adminRole.operation.W13.readytoissue", [nameof(Permissions.DeliverableTransition)], TargetKind: "Deliverable", Target: DeliverableStatus.ReadyToIssue),
            new("W13.issued", "adminRole.operation.W13.issued", [nameof(Permissions.DeliverableTransition)], TargetKind: "Deliverable", Target: DeliverableStatus.Issued),
            new("W13.accepted", "adminRole.operation.W13.accepted", [nameof(Permissions.DeliverableTransition)], TargetKind: "Deliverable", Target: DeliverableStatus.Accepted),
            new("W13.onhold", "adminRole.operation.W13.onhold", [nameof(Permissions.DeliverableTransition)], TargetKind: "Deliverable",
                Target: DeliverableStatus.OnHold, ExtraRequirements: ["deliverable-on-hold", "reason"]),
        ], ProjectScope: true),
        new("W14", "8.5.2:Delete / cancel deliverable", ["deliverable-delete", "child-records", "reason", "version"],
        [
            new("W14.delete", "adminRole.operation.W14.delete", [nameof(Permissions.DeleteDeliverable)]),
            new("W14.cancel", "adminRole.operation.W14.cancel", [nameof(Permissions.DeliverableTransition)], TargetKind: "Deliverable", Target: DeliverableStatus.Cancelled),
        ], ProjectScope: true),
        new("W15", "8.5.2:Create task", ["task-target", "active-reference"],
        [
            new("W15.operation-1", "adminRole.operation.W15.operation-1", [nameof(Permissions.CreateTask)]),
        ], ProjectScope: true),
        new("W16", "8.5.2:Create / edit / cancel calendar event tied to project", ["calendar-private", "event-source", "version"],
        [
            new("W16.create", "adminRole.operation.W16.create", [nameof(Permissions.CreateProjectEvent)]),
            new("W16.edit", "adminRole.operation.W16.edit", [nameof(Permissions.EditEvent)]),
            new("W16.cancel", "adminRole.operation.W16.cancel", [nameof(Permissions.EditEvent)]),
        ], ProjectScope: true),
        new("W17", "8.5.2:View task-hour entries", ["time-scope", "private-fields", "export-parity"],
        [
            new("W17.operation-1", "adminRole.operation.W17.operation-1", [nameof(Permissions.ViewTimeEntry)]),
            new("W17.operation-2", "adminRole.operation.W17.operation-2", [nameof(Permissions.ViewTimeEntry)]),
            new("W17.operation-3", "adminRole.operation.W17.operation-3", [nameof(Permissions.ViewTimeEntry)]),
        ], ProjectScope: true),
        new("W18", "8.5.2:Add / edit / delete task-hour entries", ["time-owner", "time-total", "reason", "version"],
        [
            new("W18.add", "adminRole.operation.W18.add", [nameof(Permissions.EnterTime)]),
            new("W18.edit", "adminRole.operation.W18.edit", [nameof(Permissions.EditTime)]),
            new("W18.delete", "adminRole.operation.W18.delete", [nameof(Permissions.EditTime)]),
            new("W18.correct", "adminRole.operation.W18.correct", [nameof(Permissions.EditTime)]),
        ], ProjectScope: true),
        new("W19", "8.5.2:Edit task fields", ["task-facts", "creator-window", "version"],
        [
            new("W19.operation-1", "adminRole.operation.W19.operation-1", [nameof(Permissions.EditTask)]),
        ], ProjectScope: true),
        new("W20", "8.5.2:Assign / reassign task", ["task-target", "team-role", "version", "atomic"],
        [
            new("W20.operation-1", "adminRole.operation.W20.operation-1", [nameof(Permissions.AssignTask)]),
            new("W20.operation-2", "adminRole.operation.W20.operation-2", [nameof(Permissions.AssignTask)]),
            new("W20.operation-3", "adminRole.operation.W20.operation-3", [nameof(Permissions.AssignTask)]),
        ], ProjectScope: true),
        new("W21", "8.5.2:Change task status", ["task-path", "task-facts", "version"],
        [
            new("W21.inprogress", "adminRole.operation.W21.inprogress", [nameof(Permissions.TaskTransition)], TargetKind: "Task", Target: TaskStatuses.InProgress),
            new("W21.readyforreview", "adminRole.operation.W21.readyforreview", [nameof(Permissions.TaskTransition)], TargetKind: "Task", Target: TaskStatuses.ReadyForReview),
            new("W21.inreview", "adminRole.operation.W21.inreview", [nameof(Permissions.TaskTransition)], TargetKind: "Task", Target: TaskStatuses.InReview),
            new("W21.revisionrequired", "adminRole.operation.W21.revisionrequired", [nameof(Permissions.TaskTransition)], TargetKind: "Task", Target: TaskStatuses.RevisionRequired),
            new("W21.complete", "adminRole.operation.W21.complete", [nameof(Permissions.TaskTransition)], TargetKind: "Task", Target: TaskStatuses.Complete),
            new("W21.onhold", "adminRole.operation.W21.onhold", [nameof(Permissions.TaskTransition)], TargetKind: "Task", Target: TaskStatuses.OnHold),
        ], ProjectScope: true),
        new("W22", "8.5.2:Set reviewer on a task", ["task-reviewer", "version"],
        [
            new("W22.operation-1", "adminRole.operation.W22.operation-1", [nameof(Permissions.SetReviewer)]),
        ], ProjectScope: true),
        new("W23", "8.5.2:Add / remove task dependency", ["dependency", "version"],
        [
            new("W23.add", "adminRole.operation.W23.add", [nameof(Permissions.ManageDependency)]),
            new("W23.remove", "adminRole.operation.W23.remove", [nameof(Permissions.ManageDependency)]),
        ], ProjectScope: true),
        new("W24", "8.5.2:Set manual block / clear manual block", ["manual-block", "reason", "version"],
        [
            new("W24.set", "adminRole.operation.W24.set", [nameof(Permissions.ManualBlock)]),
            new("W24.clear", "adminRole.operation.W24.clear", [nameof(Permissions.ManualBlock)]),
        ], ProjectScope: true),
        new("W25", "8.5.2:Change task due date", ["task-due", "reason", "version"],
        [
            new("W25.operation-1", "adminRole.operation.W25.operation-1", [nameof(Permissions.ChangeDueDate), nameof(Permissions.DueChangeNeedsReason)]),
        ], ProjectScope: true),
        new("W26", "8.5.2:Delete task", ["task-delete", "child-records", "version"],
        [
            new("W26.operation-1", "adminRole.operation.W26.operation-1", [nameof(Permissions.DeleteTask)]),
        ], ProjectScope: true),
        new("W27", "8.5.2:Cancel task", ["task-path", "reason", "version"],
        [
            new("W27.cancel", "adminRole.operation.W27.cancel", [nameof(Permissions.TaskTransition)], TargetKind: "Task", Target: TaskStatuses.Cancelled),
        ], ProjectScope: true),
        new("W28", "8.5.2:Reopen completed task", ["task-path", "reason", "version"],
        [
            new("W28.reopen", "adminRole.operation.W28.reopen", [nameof(Permissions.TaskTransition)], TargetKind: "Task", Target: TaskStatuses.InProgress, RequiredStatus: TaskStatuses.Complete),
        ], ProjectScope: true),
        new("W29", "8.5.2:Comment, @mention, add document link", ["comment-target", "viewer-comments", "document-link"],
        [
            new("W29.comment", "adminRole.operation.W29.comment", [nameof(Permissions.Comment)]),
            new("W29.mention", "adminRole.operation.W29.mention", [nameof(Permissions.Comment)]),
            new("W29.project-link", "adminRole.operation.W29.project-link", [nameof(Permissions.EditProject)]),
            new("W29.item-link", "adminRole.operation.W29.item-link", [nameof(Permissions.AddLink)]),
        ], ProjectScope: true),
        new("W30", "8.5.2:Edit / delete own comment (within rules, Section 12.8)", ["comment-window", "comment-source"],
        [
            new("W30.edit", "adminRole.operation.W30.edit", [nameof(Permissions.EditComment)]),
            new("W30.delete", "adminRole.operation.W30.delete", [nameof(Permissions.DeleteComment)]),
        ], ProjectScope: true),
        new("W31", "8.5.2:Create / edit decision, risk, issue, meeting, action", ["register-family", "active-reference", "reason", "version"],
        [
            new("W31.decision-create", "adminRole.operation.W31.decision-create", [nameof(Permissions.RaiseRegisterItem)]),
            new("W31.decision-edit", "adminRole.operation.W31.decision-edit", [nameof(Permissions.EditRegisterItem)]),
            new("W31.decision-cancel", "adminRole.operation.W31.decision-cancel", [nameof(Permissions.DecideOrDefer)]),
            new("W31.risk-create", "adminRole.operation.W31.risk-create", [nameof(Permissions.RaiseRegisterItem)]),
            new("W31.risk-edit", "adminRole.operation.W31.risk-edit", [nameof(Permissions.EditRegisterItem)]),
            new("W31.risk-transition", "adminRole.operation.W31.risk-transition", [nameof(Permissions.EditRegisterItem)], ExtraRequirements: ["risk-transition"]),
            new("W31.issue-create", "adminRole.operation.W31.issue-create", [nameof(Permissions.RaiseRegisterItem)]),
            new("W31.issue-edit", "adminRole.operation.W31.issue-edit", [nameof(Permissions.EditRegisterItem)]),
            new("W31.issue-cancel", "adminRole.operation.W31.issue-cancel", [nameof(Permissions.EditRegisterItem)]),
            new("W31.meeting-create", "adminRole.operation.W31.meeting-create", [nameof(Permissions.RunCoordination)]),
            new("W31.meeting-edit", "adminRole.operation.W31.meeting-edit", [nameof(Permissions.RunCoordination)]),
            new("W31.meeting-current", "adminRole.operation.W31.meeting-current", [nameof(Permissions.RunCoordination)]),
            new("W31.action-create", "adminRole.operation.W31.action-create", [nameof(Permissions.RaiseRegisterItem)]),
            new("W31.action-edit", "adminRole.operation.W31.action-edit", [nameof(Permissions.EditRegisterItem)]),
            new("W31.action-cancel", "adminRole.operation.W31.action-cancel", [nameof(Permissions.EditRegisterItem)]),
        ], ProjectScope: true),
        new("W32", "8.5.2:Record a decision outcome", ["decision-outcome", "reason", "version"],
        [
            new("W32.record", "adminRole.operation.W32.record", [nameof(Permissions.DecideOrDefer)]),
            new("W32.defer", "adminRole.operation.W32.defer", [nameof(Permissions.DecideOrDefer)]),
            new("W32.reopen", "adminRole.operation.W32.reopen", [nameof(Permissions.Writable)], ExtraRequirements: ["decision-reopen"]),
        ], ProjectScope: true),
        new("W33", "8.5.2:Run Weekly Coordination in meeting mode (record \"reviewed\" timestamp)", ["coordination-reviewed"],
        [
            new("W33.operation-1", "adminRole.operation.W33.operation-1", [nameof(Permissions.RunCoordination)]),
        ], ProjectScope: true),
        new("C01", "8.10:Create/assign handoff", ["handoff-source", "handoff-owners", "version", "reason"],
        [
            new("C01.create", "adminRole.operation.C01.create", [nameof(Permissions.CreateHandoff)]),
            new("C01.edit", "adminRole.operation.C01.edit", [nameof(Permissions.EditHandoff)]),
            new("C01.assign", "adminRole.operation.C01.assign", [nameof(Permissions.AssignHandoff)]),
        ], ProjectScope: true),
        new("C02", "8.10:Submit/respond to handoff", ["handoff-source", "handoff-signature", "version", "reason"],
        [
            new("C02.submit", "adminRole.operation.C02.submit", [nameof(Permissions.HandoffTransition)], TargetKind: "Handoff", Target: HandoffStatus.Submitted),
            new("C02.respond", "adminRole.operation.C02.respond", [nameof(Permissions.HandoffTransition)], TargetKind: "Handoff", Target: HandoffStatus.Returned),
            new("C02.cancel", "adminRole.operation.C02.cancel", [nameof(Permissions.HandoffTransition)], TargetKind: "Handoff", Target: HandoffStatus.Cancelled),
        ], ProjectScope: true),
        new("C03", "8.10:Accept/incorporate handoff", ["handoff-signature", "independent-source", "source-kinds", "version"],
        [
            new("C03.accept", "adminRole.operation.C03.accept", [nameof(Permissions.HandoffTransition)], TargetKind: "Handoff", Target: HandoffStatus.Accepted),
            new("C03.incorporate", "adminRole.operation.C03.incorporate", [nameof(Permissions.HandoffTransition)], TargetKind: "Handoff", Target: HandoffStatus.Incorporated),
        ], ProjectScope: true),
        new("C04", "8.10:Coordinate review package", ["review-round", "review-source", "independent-source", "version"],
        [
            new("C04.create", "adminRole.operation.C04.create", [nameof(Permissions.ManageCoordination)]),
            new("C04.new-round", "adminRole.operation.C04.new-round", [nameof(Permissions.CoordinateReview)], NamedActor: "Coordinator"),
            new("C04.start", "adminRole.operation.C04.start", [nameof(Permissions.CoordinateReview)], NamedActor: "Coordinator"),
            new("C04.assign-coordinator", "adminRole.operation.C04.assign-coordinator", [nameof(Permissions.ManageCoordination)]),
            new("C04.assign-reviewer", "adminRole.operation.C04.assign-reviewer", [nameof(Permissions.CoordinateReview)], NamedActor: "Coordinator"),
            new("C04.cancel", "adminRole.operation.C04.cancel", [nameof(Permissions.ManageCoordination)]),
        ], ProjectScope: true),
        new("C05", "8.10:Technical review approval", ["review-signature", "independent-source", "review-source", "version"],
        [
            new("C05.decide", "adminRole.operation.C05.decide", [nameof(Permissions.NamedCoordinationAction)], NamedActor: "Reviewer"),
            new("C05.pre-issue-gate", "adminRole.operation.C05.pre-issue-gate", [], Mode: "Context"),
        ], ProjectScope: true),
        new("C06", "8.10:Respond to/verify review finding", ["finding-source", "finding-signature", "independent-source", "version", "reason", "replay"],
        [
            new("C06.add-internal", "adminRole.operation.C06.add-internal", [nameof(Permissions.CoordinationWrite)]),
            new("C06.record-incoming", "adminRole.operation.C06.record-incoming", [nameof(Permissions.RecordExternalReview)]),
            new("C06.respond", "adminRole.operation.C06.respond", [nameof(Permissions.NamedCoordinationAction)]),
            new("C06.assign-resolver", "adminRole.operation.C06.assign-resolver", [nameof(Permissions.CoordinateReview)], NamedActor: "Coordinator"),
            new("C06.assign-verifier", "adminRole.operation.C06.assign-verifier", [nameof(Permissions.ManageTeam)]),
            new("C06.verify", "adminRole.operation.C06.verify", [nameof(Permissions.NamedCoordinationAction)], NamedActor: "Verifier"),
            new("C06.return", "adminRole.operation.C06.return", [nameof(Permissions.NamedCoordinationAction)], NamedActor: "Verifier"),
            new("C06.withdraw", "adminRole.operation.C06.withdraw", [nameof(Permissions.NamedCoordinationAction)], NamedActor: "Verifier"),
            new("C06.acknowledge-withdrawal", "adminRole.operation.C06.acknowledge-withdrawal", [nameof(Permissions.NamedCoordinationAction)],
                NamedActor: "Coordinator", ExtraRequirements: ["withdrawal-acknowledgement"], ExcludedRequirements: ["independent-source"]),
        ], ProjectScope: true),
        new("C07", "8.10:Publish source revision/change", ["published-source", "active-reference", "version"],
        [
            new("C07.register-source", "adminRole.operation.C07.register-source", [nameof(Permissions.PublishSource)]),
            new("C07.external-source", "adminRole.operation.C07.external-source", [nameof(Permissions.ManageCoordination)]),
            new("C07.publish", "adminRole.operation.C07.publish", [nameof(Permissions.PublishSource)]),
            new("C07.close-notice", "adminRole.operation.C07.close-notice", [nameof(Permissions.NamedCoordinationAction)], ExtraRequirements: ["change-notice-close", "reason"]),
            new("C07.assign-notice", "adminRole.operation.C07.assign-notice", [nameof(Permissions.ManageCoordination)], ExtraRequirements: ["change-notice-manage", "reason"]),
            new("C07.cancel-notice", "adminRole.operation.C07.cancel-notice", [nameof(Permissions.ManageCoordination)], ExtraRequirements: ["change-notice-manage", "reason"]),
        ], ProjectScope: true),
        new("C08", "8.10:Assess changed input", ["changed-input", "named-signature", "independent-source", "version"],
        [
            new("C08.adopt", "adminRole.operation.C08.adopt", [nameof(Permissions.NamedCoordinationAction)]),
            new("C08.assign", "adminRole.operation.C08.assign", [nameof(Permissions.ManageCoordination)]),
            new("C08.retain", "adminRole.operation.C08.retain", [nameof(Permissions.ManageCoordination)]),
            new("C08.verify", "adminRole.operation.C08.verify", [nameof(Permissions.NamedCoordinationAction)], NamedActor: "Reviewer"),
        ], ProjectScope: true),
        new("C09", "8.10:Issue submission package", ["submission", "named-signature", "review-source", "version", "atomic"],
        [
            new("C09.create", "adminRole.operation.C09.create", [nameof(Permissions.CreateSubmission)]),
            new("C09.edit", "adminRole.operation.C09.edit", [nameof(Permissions.CoordinateSubmission)], NamedActor: "Coordinator"),
            new("C09.assign", "adminRole.operation.C09.assign", [nameof(Permissions.AuthoriseSubmission)]),
            new("C09.check", "adminRole.operation.C09.check", [nameof(Permissions.SignSubmissionCheck)]),
            new("C09.not-applicable-check", "adminRole.operation.C09.not-applicable-check", [nameof(Permissions.AuthoriseSubmission)], ExtraRequirements: ["submission-optional"]),
            new("C09.issue", "adminRole.operation.C09.issue", [nameof(Permissions.AuthoriseSubmission)]),
            new("C09.cancel", "adminRole.operation.C09.cancel", [nameof(Permissions.AuthoriseSubmission)]),
        ], ProjectScope: true),
        new("C10", "8.10:Propose allocation", ["project-allocation", "entry-source", "version"],
        [
            new("C10.create", "adminRole.operation.C10.create", [nameof(Permissions.ProposeAllocation)]),
            new("C10.edit", "adminRole.operation.C10.edit", [nameof(Permissions.ProposeAllocation)]),
            new("C10.cancel", "adminRole.operation.C10.cancel", [nameof(Permissions.ProposeAllocation)]),
            new("C10.complete", "adminRole.operation.C10.complete", [nameof(Permissions.ProposeAllocation)]),
        ], ProjectScope: true),
        new("C11", "8.10:Confirm allocation/change availability", ["staff-scope", "project-allocation", "hr-source", "version"],
        [
            new("C11.preview", "adminRole.operation.C11.preview", [nameof(Permissions.ConfirmAllocation)]),
            new("C11.confirm", "adminRole.operation.C11.confirm", [nameof(Permissions.ConfirmAllocation)]),
            new("C11.decline", "adminRole.operation.C11.decline", [nameof(Permissions.ConfirmAllocation)]),
            new("C11.availability", "adminRole.operation.C11.availability", [nameof(Permissions.ActOnStaff)], ProjectScope: false),
            new("C11.manual-leave", "adminRole.operation.C11.manual-leave", [nameof(Permissions.ActOnStaff)], ProjectScope: false),
            new("C11.import", "adminRole.operation.C11.import", [nameof(Permissions.Administer)], ProjectScope: false),
        ], ProjectScope: true),
        new("C12", "8.10:Propose design basis/assumption", ["basis-proposal", "active-reference", "version"],
        [
            new("C12.create", "adminRole.operation.C12.create", [nameof(Permissions.CreateBasis)]),
            new("C12.edit", "adminRole.operation.C12.edit", [nameof(Permissions.ManageCoordination), nameof(Permissions.NamedCoordinationAction)], Mode: "Any"),
            new("C12.replace", "adminRole.operation.C12.replace", [nameof(Permissions.ManageCoordination)]),
            new("C12.assign", "adminRole.operation.C12.assign", [nameof(Permissions.ManageCoordination)]),
            new("C12.withdraw", "adminRole.operation.C12.withdraw", [nameof(Permissions.ManageCoordination)]),
        ], ProjectScope: true),
        new("C13", "8.10:Confirm design basis", ["basis-confirm", "independent-source", "version"],
        [
            new("C13.confirm", "adminRole.operation.C13.confirm", [nameof(Permissions.ConfirmBasis)]),
            new("C13.use", "adminRole.operation.C13.use", [nameof(Permissions.NamedCoordinationAction)]),
            new("C13.unaffected", "adminRole.operation.C13.unaffected", [nameof(Permissions.ManageCoordination)]),
            new("C13.resolve-conflict", "adminRole.operation.C13.resolve-conflict", [nameof(Permissions.ManageCoordination)], ExtraRequirements: ["basis-conflict", "reason"]),
        ], ProjectScope: true),
        new("C14", "8.10:Approve Proceed under Assumption", ["assumption", "independent-source", "reason", "version"],
        [
            new("C14.proceed", "adminRole.operation.C14.proceed", [nameof(Permissions.ManageCoordination)]),
            new("C14.exception", "adminRole.operation.C14.exception", [nameof(Permissions.ManageCoordination)]),
        ], ProjectScope: true),
        new("C15", "8.10:Propose/verify constraint removal", ["constraint", "named-signature", "version"],
        [
            new("C15.add", "adminRole.operation.C15.add", [nameof(Permissions.NamedCoordinationAction), nameof(Permissions.ManageCoordination)], Mode: "Any"),
            new("C15.propose-removal", "adminRole.operation.C15.propose-removal", [nameof(Permissions.NamedCoordinationAction)]),
            new("C15.verify-removal", "adminRole.operation.C15.verify-removal", [nameof(Permissions.NamedCoordinationAction)]),
            new("C15.cancel", "adminRole.operation.C15.cancel", [nameof(Permissions.ManageCoordination)]),
        ], ProjectScope: true),
        new("C16", "8.10:Commit weekly output", ["weekly-output", "named-signature", "version"],
        [
            new("C16.propose", "adminRole.operation.C16.propose", [nameof(Permissions.NamedCoordinationAction), nameof(Permissions.ManageCoordination)], Mode: "Any"),
            new("C16.commit", "adminRole.operation.C16.commit", [nameof(Permissions.NamedCoordinationAction)]),
            new("C16.record-outcome", "adminRole.operation.C16.record-outcome", [nameof(Permissions.NamedCoordinationAction)]),
            new("C16.record-not-met", "adminRole.operation.C16.record-not-met", [nameof(Permissions.NamedCoordinationAction), nameof(Permissions.ManageCoordination)], Mode: "Any", ExtraRequirements: ["weekly-not-met"]),
            new("C16.snapshot", "adminRole.operation.C16.snapshot", [nameof(Permissions.ManageCoordination)]),
        ], ProjectScope: true),
        new("C17", "8.10:Resolve/verify location issue", ["issue-verification", "independent-source", "version"],
        [
            new("C17.appoint", "adminRole.operation.C17.appoint", [nameof(Permissions.Writable)], Mode: "Context"),
            new("C17.verify", "adminRole.operation.C17.verify", [nameof(Permissions.Writable)], Mode: "Context", NamedActor: "Reviewer"),
            new("C17.resolve", "adminRole.operation.C17.resolve", [nameof(Permissions.EditRegisterItem)]),
        ], ProjectScope: true),
        new("C18", "8.10:View coordination, sources and export", ["project-scope", "source-kinds", "private-fields", "export-parity"],
        [
            new("C18.read", "adminRole.operation.C18.read", [nameof(Permissions.CanView)]),
            new("C18.export", "adminRole.operation.C18.export", [nameof(Permissions.CanView)]),
            new("C18.incoming", "adminRole.operation.C18.incoming", [nameof(Permissions.CanView)]),
        ], ProjectScope: true),
        new("P01", "8.11:Open the Weekly Planner (§13.21)", ["people-read", "planner-vs-own-week"],
        [
            new("P01.open", "adminRole.operation.P01.open", [nameof(Permissions.ViewWorkload)], Mode: "Context"),
            new("P01.my-week", "adminRole.operation.P01.my-week", [nameof(Permissions.ViewPersonWork)], Mode: "Context"),
        ], ProjectScope: false),
        new("P02", "8.11:See a person's Self-entered entries, Proposed and Confirmed assignments, approved project allocations, capacity and time away", ["people-read", "source-kinds", "private-source", "list-parity"],
        [
            new("P02.self-entries", "adminRole.operation.P02.self-entries", [nameof(Permissions.SeePlanningEntry)], Mode: "Context"),
            new("P02.assignments", "adminRole.operation.P02.assignments", [nameof(Permissions.SeePlanningEntry)], Mode: "Context"),
            new("P02.approved-allocations", "adminRole.operation.P02.approved-allocations", [nameof(Permissions.SeePlanningEntry)], Mode: "Context"),
            new("P02.capacity", "adminRole.operation.P02.capacity", [nameof(Permissions.SeePlanningEntry)], Mode: "Context"),
            new("P02.time-away", "adminRole.operation.P02.time-away", [nameof(Permissions.SeePlanningEntry)], Mode: "Context"),
        ], ProjectScope: false),
        new("P03", "8.11:See a Private draft", ["private-source", "correction", "list-parity"],
        [
            new("P03.view-draft", "adminRole.operation.P03.view-draft", [nameof(Permissions.SeePlanningEntry)], Mode: "Context", EntryPrivacy: true),
            new("P03.correction-read", "adminRole.operation.P03.correction-read", [nameof(Permissions.Administer), nameof(Permissions.SeePlanningEntry)], ExtraRequirements: ["correction"], EntryPrivacy: true),
        ], ProjectScope: false),
        new("P04", "8.11:Create a self entry", ["self-entry", "entry-source", "replay"],
        [
            new("P04.operation-1", "adminRole.operation.P04.operation-1", [nameof(Permissions.CreatePlanningEntry)]),
        ], ProjectScope: false),
        new("P05", "8.11:Create a manager entry", ["planning-owner", "entry-source", "replay"],
        [
            new("P05.operation-1", "adminRole.operation.P05.operation-1", [nameof(Permissions.CreatePlanningEntry)]),
        ], ProjectScope: false),
        new("P06", "8.11:Change, move, extend, mark Still valid or delete an entry; copy creates a new entry and needs the create right for its person", ["planning-owner", "entry-source", "copy-target", "version", "replay"],
        [
            new("P06.edit", "adminRole.operation.P06.edit", [nameof(Permissions.ManagePlanningEntry)]),
            new("P06.move", "adminRole.operation.P06.move", [nameof(Permissions.ManagePlanningEntry)]),
            new("P06.extend", "adminRole.operation.P06.extend", [nameof(Permissions.ManagePlanningEntry)]),
            new("P06.still-valid", "adminRole.operation.P06.still-valid", [nameof(Permissions.ManagePlanningEntry)]),
            new("P06.delete", "adminRole.operation.P06.delete", [nameof(Permissions.ManagePlanningEntry)]),
            new("P06.copy", "adminRole.operation.P06.copy", [nameof(Permissions.CreatePlanningEntry)], ExtraRequirements: ["copy-target"], ExcludedRequirements: ["planning-owner", "version"]),
        ], ProjectScope: false),
        new("P07", "8.11:Change a manager entry's visibility", ["planning-owner", "entry-transition", "version"],
        [
            new("P07.operation-1", "adminRole.operation.P07.operation-1", [nameof(Permissions.ManagePlanningEntry)]),
        ], ProjectScope: false),
        new("P08", "8.11:Record or clear time away (availability overrides, §39.3)", ["legacy-timeaway", "hr-source", "staff-scope", "replay"],
        [
            new("P08.legacy", "adminRole.operation.P08.legacy", [nameof(Permissions.RecordTimeAway)], LegacyUnavailable: true),
            new("P08.manual", "adminRole.operation.P08.manual", [nameof(Permissions.ActOnStaff)]),
            new("P08.correct", "adminRole.operation.P08.correct", [nameof(Permissions.ActOnStaff)]),
            new("P08.import-cancel", "adminRole.operation.P08.import-cancel", [nameof(Permissions.Administer)]),
        ], ProjectScope: false),
        new("P09", "8.11:Export planner and entry lists; save personal planner views", ["people-read", "private-source", "export-parity", "view"],
        [
            new("P09.planner-export", "adminRole.operation.P09.planner-export", [nameof(Permissions.ViewWorkload)], Mode: "Context"),
            new("P09.entry-export", "adminRole.operation.P09.entry-export", [nameof(Permissions.ViewWorkload)], Mode: "Context"),
            new("P09.personal-view", "adminRole.operation.P09.personal-view", [], Mode: "Context"),
        ], ProjectScope: false),
        new("P10", "8.11:Change planning settings (§10.9)", ["org-settings"],
        [
            new("P10.operation-1", "adminRole.operation.P10.operation-1", [nameof(Permissions.Administer)]),
        ], ProjectScope: false),
    ];

    // Integration defaults for Root's locale seam. The API still resolves product text through Text.Get.
    public static IReadOnlyDictionary<string, string> TextDefaults { get; } = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["adminRole.scenario.direct-report-self-entry"] = "A direct report's confirmed self entry",
        ["adminRole.factor.Workflow.DeliverableIssueAllowedFrom"] = "Dedicated deliverable issue source check",
        ["adminRole.requirement.basis-conflict"] = "Requires an unresolved conflict, a distinct Confirmed replacement superseding one conflicted version, matching source disciplines and both current versions.",
        ["adminRole.requirement.change-notice-manage"] = "Requires a Draft or Open notice, current version, reason and any new owner to remain an active project person.",
        ["adminRole.requirement.change-notice-close"] = "The open notice owner must confirm every assessment is complete, its current work owner is unchanged and evidence is recorded.",
        ["adminRole.requirement.risk-transition"] = "Requires an allowed Open, Monitoring, Closed or Realised risk transition with its current version and required reason.",
        ["adminRole.requirement.decision-reopen"] = "Only project management may reopen a Decided decision to Pending; requires the current source, version and reason.",
        ["adminRole.requirement.milestone-reopen"] = "The milestone must currently be complete; reopening requires its current version and a reason.",
        ["adminRole.requirement.milestone-cancel"] = "Requires current milestone version, a cancellation reason and review of retained deliverable targets.",
        ["adminRole.operation.C13.resolve-conflict"] = "Resolve a design basis conflict",
        ["adminRole.operation.C07.cancel-notice"] = "Cancel a change notice",
        ["adminRole.operation.C07.assign-notice"] = "Assign the change notice owner",
        ["adminRole.operation.C07.close-notice"] = "Close an assessed change notice",
        ["adminRole.operation.W32.reopen"] = "Reopen a decided decision",
        ["adminRole.operation.W10.reopen"] = "Reopen a completed milestone",
        ["adminRole.operation.W08.cancel"] = "Cancel a milestone and review retained deliverable targets",
        ["adminRole.row.S01"] = "Sign in and use the application",
        ["adminRole.operation.S01.sign-in"] = "Sign in and use the application",
        ["adminRole.row.S02"] = "View project list and any project (subject to 8.7)",
        ["adminRole.operation.S02.operation-1"] = "project list",
        ["adminRole.operation.S02.operation-2"] = "single project",
        ["adminRole.row.S03"] = "Create a project",
        ["adminRole.operation.S03.operation-1"] = "create project",
        ["adminRole.row.S04"] = "View Portfolio Dashboard",
        ["adminRole.operation.S04.operation-1"] = "portfolio read",
        ["adminRole.operation.S04.operation-2"] = "scoped portfolio counts",
        ["adminRole.row.S05"] = "View Weekly Resource Planner (published and confirmed allocations)",
        ["adminRole.operation.S05.view-planner"] = "View the resource planner",
        ["adminRole.operation.S05.view-approved"] = "View published and confirmed source allocations",
        ["adminRole.row.S06"] = "View Private Draft allocations",
        ["adminRole.operation.S06.view-draft"] = "View a private draft",
        ["adminRole.operation.S06.correction-read"] = "Read a private draft in correction mode",
        ["adminRole.row.S07"] = "Create / edit / delete manager allocations for a person",
        ["adminRole.operation.S07.create"] = "Create an entry",
        ["adminRole.operation.S07.edit"] = "Edit an entry",
        ["adminRole.operation.S07.delete"] = "Delete an entry",
        ["adminRole.row.S08"] = "Publish / confirm / unpublish a manager allocation",
        ["adminRole.operation.S08.publish-entry"] = "Publish a manager entry",
        ["adminRole.operation.S08.confirm-allocation"] = "Confirm a project allocation",
        ["adminRole.operation.S08.unpublish-entry"] = "Unpublish a manager entry",
        ["adminRole.row.S09"] = "Create / edit / delete own employee allocations",
        ["adminRole.operation.S09.create"] = "Create an entry",
        ["adminRole.operation.S09.edit"] = "Edit an entry",
        ["adminRole.operation.S09.delete"] = "Delete an entry",
        ["adminRole.row.S10"] = "Record / correct a manual copy of HR-approved leave (with HR reference)",
        ["adminRole.operation.S10.manual"] = "Record a manual copy of HR-approved leave",
        ["adminRole.operation.S10.correct"] = "Correct a manual HR copy",
        ["adminRole.operation.S10.reconcile"] = "Reconcile a manual copy with an HR import",
        ["adminRole.row.S11"] = "Preview / apply HR-approved leave CSV; configure office holidays",
        ["adminRole.operation.S11.preview"] = "Preview an HR-approved leave import",
        ["adminRole.operation.S11.apply"] = "Apply an HR-approved leave import",
        ["adminRole.operation.S11.holidays"] = "Configure office holidays",
        ["adminRole.row.S12"] = "View Vacation & Coverage availability",
        ["adminRole.operation.S12.availability"] = "View permitted availability",
        ["adminRole.operation.S12.coverage"] = "View project coverage",
        ["adminRole.row.S13"] = "Set a person's weekly capacity and working days",
        ["adminRole.operation.S13.capacity"] = "Set weekly capacity",
        ["adminRole.operation.S13.schedule"] = "Set working days and dated schedules",
        ["adminRole.row.S14"] = "Reassign tasks across projects for supervised staff",
        ["adminRole.operation.S14.operation-1"] = "cross-project staff reassignment",
        ["adminRole.row.S15"] = "Add or remove supervised staff on a project team (as Team Member)",
        ["adminRole.operation.S15.add-report"] = "Add a direct report as Team Member",
        ["adminRole.operation.S15.remove-report"] = "Remove a direct report as Team Member",
        ["adminRole.row.S16"] = "Manage users, system roles, offices, disciplines, clients, deliverable types, phases",
        ["adminRole.operation.S16.users"] = "Manage users",
        ["adminRole.operation.S16.system-roles"] = "Manage system roles",
        ["adminRole.operation.S16.offices"] = "Manage offices",
        ["adminRole.operation.S16.disciplines"] = "Manage disciplines",
        ["adminRole.operation.S16.clients"] = "Manage clients",
        ["adminRole.operation.S16.deliverable-types"] = "Manage deliverable types",
        ["adminRole.operation.S16.phases"] = "Manage phases",
        ["adminRole.row.S17"] = "Manage project templates",
        ["adminRole.operation.S17.create"] = "Create a template",
        ["adminRole.operation.S17.edit"] = "Edit a template",
        ["adminRole.operation.S17.publish"] = "Publish a template",
        ["adminRole.operation.S17.new-draft"] = "Create a new template version",
        ["adminRole.operation.S17.retire"] = "Retire a template",
        ["adminRole.operation.S17.discard"] = "Discard a template",
        ["adminRole.row.S18"] = "Manage organisation thresholds and notification defaults",
        ["adminRole.operation.S18.thresholds"] = "Change organisation thresholds",
        ["adminRole.operation.S18.notification-defaults"] = "Change organisation notification defaults",
        ["adminRole.row.S19"] = "Unarchive a project",
        ["adminRole.operation.S19.unarchive"] = "Unarchive a project",
        ["adminRole.row.S20"] = "View all activity history",
        ["adminRole.operation.S20.project-history"] = "View permitted project activity history",
        ["adminRole.operation.S20.audit"] = "View the administrative audit log",
        ["adminRole.operation.S20.export"] = "Export permitted activity history",
        ["adminRole.row.S21"] = "Export reports",
        ["adminRole.operation.S21.export"] = "Export a permitted report",
        ["adminRole.row.S22"] = "Manage own notification preferences, project follow levels, and saved views",
        ["adminRole.operation.S22.notification-preferences"] = "Change own notification preferences",
        ["adminRole.operation.S22.follow"] = "Change own project follow level",
        ["adminRole.operation.S22.personal-view"] = "Manage own saved views",
        ["adminRole.operation.S22.shared-view"] = "Manage a shared project view",
        ["adminRole.row.W01"] = "View project, dashboard, all registers",
        ["adminRole.operation.W01.operation-1"] = "project read",
        ["adminRole.operation.W01.operation-2"] = "dashboard read",
        ["adminRole.operation.W01.operation-3"] = "all register reads",
        ["adminRole.row.W02"] = "Edit project information (name, client, description, dates, phase, links)",
        ["adminRole.operation.W02.operation-1"] = "edit project fields",
        ["adminRole.row.W03"] = "Change project status (Active, On Hold, Complete, Cancelled)",
        ["adminRole.operation.W03.active"] = "Change project status to Active",
        ["adminRole.operation.W03.onhold"] = "Change project status to On Hold",
        ["adminRole.operation.W03.complete"] = "Change project status to Complete",
        ["adminRole.operation.W03.cancelled"] = "Change project status to Cancelled",
        ["adminRole.row.W04"] = "Archive project",
        ["adminRole.operation.W04.archive"] = "Archive a completed project",
        ["adminRole.row.W05"] = "Manage project team and roles (Supervisors may also add or remove their direct reports as Team Members, §8.5.1)",
        ["adminRole.operation.W05.add-member"] = "Add a project team member",
        ["adminRole.operation.W05.remove-member"] = "Remove a project team member",
        ["adminRole.operation.W05.edit-role"] = "Change project roles or primary PM",
        ["adminRole.row.W06"] = "Add/remove disciplines, set Discipline Lead",
        ["adminRole.operation.W06.add-discipline"] = "Add a project discipline",
        ["adminRole.operation.W06.remove-discipline"] = "Remove a project discipline",
        ["adminRole.operation.W06.discipline-lead"] = "Set or clear a Discipline Lead",
        ["adminRole.row.W07"] = "Set/clear health override",
        ["adminRole.operation.W07.set"] = "Set a health override",
        ["adminRole.operation.W07.clear"] = "Clear a health override",
        ["adminRole.row.W08"] = "Create / edit / delete milestone; change milestone date",
        ["adminRole.operation.W08.create"] = "Create a milestone",
        ["adminRole.operation.W08.edit"] = "Edit a milestone",
        ["adminRole.operation.W08.delete"] = "Delete a milestone",
        ["adminRole.operation.W08.change-date"] = "Change a milestone date",
        ["adminRole.row.W09"] = "Re-baseline a milestone or deliverable, or return it to its original dates, with a reason (§50.5)",
        ["adminRole.operation.W09.milestone-set"] = "Re-baseline a milestone",
        ["adminRole.operation.W09.milestone-clear"] = "Return a milestone to its original dates",
        ["adminRole.operation.W09.deliverable-set"] = "Re-baseline a deliverable",
        ["adminRole.operation.W09.deliverable-clear"] = "Return a deliverable to its original dates",
        ["adminRole.row.W10"] = "Mark milestone Complete",
        ["adminRole.operation.W10.operation-1"] = "milestone complete",
        ["adminRole.row.W11"] = "Create deliverable",
        ["adminRole.operation.W11.operation-1"] = "deliverable create",
        ["adminRole.row.W12"] = "Edit deliverable (fields, owner, dates)",
        ["adminRole.operation.W12.operation-1"] = "deliverable fields edit",
        ["adminRole.operation.W12.operation-2"] = "owner edit",
        ["adminRole.operation.W12.operation-3"] = "dates edit",
        ["adminRole.operation.W12.operation-4"] = "discipline edit",
        ["adminRole.row.W13"] = "Change deliverable status",
        ["adminRole.operation.W13.inprogress"] = "Move deliverable to In Progress",
        ["adminRole.operation.W13.inreview"] = "Move deliverable to In Review",
        ["adminRole.operation.W13.revisionrequired"] = "Move deliverable to Revision Required",
        ["adminRole.operation.W13.readytoissue"] = "Move deliverable to Ready to Issue",
        ["adminRole.operation.W13.issued"] = "Move deliverable to Issued",
        ["adminRole.operation.W13.accepted"] = "Move deliverable to Accepted",
        ["adminRole.row.W14"] = "Delete / cancel deliverable",
        ["adminRole.operation.W14.delete"] = "Delete a deliverable",
        ["adminRole.operation.W14.cancel"] = "Cancel a deliverable",
        ["adminRole.row.W15"] = "Create task",
        ["adminRole.operation.W15.operation-1"] = "task create",
        ["adminRole.row.W16"] = "Create / edit / cancel calendar event tied to project",
        ["adminRole.operation.W16.create"] = "Create a project event",
        ["adminRole.operation.W16.edit"] = "Edit an event",
        ["adminRole.operation.W16.cancel"] = "Cancel an event",
        ["adminRole.row.W17"] = "View task-hour entries",
        ["adminRole.operation.W17.operation-1"] = "task-hour read",
        ["adminRole.operation.W17.operation-2"] = "totals",
        ["adminRole.operation.W17.operation-3"] = "time export",
        ["adminRole.row.W18"] = "Add / edit / delete task-hour entries",
        ["adminRole.operation.W18.add"] = "Add task hours",
        ["adminRole.operation.W18.edit"] = "Edit task hours",
        ["adminRole.operation.W18.delete"] = "Delete task hours",
        ["adminRole.operation.W18.correct"] = "Correct another person's task hours",
        ["adminRole.row.W19"] = "Edit task fields",
        ["adminRole.operation.W19.operation-1"] = "task fields edit",
        ["adminRole.row.W20"] = "Assign / reassign task",
        ["adminRole.operation.W20.operation-1"] = "task assign",
        ["adminRole.operation.W20.operation-2"] = "task reassign",
        ["adminRole.operation.W20.operation-3"] = "bulk assign",
        ["adminRole.row.W21"] = "Change task status",
        ["adminRole.operation.W21.inprogress"] = "Move task to In Progress",
        ["adminRole.operation.W21.readyforreview"] = "Move task to Ready for Review",
        ["adminRole.operation.W21.inreview"] = "Move task to In Review",
        ["adminRole.operation.W21.revisionrequired"] = "Move task to Revision Required",
        ["adminRole.operation.W21.complete"] = "Move task to Complete",
        ["adminRole.operation.W21.onhold"] = "Move task to On Hold",
        ["adminRole.row.W22"] = "Set reviewer on a task",
        ["adminRole.operation.W22.operation-1"] = "set/change task reviewer",
        ["adminRole.row.W23"] = "Add / remove task dependency",
        ["adminRole.operation.W23.add"] = "Add a task dependency",
        ["adminRole.operation.W23.remove"] = "Remove a task dependency",
        ["adminRole.row.W24"] = "Set manual block / clear manual block",
        ["adminRole.operation.W24.set"] = "Set a manual block",
        ["adminRole.operation.W24.clear"] = "Clear a manual block",
        ["adminRole.row.W25"] = "Change task due date",
        ["adminRole.operation.W25.operation-1"] = "change task due date",
        ["adminRole.row.W26"] = "Delete task",
        ["adminRole.operation.W26.operation-1"] = "task delete",
        ["adminRole.row.W27"] = "Cancel task",
        ["adminRole.operation.W27.cancel"] = "Cancel a task",
        ["adminRole.row.W28"] = "Reopen completed task",
        ["adminRole.operation.W28.reopen"] = "Reopen a completed task",
        ["adminRole.row.W29"] = "Comment, @mention, add document link",
        ["adminRole.operation.W29.comment"] = "Add a comment",
        ["adminRole.operation.W29.mention"] = "Mention an eligible person",
        ["adminRole.operation.W29.project-link"] = "Add a project document link",
        ["adminRole.operation.W29.item-link"] = "Add an item document link",
        ["adminRole.row.W30"] = "Edit / delete own comment (within rules, Section 12.8)",
        ["adminRole.operation.W30.edit"] = "Edit own comment within the edit window",
        ["adminRole.operation.W30.delete"] = "Delete own comment or moderate as PM",
        ["adminRole.row.W31"] = "Create / edit decision, risk, issue, meeting, action",
        ["adminRole.operation.W31.decision-create"] = "Create a decision",
        ["adminRole.operation.W31.decision-edit"] = "Edit a decision",
        ["adminRole.operation.W31.decision-cancel"] = "Cancel a decision",
        ["adminRole.operation.W31.risk-create"] = "Create a risk",
        ["adminRole.operation.W31.risk-edit"] = "Edit a risk",
        ["adminRole.operation.W31.risk-transition"] = "Change a risk status",
        ["adminRole.operation.W31.issue-create"] = "Create a issue",
        ["adminRole.operation.W31.issue-edit"] = "Edit a issue",
        ["adminRole.operation.W31.issue-cancel"] = "Cancel a issue",
        ["adminRole.operation.W31.meeting-create"] = "Create a meeting",
        ["adminRole.operation.W31.meeting-edit"] = "Edit a meeting",
        ["adminRole.operation.W31.meeting-current"] = "Record or open today's coordination meeting",
        ["adminRole.operation.W31.action-create"] = "Create a action",
        ["adminRole.operation.W31.action-edit"] = "Edit a action",
        ["adminRole.operation.W31.action-cancel"] = "Cancel a action",
        ["adminRole.row.W32"] = "Record a decision outcome",
        ["adminRole.operation.W32.record"] = "Record a decision outcome",
        ["adminRole.operation.W32.defer"] = "Defer a decision",
        ["adminRole.row.W33"] = "Run Weekly Coordination in meeting mode (record \"reviewed\" timestamp)",
        ["adminRole.operation.W33.operation-1"] = "record Weekly Coordination reviewed timestamp",
        ["adminRole.row.C01"] = "Create/assign handoff",
        ["adminRole.operation.C01.create"] = "Create a handoff",
        ["adminRole.operation.C01.edit"] = "Edit a handoff",
        ["adminRole.operation.C01.assign"] = "Assign or reassign a handoff",
        ["adminRole.row.C02"] = "Submit/respond to handoff",
        ["adminRole.operation.C02.submit"] = "Submit or resubmit a handoff",
        ["adminRole.operation.C02.respond"] = "Return or respond to a handoff",
        ["adminRole.operation.C02.cancel"] = "Cancel a handoff",
        ["adminRole.row.C03"] = "Accept/incorporate handoff",
        ["adminRole.operation.C03.accept"] = "Accept a handoff",
        ["adminRole.operation.C03.incorporate"] = "Incorporate a handoff",
        ["adminRole.row.C04"] = "Coordinate review package",
        ["adminRole.operation.C04.create"] = "Create a review package",
        ["adminRole.operation.C04.new-round"] = "Create a new review round",
        ["adminRole.operation.C04.start"] = "Start a review round",
        ["adminRole.operation.C04.assign-coordinator"] = "Assign a review coordinator",
        ["adminRole.operation.C04.assign-reviewer"] = "Assign a review discipline reviewer",
        ["adminRole.operation.C04.cancel"] = "Cancel a review package",
        ["adminRole.row.C05"] = "Technical review approval",
        ["adminRole.operation.C05.decide"] = "Record a technical review decision",
        ["adminRole.operation.C05.pre-issue-gate"] = "Check the required pre-issue review gate",
        ["adminRole.row.C06"] = "Respond to/verify review finding",
        ["adminRole.operation.C06.add-internal"] = "Add an internal review finding",
        ["adminRole.operation.C06.record-incoming"] = "Record incoming review comments",
        ["adminRole.operation.C06.respond"] = "Respond as the finding resolver",
        ["adminRole.operation.C06.assign-resolver"] = "Assign a finding resolver",
        ["adminRole.operation.C06.assign-verifier"] = "Designate an independent verifier",
        ["adminRole.operation.C06.verify"] = "Verify and close a finding",
        ["adminRole.operation.C06.return"] = "Return a finding for response",
        ["adminRole.operation.C06.withdraw"] = "Withdraw a finding",
        ["adminRole.row.C07"] = "Publish source revision/change",
        ["adminRole.operation.C07.register-source"] = "Register a source revision",
        ["adminRole.operation.C07.external-source"] = "Manage an external source head",
        ["adminRole.operation.C07.publish"] = "Publish a change notice",
        ["adminRole.row.C08"] = "Assess changed input",
        ["adminRole.operation.C08.adopt"] = "Adopt or assess changed input",
        ["adminRole.operation.C08.assign"] = "Assign a change assessment",
        ["adminRole.operation.C08.retain"] = "Approve retention of an older revision",
        ["adminRole.operation.C08.verify"] = "Verify a completed assessment",
        ["adminRole.row.C09"] = "Issue submission package",
        ["adminRole.operation.C09.create"] = "Create or supersede a submission",
        ["adminRole.operation.C09.edit"] = "Edit or start a submission package",
        ["adminRole.operation.C09.assign"] = "Assign submission responsibilities",
        ["adminRole.operation.C09.check"] = "Sign own submission check",
        ["adminRole.operation.C09.issue"] = "Issue the submission package",
        ["adminRole.operation.C09.cancel"] = "Cancel a submission",
        ["adminRole.row.C10"] = "Propose allocation",
        ["adminRole.operation.C10.create"] = "Propose a project allocation",
        ["adminRole.operation.C10.edit"] = "Edit a project allocation",
        ["adminRole.operation.C10.cancel"] = "Cancel a project allocation",
        ["adminRole.operation.C10.complete"] = "Complete a project allocation",
        ["adminRole.row.C11"] = "Confirm allocation/change availability",
        ["adminRole.operation.C11.preview"] = "Preview project allocation confirmation",
        ["adminRole.operation.C11.confirm"] = "Confirm a project allocation",
        ["adminRole.operation.C11.decline"] = "Decline a project allocation",
        ["adminRole.operation.C11.availability"] = "Set private day capacity",
        ["adminRole.operation.C11.manual-leave"] = "Record or reconcile an HR-approved leave copy",
        ["adminRole.operation.C11.import"] = "Import HR-approved leave or holidays",
        ["adminRole.row.C12"] = "Propose design basis/assumption",
        ["adminRole.operation.C12.create"] = "Propose a design basis or assumption",
        ["adminRole.operation.C12.edit"] = "Edit a proposed basis",
        ["adminRole.operation.C12.replace"] = "Propose a replacement basis",
        ["adminRole.operation.C12.assign"] = "Assign basis responsibilities",
        ["adminRole.operation.C12.withdraw"] = "Withdraw a basis",
        ["adminRole.row.C13"] = "Confirm design basis",
        ["adminRole.operation.C13.confirm"] = "Confirm a design basis",
        ["adminRole.operation.C13.use"] = "Adopt a confirmed basis for own work",
        ["adminRole.operation.C13.unaffected"] = "Decide an unaffected basis impact",
        ["adminRole.row.C14"] = "Approve Proceed under Assumption",
        ["adminRole.operation.C14.proceed"] = "Approve Proceed under Assumption",
        ["adminRole.operation.C14.exception"] = "Approve a scoped readiness exception",
        ["adminRole.row.C15"] = "Propose/verify constraint removal",
        ["adminRole.operation.C15.add"] = "Add a constraint",
        ["adminRole.operation.C15.propose-removal"] = "Propose constraint removal",
        ["adminRole.operation.C15.verify-removal"] = "Verify removal as affected work owner",
        ["adminRole.operation.C15.cancel"] = "Cancel a constraint",
        ["adminRole.row.C16"] = "Commit weekly output",
        ["adminRole.operation.C16.propose"] = "Propose a weekly output",
        ["adminRole.operation.C16.commit"] = "Commit as the actual performer",
        ["adminRole.operation.C16.record-outcome"] = "Record own commitment outcome",
        ["adminRole.operation.C16.snapshot"] = "Record the coordination-week snapshot",
        ["adminRole.row.C17"] = "Resolve/verify location issue",
        ["adminRole.operation.C17.appoint"] = "Propose an independent issue verifier",
        ["adminRole.operation.C17.verify"] = "Verify or reject a location issue",
        ["adminRole.operation.C17.resolve"] = "Resolve a verified location issue",
        ["adminRole.row.C18"] = "View coordination, sources and export",
        ["adminRole.operation.C18.read"] = "Read permitted coordination records and sources",
        ["adminRole.operation.C18.export"] = "Export permitted coordination records",
        ["adminRole.operation.C18.incoming"] = "Read incoming comments and issued-source guidance",
        ["adminRole.row.P01"] = "Open the Weekly Planner (§13.21)",
        ["adminRole.operation.P01.open"] = "Open the Weekly Planner",
        ["adminRole.operation.P01.my-week"] = "View own My Week",
        ["adminRole.row.P02"] = "See a person's Self-entered entries, Proposed and Confirmed assignments, approved project allocations, capacity and time away",
        ["adminRole.operation.P02.self-entries"] = "View self entries",
        ["adminRole.operation.P02.assignments"] = "View assignments",
        ["adminRole.operation.P02.approved-allocations"] = "View approved allocations",
        ["adminRole.operation.P02.capacity"] = "View capacity",
        ["adminRole.operation.P02.time-away"] = "View time away",
        ["adminRole.row.P03"] = "See a Private draft",
        ["adminRole.operation.P03.view-draft"] = "View a private draft",
        ["adminRole.operation.P03.correction-read"] = "Read a private draft in correction mode",
        ["adminRole.row.P04"] = "Create a self entry",
        ["adminRole.operation.P04.operation-1"] = "create self entry",
        ["adminRole.row.P05"] = "Create a manager entry",
        ["adminRole.operation.P05.operation-1"] = "create manager entry",
        ["adminRole.row.P06"] = "Change, move, extend, mark Still valid or delete an entry; copy creates a new entry and needs the create right for its person",
        ["adminRole.operation.P06.edit"] = "Edit an entry",
        ["adminRole.operation.P06.move"] = "Move an entry",
        ["adminRole.operation.P06.extend"] = "Extend an entry",
        ["adminRole.operation.P06.still-valid"] = "Mark an entry Still valid",
        ["adminRole.operation.P06.delete"] = "Delete an entry",
        ["adminRole.operation.P06.copy"] = "Copy an entry",
        ["adminRole.row.P07"] = "Change a manager entry's visibility",
        ["adminRole.operation.P07.operation-1"] = "change manager-entry visibility",
        ["adminRole.row.P08"] = "Record or clear time away (availability overrides, §39.3)",
        ["adminRole.operation.P08.legacy"] = "Use the retired time-away override command",
        ["adminRole.operation.P08.manual"] = "Record a manual HR-approved leave copy",
        ["adminRole.operation.P08.correct"] = "Correct or reconcile an HR source copy",
        ["adminRole.operation.P08.import-cancel"] = "Apply explicit cancellation from an HR import",
        ["adminRole.row.P09"] = "Export planner and entry lists; save personal planner views",
        ["adminRole.operation.P09.planner-export"] = "Export the permitted planner",
        ["adminRole.operation.P09.entry-export"] = "Export the permitted entry list",
        ["adminRole.operation.P09.personal-view"] = "Save a personal planner view",
        ["adminRole.row.P10"] = "Change planning settings (§10.9)",
        ["adminRole.operation.P10.operation-1"] = "organisation planning settings",
        ["adminRole.requirement.identity"] = "A current active account must pass the real sign-in checks.",
        ["adminRole.requirement.project-scope"] = "The current project and each selected source must be visible to the caller.",
        ["adminRole.requirement.list-parity"] = "Lists, counts, details and exports must use the same permitted population.",
        ["adminRole.requirement.new-project"] = "The real create command must validate project fields and create its team and history atomically.",
        ["adminRole.requirement.portfolio-scope"] = "The selected portfolio filter must resolve to projects the caller may see.",
        ["adminRole.requirement.people-read"] = "The current self, direct-report, managed-project or enabled organisation-unit read scope must be resolved.",
        ["adminRole.requirement.source-kinds"] = "Each entry, assignment, allocation, capacity and time-away source keeps its own visibility rules.",
        ["adminRole.requirement.private-source"] = "A private source requires its actual owner or explicit authorised correction access; hidden work stays out of other totals.",
        ["adminRole.requirement.correction"] = "Correction access requires an administrator, an explicit reason and the required access history.",
        ["adminRole.requirement.planning-owner"] = "The entry's actual owner must still manage its person; another manager's entry needs explicit correction authority.",
        ["adminRole.requirement.entry-source"] = "The actual person, project, source type, dates and hours must remain eligible.",
        ["adminRole.requirement.entry-transition"] = "The selected entry type, visibility or confirmation state must permit this transition.",
        ["adminRole.requirement.self-entry"] = "The entry must belong to the actual caller's own person and use the self-entry rules.",
        ["adminRole.requirement.hr-source"] = "Leave must reference an HR-approved source; imported records remain source-owned and manual copies keep their provenance.",
        ["adminRole.requirement.staff-scope"] = "The current person must be in the actor's real staff-write scope; read access does not confer management rights.",
        ["adminRole.requirement.hr-import"] = "Preview, apply, mappings, source freshness and the unchanged import receipt must each be validated before an atomic import.",
        ["adminRole.requirement.holidays"] = "Office holidays must use the current office calendar and invalidate affected confirmations where required.",
        ["adminRole.requirement.availability-scope"] = "Availability uses its own permitted people and team population, separately from the workload list.",
        ["adminRole.requirement.private-fields"] = "Private event, time-note and HR source details must remain masked under the source's field rules.",
        ["adminRole.requirement.capacity-schedule"] = "Weekly capacity and dated working-day schedules must use the correct person, dates, hours and existing version.",
        ["adminRole.requirement.task-target"] = "The actual task, assignee, project, discipline and related work must all remain eligible.",
        ["adminRole.requirement.atomic"] = "A late refusal must leave the whole command's records and history unchanged.",
        ["adminRole.requirement.team-role"] = "The person and role must be eligible for this project; supervisor staffing grants Team Member only.",
        ["adminRole.requirement.admin-child"] = "Every selected administration route and reference-data lifecycle must enforce its own current invariants.",
        ["adminRole.requirement.active-reference"] = "Every person, discipline and related record must be current, active and in the required project.",
        ["adminRole.requirement.template"] = "The selected template must be in an editable state; management rights do not grant project creation.",
        ["adminRole.requirement.org-settings"] = "The selected organisation setting, value and defaults must pass its actual validation; personal settings are separate.",
        ["adminRole.requirement.project-transition"] = "The actual source status, requested target, closeout conditions and required reason must permit this project transition.",
        ["adminRole.requirement.activity-fields"] = "Project activity and the administrative audit log are different readers with source-specific visibility.",
        ["adminRole.requirement.report-policy"] = "The Standard User report population remains an open policy decision.",
        ["adminRole.requirement.report-definition"] = "The report's own roles, enabled feature, permitted filters and current source scope must be checked.",
        ["adminRole.requirement.export-parity"] = "The download must use the same permitted source population and private-field rules as the current reader.",
        ["adminRole.requirement.personal-owner"] = "The current caller must own the personal preference, follow state or saved view being changed.",
        ["adminRole.requirement.personal-flags"] = "The relevant preference feature and recipient revision must be checked at the actual atomic update.",
        ["adminRole.requirement.view"] = "The saved view must have an eligible owner, supported filters and current visible references; sharing does not widen data access.",
        ["adminRole.requirement.project-fields"] = "The current project, editable fields, dates, phase and linked references must be validated.",
        ["adminRole.requirement.child-records"] = "The actual child records, retained links and completion conditions may prevent this operation.",
        ["adminRole.requirement.discipline-reference"] = "The discipline and lead must be active in this project, with dependent work protected on removal.",
        ["adminRole.requirement.health-override"] = "The override's reason, expiry and current project version must be valid for setting or clearing it.",
        ["adminRole.requirement.milestone"] = "The actual milestone, dates, project discipline and cascade preview must permit the selected command.",
        ["adminRole.requirement.milestone-complete"] = "Unissued deliverables and current milestone state may require explicit confirmation before completion.",
        ["adminRole.requirement.baseline"] = "The baseline feature, actual open item and retained original dates and snapshots must permit set or clear.",
        ["adminRole.requirement.deliverable-fields"] = "The actual deliverable owner, discipline, dates and any task movement must be valid for the selected edit.",
        ["adminRole.requirement.deliverable-transition"] = "The actual deliverable source status and target, review readiness and issue conditions must permit this step.",
        ["adminRole.requirement.review-source"] = "Current review rounds, source identities, captured author provenance and required approvals must be checked.",
        ["adminRole.requirement.deliverable-delete"] = "Completed tasks, current downstream source uses and retained history may prevent deletion or cancellation.",
        ["adminRole.requirement.calendar-private"] = "A private event is readable only under its actual owner and source visibility rules; management is not impersonation.",
        ["adminRole.requirement.event-source"] = "The actual event must be editable, not already cancelled, and have current valid dates and project references.",
        ["adminRole.requirement.time-scope"] = "Actual entry ownership, project visibility, discipline-lead or supervised-person scope must permit each time reader.",
        ["adminRole.requirement.time-owner"] = "Own edits and manager corrections are distinct; correcting another person's time needs the actual correction reason.",
        ["adminRole.requirement.time-total"] = "The atomic save must preserve the person's daily-hour cap across all current entries.",
        ["adminRole.requirement.task-facts"] = "The actual assignee, collaborators, reviewer, dependencies and current task state must be loaded.",
        ["adminRole.requirement.creator-window"] = "Creator rights require the actual Not Started window and no disqualifying comment by another person.",
        ["adminRole.requirement.task-path"] = "Every actual workflow step, including intermediate checkbox steps, must pass the current task and review guards.",
        ["adminRole.requirement.task-reviewer"] = "The selected reviewer must be currently eligible; initial creation and later reassignment use different rights.",
        ["adminRole.requirement.dependency"] = "Both dependency ends must be visible in the same project; cycles, duplicate links and invalid link types remain refused.",
        ["adminRole.requirement.manual-block"] = "Setting and clearing a manual block each need an eligible current source and the required command reason.",
        ["adminRole.requirement.task-due"] = "The actual assignee or manager due-date right, current dates and actor-specific reason rules must pass.",
        ["adminRole.requirement.task-delete"] = "The actual task state, creator rights, dependencies and retained source links must permit deletion.",
        ["adminRole.requirement.comment-target"] = "The actual comment target and mentioned people must be visible and eligible in this project.",
        ["adminRole.requirement.viewer-comments"] = "Viewer commenting depends on the project's current Viewer-comments setting.",
        ["adminRole.requirement.document-link"] = "Project links and other item links use distinct edit rights; URLs, references and declared sources stay validated.",
        ["adminRole.requirement.comment-window"] = "Editing requires the actual author, eligible comment type and a still-open fifteen-minute window.",
        ["adminRole.requirement.comment-source"] = "Deleted or locked comments and read-only project states retain the command's actual refusal rules.",
        ["adminRole.requirement.register-family"] = "Each decision, risk, issue, meeting and action has its own owner, source state and field requirements.",
        ["adminRole.requirement.decision-outcome"] = "The actual decision source state and selected outcome must be legal for the current owner or manager.",
        ["adminRole.requirement.coordination-reviewed"] = "The real reviewed command must save the current timestamp, actor and activity history; it has no caller-version input.",
        ["adminRole.requirement.planner-vs-own-week"] = "Opening the full planner and viewing one's own week are distinct capabilities.",
        ["adminRole.requirement.copy-target"] = "A copy creates a new entry and must recheck the create right for its actual target person.",
        ["adminRole.requirement.legacy-timeaway"] = "The retired time-away override command is unavailable; use an HR-referenced leave workflow.",
        ["adminRole.requirement.handoff-source"] = "The handoff must retain an eligible same-project source, current revision, dates and receiving target.",
        ["adminRole.requirement.handoff-owners"] = "Current sending and receiving people must be eligible; reassignment preserves historic submitters.",
        ["adminRole.requirement.handoff-signature"] = "The actual named sending or receiving person must take the selected handoff step.",
        ["adminRole.requirement.independent-source"] = "Required independence uses known current and historical author and response provenance; unknown issued authors cannot be bypassed.",
        ["adminRole.requirement.review-round"] = "The actual package, current round, manifest, assignments and legal lifecycle must be validated.",
        ["adminRole.requirement.review-signature"] = "Only the current stored discipline reviewer can sign the selected technical review decision.",
        ["adminRole.requirement.finding-source"] = "The finding must retain its real current round, source and incoming receipt or internal origin.",
        ["adminRole.requirement.finding-signature"] = "Resolver response, explicit verifier designation and independent closure are separate named-person actions.",
        ["adminRole.requirement.published-source"] = "The actual source head, predecessor, owner and published revision must remain current.",
        ["adminRole.requirement.changed-input"] = "The actual receiving work, current source and impact assessment must permit adoption, retention or verification.",
        ["adminRole.requirement.named-signature"] = "Management may coordinate or reassign work, but cannot sign for its named performer, owner, reviewer or verifier.",
        ["adminRole.requirement.submission"] = "The actual package and manifest fingerprint must pass all mandatory gates again at issue, in one atomic command.",
        ["adminRole.requirement.project-allocation"] = "The actual proposed allocation, person, source links, dates and date versions must remain current.",
        ["adminRole.requirement.basis-proposal"] = "The proposal's real owner, discipline, units, evidence and duplicate inspection must be valid.",
        ["adminRole.requirement.basis-confirm"] = "The current basis version, eligible independent approver, exact retained uses and evidence must permit confirmation.",
        ["adminRole.requirement.assumption"] = "The proposed assumption and approved disposition need a fixed valid scope, expiry, reason and current evidence.",
        ["adminRole.requirement.constraint"] = "The actual removal owner proposes evidence before the affected work owner independently verifies removal.",
        ["adminRole.requirement.weekly-output"] = "The actual performer, coordination week, readiness and recorded snapshot must permit the selected commitment step.",
        ["adminRole.requirement.issue-verification"] = "The issue's current owner, appointed independent verifier, proposal and evidence must remain valid.",
        ["adminRole.requirement.version"] = "The real command must receive and recheck every version it requires.",
        ["adminRole.requirement.reason"] = "A real command reason is still required wherever the source or transition requires one.",
        ["adminRole.requirement.replay"] = "Replaying a prior request must recheck current authority before returning its stored response.",
        ["adminRole.fact.confirmedSelfPlanning"] = "The example is a confirmed self entry.",
        ["adminRole.fact.closedItem"] = "The example source item is closed.",
        ["adminRole.fact.openItem"] = "The example source item is open.",
        ["adminRole.fact.ownPersonalState"] = "Personal-state examples refer to the example caller's own settings.",
        ["adminRole.operation.C09.not-applicable-check"] = "Mark an optional submission check Not Applicable",
        ["adminRole.operation.C16.record-not-met"] = "Record Not Met after a recorded coordination week",
        ["adminRole.requirement.submission-optional"] = "Only an optional applicability check may be marked Not Applicable; the current package, check version, reason and evidence remain required.",
        ["adminRole.requirement.weekly-not-met"] = "The week must have a recorded snapshot and a valid closure state, reason and enabled miss category; this management branch does not sign a Met outcome.",
        ["adminRole.operation.W13.onhold"] = "Put a deliverable On Hold",
        ["adminRole.operation.C06.acknowledge-withdrawal"] = "Acknowledge a blocking finding withdrawal as package coordinator",
        ["adminRole.requirement.deliverable-on-hold"] = "Putting the deliverable On Hold requires a reason and retains its current status. Leaving hold must follow the retained previous status or cancellation, with the recorded hold reason cleared.",
        ["adminRole.requirement.withdrawal-acknowledgement"] = "The current active package coordinator must acknowledge a Withdrawn, Blocking finding in its current live round, with the required reason, version and source/replay checks. This acknowledgement is not the verifier's withdrawal signature.",
        ["adminRole.fact.selectedNamedVerifier"] = "This operation uses the finding's designated verifier, separately from its resolver and review assignment.",
        ["adminRole.fact.selectedPackageCoordinator"] = "This operation uses the current package coordinator, separately from the finding's verifier.",
        ["adminRole.fact.actorIsDesignatedVerifier"] = "The example actor is the finding's designated verifier.",
        ["adminRole.fact.otherDesignatedVerifier"] = "Another example person is the finding's designated verifier.",
        ["adminRole.fact.actorIsPackageCoordinator"] = "The example actor is the current package coordinator.",
        ["adminRole.fact.otherPackageCoordinator"] = "Another example person is the current package coordinator.",
        ["adminRole.fact.deliverablePrevious.Revision Required"] = "The example retains Revision Required as the deliverable's previous status.",
        ["adminRole.fact.recordedHoldReason"] = "The example On Hold deliverable has a recorded hold reason.",
        ["adminRole.fact.onHoldRetainsPreviousStatus"] = "Entering On Hold retains the current source status and needs a real command reason.",
        ["adminRole.scenario.designated-verifier"] = "Finding's designated verifier",
        ["adminRole.scenario.package-coordinator"] = "Finding's current package coordinator",
        ["adminRole.scenario.on-hold-deliverable"] = "Own deliverable On Hold, previously Revision Required",
        ["adminRole.operation.all"] = "All row operations",
        ["adminRole.requirement.canonical-factors"] = "The selected canonical permission factors apply to these labelled example facts only.",
        ["adminRole.requirement.workflow-path"] = "The selected source and target must form a valid workflow path.",
        ["adminRole.requirement.selected-source-state"] = "The selected command requires a different current source state.",
        ["adminRole.fact.explicitMembership"] = "Project membership is selected separately from the system role.",
        ["adminRole.fact.noLiveDecision"] = "No real person, project or record has been evaluated.",
        ["adminRole.fact.noLiveEntity"] = "All identities and records in these examples are synthetic.",
        ["adminRole.fact.selectedNamedReviewer"] = "The named actor for this operation is the selected reviewer or verifier.",
        ["adminRole.fact.selectedNamedOwner"] = "The named actor for this operation is the selected owner or performer.",
        ["adminRole.fact.leadsSelectedDiscipline"] = "This example person leads the selected project discipline.",
        ["adminRole.fact.notSelectedDisciplineLead"] = "This example person is not the selected discipline's lead.",
        ["adminRole.fact.effectiveReadOnly"] = "The example account has effective Read Only restrictions.",
        ["adminRole.fact.effectiveWritable"] = "The example account has no effective Read Only veto; other write guards still apply.",
        ["adminRole.fact.storedReadOnly"] = "The example includes a stored Read Only role; Admin retains canonical precedence.",
        ["adminRole.fact.noStoredReadOnly"] = "The example does not add a stored Read Only role.",
        ["adminRole.fact.correctionRequested"] = "Explicit correction mode is selected; real authority, reason and logging remain required.",
        ["adminRole.fact.normalAccess"] = "Normal access is selected.",
        ["adminRole.fact.expiredComment"] = "The example comment is sixteen minutes old.",
        ["adminRole.fact.reviewRequired"] = "The example task or deliverable requires review.",
        ["adminRole.fact.selfReviewDisabled"] = "Self-review is disabled in the example handoff transition.",
        ["adminRole.decision.Conditional"] = "Conditional",
        ["adminRole.decision.NotEligible"] = "Not eligible in this example",
        ["adminRole.decision.PolicyPending"] = "Policy decision pending",
        ["adminRole.requirementState.RequiresLiveCheck"] = "Requires a live check",
        ["adminRole.requirementState.NotMetByExample"] = "Not met by the example facts",
        ["adminRole.requirementState.MetByExample"] = "Canonical factors met by the example facts",
        ["adminRole.membership.none"] = "No project membership",
        ["adminRole.membership.PM"] = "Project Manager",
        ["adminRole.membership.TeamMember"] = "Team Member",
        ["adminRole.membership.Reviewer"] = "Reviewer",
        ["adminRole.membership.Viewer"] = "Viewer",
        ["adminRole.membership.DisciplineLead"] = "Discipline Lead and Team Member",
        ["adminRole.scenario.restricted-project"] = "Restricted project with the selected membership",
        ["adminRole.scenario.setup-project"] = "Project in Setup",
        ["adminRole.scenario.on-hold-project"] = "Project On Hold",
        ["adminRole.scenario.cancelled-project"] = "Cancelled project",
        ["adminRole.scenario.completed-task"] = "Completed task",
        ["adminRole.scenario.unstarted-task"] = "Not Started task",
        ["adminRole.scenario.ready-to-issue"] = "Deliverable Ready to Issue",
        ["adminRole.scenario.issued-deliverable"] = "Issued deliverable",
        ["adminRole.scenario.accepted-handoff"] = "Accepted handoff with named receiver",
        ["adminRole.scenario.private-draft-owned"] = "Own manager-entry private draft",
        ["adminRole.scenario.admin-correction"] = "Private draft with explicit correction mode requested",
        ["adminRole.scenario.task-collaborator"] = "Actual current task collaborator",
        ["adminRole.scenario.task-in-review"] = "Task In Review with the named reviewer",
        ["adminRole.scenario.task-revision-required"] = "Own task requiring revision",
        ["adminRole.scenario.closed-item"] = "Closed item",
        ["adminRole.scenario.expired-comment"] = "Own comment beyond the edit window",
        ["adminRole.factor.Workflow.ProjectAllowed"] = "Project workflow step",
        ["adminRole.factor.Workflow.TaskPath"] = "Task workflow path",
        ["adminRole.factor.Workflow.DeliverableStep"] = "Deliverable workflow step",
        ["adminRole.fact.systemRole.Standard"] = "Example system role: Standard.",
        ["adminRole.fact.systemRole.Admin"] = "Example system role: Admin.",
        ["adminRole.fact.systemRole.Executive"] = "Example system role: Executive.",
        ["adminRole.fact.systemRole.Supervisor"] = "Example system role: Supervisor.",
        ["adminRole.fact.systemRole.ProjectManager"] = "Example system role: Project Manager.",
        ["adminRole.fact.systemRole.ReadOnly"] = "Example system role: Read Only.",
        ["adminRole.fact.target.Project.Setup"] = "Selected project target: Setup.",
        ["adminRole.fact.target.Project.Active"] = "Selected project target: Active.",
        ["adminRole.fact.target.Project.On Hold"] = "Selected project target: On Hold.",
        ["adminRole.fact.target.Project.Complete"] = "Selected project target: Complete.",
        ["adminRole.fact.target.Project.Archived"] = "Selected project target: Archived.",
        ["adminRole.fact.target.Project.Cancelled"] = "Selected project target: Cancelled.",
        ["adminRole.fact.target.Task.Not Started"] = "Selected task target: Not Started.",
        ["adminRole.fact.task.Not Started"] = "Example task source: Not Started.",
        ["adminRole.fact.target.Task.In Progress"] = "Selected task target: In Progress.",
        ["adminRole.fact.task.In Progress"] = "Example task source: In Progress.",
        ["adminRole.fact.target.Task.Ready for Review"] = "Selected task target: Ready for Review.",
        ["adminRole.fact.task.Ready for Review"] = "Example task source: Ready for Review.",
        ["adminRole.fact.target.Task.In Review"] = "Selected task target: In Review.",
        ["adminRole.fact.task.In Review"] = "Example task source: In Review.",
        ["adminRole.fact.target.Task.Revision Required"] = "Selected task target: Revision Required.",
        ["adminRole.fact.task.Revision Required"] = "Example task source: Revision Required.",
        ["adminRole.fact.target.Task.Complete"] = "Selected task target: Complete.",
        ["adminRole.fact.task.Complete"] = "Example task source: Complete.",
        ["adminRole.fact.target.Task.On Hold"] = "Selected task target: On Hold.",
        ["adminRole.fact.task.On Hold"] = "Example task source: On Hold.",
        ["adminRole.fact.target.Task.Cancelled"] = "Selected task target: Cancelled.",
        ["adminRole.fact.task.Cancelled"] = "Example task source: Cancelled.",
        ["adminRole.fact.target.Deliverable.Not Started"] = "Selected deliverable target: Not Started.",
        ["adminRole.fact.deliverable.Not Started"] = "Example deliverable source: Not Started.",
        ["adminRole.fact.target.Deliverable.In Progress"] = "Selected deliverable target: In Progress.",
        ["adminRole.fact.deliverable.In Progress"] = "Example deliverable source: In Progress.",
        ["adminRole.fact.target.Deliverable.In Review"] = "Selected deliverable target: In Review.",
        ["adminRole.fact.deliverable.In Review"] = "Example deliverable source: In Review.",
        ["adminRole.fact.target.Deliverable.Revision Required"] = "Selected deliverable target: Revision Required.",
        ["adminRole.fact.deliverable.Revision Required"] = "Example deliverable source: Revision Required.",
        ["adminRole.fact.target.Deliverable.Ready to Issue"] = "Selected deliverable target: Ready to Issue.",
        ["adminRole.fact.deliverable.Ready to Issue"] = "Example deliverable source: Ready to Issue.",
        ["adminRole.fact.target.Deliverable.Issued"] = "Selected deliverable target: Issued.",
        ["adminRole.fact.deliverable.Issued"] = "Example deliverable source: Issued.",
        ["adminRole.fact.target.Deliverable.Accepted"] = "Selected deliverable target: Accepted.",
        ["adminRole.fact.deliverable.Accepted"] = "Example deliverable source: Accepted.",
        ["adminRole.fact.target.Deliverable.On Hold"] = "Selected deliverable target: On Hold.",
        ["adminRole.fact.deliverable.On Hold"] = "Example deliverable source: On Hold.",
        ["adminRole.fact.target.Deliverable.Cancelled"] = "Selected deliverable target: Cancelled.",
        ["adminRole.fact.deliverable.Cancelled"] = "Example deliverable source: Cancelled.",
        ["adminRole.fact.target.Handoff.Draft"] = "Selected handoff target: Draft.",
        ["adminRole.fact.handoff.Draft"] = "Example handoff source: Draft.",
        ["adminRole.fact.target.Handoff.Submitted"] = "Selected handoff target: Submitted.",
        ["adminRole.fact.handoff.Submitted"] = "Example handoff source: Submitted.",
        ["adminRole.fact.target.Handoff.Clarification Requested"] = "Selected handoff target: Clarification Requested.",
        ["adminRole.fact.handoff.Clarification Requested"] = "Example handoff source: Clarification Requested.",
        ["adminRole.fact.target.Handoff.Returned"] = "Selected handoff target: Returned.",
        ["adminRole.fact.handoff.Returned"] = "Example handoff source: Returned.",
        ["adminRole.fact.target.Handoff.Accepted"] = "Selected handoff target: Accepted.",
        ["adminRole.fact.handoff.Accepted"] = "Example handoff source: Accepted.",
        ["adminRole.fact.target.Handoff.Incorporated"] = "Selected handoff target: Incorporated.",
        ["adminRole.fact.handoff.Incorporated"] = "Example handoff source: Incorporated.",
        ["adminRole.fact.target.Handoff.Cancelled"] = "Selected handoff target: Cancelled.",
        ["adminRole.fact.handoff.Cancelled"] = "Example handoff source: Cancelled.",
    };
}
