using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hub.Api.Data;

public sealed class HubDb(DbContextOptions<HubDb> options, AuditContext audit, TimeProvider clock, EvaluationSignal signal) : DbContext(options)
{
    public AuditContext Audit => audit;

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<UserSystemRole> UserRoles => Set<UserSystemRole>();
    public DbSet<Office> Offices => Set<Office>();
    public DbSet<Discipline> Disciplines => Set<Discipline>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<ProjectType> ProjectTypes => Set<ProjectType>();
    public DbSet<Phase> Phases => Set<Phase>();
    public DbSet<DeliverableType> DeliverableTypes => Set<DeliverableType>();
    public DbSet<OrgSetting> Settings => Set<OrgSetting>();
    public DbSet<UserSetting> UserSettings => Set<UserSetting>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();
    public DbSet<Holiday> Holidays => Set<Holiday>();
    public DbSet<IdempotencyRecord> Idempotency => Set<IdempotencyRecord>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectDiscipline> ProjectDisciplines => Set<ProjectDiscipline>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();
    public DbSet<ProjectStar> ProjectStars => Set<ProjectStar>();
    public DbSet<ExternalParty> ExternalParties => Set<ExternalParty>();
    public DbSet<ProjectHealthSnapshot> HealthSnapshots => Set<ProjectHealthSnapshot>();
    public DbSet<Milestone> Milestones => Set<Milestone>();
    public DbSet<Deliverable> Deliverables => Set<Deliverable>();
    public DbSet<DeliverableIssue> DeliverableIssues => Set<DeliverableIssue>();
    public DbSet<Handoff> Handoffs => Set<Handoff>();
    public DbSet<SourceRevision> SourceRevisions => Set<SourceRevision>();
    public DbSet<HandoffRevision> HandoffRevisions => Set<HandoffRevision>();
    public DbSet<HandoffReceiptEvent> HandoffReceiptEvents => Set<HandoffReceiptEvent>();
    public DbSet<HandoffCommand> HandoffCommands => Set<HandoffCommand>();
    public DbSet<CoordinationCommand> CoordinationCommands => Set<CoordinationCommand>();
    public DbSet<ReviewPackage> ReviewPackages => Set<ReviewPackage>();
    public DbSet<ReviewRound> ReviewRounds => Set<ReviewRound>();
    public DbSet<ReviewManifestItem> ReviewManifestItems => Set<ReviewManifestItem>();
    public DbSet<DisciplineReview> DisciplineReviews => Set<DisciplineReview>();
    public DbSet<ReviewFinding> ReviewFindings => Set<ReviewFinding>();
    public DbSet<FindingEvent> FindingEvents => Set<FindingEvent>();
    public DbSet<SourceHead> SourceHeads => Set<SourceHead>();
    public DbSet<InputUse> InputUses => Set<InputUse>();
    public DbSet<InputAdoption> InputAdoptions => Set<InputAdoption>();
    public DbSet<ChangeNotice> ChangeNotices => Set<ChangeNotice>();
    public DbSet<ChangeAssessment> ChangeAssessments => Set<ChangeAssessment>();
    public DbSet<SubmissionPackage> SubmissionPackages => Set<SubmissionPackage>();
    public DbSet<SubmissionManifestItem> SubmissionManifestItems => Set<SubmissionManifestItem>();
    public DbSet<SubmissionCheck> SubmissionChecks => Set<SubmissionCheck>();
    public DbSet<CheckEvidence> CheckEvidences => Set<CheckEvidence>();
    public DbSet<SubmissionIssue> SubmissionIssues => Set<SubmissionIssue>();
    public DbSet<PersonAvailabilityOverride> AvailabilityOverrides => Set<PersonAvailabilityOverride>();
    public DbSet<ResourceAllocation> Allocations => Set<ResourceAllocation>();
    public DbSet<AllocationDayOverride> AllocationDayOverrides => Set<AllocationDayOverride>();
    public DbSet<AllocationWorkLink> AllocationWorkLinks => Set<AllocationWorkLink>();
    public DbSet<PersonDateVersion> PersonDateVersions => Set<PersonDateVersion>();
    public DbSet<WorkTask> Tasks => Set<WorkTask>();
    public DbSet<TaskDependency> Dependencies => Set<TaskDependency>();
    public DbSet<DeliverableDependency> DeliverableDependencies => Set<DeliverableDependency>();
    public DbSet<TaskCollaborator> Collaborators => Set<TaskCollaborator>();
    public DbSet<ItemWatcher> Watchers => Set<ItemWatcher>();
    public DbSet<TaskTimeEntry> TimeEntries => Set<TaskTimeEntry>();
    public DbSet<BoardOrder> BoardOrders => Set<BoardOrder>();
    public DbSet<Decision> Decisions => Set<Decision>();
    public DbSet<ItemLink> ItemLinks => Set<ItemLink>();
    public DbSet<Risk> Risks => Set<Risk>();
    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<MeetingAction> Actions => Set<MeetingAction>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<CommentMention> Mentions => Set<CommentMention>();
    public DbSet<DocumentLink> DocumentLinks => Set<DocumentLink>();
    public DbSet<ActivityLog> ActivityLog => Set<ActivityLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<EmailMessage> Emails => Set<EmailMessage>();
    public DbSet<ProjectFollow> Follows => Set<ProjectFollow>();
    public DbSet<OutboxEvent> Outbox => Set<OutboxEvent>();
    public DbSet<TaskState> TaskStates => Set<TaskState>();
    public DbSet<DeliverableState> DeliverableStates => Set<DeliverableState>();
    public DbSet<MilestoneState> MilestoneStates => Set<MilestoneState>();
    public DbSet<DecisionState> DecisionStates => Set<DecisionState>();
    public DbSet<ProjectState> ProjectStates => Set<ProjectState>();
    public DbSet<AttentionItem> Attention => Set<AttentionItem>();
    public DbSet<AttentionSnooze> Snoozes => Set<AttentionSnooze>();
    public DbSet<JobRun> JobRuns => Set<JobRun>();
    public DbSet<SavedView> SavedViews => Set<SavedView>();
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceProject> WorkspaceProjects => Set<WorkspaceProject>();
    public DbSet<CalendarEvent> CalendarEvents => Set<CalendarEvent>();
    public DbSet<DashboardLayout> DashboardLayouts => Set<DashboardLayout>();
    public DbSet<ProjectTemplate> Templates => Set<ProjectTemplate>();
    public DbSet<TemplateDiscipline> TemplateDisciplines => Set<TemplateDiscipline>();
    public DbSet<TemplateMilestone> TemplateMilestones => Set<TemplateMilestone>();
    public DbSet<TemplateDeliverable> TemplateDeliverables => Set<TemplateDeliverable>();
    public DbSet<TemplateTask> TemplateTasks => Set<TemplateTask>();
    public DbSet<TemplateDependency> TemplateDependencies => Set<TemplateDependency>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.HasDefaultSchema("hub");
        mb.HasPostgresExtension("citext");
        mb.HasPostgresExtension("pg_trgm");

