using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using Hub.Api.Infrastructure;
using Hub.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Hub.Api.Data;

public sealed class HubDb(DbContextOptions<HubDb> options, AuditContext audit, TimeProvider clock, EvaluationSignal signal, SettingsStore settings, IConfiguration configuration) : DbContext(options)
{
    internal bool initializingAuditChains;
    // Save hooks construct notifiers over this same scoped context; all must join its atomic bulk capture.
    internal Notifier.TaskBulkCapture? TaskBulkNotifications { get; set; }
    internal Notifier.PendingCapture? PendingNoticeCapture { get; set; }
    internal string NoticeEmailBaseUrl => (configuration["Email:BaseUrl"] ?? "").TrimEnd('/');

    // Save-hook notifiers share the originating command's raw capture, never their own transaction.
    public Task FlushNotices(CancellationToken ct = default)
        => new Notifier(this, audit, settings, clock, configuration).FlushPending(ct);

    public void DiscardNotices() => PendingNoticeCapture = null;

    public AuditContext Audit => audit;

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<LocalAuthSession> LocalAuthSessions => Set<LocalAuthSession>();
    public DbSet<ExternalIdentity> ExternalIdentities => Set<ExternalIdentity>();
    public DbSet<UserSystemRole> UserRoles => Set<UserSystemRole>();
    public DbSet<Office> Offices => Set<Office>();
    public DbSet<Discipline> Disciplines => Set<Discipline>();
    public DbSet<Client> Clients => Set<Client>();
    public DbSet<ProjectType> ProjectTypes => Set<ProjectType>();
    public DbSet<Phase> Phases => Set<Phase>();
    public DbSet<DeliverableType> DeliverableTypes => Set<DeliverableType>();
    public DbSet<DeliverableReviewDefault> DeliverableReviewDefaults => Set<DeliverableReviewDefault>();
    public DbSet<IssuePurpose> IssuePurposes => Set<IssuePurpose>();
    public DbSet<IssueReturnCode> IssueReturnCodes => Set<IssueReturnCode>();
    public DbSet<DecisionCategory> DecisionCategories => Set<DecisionCategory>();
    public DbSet<IssueRootCauseCategory> IssueRootCauseCategories => Set<IssueRootCauseCategory>();
    public DbSet<CertificationTag> CertificationTags => Set<CertificationTag>();
    public DbSet<UserCertification> UserCertifications => Set<UserCertification>();
    public DbSet<OrgSetting> Settings => Set<OrgSetting>();
    public DbSet<UserSetting> UserSettings => Set<UserSetting>();
    public DbSet<WorkAssignmentOccurrence> WorkAssignmentOccurrences => Set<WorkAssignmentOccurrence>();
    public DbSet<WorkSeenAcknowledgement> WorkSeenAcknowledgements => Set<WorkSeenAcknowledgement>();
    public DbSet<WorkSeenCommandReceipt> WorkSeenCommandReceipts => Set<WorkSeenCommandReceipt>();
    public DbSet<TaskBulkCommandReceipt> TaskBulkCommandReceipts => Set<TaskBulkCommandReceipt>();
    public DbSet<RouteCount> RouteCounts => Set<RouteCount>();
    public DbSet<TemplateRole> TemplateRoles => Set<TemplateRole>();
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
    public DbSet<ExternalReview> ExternalReviews => Set<ExternalReview>();
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
    public DbSet<DesignBasisEntry> DesignBasisEntries => Set<DesignBasisEntry>();
    public DbSet<DesignBasisVersion> DesignBasisVersions => Set<DesignBasisVersion>();
    public DbSet<BasisUse> BasisUses => Set<BasisUse>();
    public DbSet<BasisConflict> BasisConflicts => Set<BasisConflict>();
    public DbSet<BasisAssumptionDisposition> BasisAssumptionDispositions => Set<BasisAssumptionDisposition>();
    public DbSet<BasisImpactAssessment> BasisImpactAssessments => Set<BasisImpactAssessment>();
    public DbSet<ReadinessAssessment> ReadinessAssessments => Set<ReadinessAssessment>();
    public DbSet<ReadinessCheckRecord> ReadinessChecks => Set<ReadinessCheckRecord>();
    public DbSet<WorkConstraint> WorkConstraints => Set<WorkConstraint>();
    public DbSet<ReadinessException> ReadinessExceptions => Set<ReadinessException>();
    public DbSet<WeeklyPlanSnapshot> WeeklyPlanSnapshots => Set<WeeklyPlanSnapshot>();
    public DbSet<OutputCommitment> OutputCommitments => Set<OutputCommitment>();
    public DbSet<OutputCommitmentEvent> OutputCommitmentEvents => Set<OutputCommitmentEvent>();
    public DbSet<ReadinessSubmissionPrerequisite> ReadinessSubmissionPrerequisites => Set<ReadinessSubmissionPrerequisite>();
    public DbSet<TaskStartAuthorisation> TaskStartAuthorisations => Set<TaskStartAuthorisation>();
    public DbSet<PersonAvailabilityOverride> AvailabilityOverrides => Set<PersonAvailabilityOverride>();
    public DbSet<ResourceAllocation> Allocations => Set<ResourceAllocation>();
    public DbSet<OpenRole> OpenRoles => Set<OpenRole>();
    public DbSet<AllocationDayOverride> AllocationDayOverrides => Set<AllocationDayOverride>();
    public DbSet<AllocationWorkLink> AllocationWorkLinks => Set<AllocationWorkLink>();
    public DbSet<PersonDateVersion> PersonDateVersions => Set<PersonDateVersion>();
    public DbSet<LeaveImportPreview> LeaveImportPreviews => Set<LeaveImportPreview>();
    public DbSet<ApprovedLeave> ApprovedLeaves => Set<ApprovedLeave>();
    public DbSet<LeaveImportState> LeaveImportStates => Set<LeaveImportState>();
    public DbSet<PersonSchedule> PersonSchedules => Set<PersonSchedule>();
    public DbSet<CalendarSubscription> CalendarSubscriptions => Set<CalendarSubscription>();
    public DbSet<CalendarFeedItem> CalendarFeedItems => Set<CalendarFeedItem>();
    public DbSet<PlanningEntry> PlanningEntries => Set<PlanningEntry>();
    public DbSet<WorkTask> Tasks => Set<WorkTask>();
    public DbSet<TaskDependency> Dependencies => Set<TaskDependency>();
    public DbSet<DeliverableDependency> DeliverableDependencies => Set<DeliverableDependency>();
    public DbSet<TaskCollaborator> Collaborators => Set<TaskCollaborator>();
    public DbSet<TaskContribution> TaskContributions => Set<TaskContribution>();
    public DbSet<ItemWatcher> Watchers => Set<ItemWatcher>();
    public DbSet<TaskTimeEntry> TimeEntries => Set<TaskTimeEntry>();
    public DbSet<BoardOrder> BoardOrders => Set<BoardOrder>();
    public DbSet<Decision> Decisions => Set<Decision>();
    public DbSet<ItemLink> ItemLinks => Set<ItemLink>();
    public DbSet<Risk> Risks => Set<Risk>();
    public DbSet<Issue> Issues => Set<Issue>();
    public DbSet<IssueLocation> IssueLocations => Set<IssueLocation>();
    public DbSet<IssueDocumentReference> IssueDocumentReferences => Set<IssueDocumentReference>();
    public DbSet<IssueVerification> IssueVerifications => Set<IssueVerification>();
    public DbSet<IssueAffectedDiscipline> IssueAffectedDisciplines => Set<IssueAffectedDiscipline>();
    public DbSet<IssueReferenceImpactAssessment> IssueReferenceImpactAssessments => Set<IssueReferenceImpactAssessment>();
    public DbSet<Meeting> Meetings => Set<Meeting>();
    public DbSet<MeetingAction> Actions => Set<MeetingAction>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<CommentMention> Mentions => Set<CommentMention>();
    public DbSet<DocumentLink> DocumentLinks => Set<DocumentLink>();
    public DbSet<ActivityLog> ActivityLog => Set<ActivityLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<EmailMessage> Emails => Set<EmailMessage>();
    public DbSet<NoticeDeliveryIntent> NoticeDeliveryIntents => Set<NoticeDeliveryIntent>();
    public DbSet<NoticeDeliverySlot> NoticeDeliverySlots => Set<NoticeDeliverySlot>();
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
    public DbSet<TemplateDesignBasis> TemplateDesignBases => Set<TemplateDesignBasis>();
    public DbSet<TemplateMilestone> TemplateMilestones => Set<TemplateMilestone>();
    public DbSet<TemplateDeliverable> TemplateDeliverables => Set<TemplateDeliverable>();
    public DbSet<TemplateTask> TemplateTasks => Set<TemplateTask>();
    public DbSet<TemplateRisk> TemplateRisks => Set<TemplateRisk>();
    public DbSet<TemplateDependency> TemplateDependencies => Set<TemplateDependency>();

    protected override void OnModelCreating(ModelBuilder mb)
    {
        mb.HasDefaultSchema("hub");
        mb.HasPostgresExtension("citext");
        mb.HasPostgresExtension("pg_trgm");

        mb.Entity<DeliverableReviewDefault>(e =>
        {
            e.HasKey(x => new { x.DeliverableTypeId, x.DisciplineId });
            e.HasOne<DeliverableType>().WithMany().HasForeignKey(x => x.DeliverableTypeId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Discipline>().WithMany().HasForeignKey(x => x.DisciplineId).OnDelete(DeleteBehavior.Restrict);
        });

        mb.Entity<LocalAuthSession>().HasIndex(x => x.ExpiresAt);
        mb.Entity<ExternalIdentity>(e =>
        {
            e.HasKey(x => new { x.Provider, x.Subject });
            e.Property(x => x.Provider).HasMaxLength(20);
            e.Property(x => x.Subject).HasMaxLength(200);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x => new { x.UserId, x.Provider }).IsUnique();
        });

        mb.Entity<AppUser>(e =>
        {
            e.ToTable("app_user");
            e.Property(x => x.Email).HasColumnType("citext");
            e.HasIndex(x => x.Email).IsUnique();
            e.HasIndex(x => x.EntraObjectId).IsUnique();
            e.HasIndex(x => x.SupervisorId);
            e.HasIndex(x => new { x.HrSourceSystem, x.HrPersonKey }).IsUnique();
            e.ToTable(t => t.HasCheckConstraint("ck_hr_identity_pair", "(hr_source_system IS NULL) = (hr_person_key IS NULL)"));
            e.HasMany(x => x.Roles).WithOne().HasForeignKey(r => r.UserId).OnDelete(DeleteBehavior.Cascade);
            e.Property(x => x.WeeklyCapacityHours).HasPrecision(5, 1);
            e.HasIndex(x => x.DisplayName).HasMethod("gin").HasOperators("gin_trgm_ops");
            e.ToTable(t => t.HasCheckConstraint("ck_app_user_not_own_supervisor", "supervisor_id IS NULL OR supervisor_id <> id"));
            e.Property(x => x.PersonType).HasMaxLength(20);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_app_user_person_type", "person_type IS NULL OR person_type IN ('Employee', 'Co-op', 'Contractor')");
                t.HasCheckConstraint("ck_app_user_person_dates", "person_start_date IS NULL OR person_end_date IS NULL OR person_start_date <= person_end_date");
            });
        });
        Fk<AppUser, Office>(mb, x => x.OfficeId);
        Fk<AppUser, AppUser>(mb, x => x.SupervisorId);
        Fk<AppUser, Discipline>(mb, x => x.HomeDisciplineId);
        mb.Entity<UserSystemRole>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.Role, x.Source }).IsUnique();
            e.ToTable(t => t.HasCheckConstraint("ck_role", $"role IN ({In(SystemRole.All)})"));
        });

        foreach (var t in new[] { typeof(Office), typeof(Discipline), typeof(Client), typeof(ProjectType), typeof(Phase), typeof(DeliverableType), typeof(IssuePurpose), typeof(IssueReturnCode), typeof(DecisionCategory) })
            mb.Entity(t).HasIndex(nameof(RefData.Name));
        mb.Entity<DecisionCategory>().Property(x => x.Name).HasMaxLength(120);
        mb.Entity<DecisionCategory>(e =>
        {
            e.Property(x => x.Name).HasColumnType("citext");
            e.HasIndex(x => x.Name).IsUnique();
            e.ToTable(t => t.HasCheckConstraint("ck_decision_category_name", "name = btrim(name) AND char_length(name) BETWEEN 1 AND 120 AND name !~ '[[:cntrl:]]'"));
        });
        mb.Entity<IssueRootCauseCategory>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120).HasColumnType("citext");
            e.HasIndex(x => x.Name).IsUnique();
            e.ToTable(t => t.HasCheckConstraint("ck_issue_root_cause_category_name", "name = btrim(name) AND char_length(name) BETWEEN 1 AND 120 AND name !~ '[[:cntrl:]]'"));
        });
        mb.Entity<IssuePurpose>().Property(x => x.Name).HasMaxLength(120);
        mb.Entity<IssueReturnCode>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120);
            e.Property(x => x.Code).HasColumnType("citext").HasMaxLength(10);
            e.HasIndex(x => x.Code).IsUnique();
            e.ToTable(t => t.HasCheckConstraint("ck_issue_return_code", "code = btrim(code) AND char_length(code) BETWEEN 1 AND 10"));
        });
        mb.Entity<Project>().Property(x => x.ClientDrawingNumberPattern).HasMaxLength(120);
        Fk<DeliverableType, Discipline>(mb, x => x.DefaultDisciplineId);
        mb.Entity<CertificationTag>(e =>
        {
            e.Property(x => x.Name).HasColumnType("citext").HasMaxLength(120);
            e.HasIndex(x => x.Name).IsUnique();
            e.ToTable(t => t.HasCheckConstraint("ck_certification_tag_name", "name = btrim(name) AND char_length(name) BETWEEN 1 AND 120"));
        });
        mb.Entity<UserCertification>().HasIndex(x => new { x.UserId, x.CertificationTagId }).IsUnique();
        Fk<UserCertification, AppUser>(mb, x => x.UserId);
        Fk<UserCertification, CertificationTag>(mb, x => x.CertificationTagId);
        mb.Entity<OrgSetting>(e => { e.HasKey(x => x.Key); e.Property(x => x.Value).HasColumnType("jsonb"); });
        mb.Entity<RouteCount>(e => { e.HasIndex(x => new { x.Day, x.RouteKey }).IsUnique(); e.Property(x => x.RouteKey).HasMaxLength(100); });
        mb.Entity<TemplateRole>(e => { e.Property(x => x.RoleLabel).HasMaxLength(80); e.Property(x => x.Note).HasMaxLength(2000); e.Property(x => x.PlannedHours).HasPrecision(9, 3); });
        Fk<TemplateRole, ProjectTemplate>(mb, x => x.TemplateId, DeleteBehavior.Cascade);
        Fk<TemplateRole, TemplateDiscipline>(mb, x => x.TemplateDisciplineId);
        Fk<TemplateRole, TemplateMilestone>(mb, x => x.StartMilestoneId);
        Fk<TemplateRole, TemplateMilestone>(mb, x => x.EndMilestoneId);
        mb.Entity<UserSetting>(e => { e.HasKey(x => x.UserId); e.Property(x => x.DigestSectionsOff).HasColumnType("jsonb"); e.Property(x => x.HoldDigestWhenAway).HasDefaultValue(true); });
        // This revision is deliberately not an EF concurrency token: unrelated preference writers own other columns.
        mb.Entity<UserSetting>().Property(x => x.MyWorkSeenRevision).HasDefaultValue(0);
        mb.Entity<UserSetting>(e =>
        {
            e.Property(x => x.TimeReminderWeekday).HasConversion<short>().HasColumnType("smallint");
            e.ToTable(t => t.HasCheckConstraint("ck_notice_preferences",
                $"(notice_digest_frequency IS NULL OR notice_digest_frequency IN ({In(NoticeDigestFrequency.All.Order(StringComparer.Ordinal))})) "
                + "AND (notice_preferences_revision IS NULL OR notice_preferences_revision >= 0) "
                + "AND (time_reminder_weekday IS NULL OR time_reminder_weekday BETWEEN 1 AND 7) "
                + "AND (time_reminder_time_local IS NULL OR time_reminder_time_local ~ '^([01][0-9]|2[0-3]):[0-5][0-9]$') "
                + "AND (NOT coalesce(time_reminder_enabled, false) OR (time_reminder_weekday IS NOT NULL AND time_reminder_time_local IS NOT NULL))"));
        });
        mb.Entity<WorkAssignmentOccurrence>(e =>
        {
            e.HasKey(x => x.Token);
            e.HasAlternateKey(x => new { x.Token, x.RecipientId });
            e.Property(x => x.ItemType).HasMaxLength(40);
            e.Property(x => x.Role).HasMaxLength(32);
            e.Property(x => x.Provenance).HasMaxLength(20);
            e.HasIndex(x => new { x.ItemType, x.ItemId, x.Role, x.SourceVersion }).IsUnique().IsDescending(false, false, false, true);
            e.HasIndex(x => new { x.SourceActivityId, x.Role }).IsUnique().HasFilter("source_activity_id IS NOT NULL");
            e.HasIndex(x => new { x.RecipientId, x.Token });
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.RecipientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Project>().WithMany().HasForeignKey(x => x.ProjectId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ActivityLog>().WithMany().HasForeignKey(x => x.SourceActivityId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_work_assignment_source_version", "source_version >= 0");
                t.HasCheckConstraint("ck_work_assignment_provenance", "(provenance = 'Published' AND source_activity_id IS NOT NULL AND source_occurred_at IS NOT NULL) OR (provenance = 'BaselineSeed' AND source_activity_id IS NULL AND source_occurred_at IS NULL)");
                t.HasCheckConstraint("ck_work_assignment_role", "(item_type = 'Task' AND role IN ('Assignee', 'Reviewer')) OR (item_type = 'Deliverable' AND role IN ('Owner', 'Reviewer')) OR (item_type IN ('Decision', 'Action') AND role = 'InternalOwner') OR (item_type IN ('Risk', 'Issue', 'ChangeNotice') AND role = 'Owner') OR (item_type = 'Handoff' AND role IN ('SendingOwner', 'ReceivingOwner')) OR (item_type = 'Project' AND role = 'ProjectManager')");
            });
        });
        mb.Entity<WorkSeenAcknowledgement>(e =>
        {
            e.HasKey(x => new { x.RecipientId, x.Token });
            e.Property(x => x.Provenance).HasMaxLength(20);
            e.HasOne<WorkAssignmentOccurrence>().WithMany().HasForeignKey(x => new { x.Token, x.RecipientId })
                .HasPrincipalKey(x => new { x.Token, x.RecipientId }).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("ck_work_seen_ack_provenance", "provenance IN ('InitialBaseline', 'Opened', 'MarkAll')"));
        });
        mb.Entity<WorkSeenCommandReceipt>(e =>
        {
            e.HasKey(x => new { x.UserId, x.RequestId });
            e.Property(x => x.Action).HasMaxLength(20);
            e.Property(x => x.PayloadHash).HasMaxLength(64);
            e.Property(x => x.ResponseJson).HasColumnType("jsonb");
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_work_seen_command_action", "action IN ('Visit', 'Opened', 'MarkAll')");
                t.HasCheckConstraint("ck_work_seen_command_hash", "payload_hash ~ '^[0-9a-f]{64}$'");
                t.HasCheckConstraint("ck_work_seen_command_response", "jsonb_typeof(response_json) = 'object'");
            });
        });
        mb.Entity<TaskBulkCommandReceipt>(e =>
        {
            e.HasIndex(x => new { x.ActorId, x.RequestId }).IsUnique();
            e.Property(x => x.IntentHash).HasMaxLength(64);
            e.Property(x => x.IntentJson).HasColumnType("jsonb");
            e.Property(x => x.ResponseJson).HasColumnType("jsonb");
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_task_bulk_command_hash", "hash_version = 1 AND intent_hash ~ '^[0-9a-f]{64}$'");
                t.HasCheckConstraint("ck_task_bulk_command_json", "jsonb_typeof(intent_json) = 'object' AND jsonb_typeof(response_json) = 'object'");
                t.HasCheckConstraint("ck_task_bulk_command_completed", "completed_at >= created_at");
            });
        });
        mb.Entity<NotificationPreference>().HasIndex(x => new { x.UserId, x.EventType }).IsUnique();
        Fk<Holiday, Office>(mb, x => x.OfficeId);
        mb.Entity<AdminReview>(e => { e.HasIndex(x => x.ReviewedAt); e.Property(x => x.Note).HasMaxLength(4000); e.Property(x => x.Checks).HasColumnType("jsonb"); });
        Fk<AdminReview, AppUser>(mb, x => x.ReviewerId);
        mb.Entity<IdempotencyRecord>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.Key }).IsUnique();
            e.Property(x => x.CalendarViewScope).HasMaxLength(8);
            e.ToTable(t => t.HasCheckConstraint("ck_idempotency_calendar_context",
                "(calendar_view_scope IS NULL AND calendar_view_project_id IS NULL) OR (calendar_view_scope IS NOT NULL AND calendar_view_scope IN ('Personal', 'Project') AND (calendar_view_scope = 'Personal' OR calendar_view_project_id IS NOT NULL))"));
        });

        mb.Entity<Project>(e =>
        {
            e.Property(x => x.IdentityColour).HasMaxLength(7);
            e.Property(x => x.StatusNote).HasMaxLength(500);
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
                t.HasCheckConstraint("ck_project_identity_colour", $"identity_colour IS NULL OR lower(identity_colour) IN ({In(IdentityColour.Palette.Select(x => x.ToLowerInvariant()).ToArray())})");
            });
        });
        Fk<Project, AppUser>(mb, x => x.StatusNoteBy);
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
            e.Property(x => x.RowVersion).IsConcurrencyToken();
        });
        Fk<ProjectDiscipline, Project>(mb, x => x.ProjectId);
        Fk<ProjectDiscipline, AppUser>(mb, x => x.LeadUserId);
        mb.Entity<ProjectMember>(e =>
        {
            e.Property(x => x.ResponsibilityNote).HasMaxLength(80);
            e.HasIndex(x => new { x.ProjectId, x.UserId }).IsUnique().HasFilter("removed_at IS NULL");
            e.HasIndex(x => x.UserId);
            e.ToTable(t => t.HasCheckConstraint("ck_member_roles", $"cardinality(roles) >= 1 AND roles <@ ARRAY[{In(ProjectRole.All)}]::text[]"));
            e.Property(x => x.RowVersion).IsConcurrencyToken();
        });
        Fk<ProjectMember, Project>(mb, x => x.ProjectId);
        Fk<ProjectMember, AppUser>(mb, x => x.UserId);
        Fk<ProjectMember, ProjectDiscipline>(mb, x => x.PrimaryDisciplineId);
        mb.Entity<ProjectStar>().HasKey(x => new { x.UserId, x.ProjectId });
        Fk<ExternalParty, Project>(mb, x => x.ProjectId);
        mb.Entity<ProjectHealthSnapshot>(e => { e.HasIndex(x => new { x.ProjectId, x.SnapshotDate }).IsUnique(); e.Property(x => x.Inputs).HasColumnType("jsonb"); e.Property(x => x.Counts).HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb"); e.Property(x => x.HomeMetrics).HasColumnType("jsonb"); });
        Fk<ProjectHealthSnapshot, Project>(mb, x => x.ProjectId, DeleteBehavior.Cascade);

        Item<Milestone>(mb, e =>
        {
            e.Property(x => x.BaselineDate).HasColumnType("date");
            e.HasIndex(x => new { x.ProjectId, x.Date });
            e.HasIndex(x => x.Name, "ix_milestone_name_trgm").HasMethod("gin").HasOperators("gin_trgm_ops"); // search (§18.1)
            e.ToTable(t => t.HasCheckConstraint("ck_milestone_type", $"milestone_type IN ({In(MilestoneType.All)})"));
        });
        Fk<Milestone, ProjectDiscipline>(mb, x => x.ProjectDisciplineId);
        Fk<Milestone, Phase>(mb, x => x.CompletesPhaseId);
        Item<Deliverable>(mb, e =>
        {
            e.Property(x => x.ClientDrawingNumber).HasMaxLength(120);
            e.Property(x => x.BaselineStartDate).HasColumnType("date");
            e.Property(x => x.BaselineDueDate).HasColumnType("date");
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
        mb.Entity<DeliverableIssue>(e =>
        {
            e.Property(x => x.RowVersion).IsConcurrencyToken();
            e.Property(x => x.IssuedAuthorIds).HasColumnType("uuid[]");
            e.Property(x => x.PurposeLabelSnapshot).HasMaxLength(120);
            e.Property(x => x.ReturnCodeSnapshot).HasMaxLength(10);
            e.Property(x => x.ReturnLabelSnapshot).HasMaxLength(120);
            e.HasIndex(x => new { x.ProjectId, x.DeliverableId, x.CreatedAt });
            e.ToTable(t => {
                t.HasCheckConstraint("ck_deliverable_issue_response_pair", "(return_code_id IS NULL) = (response_date IS NULL)");
                t.HasCheckConstraint("ck_deliverable_issue_authors", "issued_author_ids IS NULL OR (array_ndims(issued_author_ids) = 1 AND cardinality(issued_author_ids) > 0 AND array_position(issued_author_ids, NULL) IS NULL AND NOT ('00000000-0000-0000-0000-000000000000'::uuid = ANY(issued_author_ids)))");
            });
        });
        Fk<DeliverableIssue, IssuePurpose>(mb, x => x.PurposeOfIssueId);
        Fk<DeliverableIssue, IssueReturnCode>(mb, x => x.ReturnCodeId);
        Fk<DeliverableIssue, AppUser>(mb, x => x.ResponseRecordedBy);
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
            e.ToTable("task_dependency", t =>
            {
                t.HasCheckConstraint("ck_dependency_self", "predecessor_task_id <> successor_task_id");
                t.HasCheckConstraint("ck_task_dependency_type", $"dependency_type IN ({In(DependencyTypes.All)})");
            });
            e.HasIndex(x => new { x.PredecessorTaskId, x.SuccessorTaskId }).IsUnique().HasFilter("deleted_at IS NULL");
            e.HasIndex(x => x.SuccessorTaskId);
            e.HasQueryFilter(x => x.DeletedAt == null);
            e.Ignore(x => x.AuditKey);
        });
        Fk<TaskDependency, WorkTask>(mb, x => x.PredecessorTaskId);
        Fk<TaskDependency, WorkTask>(mb, x => x.SuccessorTaskId);
        mb.Entity<DeliverableDependency>(e =>
        {
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_deliverable_dependency_self", "predecessor_deliverable_id <> successor_deliverable_id");
                t.HasCheckConstraint("ck_deliverable_dependency_type", $"dependency_type IN ({In(DependencyTypes.All)})");
            });
            e.Property(x => x.DependencyType).HasDefaultValue(DependencyTypes.FinishToStart);
            e.HasIndex(x => new { x.PredecessorDeliverableId, x.SuccessorDeliverableId }).IsUnique().HasFilter("deleted_at IS NULL");
            e.HasQueryFilter(x => x.DeletedAt == null);
            e.Ignore(x => x.AuditKey);
        });
        Fk<DeliverableDependency, Deliverable>(mb, x => x.PredecessorDeliverableId);
        Fk<DeliverableDependency, Deliverable>(mb, x => x.SuccessorDeliverableId);
        mb.Entity<TaskCollaborator>().HasIndex(x => new { x.TaskId, x.UserId }).IsUnique();
        mb.Entity<TaskCollaborator>().HasAlternateKey(x => new { x.Id, x.TaskId, x.UserId });
        mb.Entity<WorkTask>().HasAlternateKey(x => new { x.Id, x.ProjectId });
        mb.Entity<TaskContribution>(e =>
        {
            e.HasIndex(x => x.CollaboratorId).IsUnique();
            e.Property(x => x.Note).HasMaxLength(200);
            e.HasOne<TaskCollaborator>().WithMany()
                .HasForeignKey(x => new { x.CollaboratorId, x.TaskId, x.UserId })
                .HasPrincipalKey(x => new { x.Id, x.TaskId, x.UserId }).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<WorkTask>().WithMany().HasForeignKey(x => new { x.TaskId, x.ProjectId })
                .HasPrincipalKey(x => new { x.Id, x.ProjectId }).OnDelete(DeleteBehavior.NoAction);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_task_contribution_version", "row_version >= 1");
                t.HasCheckConstraint("ck_task_contribution_note", "marked_at IS NOT NULL OR note IS NULL");
            });
            e.Ignore(x => x.AuditType); e.Ignore(x => x.AuditProjectId);
            e.Ignore(x => x.AuditKey); e.Ignore(x => x.AuditName);
        });
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
        Fk<Decision, DecisionCategory>(mb, x => x.CategoryId);
        mb.Entity<Decision>(e =>
        {
            e.Property(x => x.OptionsConsidered).HasColumnType("character varying(500)[]");
            e.Property(x => x.Rationale).HasMaxLength(2000);
            e.ToTable(t => t.HasCheckConstraint("ck_decision_options_considered", "options_considered IS NULL OR (cardinality(options_considered) <= 5 AND array_position(options_considered, NULL) IS NULL)"));
        });
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
        Item<Issue>(mb, e => e.ToTable(t =>
        {
            t.HasCheckConstraint("ck_issue_status", $"status IN ({In(IssueStatus.All)})");
            t.HasCheckConstraint("ck_issue_type", $"issue_type IN ({In(IssueType.All)})");
            t.HasCheckConstraint("ck_issue_root_cause_pair", "(root_cause_category_id IS NULL AND root_cause_label_snapshot IS NULL) OR (root_cause_category_id IS NOT NULL AND root_cause_label_snapshot IS NOT NULL AND btrim(root_cause_label_snapshot) <> '')");
        }));
        mb.Entity<Issue>().Property(x => x.RootCauseLabelSnapshot).HasMaxLength(120);
        Fk<Issue, IssueRootCauseCategory>(mb, x => x.RootCauseCategoryId);
        Fk<Risk, TemplateRisk>(mb, x => x.TemplateRiskId);
        mb.Entity<IssueLocation>(e =>
        {
            e.HasIndex(x => new { x.IssueId, x.CreatedAt });
            e.Property(x => x.StartStation).HasPrecision(18, 6);
            e.Property(x => x.EndStation).HasPrecision(18, 6);
            e.Property(x => x.CoordinateX).HasPrecision(18, 6);
            e.Property(x => x.CoordinateY).HasPrecision(18, 6);
            e.Property(x => x.CoordinateZ).HasPrecision(18, 6);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_issue_location_kind", $"kind IN ({In(Hub.Domain.Registers.IssueLocationKinds)})");
                t.HasCheckConstraint("ck_issue_location_station_order", "end_station IS NULL OR start_station IS NULL OR end_station >= start_station");
            });
        });
        mb.Entity<IssueDocumentReference>(e =>
        {
            e.HasIndex(x => new { x.IssueId, x.Identifier, x.Revision }).IsUnique();
            e.ToTable(t => t.HasCheckConstraint("ck_issue_document_kind", $"kind IN ({In(Hub.Domain.Registers.IssueDocumentKinds)})"));
        });
        mb.Entity<IssueVerification>(e =>
        {
            e.HasIndex(x => new { x.IssueId, x.CreatedAt });
            e.ToTable(t => t.HasCheckConstraint("ck_issue_verification_status", $"status IN ({In(IssueVerificationStatus.All)})"));
        });
        mb.Entity<IssueAffectedDiscipline>(e => e.HasIndex(x => new { x.IssueId, x.ProjectDisciplineId }).IsUnique());
        mb.Entity<IssueReferenceImpactAssessment>(e =>
        {
            e.HasIndex(x => new { x.IssueId, x.DocumentReferenceId, x.PreviousRevisionId, x.CurrentRevisionId }).IsUnique();
            e.ToTable(t => t.HasCheckConstraint("ck_issue_reference_impact_status", $"status IN ({In(IssueReferenceImpactStatus.All)})"));
        });
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
        mb.Entity<DocumentLink>(e => {
            e.HasIndex(x => new { x.ItemType, x.ItemId }); e.HasQueryFilter(x => x.DeletedAt == null);
            e.Ignore(x => x.AuditKey); e.Property(x => x.RowVersion).IsConcurrencyToken();
            e.Property(x => x.SourceSystem).HasMaxLength(200);
            e.Property(x => x.StableSourceId).HasMaxLength(200);
            e.Property(x => x.DeclaredRevision).HasMaxLength(50);
            e.ToTable(t => t.HasCheckConstraint("ck_document_link_reference_registration",
                "(source_system IS NULL AND stable_source_id IS NULL AND declared_revision IS NULL AND reference_registered_by IS NULL AND reference_registered_at IS NULL AND reference_deliverable_id IS NULL) OR " +
                "(source_system IS NOT NULL AND btrim(source_system) <> '' AND reference_registered_by IS NOT NULL AND reference_registered_at IS NOT NULL " +
                "AND (stable_source_id IS NULL OR btrim(stable_source_id) <> '') AND (declared_revision IS NULL OR btrim(declared_revision) <> ''))"));
        });
        Fk<DocumentLink, AppUser>(mb, x => x.ReferenceRegisteredBy);
        Fk<DocumentLink, Deliverable>(mb, x => x.ReferenceDeliverableId);

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
        mb.Entity<NoticeDeliveryIntent>(e =>
        {
            e.Property(x => x.BulkPolicyJson).HasColumnType("jsonb");
            e.Property(x => x.PolicyChecksInDay).HasConversion<short>().HasColumnType("smallint");
            e.HasIndex(x => new { x.UserId, x.OccurrenceKey }).IsUnique();
            e.HasIndex(x => new { x.NotificationId, x.NotificationCountAtCapture }).IsUnique()
                .HasFilter("notification_id IS NOT NULL AND notification_count_at_capture IS NOT NULL");
            e.HasIndex(x => new { x.UserId, x.SuppressionKey, x.SuppressionAnchorAt });
            e.HasIndex(x => new { x.UserId, x.Disposition, x.CreatedAt });
            e.HasIndex(x => new { x.UserId, x.CreatedAt }); // Bounded original bulk-domain lookback.
            e.HasIndex(x => x.DeliveryEmailId);
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Notification>().WithMany().HasForeignKey(x => x.NotificationId).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<EmailMessage>().WithMany().HasForeignKey(x => x.DeliveryEmailId).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("ck_notice_intent",
                $"disposition IN ({In(NoticeIntentDisposition.All.Order(StringComparer.Ordinal))}) "
                + "AND btrim(occurrence_key) <> '' AND btrim(event_type) <> '' AND btrim(suppression_key) <> '' "
                + "AND ((item_type IS NULL) = (item_id IS NULL)) "
                + "AND (notification_count_at_capture IS NULL OR notification_count_at_capture > 0) "
                + "AND (notification_id IS NULL OR notification_count_at_capture IS NOT NULL) "
                + "AND (policy_checks_in_day IS NULL OR policy_checks_in_day BETWEEN 0 AND 8) "
                + "AND ((policy_check_local_date IS NULL) = (policy_checks_in_day IS NULL)) "
                + "AND (next_policy_check_at IS NULL OR policy_check_local_date IS NOT NULL) "
                + "AND (disposition <> 'Mixed' OR bulk_policy_json IS NOT NULL) "
                + "AND (bulk_policy_json IS NULL OR (jsonb_typeof(bulk_policy_json) = 'object' "
                + "AND bulk_policy_json ? 'origin' AND bulk_policy_json ? 'progress' "
                + "AND jsonb_typeof(bulk_policy_json->'origin') = 'object' AND jsonb_typeof(bulk_policy_json->'progress') = 'array')) "
                + "AND (disposition NOT IN ('HeldAway','HeldNoDigest','Queued','ReservedForDigest','Accepted','Simulated','Unknown','Uncertain','Exhausted') "
                + "OR suppression_anchor_at IS NOT NULL)"));
        });
        mb.Entity<NoticeDeliverySlot>(e =>
        {
            e.Property(x => x.CompositionVersion).HasConversion<short>().HasColumnType("smallint");
            e.Property(x => x.ExternalAttempts).HasConversion<short>().HasColumnType("smallint");
            e.HasIndex(x => new { x.UserId, x.Kind, x.LocalDate }).IsUnique();
            e.HasIndex(x => new { x.UserId, x.State, x.LocalDate });
            e.HasOne<AppUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Notification>().WithMany().HasForeignKey(x => x.ReminderNotificationId).OnDelete(DeleteBehavior.SetNull);
            e.HasAlternateKey(x => new { x.Id, x.UserId, x.Kind });
            e.HasOne<NoticeDeliverySlot>().WithMany()
                .HasForeignKey(x => new { x.CarryForwardToSlotId, x.UserId, x.Kind })
                .HasPrincipalKey(x => new { x.Id, x.UserId, x.Kind }).OnDelete(DeleteBehavior.Restrict);
            e.ToTable(t => t.HasCheckConstraint("ck_notice_slot",
                $"kind IN ({In(NoticeSlotKind.All.Order(StringComparer.Ordinal))}) AND state IN ({In(NoticeSlotState.All.Order(StringComparer.Ordinal))}) "
                + "AND btrim(org_time_zone_id) <> '' AND scheduled_local_time ~ '^([01][0-9]|2[0-3]):[0-5][0-9]$' "
                + "AND ((kind = 'Digest' AND frequency_at_creation IS NOT NULL "
                + $"AND frequency_at_creation IN ({In(NoticeDigestFrequency.All.Order(StringComparer.Ordinal))}) "
                + "AND window_from IS NOT NULL AND window_through IS NOT NULL AND window_from <= window_through "
                + "AND reminder_notification_id IS NULL AND composition_version BETWEEN 0 AND 3 AND external_attempts BETWEEN 0 AND 8 AND state <> 'Emitted') "
                + "OR (kind = 'TimeReminder' AND frequency_at_creation IS NULL AND window_from IS NULL AND window_through IS NULL "
                + "AND composition_version = 0 AND external_attempts = 0 AND state IN ('Ready','Emitted'))) "
                + "AND (carry_forward_to_slot_id IS NULL OR (kind = 'Digest' AND carry_forward_to_slot_id <> id))"));
        });
        mb.Entity<EmailMessage>(e =>
        {
            e.HasIndex(x => x.SentAt);
            e.Property(x => x.NoticeProtocolVersion).HasConversion<short>().HasColumnType("smallint");
            e.Property(x => x.NoticeCompositionVersion).HasConversion<short>().HasColumnType("smallint");
            e.Property(x => x.BulkRenderedMembershipJson).HasColumnType("jsonb");
            e.Property(x => x.NoticeDigestPolicyJson).HasColumnType("jsonb");
            e.HasOne<NoticeDeliverySlot>().WithMany().HasForeignKey(x => x.NoticeSlotId).OnDelete(DeleteBehavior.Restrict);
            e.HasIndex(x => new { x.NoticeSlotId, x.NoticeCompositionVersion }).IsUnique()
                .HasFilter("notice_slot_id IS NOT NULL AND notice_composition_version IS NOT NULL");
            e.ToTable(t => t.HasCheckConstraint("ck_notice_email_protocol",
                "(notice_protocol_version IS NULL AND notice_slot_id IS NULL AND notice_composition_version IS NULL "
                + "AND rendered_occurrence_ids IS NULL AND bulk_rendered_membership_json IS NULL AND notice_digest_policy_json IS NULL "
                + "AND notice_claim_token IS NULL AND notice_claim_version IS NULL AND notice_claimed_at IS NULL "
                + "AND transport_kind IS NULL AND transport_outcome IS NULL AND transport_observed_at IS NULL "
                + "AND provider_accepted_at IS NULL AND acceptance_uncertain_at IS NULL) OR "
                + "(notice_protocol_version IS NOT NULL AND notice_protocol_version = 1 AND user_id IS NOT NULL "
                + "AND rendered_occurrence_ids IS NOT NULL AND attempts BETWEEN 0 AND 8 "
                + "AND ((kind IN ('Immediate','PlanningEntryChanged','PlanningWithdrawal') AND notice_slot_id IS NULL AND notice_composition_version IS NULL AND notice_digest_policy_json IS NULL AND cardinality(rendered_occurrence_ids) = 1) "
                + "OR (kind = 'Digest' AND notice_slot_id IS NOT NULL AND notice_composition_version IS NOT NULL AND notice_composition_version BETWEEN 1 AND 3)) "
                + "AND ((notice_claim_token IS NULL AND notice_claim_version IS NULL AND notice_claimed_at IS NULL) "
                + "OR (notice_claim_token IS NOT NULL AND notice_claim_version IS NOT NULL AND notice_claim_version >= 1 AND notice_claimed_at IS NOT NULL)) "
                + $"AND (transport_kind IS NULL OR transport_kind IN ({In(NoticeTransportKind.All.Order(StringComparer.Ordinal))})) "
                + $"AND (transport_outcome IS NULL OR transport_outcome IN ({In(NoticeTransportOutcome.All.Order(StringComparer.Ordinal))})) "
                + "AND (notice_claim_token IS NULL OR (transport_kind IS NOT NULL AND transport_outcome IS NOT NULL AND transport_observed_at IS NOT NULL)) "
                + "AND (provider_accepted_at IS NULL OR (notice_claim_token IS NOT NULL AND transport_kind IS NOT NULL AND transport_kind IN ('Smtp','Graph') "
                + "AND transport_outcome IS NOT NULL AND transport_outcome = 'Accepted' AND transport_observed_at IS NOT NULL)) "
                + "AND (transport_outcome IS DISTINCT FROM 'Accepted' OR provider_accepted_at IS NOT NULL) "
                + "AND (bulk_rendered_membership_json IS NULL OR jsonb_typeof(bulk_rendered_membership_json) = 'array') "
                + "AND (notice_digest_policy_json IS NULL OR (kind = 'Digest' AND jsonb_typeof(notice_digest_policy_json) = 'object')))"));
        });
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
            e.Property(x => x.RowVersion).IsConcurrencyToken();
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
        mb.Entity<ProjectTemplate>(e =>
        {
            e.HasIndex(x => x.FamilyId);
            e.Property(x => x.CopiedFromSnapshot).HasColumnType("jsonb");
            e.ToTable(t => t.HasCheckConstraint("ck_template_copy_baseline_pair", "(copied_from_template_id IS NULL) = (copied_from_snapshot IS NULL)"));
            e.Property(x => x.OffsetDayKind).HasDefaultValue(DayKind.CalendarDays);
            e.ToTable(t => t.HasCheckConstraint("ck_template_offset_day_kind", $"offset_day_kind IN ({In(DayKind.All)})"));
        });
        Fk<ProjectTemplate, ProjectTemplate>(mb, x => x.CopiedFromTemplateId);
        mb.Entity<TemplateRisk>(e =>
        {
            e.Property(x => x.Title).HasMaxLength(300);
            e.Property(x => x.Mitigation).HasMaxLength(4000);
            e.Property(x => x.TriggerIndicator).HasMaxLength(2000);
            e.HasIndex(x => new { x.TemplateId, x.SortOrder });
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_template_risk_title", "char_length(btrim(title)) BETWEEN 1 AND 300");
                t.HasCheckConstraint("ck_template_risk_scores", "probability BETWEEN 1 AND 3 AND impact BETWEEN 1 AND 3");
            });
        });
        Fk<TemplateRisk, ProjectTemplate>(mb, x => x.TemplateId, DeleteBehavior.Cascade);
        mb.Entity<TemplateDesignBasis>(e =>
        {
            e.HasIndex(x => new { x.TemplateId, x.TemplateDisciplineId });
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_template_basis_kind", $"kind IN ({In(BasisKind.All)})");
                t.HasCheckConstraint("ck_template_basis_numeric_units", "numeric_value IS NULL OR (units IS NOT NULL AND length(trim(units)) > 0)");
            });
        });
        Fk<TemplateDesignBasis, ProjectTemplate>(mb, x => x.TemplateId);
        Fk<TemplateDesignBasis, TemplateDiscipline>(mb, x => x.TemplateDisciplineId);

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
        Item<DesignBasisEntry>(mb, e =>
        {
            e.HasIndex(x => new { x.ProjectId, x.ProjectDisciplineId, x.Kind, x.Title });
            e.ToTable(t => t.HasCheckConstraint("ck_basis_entry_kind", $"kind IN ({In(BasisKind.All)})"));
        });
        mb.Entity<DesignBasisVersion>(e =>
        {
            e.HasIndex(x => new { x.EntryId, x.Number }).IsUnique();
            e.HasIndex(x => new { x.ProjectId, x.Status });
            e.Property(x => x.NumericValue).HasPrecision(18, 6);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_basis_version_status", $"status IN ({In(BasisStatus.All)})");
                t.HasCheckConstraint("ck_basis_numeric_units", "status NOT IN ('Confirmed', 'Superseded') OR numeric_value IS NULL OR (units IS NOT NULL AND length(trim(units)) > 0)");
                t.HasCheckConstraint("ck_basis_confirmed_evidence", "status <> 'Confirmed' OR (source_url IS NOT NULL AND confirmation_rationale IS NOT NULL AND confirmed_by IS NOT NULL AND confirmed_at IS NOT NULL)");
                t.HasCheckConstraint("ck_basis_version_number", "number > 0");
            });
        });
        mb.Entity<BasisUse>(e =>
        {
            e.HasIndex(x => new { x.VersionId, x.TargetType, x.TargetId }).IsUnique();
            e.ToTable(t => t.HasCheckConstraint("ck_basis_use_target", "target_type IN ('Task', 'Deliverable')"));
        });
        mb.Entity<BasisConflict>(e =>
        {
            e.HasIndex(x => new { x.LeftVersionId, x.RightVersionId }).IsUnique();
            e.ToTable(t => t.HasCheckConstraint("ck_basis_conflict_order", "left_version_id < right_version_id"));
        });
        mb.Entity<BasisAssumptionDisposition>(e => e.HasIndex(x => new { x.VersionId, x.ExpiresOn }));
        mb.Entity<BasisImpactAssessment>(e =>
        {
            e.HasIndex(x => new { x.BasisUseId, x.NewVersionId }).IsUnique();
            e.HasIndex(x => new { x.ProjectId, x.Status });
            e.ToTable(t => t.HasCheckConstraint("ck_basis_impact_status", $"status IN ({In(AssessmentStatus.All)})"));
        });
        Fk<DesignBasisEntry, AppUser>(mb, x => x.OwnerId);
        Fk<DesignBasisEntry, ProjectDiscipline>(mb, x => x.ProjectDisciplineId);
        Fk<DesignBasisEntry, AppUser>(mb, x => x.IndependentApproverId);
        Fk<DesignBasisEntry, DesignBasisVersion>(mb, x => x.CurrentVersionId);
        Fk<DesignBasisVersion, DesignBasisEntry>(mb, x => x.EntryId);
        Fk<DesignBasisVersion, DesignBasisVersion>(mb, x => x.SupersedesVersionId);
        Fk<DesignBasisVersion, Decision>(mb, x => x.DecisionId);
        Fk<DesignBasisVersion, AppUser>(mb, x => x.ConfirmedBy);
        Fk<BasisUse, DesignBasisVersion>(mb, x => x.VersionId);
        Fk<BasisUse, AppUser>(mb, x => x.OwnerId);
        Fk<BasisConflict, DesignBasisVersion>(mb, x => x.LeftVersionId);
        Fk<BasisConflict, DesignBasisVersion>(mb, x => x.RightVersionId);
        Fk<BasisConflict, DesignBasisVersion>(mb, x => x.ResolutionVersionId);
        Fk<BasisConflict, AppUser>(mb, x => x.ResolvedBy);
        Fk<BasisAssumptionDisposition, DesignBasisVersion>(mb, x => x.VersionId);
        Fk<BasisAssumptionDisposition, AppUser>(mb, x => x.OwnerId);
        Fk<BasisAssumptionDisposition, AppUser>(mb, x => x.ApprovedBy);
        Fk<BasisImpactAssessment, BasisUse>(mb, x => x.BasisUseId);
        Fk<BasisImpactAssessment, DesignBasisVersion>(mb, x => x.OldVersionId);
        Fk<BasisImpactAssessment, DesignBasisVersion>(mb, x => x.NewVersionId);
        Fk<BasisImpactAssessment, AppUser>(mb, x => x.OwnerId);
        Fk<BasisImpactAssessment, AppUser>(mb, x => x.DecidedBy);
        mb.Entity<ReadinessAssessment>(e =>
        {
            e.HasIndex(x => new { x.ProjectId, x.TargetType, x.TargetId }).IsUnique();
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_readiness_target", "target_type IN ('Task', 'Deliverable')");
                t.HasCheckConstraint("ck_readiness_state", $"state IN ({In(ReadinessState.All)})");
            });
        });
        mb.Entity<ReadinessCheckRecord>(e =>
        {
            e.HasIndex(x => new { x.AssessmentId, x.Code }).IsUnique();
            e.ToTable(t => t.HasCheckConstraint("ck_readiness_check_code", $"code IN ({In(ReadinessCheckCode.All)})"));
        });
        mb.Entity<WorkConstraint>(e =>
        {
            e.HasIndex(x => new { x.ProjectId, x.TargetType, x.TargetId, x.State });
            e.HasIndex(x => new { x.ProjectId, x.Seq }).IsUnique();
            e.HasIndex(x => x.Key);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_work_constraint_target", "target_type IN ('Task', 'Deliverable')");
                t.HasCheckConstraint("ck_work_constraint_state", $"state IN ({In(ConstraintState.All)})");
                t.HasCheckConstraint("ck_work_constraint_link", "(linked_type IS NULL) = (linked_id IS NULL) AND (linked_type IS NULL OR linked_type IN ('Decision', 'Issue', 'Handoff'))");
            });
        });
        mb.Entity<WeeklyPlanSnapshot>(e => e.HasIndex(x => new { x.ProjectId, x.WeekStart }).IsUnique());
        mb.Entity<OutputCommitment>(e =>
        {
            e.HasIndex(x => new { x.ProjectId, x.WeekStart, x.PerformerId });
            e.HasIndex(x => new { x.ProjectId, x.Seq }).IsUnique();
            e.HasIndex(x => x.Key);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_commitment_target", "target_type IN ('Task', 'Deliverable')");
                t.HasCheckConstraint("ck_commitment_state", $"state IN ({In(CommitmentState.All)})");
                t.HasCheckConstraint("ck_commitment_readiness", $"readiness_at_commit IN ({In(ReadinessState.All)})");
            });
        });
        mb.Entity<OutputCommitmentEvent>(e => e.HasIndex(x => new { x.CommitmentId, x.CreatedAt }));
        mb.Entity<ReadinessSubmissionPrerequisite>(e =>
        {
            e.HasIndex(x => new { x.ProjectId, x.TargetType, x.TargetId, x.PackageId }).IsUnique().HasFilter("removed_at IS NULL");
            e.ToTable(t => t.HasCheckConstraint("ck_readiness_prerequisite_target", "target_type IN ('Task', 'Deliverable')"));
        });
        Fk<ReadinessSubmissionPrerequisite, Project>(mb, x => x.ProjectId);
        Fk<ReadinessSubmissionPrerequisite, SubmissionPackage>(mb, x => x.PackageId);
        Fk<ReadinessSubmissionPrerequisite, AppUser>(mb, x => x.RemovedBy);
        Fk<ReadinessAssessment, AppUser>(mb, x => x.OwnerId);
        Fk<ReadinessCheckRecord, ReadinessAssessment>(mb, x => x.AssessmentId);
        Fk<ReadinessCheckRecord, AppUser>(mb, x => x.RecordedBy);
        Fk<WorkConstraint, AppUser>(mb, x => x.RemovalOwnerId);
        Fk<WorkConstraint, AppUser>(mb, x => x.AffectedOwnerId);
        Fk<WorkConstraint, AppUser>(mb, x => x.VerifiedBy);
        Fk<ReadinessException, ReadinessAssessment>(mb, x => x.AssessmentId);
        Fk<ReadinessException, DesignBasisVersion>(mb, x => x.BasisVersionId);
        Fk<ReadinessException, AppUser>(mb, x => x.ApprovedBy);
        Fk<ReadinessException, AppUser>(mb, x => x.VerifierId);
        Fk<WeeklyPlanSnapshot, AppUser>(mb, x => x.CapturedBy);
        Fk<OutputCommitment, WeeklyPlanSnapshot>(mb, x => x.SnapshotId);
        Fk<OutputCommitment, AppUser>(mb, x => x.PerformerId);
        Fk<OutputCommitmentEvent, OutputCommitment>(mb, x => x.CommitmentId);
        Fk<OutputCommitmentEvent, AppUser>(mb, x => x.ActorId);
        mb.Entity<ApprovedLeave>(e =>
        {
            e.HasIndex(x => new { x.SourceSystem, x.ExternalId }).IsUnique();
            e.HasIndex(x => new { x.PersonId, x.FromDate, x.ThroughDate });
            e.Property(x => x.HoursPerDay).HasPrecision(5, 2);
            e.ToTable(t => {
                t.HasCheckConstraint("ck_leave_dates", "through_date >= from_date AND through_date - from_date <= 365");
                t.HasCheckConstraint("ck_leave_hours", "hours_per_day IS NULL OR hours_per_day > 0 AND hours_per_day <= 24");
                t.HasCheckConstraint("ck_leave_source", "source_kind IN ('HRImport','ManualHR')");
                t.HasCheckConstraint("ck_leave_status", "status IN ('Approved','Cancelled','Unverified')");
            });
        });
        mb.Entity<LeaveImportState>().HasIndex(x => x.SourceSystem).IsUnique();
        mb.Entity<PersonSchedule>(e => {
            e.HasIndex(x => new { x.PersonId, x.FromDate }).IsUnique();
            e.Property(x => x.WeeklyHours).HasPrecision(5, 1);
            e.ToTable(t => t.HasCheckConstraint("ck_person_schedule", "weekly_hours >= 0 AND weekly_hours <= 168 AND cardinality(working_days) BETWEEN 1 AND 7 AND working_days <@ ARRAY[0,1,2,3,4,5,6]"));
        });
        mb.Entity<CalendarSubscription>().HasIndex(x => x.UserId).IsUnique();
        mb.Entity<CalendarSubscription>().HasIndex(x => x.TokenHash).IsUnique();
        mb.Entity<CalendarFeedItem>().HasIndex(x => new { x.UserId, x.SourceKey }).IsUnique();
        Fk<LeaveImportPreview, AppUser>(mb, x => x.UserId);
        Fk<PersonAvailabilityOverride, ApprovedLeave>(mb, x => x.ReconciledLeaveId);
        Fk<ApprovedLeave, AppUser>(mb, x => x.PersonId);
        Fk<PersonSchedule, AppUser>(mb, x => x.PersonId);
        Fk<CalendarSubscription, AppUser>(mb, x => x.UserId);
        Fk<CalendarFeedItem, AppUser>(mb, x => x.UserId);
        mb.Entity<PersonAvailabilityOverride>(e =>
        {
            e.HasIndex(x => new { x.PersonId, x.WorkDate }).IsUnique();
            e.Property(x => x.AvailableHours).HasPrecision(9, 3);
            e.ToTable(t => { t.HasCheckConstraint("ck_availability_hours", "available_hours >= 0"); t.HasCheckConstraint("ck_availability_category", $"category IN ({In(AvailabilityCategory.All)})"); });
        });
        mb.Entity<PlanningEntry>(e =>
        {
            e.HasIndex(x => new { x.PersonId, x.StartWeek, x.EndWeek }).HasFilter("deleted_at IS NULL");
            e.HasIndex(x => new { x.CreatedBy, x.EndWeek }).HasFilter("deleted_at IS NULL");
            e.HasIndex(x => x.ProjectId).HasFilter("project_id IS NOT NULL");
            e.Property(x => x.HoursPerWeek).HasPrecision(5, 1);
            e.HasQueryFilter(x => x.DeletedAt == null);
            e.ToTable("planning_entry", t =>
            {
                t.HasCheckConstraint("ck_planning_owner", "created_by IS NOT NULL");
                t.HasCheckConstraint("ck_planning_hours", "hours_per_week > 0 AND hours_per_week <= 168 AND hours_per_week * 2 = trunc(hours_per_week * 2)");
                t.HasCheckConstraint("ck_planning_weeks", "extract(isodow FROM start_week) = 1 AND extract(isodow FROM end_week) = 1 AND end_week >= start_week AND end_week - start_week <= 721");
                t.HasCheckConstraint("ck_planning_label", "char_length(btrim(label)) BETWEEN 1 AND 120");
                t.HasCheckConstraint("ck_planning_notes", "notes IS NULL OR char_length(notes) <= 2000");
                t.HasCheckConstraint("ck_planning_source", "source_category IN ('MajorProject','OtherProject','Proposal','BusinessDevelopment','Training','Admin','Supervision','InternalInitiative','FieldWork','Other')");
                t.HasCheckConstraint("ck_planning_confidence", "confidence IN ('Confirmed','Expected','Possible')");
                t.HasCheckConstraint("ck_planning_visibility", "visibility IN ('Draft','Published','Confirmed')");
                t.HasCheckConstraint("ck_planning_self_visible", "created_by <> person_id OR visibility = 'Confirmed'");
                t.HasCheckConstraint("ck_planning_project_source", "(source_category = 'MajorProject') = (project_id IS NOT NULL)");
                t.HasCheckConstraint("ck_planning_discipline", "project_discipline_id IS NULL OR project_id IS NOT NULL");
            });
        });
        mb.Entity<ResourceAllocation>(e =>
        {
            e.HasIndex(x => new { x.PersonId, x.FromDate, x.ThroughDate });
            e.HasIndex(x => new { x.ProjectId, x.Status });
            e.Property(x => x.PlannedHours).HasPrecision(9, 3);
            e.Property(x => x.ConfirmationSnapshot).HasColumnType("jsonb");
            e.ToTable(t => { t.HasCheckConstraint("ck_allocation_dates", "through_date >= from_date"); t.HasCheckConstraint("ck_allocation_hours", "planned_hours > 0"); t.HasCheckConstraint("ck_allocation_status", $"status IN ({In(AllocationStatus.All)})"); t.HasCheckConstraint("ck_allocation_purpose", $"purpose IN ({In(AllocationPurpose.All)})"); });
        });
        mb.Entity<OpenRole>(e => // FR-ROLE-01 (packet 036)
        {
            e.HasIndex(x => new { x.ProjectId, x.Status }).HasFilter("deleted_at IS NULL");
            e.HasIndex(x => x.FilledAllocationId).IsUnique().HasFilter("filled_allocation_id IS NOT NULL");
            e.Property(x => x.PlannedHours).HasPrecision(9, 3);
            e.HasQueryFilter(x => x.DeletedAt == null);
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_open_role_dates", "through_date >= from_date");
                t.HasCheckConstraint("ck_open_role_hours", "planned_hours > 0");
                t.HasCheckConstraint("ck_open_role_status", $"status IN ({In(OpenRoleStatus.All)})");
                t.HasCheckConstraint("ck_open_role_label", $"char_length(btrim(role_label)) BETWEEN 1 AND {OpenRoleLimits.Label}");
            });
        });
        Fk<OpenRole, Project>(mb, x => x.ProjectId);
        Fk<OpenRole, ProjectDiscipline>(mb, x => x.ProjectDisciplineId);
        Fk<OpenRole, ResourceAllocation>(mb, x => x.FilledAllocationId);
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
        Fk<PlanningEntry, AppUser>(mb, x => x.PersonId);
        Fk<PlanningEntry, AppUser>(mb, x => x.CreatedBy);
        Fk<PlanningEntry, AppUser>(mb, x => x.UpdatedBy);
        Fk<PlanningEntry, AppUser>(mb, x => x.DeletedBy);
        Fk<PlanningEntry, Project>(mb, x => x.ProjectId);
        Fk<PlanningEntry, ProjectDiscipline>(mb, x => x.ProjectDisciplineId);
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
        mb.Entity<ReviewManifestItem>().ToTable(t => t.HasCheckConstraint("ck_review_manifest_source", "(source_revision_id IS NULL) <> (deliverable_issue_id IS NULL)"));
        mb.Entity<ExternalReview>(e =>
        {
            e.Property(x => x.ReturnCodeSnapshot).HasMaxLength(10);
            e.Property(x => x.ReturnLabelSnapshot).HasMaxLength(120);
            e.Property(x => x.PartyNameSnapshot).HasMaxLength(200);
            e.Property(x => x.PartyOrganisationSnapshot).HasMaxLength(200);
            e.HasIndex(x => new { x.PackageId, x.RoundId });
            e.HasIndex(x => x.DeliverableIssueId);
        });
        mb.Entity<DisciplineReview>().HasIndex(x => new { x.RoundId, x.ProjectDisciplineId }).IsUnique();
        mb.Entity<DisciplineReview>().ToTable(t => t.HasCheckConstraint("ck_discipline_review_status", $"status IN ({In(DisciplineReviewStatus.All)})"));
        mb.Entity<ReviewFinding>().HasIndex(x => new { x.RoundId, x.Status });
        mb.Entity<ReviewFinding>().Property(x => x.ResponseOutcome).HasMaxLength(32);
        mb.Entity<FindingEvent>().Property(x => x.ResponseOutcome).HasMaxLength(32);
        mb.Entity<ReviewFinding>().ToTable(t => {
            t.HasCheckConstraint("ck_review_finding_status", $"status IN ({In(FindingStatus.All)})");
            t.HasCheckConstraint("ck_review_finding_severity", "severity IN ('Blocking', 'Advisory')");
            t.HasCheckConstraint("ck_review_finding_response_outcome", "response_outcome IS NULL OR (response_outcome IN ('Incorporated','Not incorporated','Clarified','Noted') AND response IS NOT NULL)");
            t.HasCheckConstraint("ck_review_finding_source_origin", "(source_revision_id IS NOT NULL AND deliverable_issue_id IS NULL AND originator_id IS NOT NULL AND external_review_id IS NULL AND verifier_id IS NOT NULL) OR (source_revision_id IS NULL AND deliverable_issue_id IS NOT NULL AND originator_id IS NULL AND external_review_id IS NOT NULL)");
        });
        mb.Entity<FindingEvent>().ToTable(t => t.HasCheckConstraint("ck_finding_event_response_outcome",
            "response_outcome IS NULL OR (response_outcome IN ('Incorporated','Not incorporated','Clarified','Noted') AND action = 'Responded')"));
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
        Fk<ReviewManifestItem, DeliverableIssue>(mb, x => x.DeliverableIssueId);
        Fk<ReviewManifestItem, Deliverable>(mb, x => x.DeliverableId);
        Fk<ExternalReview, Project>(mb, x => x.ProjectId);
        Fk<ExternalReview, ReviewPackage>(mb, x => x.PackageId);
        Fk<ExternalReview, ReviewRound>(mb, x => x.RoundId);
        Fk<ExternalReview, DeliverableIssue>(mb, x => x.DeliverableIssueId);
        Fk<ExternalReview, ExternalParty>(mb, x => x.ExternalPartyId);
        Fk<ExternalReview, IssueReturnCode>(mb, x => x.ReturnCodeId);
        Fk<ExternalReview, AppUser>(mb, x => x.CreatedBy);
        Fk<DisciplineReview, ReviewRound>(mb, x => x.RoundId);
        Fk<DisciplineReview, AppUser>(mb, x => x.ReviewerId);
        Fk<DisciplineReview, ProjectDiscipline>(mb, x => x.ProjectDisciplineId);
        Fk<ReviewFinding, ReviewPackage>(mb, x => x.PackageId);
        Fk<ReviewFinding, ReviewRound>(mb, x => x.RoundId);
        Fk<ReviewFinding, ReviewFinding>(mb, x => x.CarriedFromId);
        Fk<ReviewFinding, SourceRevision>(mb, x => x.SourceRevisionId);
        Fk<ReviewFinding, DeliverableIssue>(mb, x => x.DeliverableIssueId);
        Fk<ReviewFinding, ExternalReview>(mb, x => x.ExternalReviewId);
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
        Fk<IssueReferenceImpactAssessment, Issue>(mb, x => x.IssueId);
        Fk<IssueReferenceImpactAssessment, IssueDocumentReference>(mb, x => x.DocumentReferenceId);
        Fk<IssueReferenceImpactAssessment, SourceRevision>(mb, x => x.PreviousRevisionId);
        Fk<IssueReferenceImpactAssessment, SourceRevision>(mb, x => x.CurrentRevisionId);
        Fk<IssueReferenceImpactAssessment, AppUser>(mb, x => x.OwnerId);
        Fk<IssueReferenceImpactAssessment, AppUser>(mb, x => x.VerifierId);
        Fk<IssueReferenceImpactAssessment, AppUser>(mb, x => x.OwnerDecidedBy);
        Fk<IssueReferenceImpactAssessment, AppUser>(mb, x => x.VerifierDecidedBy);
        Fk<ReviewFinding, Issue>(mb, x => x.IssueId);
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
        Fk<DesignBasisVersion, Project>(mb, x => x.ProjectId);
        Fk<BasisUse, Project>(mb, x => x.ProjectId);
        Fk<BasisConflict, Project>(mb, x => x.ProjectId);
        Fk<BasisAssumptionDisposition, Project>(mb, x => x.ProjectId);
        Fk<BasisImpactAssessment, Project>(mb, x => x.ProjectId);
        Fk<IssueLocation, Project>(mb, x => x.ProjectId);
        Fk<IssueLocation, Issue>(mb, x => x.IssueId);
        Fk<IssueDocumentReference, Project>(mb, x => x.ProjectId);
        Fk<IssueDocumentReference, Issue>(mb, x => x.IssueId);
        Fk<IssueDocumentReference, IssueDocumentReference>(mb, x => x.ReplacedById);
        Fk<IssueVerification, Project>(mb, x => x.ProjectId);
        Fk<IssueVerification, Issue>(mb, x => x.IssueId);
        Fk<IssueVerification, AppUser>(mb, x => x.VerifierId);
        Fk<IssueAffectedDiscipline, Project>(mb, x => x.ProjectId);
        Fk<IssueAffectedDiscipline, Issue>(mb, x => x.IssueId);
        Fk<IssueAffectedDiscipline, ProjectDiscipline>(mb, x => x.ProjectDisciplineId);
        Fk<ReadinessAssessment, Project>(mb, x => x.ProjectId);
        Fk<ReadinessCheckRecord, Project>(mb, x => x.ProjectId);
        Fk<WorkConstraint, Project>(mb, x => x.ProjectId);
        Fk<ReadinessException, Project>(mb, x => x.ProjectId);
        Fk<WeeklyPlanSnapshot, Project>(mb, x => x.ProjectId);
        Fk<OutputCommitment, Project>(mb, x => x.ProjectId);
        Fk<OutputCommitmentEvent, Project>(mb, x => x.ProjectId);
        // FR-RDY-02 task start authorisations (TaskStartAuthorisation migration).
        mb.Entity<TaskStartAuthorisation>(e =>
        {
            e.HasIndex(x => new { x.TaskId, x.CreatedAt });
            e.ToTable(t =>
            {
                t.HasCheckConstraint("ck_task_start_authorisation_readiness",
                    $"readiness_at_authorisation IN ({In([ReadinessState.NotReady, ReadinessState.NeedsAssessment])})");
                t.HasCheckConstraint("ck_task_start_authorisation_start", "(started_by IS NULL) = (started_at IS NULL)");
            });
        });
        Fk<TaskStartAuthorisation, Project>(mb, x => x.ProjectId);
        Fk<TaskStartAuthorisation, WorkTask>(mb, x => x.TaskId);
        Fk<TaskStartAuthorisation, AppUser>(mb, x => x.AuthorisedBy);
        Fk<TaskStartAuthorisation, AppUser>(mb, x => x.StartedBy);


        VerifiableRecordsModel.Configure(mb);
        TaskPickupModel.Configure(mb);
        OrgStructureModel.Configure(mb);
        Fk<AppUser, OrgUnit>(mb, x => x.UnitId);
        Fk<Project, OrgUnit>(mb, x => x.LeadUnitId);

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

    /// Organisation settings through the shared 30-second store, the one flag guard every endpoint uses (FR-DLV-08).
    public Task<OrgSettings> CurrentSettings() => settings.Get(this);

    internal async Task<bool> AllowSelfReviewForAuthorship(CancellationToken ct = default)
    {
        var setting = await Settings.FirstOrDefaultAsync(row => row.Key == "allow_self_review", ct);
        return setting is not null && JsonSerializer.Deserialize<bool>(setting.Value);
    }

    // Shared read-only validation for save hooks and a bulk command's prospective staged authors.
    internal async Task DemandReviewAuthorship(Guid? deliverableId, Guid[] authorIds, bool allowSelf, CancellationToken ct = default)
    {
        if (allowSelf || deliverableId is null) return;
        Check.That(!await DisciplineReviews.AnyAsync(assignment => authorIds.Contains(assignment.ReviewerId)
            && ReviewPackages.Any(package => package.CurrentRoundId == assignment.RoundId && package.Status != ReviewStatus.Cancelled && package.Status != ReviewStatus.Superseded)
            && ReviewManifestItems.Any(item => item.RoundId == assignment.RoundId && item.DeliverableId == deliverableId
                && item.SourceRevisionId != null && item.DeliverableIssueId == null), ct), "ownerId", "review.independent");
    }

    bool HasIssuedReviewChanges() => ChangeTracker.Entries().Any(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted
        && (entry.Entity is DeliverableIssue or ExternalReview or ReviewManifestItem or ReviewFinding or ReviewPackage
            || entry.Entity is Deliverable deliverable && deliverable.RequiredReviewPackageId is not null
                && (entry.State == EntityState.Added || entry.State == EntityState.Modified && entry.Property(nameof(Deliverable.RequiredReviewPackageId)).IsModified)));

    // Only new issued-source invariants. Internal-source command/independence behavior remains in its existing engine.
    async Task DemandIssuedReviewProvenance(OrgSettings org, CancellationToken ct)
    {
        static void Require(bool valid) { if (!valid) throw new InvalidOperationException("Issued review evidence has inconsistent retained provenance."); }
        var parents = new Dictionary<(Type, Guid), Entity?>();
        var knownIssues = new Dictionary<Guid, DeliverableIssue>();
        async Task<T?> Parent<T>(Guid id) where T : Entity
        {
            if (id == Guid.Empty) return null;
            if (parents.TryGetValue((typeof(T), id), out var cached)) return (T?)cached;
            var local = Set<T>().Local.FirstOrDefault(row => row.Id == id);
            var parent = local is not null ? Entry(local).State == EntityState.Deleted ? null : local : await Set<T>().SingleOrDefaultAsync(row => row.Id == id, ct);
            parents[(typeof(T), id)] = parent; return parent;
        }
        async Task<(ReviewRound Round, ReviewPackage Package)> Round(Guid project, Guid id)
        {
            var round = await Parent<ReviewRound>(id);
            var package = round is null ? null : await Parent<ReviewPackage>(round.PackageId);
            Require(round is not null && package is not null && round.ProjectId == project && package.ProjectId == project && !package.RequiredForIssue);
            return (round!, package!);
        }
        async Task<DeliverableIssue> KnownIssue(Guid project, Guid id)
        {
            if (knownIssues.TryGetValue(id, out var known)) { Require(known.ProjectId == project); return known; }
            var issue = await Parent<DeliverableIssue>(id);
            Require(issue is not null && issue.ProjectId == project && await IssuedAuthorship.ReadAsync(this, issue!, ct) is not null);
            knownIssues[id] = issue!;
            return issue!;
        }
        var issueEntries = ChangeTracker.Entries<DeliverableIssue>().Where(entry => entry.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray();
        foreach (var entry in issueEntries)
        {
            if (entry.State == EntityState.Deleted) throw new InvalidOperationException("An issued revision's captured provenance is retained.");
            if (entry.State == EntityState.Modified)
            {
                if (entry.Property(row => row.IssuedAuthorIds).IsModified || entry.Property(row => row.DeliverableId).IsModified || entry.Property(row => row.ProjectId).IsModified)
                    throw new InvalidOperationException("An issued revision's parent and captured authors are immutable, including Unknown.");
                continue; // Existing register-response fields retain their established update behavior.
            }
            var issue = entry.Entity; var deliverable = await Parent<Deliverable>(issue.DeliverableId);
            Require(deliverable is not null && deliverable.ProjectId == issue.ProjectId);
            if (issue.IssuedAuthorIds is not null)
            {
                Require(IssuedAuthorship.Shape(issue.IssuedAuthorIds));
                var captured = await IssuedAuthorship.CaptureAsync(this, issue.DeliverableId, ct);
                Require(captured is not null && captured.SequenceEqual(issue.IssuedAuthorIds));
            }
        }
        foreach (var entry in ChangeTracker.Entries<ExternalReview>())
            if (entry.State is EntityState.Modified or EntityState.Deleted)
                throw new InvalidOperationException("An incoming external review is immutable.");

        var manifest = ChangeTracker.Entries<ReviewManifestItem>().Where(entry => entry.State == EntityState.Added).Select(entry => entry.Entity).ToArray();
        var affectedPackages = ChangeTracker.Entries<ReviewPackage>().Where(entry => entry.State is EntityState.Added or EntityState.Modified)
            .Select(entry => entry.Entity.Id).ToHashSet();
        foreach (var item in manifest)
        {
            var parent = await Parent<ReviewRound>(item.RoundId);
            if (parent is not null) affectedPackages.Add(parent.PackageId);
            if (item.DeliverableIssueId is not { } issueId) continue;
            Require(item.SourceRevisionId is null);
            var (round, _) = await Round(item.ProjectId, item.RoundId);
            var issue = await KnownIssue(item.ProjectId, issueId);
            Require(issue.DeliverableId == item.DeliverableId && item.AuthorIds.SequenceEqual(issue.IssuedAuthorIds!));
        }
        foreach (var packageId in affectedPackages)
        {
            var stored = await ReviewManifestItems.AsNoTracking().Where(item => ReviewRounds.Any(round => round.Id == item.RoundId && round.PackageId == packageId))
                .Select(item => new { item.SourceRevisionId, item.DeliverableIssueId }).ToListAsync(ct);
            var local = new List<ReviewManifestItem>();
            foreach (var item in manifest)
                if ((await Parent<ReviewRound>(item.RoundId))?.PackageId == packageId) local.Add(item);
            var issued = stored.Any(item => item.DeliverableIssueId != null) || local.Any(item => item.DeliverableIssueId != null);
            if (!issued) continue;
            var package = await Parent<ReviewPackage>(packageId);
            Require(package is not null && !package.RequiredForIssue
                && stored.All(item => item.SourceRevisionId is null && item.DeliverableIssueId is not null)
                && local.All(item => item.SourceRevisionId is null && item.DeliverableIssueId is not null));
        }
        foreach (var entry in ChangeTracker.Entries<Deliverable>().Where(entry => entry.Entity.RequiredReviewPackageId is not null
            && (entry.State == EntityState.Added || entry.State == EntityState.Modified && entry.Property(row => row.RequiredReviewPackageId).IsModified)))
        {
            var packageId = entry.Entity.RequiredReviewPackageId!.Value;
            Require(!await ReviewManifestItems.AnyAsync(item => item.DeliverableIssueId != null
                && ReviewRounds.Any(round => round.Id == item.RoundId && round.PackageId == packageId), ct));
            foreach (var item in manifest.Where(item => item.DeliverableIssueId is not null))
                Require((await Parent<ReviewRound>(item.RoundId))?.PackageId != packageId);
        }
        var manifestSources = new Dictionary<(Guid Project, Guid Round, Guid Issue), bool>();
        async Task<bool> ManifestHas(Guid project, Guid round, DeliverableIssue issue)
        {
            if (manifestSources.TryGetValue((project, round, issue.Id), out var present)) return present;
            present = ReviewManifestItems.Local.Any(item => item.ProjectId == project
                && item.RoundId == round && item.DeliverableIssueId == issue.Id && item.DeliverableId == issue.DeliverableId && item.SourceRevisionId == null
                && Entry(item).State != EntityState.Deleted)
            || await ReviewManifestItems.AnyAsync(item => item.ProjectId == project && item.RoundId == round && item.DeliverableIssueId == issue.Id
                && item.DeliverableId == issue.DeliverableId && item.SourceRevisionId == null, ct);
            manifestSources[(project, round, issue.Id)] = present; return present;
        }
        var carryManifests = new Dictionary<(Guid Round, Guid Deliverable), ReviewManifestItem?>();
        async Task<ReviewManifestItem?> CarryManifest(Guid project, Guid round, Guid deliverable)
        {
            if (carryManifests.TryGetValue((round, deliverable), out var cached)) return cached;
            var rows = await ReviewManifestItems.Where(item => item.RoundId == round && item.DeliverableId == deliverable).ToDictionaryAsync(item => item.Id, ct);
            foreach (var entry in ChangeTracker.Entries<ReviewManifestItem>().Where(entry => entry.Entity.RoundId == round && entry.Entity.DeliverableId == deliverable))
                if (entry.State == EntityState.Deleted) rows.Remove(entry.Entity.Id); else rows[entry.Entity.Id] = entry.Entity;
            Require(rows.Count <= 1 && rows.Values.All(item => item.ProjectId == project && item.SourceRevisionId is null && item.DeliverableIssueId is not null));
            var result = rows.Values.SingleOrDefault(); carryManifests[(round, deliverable)] = result; return result;
        }
        var carryAssignments = new Dictionary<(Guid Round, Guid Discipline), bool>();
        async Task<bool> CarryAssigned(Guid project, Guid round, Guid discipline)
        {
            if (carryAssignments.TryGetValue((round, discipline), out var cached)) return cached;
            var rows = await DisciplineReviews.Where(item => item.ProjectId == project && item.RoundId == round && item.ProjectDisciplineId == discipline).ToDictionaryAsync(item => item.Id, ct);
            foreach (var entry in ChangeTracker.Entries<DisciplineReview>().Where(entry => entry.Entity.ProjectId == project && entry.Entity.RoundId == round && entry.Entity.ProjectDisciplineId == discipline))
                if (entry.State == EntityState.Deleted) rows.Remove(entry.Entity.Id); else rows[entry.Entity.Id] = entry.Entity;
            return carryAssignments[(round, discipline)] = rows.Count != 0;
        }

        foreach (var receipt in ChangeTracker.Entries<ExternalReview>().Where(entry => entry.State == EntityState.Added).Select(entry => entry.Entity).ToArray())
        {
            var (round, package) = await Round(receipt.ProjectId, receipt.RoundId);
            Require(package.Id == receipt.PackageId);
            var issue = await KnownIssue(receipt.ProjectId, receipt.DeliverableIssueId);
            var party = await Parent<ExternalParty>(receipt.ExternalPartyId); var code = await Parent<IssueReturnCode>(receipt.ReturnCodeId);
            Require(party is not null && party.ProjectId == receipt.ProjectId && code is not null
                && receipt.PartyNameSnapshot == party.Name && receipt.PartyOrganisationSnapshot == party.Organisation
                && receipt.ReturnCodeSnapshot == code.Code && receipt.ReturnLabelSnapshot == code.Name
                && await ManifestHas(receipt.ProjectId, round.Id, issue));
        }
        foreach (var entry in ChangeTracker.Entries<ReviewFinding>().Where(entry => entry.State is EntityState.Added or EntityState.Modified).ToArray())
        {
            var finding = entry.Entity;
            var external = finding.ExternalReviewId is not null || entry.State == EntityState.Modified && entry.Property(row => row.ExternalReviewId).OriginalValue is not null;
            if (!external) continue;
            if (entry.State == EntityState.Modified && entry.Properties.Any(property => property.IsModified && property.Metadata.Name is
                nameof(ReviewFinding.ProjectId) or nameof(ReviewFinding.PackageId) or nameof(ReviewFinding.RoundId) or nameof(ReviewFinding.CarriedFromId)
                or nameof(ReviewFinding.SourceRevisionId) or nameof(ReviewFinding.DeliverableIssueId) or nameof(ReviewFinding.ExternalReviewId) or nameof(ReviewFinding.OriginatorId)))
                throw new InvalidOperationException("An external finding's original source and receipt are retained; carry creates a new finding.");
            Require(finding.OriginatorId is null && finding.SourceRevisionId is null && finding.DeliverableIssueId is not null && finding.ExternalReviewId is not null);
            var (round, package) = await Round(finding.ProjectId, finding.RoundId);
            var receipt = await Parent<ExternalReview>(finding.ExternalReviewId!.Value);
            Require(receipt is not null && receipt.ProjectId == finding.ProjectId && receipt.PackageId == package.Id && finding.PackageId == package.Id);
            var issue = await KnownIssue(finding.ProjectId, finding.DeliverableIssueId!.Value);
            var original = await KnownIssue(finding.ProjectId, receipt!.DeliverableIssueId);
            Require(issue.DeliverableId == original.DeliverableId);
            var discipline = await Parent<ProjectDiscipline>(finding.ProjectDisciplineId);
            Require(discipline is not null && discipline.ProjectId == finding.ProjectId);
            if (finding.IssueId is { } linkedId)
                Require((await Parent<Issue>(linkedId))?.ProjectId == finding.ProjectId);
            var seen = new HashSet<Guid>(); var cursor = finding;
            var chain = new List<(ReviewFinding Finding, ReviewRound Round, DeliverableIssue Issue)>();
            while (true)
            {
                Require(seen.Add(cursor.Id) && seen.Count <= Math.Max(1, org.ChainDepthLimit)
                    && cursor.ProjectId == finding.ProjectId && cursor.PackageId == package.Id && cursor.ExternalReviewId == receipt.Id
                    && cursor.ProjectDisciplineId == finding.ProjectDisciplineId
                    && cursor.SourceRevisionId is null && cursor.OriginatorId is null && cursor.DeliverableIssueId is not null);
                var ancestorIssue = await KnownIssue(cursor.ProjectId, cursor.DeliverableIssueId!.Value);
                Require(ancestorIssue.DeliverableId == issue.DeliverableId);
                var (ancestorRound, ancestorPackage) = await Round(cursor.ProjectId, cursor.RoundId);
                Require(ancestorPackage.Id == package.Id);
                chain.Add((cursor, ancestorRound, ancestorIssue));
                if (cursor.CarriedFromId is not { } previousId) break;
                var previous = await Parent<ReviewFinding>(previousId);
                Require(previous is not null);
                cursor = previous!;
            }
            for (var index = 0; index < chain.Count; index++)
            {
                var node = chain[index];
                var source = await CarryManifest(finding.ProjectId, node.Round.Id, original.DeliverableId);
                if (source is not null)
                    Require(source.DeliverableIssueId == node.Issue.Id && source.AuthorIds.SequenceEqual(node.Issue.IssuedAuthorIds!));
                if (index == chain.Count - 1)
                {
                    Require(source is not null && node.Round.Id == receipt.RoundId && node.Issue.Id == receipt.DeliverableIssueId);
                    break;
                }
                var previous = chain[index + 1];
                Require(previous.Round.Number < node.Round.Number);
                var previousSource = await CarryManifest(finding.ProjectId, previous.Round.Id, original.DeliverableId);
                var sourceRemoved = source is null && previousSource is not null;
                var disciplineRemoved = !await CarryAssigned(finding.ProjectId, node.Round.Id, finding.ProjectDisciplineId)
                    && await CarryAssigned(finding.ProjectId, previous.Round.Id, finding.ProjectDisciplineId);
                if (sourceRemoved || disciplineRemoved)
                    Require(!string.IsNullOrWhiteSpace(node.Round.Reason) && !string.IsNullOrWhiteSpace(node.Round.RemovalImpact));
                // A source absent in both rounds remains the same retained issue. Its original removal
                // is validated on the actual older edge, never by an unrelated or future manifest.
                if (source is null) Require(node.Issue.Id == previous.Issue.Id);
            }
        }
    }

    // ---------- Audited save (§20.3, §20.4, G-07, G-10) ----------

    void DemandNoticeDeliveryProvenance()
    {
        bool ClearingDeletedNotification(PropertyEntry property) => property.OriginalValue is Guid id
            && property.CurrentValue is null && ChangeTracker.Entries<Notification>()
                .Any(notification => notification.State == EntityState.Deleted && notification.Entity.Id == id);
        foreach (var entry in ChangeTracker.Entries<NoticeDeliveryIntent>())
        {
            if (entry.State == EntityState.Deleted)
                throw new InvalidOperationException("Notice occurrence provenance is retained.");
            if (entry.State != EntityState.Modified) continue;
            var mutable = new HashSet<string>(StringComparer.Ordinal)
            {
                nameof(NoticeDeliveryIntent.Disposition), nameof(NoticeDeliveryIntent.UpdatedAt),
                nameof(NoticeDeliveryIntent.DeliveryEmailId), nameof(NoticeDeliveryIntent.BulkPolicyJson),
                nameof(NoticeDeliveryIntent.NextPolicyCheckAt), nameof(NoticeDeliveryIntent.PolicyCheckLocalDate),
                nameof(NoticeDeliveryIntent.PolicyChecksInDay),
            };
            if (entry.Properties.Any(property => property.IsModified && !mutable.Contains(property.Metadata.Name)
                && !(property.Metadata.Name == nameof(NoticeDeliveryIntent.SuppressionAnchorAt)
                    && property.OriginalValue is null && property.CurrentValue is DateTimeOffset)
                && !(property.Metadata.Name == nameof(NoticeDeliveryIntent.NotificationId)
                    && ClearingDeletedNotification(entry.Property(x => x.NotificationId)))))
                throw new InvalidOperationException("A notice occurrence's source facts are immutable.");
            var oldDisposition = entry.Property(x => x.Disposition).OriginalValue;
            var sourceRefused = oldDisposition is NoticeIntentDisposition.Accepted or NoticeIntentDisposition.Simulated
                or NoticeIntentDisposition.Unknown or NoticeIntentDisposition.Exhausted
                && entry.Entity.Disposition == NoticeIntentDisposition.AccessRemoved;
            if (NoticeIntentDisposition.Terminal.Contains(oldDisposition)
                && (entry.Property(x => x.DeliveryEmailId).IsModified || entry.Property(x => x.Disposition).IsModified && !sourceRefused))
                throw new InvalidOperationException("A terminal notice occurrence cannot be revived.");
            var policy = entry.Property(x => x.BulkPolicyJson);
            if (policy.IsModified && (policy.OriginalValue is null || policy.CurrentValue is null
                || !NoticeDeliveryProof.HasSameImmutableOrigin(policy.OriginalValue, policy.CurrentValue, entry.Entity.UserId)))
                throw new InvalidOperationException("A bulk notice's immutable origin cannot change.");
        }
        foreach (var entry in ChangeTracker.Entries<NoticeDeliverySlot>())
        {
            if (entry.State == EntityState.Deleted)
                throw new InvalidOperationException("Notice scheduling provenance is retained.");
            if (entry.State == EntityState.Added && entry.Entity.CarryForwardToSlotId is not null)
                throw new InvalidOperationException("A new notice slot cannot invent an earlier coverage transfer.");
            if (entry.State != EntityState.Modified) continue;
            var mutable = new HashSet<string>(StringComparer.Ordinal)
            {
                nameof(NoticeDeliverySlot.State), nameof(NoticeDeliverySlot.CompositionVersion),
                nameof(NoticeDeliverySlot.ExternalAttempts), nameof(NoticeDeliverySlot.UpdatedAt),
                nameof(NoticeDeliverySlot.ReminderNotificationId), nameof(NoticeDeliverySlot.CarryForwardToSlotId),
            };
            if (entry.Properties.Any(property => property.IsModified && !mutable.Contains(property.Metadata.Name)))
                throw new InvalidOperationException("A notice slot's schedule and window are immutable.");
            if (entry.Entity.CompositionVersion < entry.Property(x => x.CompositionVersion).OriginalValue
                || entry.Entity.ExternalAttempts < entry.Property(x => x.ExternalAttempts).OriginalValue)
                throw new InvalidOperationException("Notice composition and sender budgets cannot be reset.");
            var reminder = entry.Property(x => x.ReminderNotificationId);
            if (entry.Property(x => x.State).OriginalValue == NoticeSlotState.Emitted
                && (entry.Property(x => x.State).IsModified || reminder.IsModified && !ClearingDeletedNotification(reminder)))
                throw new InvalidOperationException("An emitted reminder cannot be reopened.");
            if (reminder.IsModified && reminder.OriginalValue is not null && !ClearingDeletedNotification(reminder))
                throw new InvalidOperationException("A reminder's original notification cannot be replaced.");
            var carry = entry.Property(x => x.CarryForwardToSlotId);
            if (carry.IsModified)
            {
                if (carry.OriginalValue is not null || carry.CurrentValue is not { } targetId)
                    throw new InvalidOperationException("A recorded digest coverage transfer cannot be cleared or replaced.");
                var source = entry.Entity;
                var target = NoticeDeliverySlots.Local.SingleOrDefault(slot => slot.Id == targetId
                    && Entry(slot).State != EntityState.Deleted);
                if (source.Kind != NoticeSlotKind.Digest || source.State != NoticeSlotState.Exhausted
                    || entry.Property(x => x.State).OriginalValue != NoticeSlotState.Exhausted
                    || target is null || target.Id == source.Id || target.Kind != NoticeSlotKind.Digest || target.UserId != source.UserId
                    || source.WindowFrom is not { } sourceFrom || source.WindowThrough is not { } sourceThrough
                    || target.WindowFrom is not { } targetFrom || target.WindowThrough is not { } targetThrough
                    || targetFrom > sourceFrom || targetThrough <= sourceThrough)
                    throw new InvalidOperationException("Digest coverage must move once to a bound same-person later containing window.");
            }
        }
        foreach (var entry in ChangeTracker.Entries<EmailMessage>())
        {
            var linked = entry.Entity.NoticeProtocolVersion is not null
                || entry.State != EntityState.Added && entry.Property(x => x.NoticeProtocolVersion).OriginalValue is not null;
            if (!linked) continue;
            if (entry.State == EntityState.Deleted)
                throw new InvalidOperationException("Linked notice transport provenance is retained.");
            if (entry.State != EntityState.Modified) continue;
            var immutable = new HashSet<string>(StringComparer.Ordinal)
            {
                nameof(EmailMessage.NoticeProtocolVersion), nameof(EmailMessage.NoticeSlotId),
                nameof(EmailMessage.NoticeCompositionVersion), nameof(EmailMessage.RenderedOccurrenceIds),
                nameof(EmailMessage.BulkRenderedMembershipJson), nameof(EmailMessage.NoticeDigestPolicyJson), nameof(EmailMessage.UserId),
                nameof(EmailMessage.ToAddress), nameof(EmailMessage.Subject), nameof(EmailMessage.BodyText),
                nameof(EmailMessage.BodyHtml), nameof(EmailMessage.Kind), nameof(EmailMessage.CreatedAt),
                nameof(EmailMessage.DedupKey), nameof(EmailMessage.RequiredProjectIds), nameof(EmailMessage.RequiredPlanningEntryIds),
            };
            if (entry.Properties.Any(property => property.IsModified && immutable.Contains(property.Metadata.Name)))
                throw new InvalidOperationException("A linked notice envelope and membership are immutable.");
            if (entry.Entity.Attempts < entry.Property(x => x.Attempts).OriginalValue)
                throw new InvalidOperationException("A linked notice sender budget cannot be reset.");
            var transport = entry.Property(x => x.TransportKind);
            if (transport.IsModified && transport.OriginalValue is not null)
                throw new InvalidOperationException("A linked notice's recorded transport kind is pinned.");
            var uncertain = entry.Property(x => x.AcceptanceUncertainAt);
            if (uncertain.IsModified && uncertain.OriginalValue is not null)
                throw new InvalidOperationException("Possible provider acceptance cannot be erased.");
            var accepted = entry.Property(x => x.ProviderAcceptedAt);
            if (accepted.IsModified && accepted.OriginalValue is not null)
                throw new InvalidOperationException("Recorded provider acceptance cannot be replaced.");
            var claimVersion = entry.Property(x => x.NoticeClaimVersion);
            if (claimVersion.OriginalValue is { } originalVersion && (claimVersion.CurrentValue is null || claimVersion.CurrentValue < originalVersion))
                throw new InvalidOperationException("A linked notice claim version cannot be reset.");
            if (accepted.OriginalValue is not null && (entry.Property(x => x.TransportOutcome).IsModified
                || claimVersion.IsModified || entry.Property(x => x.NoticeClaimToken).IsModified
                || entry.Property(x => x.NoticeClaimedAt).IsModified || entry.Property(x => x.Attempts).IsModified))
                throw new InvalidOperationException("An accepted notice payload cannot be claimed again.");
        }
    }

    public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        if (Database.CurrentTransaction is null && PendingNoticeCapture is not null)
            return await Tx.Run(this, () => SaveChangesAsync(ct));
        ChangeTracker.DetectChanges();
        DemandNoticeDeliveryProvenance();
        // Contribution identity is the observed membership, never an editable task/person target.
        foreach (var entry in ChangeTracker.Entries<TaskContribution>().Where(e => e.State == EntityState.Modified))
            if (entry.Properties.Any(p => p.IsModified && p.Metadata.Name is nameof(TaskContribution.CollaboratorId)
                or nameof(TaskContribution.TaskId) or nameof(TaskContribution.UserId) or nameof(TaskContribution.ProjectId)
                or nameof(TaskContribution.TaskKeySnapshot) or nameof(TaskContribution.TaskNameSnapshot)))
                throw new InvalidOperationException("A contribution's source membership and provenance are immutable.");
        if (ChangeTracker.Entries<ActivityLog>().Any(e => e.State is EntityState.Modified or EntityState.Deleted)
            || ChangeTracker.Entries<DeliverableBaseline>().Any(e => e.State is EntityState.Modified or EntityState.Deleted)
            || ChangeTracker.Entries<WorkAssignmentOccurrence>().Any(e => e.State is EntityState.Modified or EntityState.Deleted)
            || ChangeTracker.Entries<WorkSeenAcknowledgement>().Any(e => e.State is EntityState.Modified or EntityState.Deleted)
            || ChangeTracker.Entries<WorkSeenCommandReceipt>().Any(e => e.State is EntityState.Modified or EntityState.Deleted)
            || ChangeTracker.Entries<TaskBulkCommandReceipt>().Any(e => e.State is EntityState.Modified or EntityState.Deleted))
            throw new InvalidOperationException("Published history is immutable.");
        // Assignment evidence commits with its canonical change/event; publishers never acquire a personal-state lock.
        if (Database.CurrentTransaction == null && WorkAssignmentPublication.HasChanges(this))
            return await Tx.Run(this, () => SaveChangesAsync(ct));
        var org = await settings.Get(this); // one settings read per save, shared with the request's own flag checks
        var issuedReviewChanges = HasIssuedReviewChanges();
        if (issuedReviewChanges && Database.CurrentTransaction is null)
            return await Tx.Run(this, () => SaveChangesAsync(ct));
        // A hook can create its first raw notice during this save; give it an owner before any hook runs.
        if (org.IsFlagOn("people_screens") && Database.CurrentTransaction is null)
            return await Tx.Run(this, () => SaveChangesAsync(ct));
        var structureOn = org.IsFlagOn("org_structure");
        if (structureOn && ChangeTracker.Entries().Any(e => e.State is EntityState.Added or EntityState.Modified && e.Entity is AppUser or OrgUnit or OrgLevel))
        {
            if (Database.CurrentTransaction == null) return await Tx.Run(this, () => SaveChangesAsync(ct));
            await Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock(hashtext({"hub-org-structure"}))", ct);
            await Hub.Api.Features.OrgStructureValidation.BeforeSave(this, ct);
        }
        var chainOn = Settings.Local.Any(s => s.Key == "flag.verifiable_log" && s.Value == "true") || org.IsFlagOn("verifiable_log")
            || await Set<ActivityChainHead>().AnyAsync(ct);
        if (chainOn && Database.CurrentTransaction == null) return await Tx.Run(this, () => SaveChangesAsync(ct));
        var pickupOn = org.IsFlagOn("task_pickup");
        var noticesOn = org.IsFlagOn("people_screens");
        var changedProjects = ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
            .Select(e => e.Entity is IAuditable item ? item.AuditProjectId
                : noticesOn && e.Entity is ProjectFollow follow ? follow.ProjectId
                : issuedReviewChanges && e.Entity is DeliverableIssue issue ? issue.ProjectId : (Guid?)null).OfType<Guid>().Distinct().Order().ToArray();
        if ((chainOn || pickupOn || noticesOn || issuedReviewChanges) && changedProjects.Length > 0)
        {
            if (Database.CurrentTransaction == null) return await Tx.Run(this, () => SaveChangesAsync(ct));
            foreach (var project in changedProjects) await Coordination.Lock(this, project);
        }
        if (noticesOn)
        {
            // Automatic follows may have been read before the project lock. A later Manual choice wins.
            foreach (var entry in ChangeTracker.Entries<ProjectFollow>().Where(e =>
                (e.State is EntityState.Added or EntityState.Modified) && e.Entity.Source == FollowSource.Assignment
                || e.State == EntityState.Deleted).ToArray())
            {
                var desired = entry.Entity;
                var current = await Follows.AsNoTracking().SingleOrDefaultAsync(row => row.ProjectId == desired.ProjectId && row.UserId == desired.UserId, ct);
                if (entry.State == EntityState.Deleted)
                {
                    var project = Projects.Local.FirstOrDefault(row => row.Id == desired.ProjectId);
                    var restricted = project is not null && Entry(project).Property(row => row.Visibility).IsModified
                        ? project.Visibility == Visibility.Restricted
                        : await Projects.AsNoTracking().AnyAsync(row => row.Id == desired.ProjectId && row.Visibility == Visibility.Restricted, ct);
                    entry.State = EntityState.Detached;
                    if (current is not null)
                    {
                        Follows.Attach(current);
                        if (restricted || current.Source == FollowSource.Assignment) Follows.Remove(current);
                    }
                }
                else if ((entry.State == EntityState.Added || current?.Id != desired.Id) && current is not null)
                {
                    var promote = desired.Level == FollowLevel.AllActivity && current.Source == FollowSource.Assignment
                        && current.Level == FollowLevel.MyItemsOnly;
                    var at = desired.UpdatedAt;
                    entry.State = EntityState.Detached;
                    Follows.Attach(current);
                    if (promote) { current.Level = FollowLevel.AllActivity; current.UpdatedAt = at; }
                }
                else if (entry.State == EntityState.Modified && current is { Source: FollowSource.Manual })
                {
                    entry.State = EntityState.Detached;
                    Follows.Attach(current);
                }
                else if (entry.State == EntityState.Modified && current is null)
                {
                    desired.CreatedAt = desired.UpdatedAt;
                    desired.LastSeenAt = desired.UpdatedAt;
                    entry.State = EntityState.Added;
                }
            }
        }
        if (pickupOn)
            foreach (var task in ChangeTracker.Entries<WorkTask>().Where(e => e.State is EntityState.Modified or EntityState.Deleted).Select(e => e.Entity.Id).Order())
                await Database.SqlQuery<Guid>($"SELECT id AS \"Value\" FROM hub.task WHERE id={task} FOR UPDATE").ToListAsync(ct);
        var authorship = ChangeTracker.Entries().Where(e =>
            e.Entity is WorkTask && (e.State == EntityState.Added || e.State == EntityState.Modified && (e.Property("AssigneeId").IsModified || e.Property("DeliverableId").IsModified))
            || e.Entity is Deliverable && (e.State == EntityState.Added || e.State == EntityState.Modified && e.Property("OwnerId").IsModified)).ToList();
        // Soft delete/restore changes the filtered canonical author set. Serialize with real issuance
        // even while presentation flags are off, without treating removal/restoration as a new author grant.
        var authorshipProjects = authorship.Select(e => ((ProjectItem)e.Entity).ProjectId)
            .Concat(ChangeTracker.Entries<WorkTask>().Where(e => e.State == EntityState.Modified && e.Property(row => row.DeletedAt).IsModified)
                .Select(e => e.Entity.ProjectId)).Distinct().Order().ToArray();
        if (authorshipProjects.Length > 0) {
            if (Database.CurrentTransaction == null) return await Tx.Run(this, () => SaveChangesAsync(ct));
            foreach (var projectId in authorshipProjects) await Coordination.Lock(this, projectId);
        }
        if (authorship.Count > 0) {
            var allowSelf = await AllowSelfReviewForAuthorship(ct);
            if (!allowSelf) foreach (var e in authorship) {
                var deliverableId = e.Entity is WorkTask t ? t.DeliverableId : ((Deliverable)e.Entity).Id;
                if (deliverableId == null) continue;
                var owners = e.Entity is WorkTask task ? new[] { task.AssigneeId, task.CreatedBy ?? (e.State == EntityState.Added ? audit.ActorId : null) } : new[] { ((Deliverable)e.Entity).OwnerId, ((Deliverable)e.Entity).CreatedBy ?? (e.State == EntityState.Added ? audit.ActorId : null) };
                var ids = owners.OfType<Guid>().ToArray();
                await DemandReviewAuthorship(deliverableId, ids, allowSelf, ct);
            }
        }
        if (issuedReviewChanges) await DemandIssuedReviewProvenance(org, ct);
        var now = clock.GetUtcNow();
        if (pickupOn) await Hub.Api.Features.TaskPickupLifecycle.BeforeSave(this, now, new Notifier(this, audit, settings, clock, configuration), ct); // FR-PKP-13
        ChangeTracker.DetectChanges();
        if (await FollowTasks(now, ct)) ChangeTracker.DetectChanges();
        if (org.IsFlagOn(TaskRules.BacklogFlag)) await Hub.Api.Features.TaskEndpoints.KeepBacklogOrder(this, ct); // AC-BKL-03
        var comments = ChangeTracker.Entries<Comment>()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified && e.Property(nameof(Comment.Body)).IsModified)
            .Select(e => (Comment)e.Entity).ToList();
        var taskCommentIds = comments.Where(c => c.ItemType == ItemType.Task).Select(c => c.ItemId).Distinct().ToList();
        var deliverableCommentIds = comments.Where(c => c.ItemType == ItemType.Deliverable).Select(c => c.ItemId).Distinct().ToList();
        if (taskCommentIds.Count > 0)
            foreach (var task in await Tasks.Where(t => taskCommentIds.Contains(t.Id)).ToListAsync(ct)) task.LastActivityAt = now;
        if (deliverableCommentIds.Count > 0)
            foreach (var deliverable in await Deliverables.Where(d => deliverableCommentIds.Contains(d.Id)).ToListAsync(ct)) deliverable.LastActivityAt = now;
        if (taskCommentIds.Count > 0 || deliverableCommentIds.Count > 0) ChangeTracker.DetectChanges();
        var projects = new HashSet<Guid>();
        var users = new HashSet<Guid>();
        foreach (var entry in ChangeTracker.Entries().ToList())
        {
            if (entry.State is EntityState.Unchanged or EntityState.Detached) continue;
            if (entry.State is EntityState.Modified or EntityState.Deleted
                && entry.Entity is SourceRevision or HandoffRevision or HandoffReceiptEvent or HandoffCommand or CoordinationCommand or ReviewManifestItem or ExternalReview or FindingEvent or InputAdoption)
                throw new InvalidOperationException("Published coordination evidence is immutable.");
            if (entry.State is EntityState.Modified or EntityState.Deleted && entry.Entity is AdminReview)
                throw new InvalidOperationException("A recorded Admin review is never edited or deleted (FR-ADR-03).");
            if (entry.Entity is IVersioned versioned && entry.State == EntityState.Modified)
                versioned.RowVersion++;
            if (entry.Entity is Audited a)
            {
                if (entry.State == EntityState.Added) { a.CreatedAt = now; a.CreatedBy ??= audit.ActorId; }
                if (entry.State is EntityState.Added or EntityState.Modified) { a.UpdatedAt = now; a.UpdatedBy = audit.ActorId; }
            }
            ActivityLog? sourceActivity = null;
            if (entry.Entity is IAuditable item && AuditRules.Fields.TryGetValue(entry.Metadata.ClrType, out var fields))
            {
                sourceActivity = BuildLog(entry, item, fields, now);
                if (sourceActivity is not null) ActivityLog.Add(sourceActivity);
            }
            WorkAssignmentPublication.Publish(this, entry, sourceActivity, now);
            if (AuditRules.AffectsEvaluation(entry.Entity) is { } pid) projects.Add(pid);
            if (entry.Entity is AppUser u && entry.State == EntityState.Modified
                && (entry.Property(nameof(AppUser.IsActive)).IsModified || entry.Property(nameof(AppUser.SupervisorId)).IsModified))
                users.Add(u.Id);
        }
        foreach (var pid in projects)
            Outbox.Add(new OutboxEvent { EventType = "ProjectChanged", ProjectId = pid, CreatedAt = now, Payload = JsonSerializer.Serialize(new { correlationId = audit.CorrelationId }) });
        foreach (var uid in users)
            Outbox.Add(new OutboxEvent { EventType = "UserChanged", CreatedAt = now, Payload = JsonSerializer.Serialize(new { userId = uid }) });
        if (chainOn) await AuditChainRows.Assign(this, now, ct, initializingAuditChains);
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