        mb.Entity<AppUser>(e =>
        {
            e.ToTable("app_user");
            e.Property(x => x.Email).HasColumnType("citext");
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.EntraObjectId).IsUnique();
            e.HasIndex(x => x.SupervisorId);
            e.HasMany(x => x.Roles).WithOne().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.WeeklyCapacityHours).HasPrecision(5, 1);
            e.HasIndex(x => x.DisplayName).HasMethod("gin").HasOperators("gin_trgm_ops");
            e.ToTable(t => t.HasCheckConstraint("ck_app_user_not_own_supervisor", "supervisor_id IS NULL OR supervisor_id <> id"));
        });
        Fk<AppUser, Office>(mb, x => x.OfficeId);
        Fk<AppUser, AppUser>(mb, x => x.SupervisorId);
        mb.Entity<UserSystemRole>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.Role, x.Source }).IsUnique();
            e.ToTable(t => t.HasCheckConstraint("ck_role", $"role IN ({In(SystemRole.All)})"));
        });

        foreach (var t in new[] { typeof(Office), typeof(Discipline), typeof(Client), typeof(ProjectType), typeof(Phase), typeof(DeliverableType) })
            mb.Entity(t).HasIndex(nameof(RefData.Name));
        Fk<DeliverableType, Discipline>(mb, x => x.DefaultDisciplineId);
        mb.Entity<OrgSetting>(e => { e.HasKey(x => x.Key); e.Property(x => x.Value).HasColumnType("jsonb"); });
        mb.Entity<UserSetting>(e => { e.HasKey(x => x.UserId); e.Property(x => x.DigestSectionsOff).HasColumnType("jsonb"); });
        mb.Entity<NotificationPreference>().HasIndex(x => new { x.UserId, x.EventType }).IsUnique();
        Fk<Holiday, Office>(mb, x => x.OfficeId);
        mb.Entity<IdempotencyRecord>(e => e.HasIndex(x => new { x.UserId, x.Key }).IsUnique());

        mb.Entity<Project>(e =>
        {
            e.Property(x => x.ProjectNumber).HasColumnType("citext");
            e.HasIndex(x => x.ProjectNumber).IsUnique();
            e.HasIndex(x => x.ProjectNumber, "ix_project_number_trgm").HasMethod("gin").HasOperators("gin_trgm_ops");
            e.HasIndex(x => x.Name).HasMethod("gin").HasOperators("gin_trgm_ops");
            e.HasIndex(x => x.Status);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_project_status", $"status IN ({In(ProjectStatus.All)})");
                t.HasCheckConstraint("ck_project_priority", $"priority IN ({In(Priority.All)})");
                t.HasCheckConstraint("ck_project_visibility", "visibility IN ('Open','Restricted')");
                t.HasCheckConstraint("ck_project_override", "health_override IS NULL OR health_override IN ('Green','Yellow','Red')");
            });
        });
        Fk<Project, Client>(mb, x => x.ClientId);
        Fk<Project, AppUser>(mb, x => x.ProjectManagerId);
        Fk<Project, Office>(mb, x => x.OfficeId);
        Fk<Project, ProjectType>(mb, x => x.ProjectTypeId);
        Fk<Project, Phase>(mb, x => x.PhaseId);
        Fk<Project, ProjectTemplate>(mb, x => x.CreatedFromTemplateId);
        mb.Entity<ProjectDiscipline>(e =>
        {
            e.HasIndex(x => new { x.ProjectId, x.DisciplineId }).IsUnique();
            e.HasOne(x => x.Discipline).WithMany().HasForeignKey(x => x.DisciplineId).OnDelete(DeleteBehavior.Restrict);
        });
        Fk<ProjectDiscipline, Project>(mb, x => x.ProjectId);
        Fk<ProjectDiscipline, AppUser>(mb, x => x.LeadUserId);
        mb.Entity<ProjectMember>(e =>
        {
            e.HasIndex(x => new { x.ProjectId, x.UserId }).IsUnique().HasFilter("removed_at IS NULL");
            e.HasIndex(x => x.UserId);
            e.ToTable(t => t.HasCheckConstraint("ck_member_roles", $"cardinality(roles) >= 1 AND roles <@ ARRAY[{In(ProjectRole.All)}]::text[]"));
        });
        Fk<ProjectMember, Project>(mb, x => x.ProjectId);
        Fk<ProjectMember, AppUser>(mb, x => x.UserId);
        Fk<ProjectMember, ProjectDiscipline>(mb, x => x.PrimaryDisciplineId);
        mb.Entity<ProjectStar>().HasKey(x => new { x.UserId, x.ProjectId });
        Fk<ExternalParty, Project>(mb, x => x.ProjectId);
        mb.Entity<ProjectHealthSnapshot>(e => { e.HasIndex(x => new { x.ProjectId, x.SnapshotDate }).IsUnique(); e.Property(x => x.Inputs).HasColumnType("jsonb"); e.Property(x => x.Counts).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb"); });
        Fk<ProjectHealthSnapshot, Project>(mb, x => x.ProjectId, DeleteBehavior.Cascade);

        Item<Milestone>(mb, e =>
        {
            e.HasIndex(x => new { x.ProjectId, x.Date });
            e.HasIndex(x => x.Name, "ix_milestone_name_trgm").HasMethod("gin").HasOperators("gin_trgm_ops"); // search (§18.1)
            e.ToTable(t => t.HasCheckConstraint("ck_milestone_type", $"milestone_type IN ({In(MilestoneType.All)})"));
        });
        Fk<Milestone, ProjectDiscipline>(mb, x => x.ProjectDisciplineId);
        Fk<Milestone, Phase>(mb, x => x.CompletesPhaseId);
        Item<Deliverable>(mb, e =>
        {
            e.HasIndex(x => new { x.ProjectId, x.Status });
            e.HasIndex(x => x.Name, "ix_deliverable_name_trgm").HasMethod("gin").HasOperators("gin_trgm_ops");
            e.HasIndex(x => x.Key, "ix_deliverable_key_trgm").HasMethod("gin").HasOperators("gin_trgm_ops");
            e.HasIndex(x => x.Description, "ix_deliverable_description_trgm").HasMethod("gin").HasOperators("gin_trgm_ops"); // search in descriptions (packet 020)
            e.HasIndex(x => x.MilestoneId);
            e.HasIndex(x => new { x.OwnerId, x.Status });
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_deliverable_status", $"status IN ({In(DeliverableStatus.All)})");
                t.HasCheckConstraint("ck_deliverable_dates", "start_date IS NULL OR due_date IS NULL OR start_date <= due_date");
            });
        });
        Fk<Deliverable, ProjectDiscipline>(mb, x => x.ProjectDisciplineId);
        Fk<Deliverable, DeliverableType>(mb, x => x.DeliverableTypeId);
        Fk<Deliverable, AppUser>(mb, x => x.OwnerId);
        Fk<Deliverable, AppUser>(mb, x => x.ReviewerId);
        Fk<Deliverable, Milestone>(mb, x => x.MilestoneId);
        Fk<DeliverableIssue, Deliverable>(mb, x => x.DeliverableId, DeleteBehavior.Cascade);
        Item<WorkTask>(mb, e =>
        {
            // Contains-search on names and keys at 100,000+ tasks (§18.1, packet 011 scale run).
            e.HasIndex(x => x.Name, "ix_task_name_trgm").HasMethod("gin").HasOperators("gin_trgm_ops");
            e.HasIndex(x => x.Key, "ix_task_key_trgm").HasMethod("gin").HasOperators("gin_trgm_ops");
            e.HasIndex(x => x.Description, "ix_task_description_trgm").HasMethod("gin").HasOperators("gin_trgm_ops");
            e.ToTable("task", t =>
            {
                t.HasCheckConstraint("ck_task_status", $"status IN ({In(TaskStatuses.All)})");
                t.HasCheckConstraint("ck_task_parent", "milestone_id IS NULL OR deliverable_id IS NULL");
                t.HasCheckConstraint("ck_task_progress", "progress_pct BETWEEN 0 AND 100 AND progress_pct % 10 = 0");
                t.HasCheckConstraint("ck_task_dates", "start_date IS NULL OR due_date IS NULL OR start_date <= due_date");
            });
            e.HasIndex(x => new { x.ProjectId, x.Status }).HasFilter("deleted_at IS NULL");
            e.HasIndex(x => new { x.AssigneeId, x.Status }).HasFilter("deleted_at IS NULL");
            e.HasIndex(x => new { x.ReviewerId, x.Status });
            e.HasIndex(x => x.DeliverableId);
            e.HasIndex(x => x.CreatedBy);
            e.Property(x => x.EstimatedHours).HasPrecision(7, 1);
        });
        Fk<WorkTask, ProjectDiscipline>(mb, x => x.ProjectDisciplineId);
        Fk<WorkTask, Deliverable>(mb, x => x.DeliverableId);
        Fk<WorkTask, Milestone>(mb, x => x.MilestoneId);
        Fk<WorkTask, AppUser>(mb, x => x.AssigneeId);
        Fk<WorkTask, AppUser>(mb, x => x.ReviewerId);
        mb.Entity<TaskDependency>(e =>
        {
            e.ToTable("task_dependency", t => t.HasCheckConstraint("ck_dependency_self", "predecessor_task_id <> successor_task_id"));
            e.HasIndex(x => new { x.PredecessorTaskId, x.SuccessorTaskId }).IsUnique().HasFilter("deleted_at IS NULL");
            e.HasIndex(x => x.SuccessorTaskId);
            e.HasQueryFilter(x => x.DeletedAt == null);
            e.Ignore(x => x.AuditKey);
        });
        Fk<TaskDependency, WorkTask>(mb, x => x.PredecessorTaskId);
        Fk<TaskDependency, WorkTask>(mb, x => x.SuccessorTaskId);
        mb.Entity<DeliverableDependency>(e =>
        {
            e.ToTable(t => t.HasCheckConstraint("ck_deliverable_dependency_self", "predecessor_deliverable_id <> successor_deliverable_id"));
            e.HasIndex(x => new { x.PredecessorDeliverableId, x.SuccessorDeliverableId }).IsUnique().HasFilter("deleted_at IS NULL");
            e.HasQueryFilter(x => x.DeletedAt == null);
            e.Ignore(x => x.AuditKey);
        });
        Fk<DeliverableDependency, Deliverable>(mb, x => x.PredecessorDeliverableId);
        Fk<DeliverableDependency, Deliverable>(mb, x => x.SuccessorDeliverableId);
        mb.Entity<TaskCollaborator>().HasIndex(x => new { x.TaskId, x.UserId }).IsUnique();
        Fk<TaskCollaborator, WorkTask>(mb, x => x.TaskId, DeleteBehavior.Cascade);
        Fk<TaskCollaborator, AppUser>(mb, x => x.UserId);
        mb.Entity<ItemWatcher>().HasIndex(x => new { x.ItemType, x.ItemId, x.UserId }).IsUnique();
        mb.Entity<TaskTimeEntry>(e =>
        {
            e.Property(x => x.Hours).HasPrecision(5, 2);
            e.HasIndex(x => new { x.UserId, x.WorkDate }).HasFilter("deleted_at IS NULL");
            e.HasIndex(x => new { x.ProjectId, x.WorkDate }).HasFilter("deleted_at IS NULL");
            e.HasIndex(x => new { x.TaskId, x.WorkDate }).HasFilter("deleted_at IS NULL");
            e.HasQueryFilter(x => x.DeletedAt == null);
            e.Ignore(x => x.AuditKey);
            e.ToTable(t => t.HasCheckConstraint("ck_time_hours", "hours > 0 AND hours <= 24"));
        });
        Fk<TaskTimeEntry, WorkTask>(mb, x => x.TaskId);
        Fk<TaskTimeEntry, AppUser>(mb, x => x.UserId);
        mb.Entity<BoardOrder>(e =>
        {
            e.HasIndex(x => new { x.ProjectId, x.TaskId }).IsUnique().HasFilter("project_id IS NOT NULL");
            e.HasIndex(x => new { x.WorkspaceId, x.TaskId }).IsUnique().HasFilter("workspace_id IS NOT NULL");
        });

        Item<Decision>(mb, e =>
        {
            e.HasIndex(x => new { x.ProjectId, x.Status });
            e.HasIndex(x => x.Subject).HasMethod("gin").HasOperators("gin_trgm_ops");
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_decision_owner", "num_nonnulls(owner_user_id, owner_external_party_id) = 1");
                t.HasCheckConstraint("ck_decision_status", $"status IN ({In(DecisionStatus.All)})");
            });
        });
        Fk<Decision, AppUser>(mb, x => x.RequestedById);
        Fk<Decision, AppUser>(mb, x => x.OwnerUserId);
        Fk<Decision, ExternalParty>(mb, x => x.OwnerExternalPartyId);
        mb.Entity<ItemLink>(e =>
        {
            e.HasIndex(x => new { x.SourceType, x.SourceId });
            e.HasIndex(x => new { x.TargetType, x.TargetId });
            e.HasIndex(x => new { x.SourceType, x.SourceId, x.TargetType, x.TargetId, x.Relation }).IsUnique().HasFilter("deleted_at IS NULL");
            e.HasQueryFilter(x => x.DeletedAt == null);
            e.Ignore(x => x.AuditKey);
        });
        Item<Risk>(mb, e => e.ToTable(t =>
        {
            t.HasCheckConstraint("ck_risk_scores", "probability BETWEEN 1 AND 3 AND impact BETWEEN 1 AND 3");
            t.HasCheckConstraint("ck_risk_status", $"status IN ({In(RiskStatus.All)})");
        }));
        Item<Issue>(mb, e => e.ToTable(t => t.HasCheckConstraint("ck_issue_status", $"status IN ({In(IssueStatus.All)})")));
        mb.Entity<Meeting>(e => { e.HasQueryFilter(x => x.DeletedAt == null); e.HasIndex(x => new { x.ProjectId, x.MeetingDate }); });
        Item<MeetingAction>(mb, e => e.ToTable(t => t.HasCheckConstraint("ck_action_status", $"status IN ({In(ActionStatus.All)})")));
        Fk<MeetingAction, Meeting>(mb, x => x.MeetingId);

        mb.Entity<Comment>(e =>
        {
            e.HasIndex(x => new { x.ItemType, x.ItemId, x.CreatedAt });
            e.HasIndex(x => x.Body, "ix_comment_body_trgm").HasMethod("gin").HasOperators("gin_trgm_ops").HasFilter("deleted_at IS NULL"); // search in comments (packet 020)
            e.Ignore(x => x.AuditKey);
        });
        Fk<Comment, AppUser>(mb, x => x.AuthorId);
        Fk<CommentMention, Comment>(mb, x => x.CommentId, DeleteBehavior.Cascade);
        mb.Entity<DocumentLink>(e => { e.HasIndex(x => new { x.ItemType, x.ItemId }); e.HasQueryFilter(x => x.DeletedAt == null); e.Ignore(x => x.AuditKey); });

        mb.Entity<ActivityLog>(e =>
        {
            e.Property(x => x.Changes).HasColumnType("jsonb");
            e.Property(x => x.Snapshot).HasColumnType("jsonb");
            e.HasIndex(x => new { x.ProjectId, x.OccurredAt }).IsDescending(false, true);
            e.HasIndex(x => new { x.ItemType, x.ItemId, x.OccurredAt }).IsDescending(false, false, true);
            e.HasIndex(x => new { x.ActorUserId, x.OccurredAt }).IsDescending(false, true);
            e.HasIndex(x => x.CorrelationId);
        });
        mb.Entity<Notification>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.ReadAt, x.CreatedAt }).IsDescending(false, false, true);
            e.HasIndex(x => new { x.UserId, x.CollapseKey });
        });
        mb.Entity<EmailMessage>().HasIndex(x => x.SentAt);
        mb.Entity<ProjectFollow>(e => { e.HasIndex(x => new { x.UserId, x.ProjectId }).IsUnique(); e.HasIndex(x => new { x.ProjectId, x.Level }); });
        Fk<ProjectFollow, Project>(mb, x => x.ProjectId, DeleteBehavior.Cascade);
        mb.Entity<OutboxEvent>(e => { e.Property(x => x.Payload).HasColumnType("jsonb"); e.HasIndex(x => x.ProcessedAt).HasFilter("processed_at IS NULL"); });

        mb.Entity<TaskState>(e =>
        {
            e.HasKey(x => x.TaskId);
            e.Property(x => x.BlockedBy).HasColumnType("jsonb");
            e.Property(x => x.InconsistencyDetail).HasColumnType("jsonb");
            e.HasIndex(x => new { x.ProjectId, x.IsBlocked });
            e.HasIndex(x => new { x.ProjectId, x.IsOverdue });
            e.HasIndex(x => x.IsBlocking);
        });
        Fk<TaskState, WorkTask>(mb, x => x.TaskId, DeleteBehavior.Cascade);
        mb.Entity<DeliverableState>(e =>
        {
            e.HasKey(x => x.DeliverableId);
            foreach (var p in new[] { nameof(DeliverableState.AtRiskReasons), nameof(DeliverableState.InconsistencyDetail), nameof(DeliverableState.BlockedBy) })
                e.Property(p).HasColumnType("jsonb");
            e.HasIndex(x => x.ProjectId);
        });
        Fk<DeliverableState, Deliverable>(mb, x => x.DeliverableId, DeleteBehavior.Cascade);
        mb.Entity<MilestoneState>(e => { e.HasKey(x => x.MilestoneId); e.Property(x => x.StatusReasons).HasColumnType("jsonb"); e.HasIndex(x => x.ProjectId); });
        Fk<MilestoneState, Milestone>(mb, x => x.MilestoneId, DeleteBehavior.Cascade);
        mb.Entity<DecisionState>(e => { e.HasKey(x => x.DecisionId); e.HasIndex(x => x.ProjectId); });
        Fk<DecisionState, Decision>(mb, x => x.DecisionId, DeleteBehavior.Cascade);
        mb.Entity<ProjectState>(e =>
        {
            e.HasKey(x => x.ProjectId);
            foreach (var p in new[] { nameof(ProjectState.HealthReasons), nameof(ProjectState.Inputs), nameof(ProjectState.Counts), nameof(ProjectState.DisciplineStates) })
                e.Property(p).HasColumnType("jsonb");
        });
        Fk<ProjectState, Project>(mb, x => x.ProjectId, DeleteBehavior.Cascade);
        mb.Entity<AttentionItem>(e =>
        {
            e.Property(x => x.Why).HasColumnType("jsonb");
            e.HasIndex(x => new { x.RuleId, x.ItemType, x.ItemId }).IsUnique();
            e.HasIndex(x => x.ProjectId);
            e.HasIndex(x => x.RouteToUserIds).HasMethod("gin");
        });
        Fk<AttentionItem, Project>(mb, x => x.ProjectId, DeleteBehavior.Cascade);
        mb.Entity<AttentionSnooze>().HasIndex(x => new { x.RuleId, x.ItemType, x.ItemId });
        mb.Entity<JobRun>(e => { e.Property(x => x.Details).HasColumnType("jsonb"); e.HasIndex(x => new { x.JobName, x.StartedAt }); });
        mb.Entity<SavedView>(e => { e.Property(x => x.Filters).HasColumnType("jsonb"); e.Property(x => x.Columns).HasColumnType("jsonb"); e.HasIndex(x => new { x.OwnerId, x.ListType }); });
        mb.Entity<Workspace>(e =>
        {
            e.HasIndex(x => new { x.OwnerId, x.Name }).IsUnique();
            e.HasMany(x => x.Projects).WithOne().HasForeignKey(p => p.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
        });
        mb.Entity<WorkspaceProject>().HasIndex(x => new { x.WorkspaceId, x.ProjectId }).IsUnique();
        Fk<WorkspaceProject, Project>(mb, x => x.ProjectId, DeleteBehavior.Cascade);
        mb.Entity<CalendarEvent>(e =>
        {
            e.HasIndex(x => new { x.ProjectId, x.StartAt });
            e.HasIndex(x => new { x.OwnerId, x.StartAt });
            e.ToTable(t => t.HasCheckConstraint("ck_event_times", "end_at > start_at"));
        });
        mb.Entity<DashboardLayout>(e => { e.HasKey(x => x.UserId); e.Property(x => x.Widgets).HasColumnType("jsonb"); });
        mb.Entity<ProjectTemplate>().HasIndex(x => x.FamilyId);

        // Optimistic concurrency on every mutable entity (G-07).
        Item<Handoff>(mb, e =>
        {
            e.HasIndex(x => new { x.ProjectId, x.Status, x.NeededBy }).HasFilter("deleted_at IS NULL");
            e.HasIndex(x => x.ReceivingOwnerId);
            e.HasIndex(x => x.SendingOwnerId);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_handoff_status", $"status IN ({In(HandoffStatus.All)})");
                t.HasCheckConstraint("ck_handoff_target", "(target_task_id IS NULL) <> (target_deliverable_id IS NULL)");
            });
        });
        Fk<Handoff, Deliverable>(mb, x => x.SourceDeliverableId);
        Fk<Handoff, WorkTask>(mb, x => x.TargetTaskId);
        Fk<Handoff, Deliverable>(mb, x => x.TargetDeliverableId);
        Fk<Handoff, AppUser>(mb, x => x.SendingOwnerId);
        Fk<Handoff, AppUser>(mb, x => x.ReceivingOwnerId);
        Fk<Handoff, ProjectDiscipline>(mb, x => x.SendingDisciplineId);
        Fk<Handoff, ProjectDiscipline>(mb, x => x.ReceivingDisciplineId);
        // CurrentRevisionId is set in the same transaction after its immutable snapshot is inserted.
        Fk<Handoff, HandoffRevision>(mb, x => x.CurrentRevisionId);
        Fk<Handoff, HandoffRevision>(mb, x => x.IncorporatedRevisionId);
        mb.Entity<SourceRevision>().HasIndex(x => new { x.ProjectId, x.IdentityHash }).IsUnique();
        Fk<SourceRevision, Project>(mb, x => x.ProjectId);
        Fk<SourceRevision, Deliverable>(mb, x => x.DeliverableId);
        Fk<HandoffRevision, Handoff>(mb, x => x.HandoffId);
        Fk<HandoffRevision, SourceRevision>(mb, x => x.SourceRevisionId);
        Fk<HandoffRevision, HandoffRevision>(mb, x => x.PreviousRevisionId);
        Fk<HandoffReceiptEvent, Handoff>(mb, x => x.HandoffId);
        Fk<HandoffReceiptEvent, HandoffRevision>(mb, x => x.RevisionId);
        mb.Entity<HandoffReceiptEvent>().HasIndex(x => new { x.HandoffId, x.CreatedAt });
        mb.Entity<HandoffCommand>().HasIndex(x => new { x.ProjectId, x.ActorId, x.RequestId }).IsUnique();
        Fk<HandoffCommand, Handoff>(mb, x => x.HandoffId);

        Item<ReviewPackage>(mb, e => { e.HasIndex(x => new { x.ProjectId, x.Status }); e.ToTable(t => t.HasCheckConstraint("ck_review_package_status", $"status IN ({In(ReviewStatus.All)})")); });
        Item<ChangeNotice>(mb, e => { e.HasIndex(x => new { x.ProjectId, x.Status }); e.ToTable(t => t.HasCheckConstraint("ck_change_notice_status", $"status IN ({In(ChangeStatus.All)})")); });
        Item<SubmissionPackage>(mb, e => { e.HasIndex(x => new { x.ProjectId, x.Status }); e.ToTable(t => t.HasCheckConstraint("ck_submission_package_status", $"status IN ({In(SubmissionStatus.All)})")); });
        mb.Entity<SubmissionManifestItem>().HasIndex(x => new { x.PackageId, x.ManifestVersion, x.DeliverableId }).IsUnique();
        mb.Entity<SubmissionCheck>().HasIndex(x => new { x.PackageId, x.ManifestVersion, x.Kind, x.SourceId }).IsUnique();
        mb.Entity<SubmissionCheck>().HasIndex(x => new { x.PackageId, x.ManifestVersion, x.Kind }).IsUnique().HasFilter("source_id IS NULL");
        mb.Entity<SubmissionCheck>().ToTable(t => { t.HasCheckConstraint("ck_submission_check_status", $"status IN ({In(SubmissionCheckStatus.All)})"); t.HasCheckConstraint("ck_submission_check_kind", $"kind IN ({In(SubmissionCheckKind.All)})"); t.HasCheckConstraint("ck_submission_check_waiver", "status <> 'Not Applicable' OR (kind = 'Applicability' AND required = false AND reason IS NOT NULL AND evidence_url IS NOT NULL AND approved_by IS NOT NULL AND approved_at IS NOT NULL)"); });
        mb.Entity<SubmissionIssue>().HasIndex(x => x.PackageId).IsUnique();
        mb.Entity<SubmissionIssue>().Property(x => x.ManifestSnapshot).HasColumnType("jsonb");
        mb.Entity<SubmissionIssue>().Property(x => x.CheckSnapshot).HasColumnType("jsonb");
        mb.Entity<PersonAvailabilityOverride>(e =>
        {
            e.HasIndex(x => new { x.PersonId, x.WorkDate }).IsUnique();
            e.Property(x => x.AvailableHours).HasPrecision(9, 3);
            e.ToTable(t => { t.HasCheckConstraint("ck_availability_hours", "available_hours >= 0"); t.HasCheckConstraint("ck_availability_category", $"category IN ({In(AvailabilityCategory.All)})"); });
        });
        mb.Entity<ResourceAllocation>(e =>
        {
            e.HasIndex(x => new { x.PersonId, x.FromDate, x.ThroughDate });
            e.HasIndex(x => new { x.ProjectId, x.Status });
            e.Property(x => x.PlannedHours).HasPrecision(9, 3);
            e.Property(x => x.ConfirmationSnapshot).HasColumnType("jsonb");
            e.ToTable(t => { t.HasCheckConstraint("ck_allocation_dates", "through_date >= from_date"); t.HasCheckConstraint("ck_allocation_hours", "planned_hours > 0"); t.HasCheckConstraint("ck_allocation_status", $"status IN ({In(AllocationStatus.All)})"); t.HasCheckConstraint("ck_allocation_purpose", $"purpose IN ({In(AllocationPurpose.All)})"); });
        });
        mb.Entity<AllocationDayOverride>(e =>
        {
            e.HasIndex(x => new { x.AllocationId, x.WorkDate }).IsUnique();
            e.Property(x => x.Hours).HasPrecision(9, 3);
            e.ToTable(t => t.HasCheckConstraint("ck_allocation_day_hours", "hours >= 0"));
        });
        mb.Entity<AllocationWorkLink>(e =>
        {
            e.HasIndex(x => new { x.PersonId, x.WorkType, x.WorkId, x.WorkDate }).IsUnique().HasFilter("released_at IS NULL");
            e.HasIndex(x => x.AllocationId);
            e.Property(x => x.ReviewHours).HasPrecision(9, 3);
            e.ToTable(t => { t.HasCheckConstraint("ck_allocation_work_type", "work_type IN ('Task', 'Review')");
                t.HasCheckConstraint("ck_allocation_review_hours", "(work_type = 'Task' AND review_hours IS NULL) OR (work_type = 'Review' AND ((review_hours IS NOT NULL AND review_hours > 0) OR (released_at IS NOT NULL AND review_hours IS NULL)))"); });
        });
        mb.Entity<PersonDateVersion>().HasIndex(x => new { x.PersonId, x.WorkDate }).IsUnique();
        Fk<PersonAvailabilityOverride, AppUser>(mb, x => x.PersonId);
        Fk<ResourceAllocation, Project>(mb, x => x.ProjectId);
        Fk<ResourceAllocation, AppUser>(mb, x => x.PersonId);
        Fk<ResourceAllocation, AppUser>(mb, x => x.ConfirmedBy);
        Fk<AllocationDayOverride, ResourceAllocation>(mb, x => x.AllocationId);
        Fk<AllocationWorkLink, ResourceAllocation>(mb, x => x.AllocationId);
        Fk<AllocationWorkLink, AppUser>(mb, x => x.PersonId);
        Fk<PersonDateVersion, AppUser>(mb, x => x.PersonId);
        mb.Entity<CoordinationCommand>().HasIndex(x => new { x.ProjectId, x.ActorId, x.RequestId }).IsUnique();
        mb.Entity<ReviewRound>().HasIndex(x => new { x.PackageId, x.Number }).IsUnique();
        mb.Entity<ReviewManifestItem>().HasIndex(x => new { x.RoundId, x.DeliverableId }).IsUnique();
        mb.Entity<DisciplineReview>().HasIndex(x => new { x.RoundId, x.ProjectDisciplineId }).IsUnique();
        mb.Entity<DisciplineReview>().ToTable(t => t.HasCheckConstraint("ck_discipline_review_status", $"status IN ({In(DisciplineReviewStatus.All)})"));
        mb.Entity<ReviewFinding>().HasIndex(x => new { x.RoundId, x.Status });
        mb.Entity<ReviewFinding>().ToTable(t => { t.HasCheckConstraint("ck_review_finding_status", $"status IN ({In(FindingStatus.All)})"); t.HasCheckConstraint("ck_review_finding_severity", "severity IN ('Blocking', 'Advisory')"); });
        mb.Entity<SourceHead>().HasIndex(x => new { x.ProjectId, x.Identity }).IsUnique();
        mb.Entity<InputUse>().HasIndex(x => new { x.ProjectId, x.TargetType, x.TargetId, x.SourceIdentity }).IsUnique();
        mb.Entity<InputUse>().ToTable(t => t.HasCheckConstraint("ck_input_use_type", "target_type IN ('Task', 'Deliverable')"));
        mb.Entity<ChangeAssessment>().HasIndex(x => new { x.ChangeNoticeId, x.TargetType, x.TargetId }).IsUnique();
        mb.Entity<ChangeAssessment>().ToTable(t => t.HasCheckConstraint("ck_change_assessment_status", $"status IN ({In(AssessmentStatus.All)})"));
        mb.Entity<ChangeNotice>().HasIndex(x => x.NewRevisionId).IsUnique();
        Fk<ReviewPackage, ReviewRound>(mb, x => x.CurrentRoundId);
        Fk<ReviewPackage, AppUser>(mb, x => x.CoordinatorId);
        Fk<ReviewPackage, ProjectDiscipline>(mb, x => x.ProjectDisciplineId);
        Fk<Deliverable, ReviewPackage>(mb, x => x.RequiredReviewPackageId);
        Fk<ReviewRound, ReviewPackage>(mb, x => x.PackageId);
        Fk<ReviewManifestItem, ReviewRound>(mb, x => x.RoundId);
        Fk<ReviewManifestItem, SourceRevision>(mb, x => x.SourceRevisionId);
        Fk<ReviewManifestItem, Deliverable>(mb, x => x.DeliverableId);
        Fk<DisciplineReview, ReviewRound>(mb, x => x.RoundId);
        Fk<DisciplineReview, AppUser>(mb, x => x.ReviewerId);
        Fk<DisciplineReview, ProjectDiscipline>(mb, x => x.ProjectDisciplineId);
        Fk<ReviewFinding, ReviewPackage>(mb, x => x.PackageId);
        Fk<ReviewFinding, ReviewRound>(mb, x => x.RoundId);
        Fk<ReviewFinding, ReviewFinding>(mb, x => x.CarriedFromId);
        Fk<ReviewFinding, SourceRevision>(mb, x => x.SourceRevisionId);
        Fk<ReviewFinding, AppUser>(mb, x => x.ResolverId);
        Fk<ReviewFinding, AppUser>(mb, x => x.VerifierId);
        Fk<FindingEvent, ReviewFinding>(mb, x => x.FindingId);
        Fk<SourceRevision, SourceRevision>(mb, x => x.SupersedesId);
        Fk<SourceHead, SourceRevision>(mb, x => x.CurrentRevisionId);
        Fk<InputUse, SourceRevision>(mb, x => x.SourceRevisionId);
        Fk<InputAdoption, InputUse>(mb, x => x.InputUseId);
        Fk<InputAdoption, SourceRevision>(mb, x => x.SourceRevisionId);
        Fk<ChangeNotice, SourceRevision>(mb, x => x.OldRevisionId);
        Fk<ChangeNotice, SourceRevision>(mb, x => x.NewRevisionId);
        Fk<ChangeNotice, AppUser>(mb, x => x.OwnerId);
        Fk<ChangeAssessment, ChangeNotice>(mb, x => x.ChangeNoticeId);
        Fk<ChangeAssessment, InputUse>(mb, x => x.InputUseId);
        Fk<ChangeAssessment, Handoff>(mb, x => x.HandoffId);
        Fk<ChangeAssessment, SourceRevision>(mb, x => x.RevisionUsedId);
        Fk<ChangeAssessment, WorkTask>(mb, x => x.CorrectionTaskId);
        Fk<ChangeAssessment, AppUser>(mb, x => x.OwnerId);
        Fk<ChangeAssessment, AppUser>(mb, x => x.ReviewerId);
        Fk<SubmissionPackage, AppUser>(mb, x => x.CoordinatorId);
        Fk<SubmissionPackage, Milestone>(mb, x => x.MilestoneId);
        Fk<SubmissionPackage, SubmissionPackage>(mb, x => x.SupersedesPackageId);
        Fk<SubmissionManifestItem, SubmissionPackage>(mb, x => x.PackageId);
        Fk<SubmissionManifestItem, Deliverable>(mb, x => x.DeliverableId);
        Fk<SubmissionManifestItem, SourceRevision>(mb, x => x.SourceRevisionId);
        Fk<SubmissionManifestItem, ReviewRound>(mb, x => x.ReviewRoundId);
        Fk<SubmissionCheck, SubmissionPackage>(mb, x => x.PackageId);
        Fk<SubmissionCheck, AppUser>(mb, x => x.OwnerId);
        Fk<SubmissionCheck, ProjectDiscipline>(mb, x => x.ProjectDisciplineId);
        Fk<SubmissionCheck, AppUser>(mb, x => x.ApprovedBy);
        Fk<CheckEvidence, SubmissionCheck>(mb, x => x.CheckId);
        Fk<SubmissionIssue, SubmissionPackage>(mb, x => x.PackageId);
        Fk<SubmissionIssue, AppUser>(mb, x => x.AuthorisedBy);
        Fk<SourceHead, AppUser>(mb, x => x.OwnerId);
        Fk<SourceHead, ProjectDiscipline>(mb, x => x.ProjectDisciplineId);
        Fk<InputUse, AppUser>(mb, x => x.OwnerId);
        Fk<ReviewFinding, AppUser>(mb, x => x.OriginatorId);
        Fk<ReviewFinding, ProjectDiscipline>(mb, x => x.ProjectDisciplineId);
        Fk<ChangeNotice, ProjectDiscipline>(mb, x => x.ProjectDisciplineId);
        Fk<CoordinationCommand, Project>(mb, x => x.ProjectId);
        Fk<ReviewRound, Project>(mb, x => x.ProjectId);
        Fk<ReviewManifestItem, Project>(mb, x => x.ProjectId);
        Fk<DisciplineReview, Project>(mb, x => x.ProjectId);
        Fk<ReviewFinding, Project>(mb, x => x.ProjectId);
        Fk<FindingEvent, Project>(mb, x => x.ProjectId);
        Fk<SourceHead, Project>(mb, x => x.ProjectId);
        Fk<InputUse, Project>(mb, x => x.ProjectId);
        Fk<InputAdoption, Project>(mb, x => x.ProjectId);
        Fk<ChangeAssessment, Project>(mb, x => x.ProjectId);
        Fk<SubmissionManifestItem, Project>(mb, x => x.ProjectId);
        Fk<SubmissionCheck, Project>(mb, x => x.ProjectId);
        Fk<CheckEvidence, Project>(mb, x => x.ProjectId);
        Fk<SubmissionIssue, Project>(mb, x => x.ProjectId);


        foreach (var et in mb.Model.GetEntityTypes().Where(t => typeof(Audited).IsAssignableFrom(t.ClrType)))
            et.FindProperty(nameof(Audited.RowVersion))!.IsConcurrencyToken = true;

        // snake_case names everywhere (§24.1)
        foreach (var et in mb.Model.GetEntityTypes())
        {
            et.SetTableName(et.ClrType == typeof(WorkTask) ? "task" : Snake(et.ClrType.Name));
            foreach (var p in et.GetProperties()) p.SetColumnName(Snake(p.Name));
            foreach (var k in et.GetKeys()) k.SetName(Snake(k.GetName()!));
            foreach (var fk in et.GetForeignKeys()) fk.SetConstraintName(Snake(fk.GetConstraintName()!));
            foreach (var ix in et.GetIndexes()) ix.SetDatabaseName(Snake(ix.GetDatabaseName()!));
        }
    }

    static void Item<T>(ModelBuilder mb, Action<EntityTypeBuilder<T>> more) where T : ProjectItem
    {
        mb.Entity<T>(e =>
        {
            e.HasIndex(x => new { x.ProjectId, x.Seq }).IsUnique();
            e.HasIndex(x => x.Key);
            e.HasQueryFilter(x => x.DeletedAt == null);
            e.Property(x => x.RowVersion).IsConcurrencyToken();
            more(e);
        });
        Fk<T, Project>(mb, x => x.ProjectId);
    }

    static void Fk<TDep, TPrin>(ModelBuilder mb, Expression<Func<TDep, object?>> fk, DeleteBehavior del = DeleteBehavior.Restrict)
        where TDep : class where TPrin : class
        => mb.Entity<TDep>().HasOne<TPrin>().WithMany().HasForeignKey(fk).OnDelete(del);

    static string In(IEnumerable<string> v) => string.Join(",", v.Select(s => $"'{s.Replace("'", "''")}'"));

    public static string Snake(string s)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (char.IsUpper(c) && i > 0 && (char.IsLower(s[i - 1]) || (i + 1 < s.Length && char.IsLower(s[i + 1]) && char.IsUpper(s[i - 1]))) && s[i - 1] != '_')
                sb.Append('_');
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    /// Records an event that is not a row change: sign-ins, exports, reviews, cascades (§20.1).
    public ActivityLog LogEvent(string itemType, Guid? itemId, string action, string category, Guid? projectId = null, string? key = null,
        string? name = null, object? changes = null, string? reason = null, Guid? disciplineId = null, string? snapshot = null)
    {
        var row = new ActivityLog
        {
            OccurredAt = clock.GetUtcNow(), ActorUserId = audit.ActorType == "System" ? null : audit.ActorId, ActorType = audit.ActorType,
            ProjectId = projectId, ProjectDisciplineId = disciplineId, ItemType = itemType, ItemId = itemId, ItemKey = key, ItemName = name,
            Action = action, Categories = [category], Changes = JsonSerializer.Serialize(changes ?? Array.Empty<object>(), JsonOpts.Web),
            Reason = reason, CorrelationId = audit.CorrelationId, Source = audit.Source, Snapshot = snapshot,
        };
        ActivityLog.Add(row);
        return row;
    }

    /// MTG-03: an action converted to a task follows the task's completion, whichever path changed the task (panel,
    /// board, bulk edit), in the same save and logged with the same actor.
    async Task<bool> FollowTasks(DateTimeOffset now, CancellationToken ct)
    {
        var moved = ChangeTracker.Entries<WorkTask>().Where(e => e.State == EntityState.Modified && e.Property(x => x.Status).IsModified)
            .ToDictionary(e => e.Entity.Id, e => e.Entity.Status);
        if (moved.Count == 0) return false;
        var ids = moved.Keys.ToList();
        var links = await ItemLinks.Where(l => l.Relation == ItemRelation.ConvertedToTask && ids.Contains(l.TargetId)).Select(l => new { l.SourceId, l.TargetId }).ToListAsync(ct);
        if (links.Count == 0) return false;
        var bySource = links.ToDictionary(l => l.SourceId, l => l.TargetId);
        var sourceIds = bySource.Keys.ToList();
        var changed = false;
        foreach (var a in await Actions.Where(a => sourceIds.Contains(a.Id)).ToListAsync(ct))
        {
            var next = Workflow.ActionFollowing(moved[bySource[a.Id]]);
            if (a.Status == next) continue;
            (a.Status, a.StatusChangedAt, a.LastActivityAt, changed) = (next, now, now, true);
        }
        return changed;
    }

    // ---------- Audited save (§20.3, §20.4, G-07, G-10) ----------

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        ChangeTracker.DetectChanges();
        var authorship = ChangeTracker.Entries().Where(e =>
            e.Entity is WorkTask && (e.State == EntityState.Added || e.State == EntityState.Modified && (e.Property("AssigneeId").IsModified || e.Property("DeliverableId").IsModified))
            || e.Entity is Deliverable && (e.State == EntityState.Added || e.State == EntityState.Modified && e.Property("OwnerId").IsModified)).ToList();
        if (authorship.Count > 0) {
            if (Database.CurrentTransaction == null) return await Tx.Run(this, () => SaveChangesAsync(ct));
            foreach (var projectId in authorship.Select(e => ((ProjectItem)e.Entity).ProjectId).Distinct().Order()) await Coordination.Lock(this, projectId);
            var settings = await Settings.FirstOrDefaultAsync(x => x.Key == "allow_self_review", ct);
            var allowSelf = settings != null && JsonSerializer.Deserialize<bool>(settings.Value);
            if (!allowSelf) foreach (var e in authorship) {
                var deliverableId = e.Entity is WorkTask t ? t.DeliverableId : ((Deliverable)e.Entity).Id;
                if (deliverableId == null) continue;
                var owners = e.Entity is WorkTask task ? new[] { task.AssigneeId, task.CreatedBy ?? (e.State == EntityState.Added ? audit.ActorId : null) } : new[] { ((Deliverable)e.Entity).OwnerId, ((Deliverable)e.Entity).CreatedBy ?? (e.State == EntityState.Added ? audit.ActorId : null) };
                var ids = owners.OfType<Guid>().ToArray();
                Check.That(!await DisciplineReviews.AnyAsync(a => ids.Contains(a.ReviewerId)
                    && ReviewPackages.Any(p => p.CurrentRoundId == a.RoundId && p.Status != ReviewStatus.Cancelled && p.Status != ReviewStatus.Superseded)
                    && ReviewManifestItems.Any(m => m.RoundId == a.RoundId && m.DeliverableId == deliverableId), ct), "ownerId", "review.independent");
            }
        }
        var now = clock.GetUtcNow();
        if (await FollowTasks(now, ct)) ChangeTracker.DetectChanges();
        var projects = new HashSet<Guid>();
        var users = new HashSet<Guid>();
        foreach (var entry in ChangeTracker.Entries().ToList())
        {
            if (entry.State is EntityState.Unchanged or EntityState.Detached) continue;
            if (entry.State is EntityState.Modified or EntityState.Deleted
                && entry.Entity is SourceRevision or HandoffRevision or HandoffReceiptEvent or HandoffCommand or CoordinationCommand or ReviewManifestItem or FindingEvent or InputAdoption)
                throw new InvalidOperationException("Published coordination evidence is immutable.");
            if (entry.Entity is Audited a)
            {
                if (entry.State == EntityState.Added) { a.CreatedAt = now; a.CreatedBy ??= audit.ActorId; }
                if (entry.State is EntityState.Added or EntityState.Modified) { a.UpdatedAt = now; a.UpdatedBy = audit.ActorId; }
                if (entry.State == EntityState.Modified) a.RowVersion++;
            }
            if (entry.Entity is IAuditable item && AuditRules.Fields.TryGetValue(entry.Metadata.ClrType, out var fields))
            {
                var row = BuildLog(entry, item, fields, now);
                if (row is not null) ActivityLog.Add(row);
            }
            if (AuditRules.AffectsEvaluation(entry.Entity) is { } pid) projects.Add(pid);
            if (entry.Entity is AppUser u && entry.State == EntityState.Modified
                && (entry.Property(nameof(AppUser.IsActive)).IsModified || entry.Property(nameof(AppUser.SupervisorId)).IsModified))
                users.Add(u.Id);
        }
        foreach (var pid in projects)
            Outbox.Add(new OutboxEvent { EventType = "ProjectChanged", ProjectId = pid, CreatedAt = now, Payload = JsonSerializer.Serialize(new { correlationId = audit.CorrelationId }) });
        foreach (var uid in users)
            Outbox.Add(new OutboxEvent { EventType = "UserChanged", CreatedAt = now, Payload = JsonSerializer.Serialize(new { userId = uid }) });
        var n = await base.SaveChangesAsync(ct);
        audit.Clear();
        if (projects.Count > 0 || users.Count > 0) signal.Poke();
        return n;
    }

    ActivityLog? BuildLog(EntityEntry entry, IAuditable item, string[] fields, DateTimeOffset now)
    {
        var note = audit.NoteFor(entry.Entity);
        var changes = new List<object>();
        var cats = new SortedSet<string>();
        string action;
        string? snapshot = null;
        object? V(string f) => entry.Metadata.FindProperty(f) is null ? null : entry.Property(f).CurrentValue;

        switch (entry.State)
        {
            case EntityState.Added when entry.Entity is Comment c:
                action = "Commented"; // the text is conversation, not history (C-08); only a Review round is recorded
                if (c.CommentKind == CommentKind.Review) changes.Add(new { field = nameof(Comment.ReviewRound), old = (object?)null, @new = Json(c.ReviewRound) });
                break;
            case EntityState.Added:
                action = "Created";
                cats.Add("creation");
                foreach (var f in fields.Where(f => f != nameof(ISoftDeletable.DeletedAt) && V(f) is not null && V(f) is not false))
                    changes.Add(new { field = f, old = (object?)null, @new = Json(V(f), entry.Entity is OrgSetting) });
                break;
            case EntityState.Deleted:
                action = "Removed";
                cats.Add("deletion");
                snapshot = JsonSerializer.Serialize(fields.ToDictionary(f => f, f => Json(V(f))));
                break;
            default:
                foreach (var f in fields)
                {
                    var p = entry.Metadata.FindProperty(f) is null ? null : entry.Property(f);
                    if (p is null || !p.IsModified || Equivalent(p.OriginalValue, p.CurrentValue)) continue;
                    var jt = entry.Entity is OrgSetting;
                    changes.Add(new { field = f, old = Json(p.OriginalValue, jt), @new = Json(p.CurrentValue, jt) });
                    if (AuditRules.Category(f) is { } c) cats.Add(c);
                }
                if (changes.Count == 0 && note is null) return null;
                var deleted = entry.Metadata.FindProperty("DeletedAt") is not null && entry.Property("DeletedAt").IsModified;
                if (deleted && entry.Property("DeletedAt").CurrentValue is not null)
                {
                    action = "Deleted";
                    snapshot = JsonSerializer.Serialize(fields.Where(f => f != "DeletedAt").ToDictionary(f => f, f => Json(entry.Property(f).OriginalValue)));
                }
                else if (deleted) action = "Restored";
                else if (cats.Contains("status")) action = "StatusChanged";
                else if (cats.Contains("assignment")) action = "Assigned";
                else if (cats.Contains("date")) action = "DateChanged";
                else action = "Updated";
                break;
        }
        if (AuditRules.TypeCategory(item) is { } tc) { if (tc == "admin") cats.Clear(); cats.Add(tc); }
        if (note?.Categories is { } extra) foreach (var c in extra) cats.Add(c);
        if (note?.Snapshot is { } snap) snapshot = snap;
        return new ActivityLog
        {
            OccurredAt = now,
            ActorUserId = audit.ActorType == "System" ? null : audit.ActorId,
            ActorType = audit.ActorType,
            ProjectId = item.AuditProjectId,
            ProjectDisciplineId = item.AuditDisciplineId,
            ItemType = item.AuditType,
            ItemId = entry.Entity is Entity en ? en.Id : null,
            ItemKey = note?.Key ?? item.AuditKey,
            ItemName = item.AuditName,
            Action = note?.Action ?? action,
            Categories = [.. cats],
            Changes = JsonSerializer.Serialize(changes),
            Reason = note?.Reason ?? audit.Reason,
            CorrelationId = audit.CorrelationId,
            Source = audit.Source,
            Snapshot = snapshot,
        };
    }

    static bool Equivalent(object? a, object? b) => a is System.Collections.IEnumerable ea and not string && b is System.Collections.IEnumerable eb and not string
        ? ea.Cast<object>().SequenceEqual(eb.Cast<object>())
        : Equals(a, b);

    // Setting values are stored as JSON text; everything else is logged as its plain value.
    static object? Json(object? v, bool jsonText = false) => v switch
    {
        null => null,
        DateOnly d => d.ToString("yyyy-MM-dd"),
        DateTimeOffset t => t.ToString("O"),
        string s when jsonText => JsonDocument.Parse(s).RootElement.Clone(),
        _ => v,
    };
}
