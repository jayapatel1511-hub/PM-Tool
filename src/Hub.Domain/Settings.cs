using System.Globalization;
using System.Text.Json;

namespace Hub.Domain;

public enum SettingKind { Int, Bool, Text, Time, TimeZone, Regex, Channels }

public sealed record SettingDef(string Key, SettingKind Kind, object Default, string Group);

/// Organisation settings (§10.4, §12.12 rule flags, §17.4 notification defaults, §13.15).
/// Thresholds are never hard-coded elsewhere: rules read them from this record.
public sealed record OrgSettings
{
    public int TaskDueSoonDays { get; init; } = 5;
    public int DeliverableDueSoonDays { get; init; } = 10;
    public int MilestoneApproachingDays { get; init; } = 14;
    public int DecisionDueSoonDays { get; init; } = 5;
    public int TaskStaleDays { get; init; } = 10;
    public int ReviewStaleDays { get; init; } = 5;
    public int BlockedAttentionDays { get; init; } = 0;
    public int HealthOverdueTaskPctYellow { get; init; } = 10;
    public int HealthOverdueTaskPctRed { get; init; } = 25;
    public int HealthOverdueTaskMinYellow { get; init; } = 3;
    public int HealthBlockedDaysYellow { get; init; } = 5;
    public int HealthOverrideExpiryDays { get; init; } = 14;
    public int ChainDepthLimit { get; init; } = 10;
    public bool AllowSelfReview { get; init; }
    public int CompleteProjectEditWindowDays { get; init; } = 30;
    public int DefaultWeeklyCapacityHours { get; init; } = 40;
    public int CoordinationLookaheadWeeks { get; init; } = 3;
    public string OrgTimeZone { get; init; } = "America/Halifax";
    public string DigestSendTimeLocal { get; init; } = "07:00";
    public bool WeekendDigests { get; init; }
    public string DateFormat { get; init; } = "yyyy-MM-dd";
    public string ProjectNumberFormat { get; init; } = "^[A-Za-z0-9][A-Za-z0-9-]{0,31}$";
    public bool RestrictedProjectsEnabled { get; init; }
    public bool ViewerCommentsDefault { get; init; } = true;
    public int IdleTimeoutHours { get; init; } = 8;
    public bool WorkingDaysEnabled { get; init; }
    public IReadOnlyDictionary<string, bool> RuleEnabled { get; init; } = Rules.Ids.ToDictionary(r => r, _ => true);
    public IReadOnlyDictionary<string, Channels> NotificationDefaults { get; init; } =
        NotificationEvents.All.ToDictionary(e => e.Code, e => new Channels(e.App, e.Email));

    public bool IsRuleEnabled(string id) => !RuleEnabled.TryGetValue(id, out var on) || on;

    public static readonly SettingDef[] Defs =
    [
        new("task_due_soon_days", SettingKind.Int, 5, "thresholds"),
        new("deliverable_due_soon_days", SettingKind.Int, 10, "thresholds"),
        new("milestone_approaching_days", SettingKind.Int, 14, "thresholds"),
        new("decision_due_soon_days", SettingKind.Int, 5, "thresholds"),
        new("task_stale_days", SettingKind.Int, 10, "thresholds"),
        new("review_stale_days", SettingKind.Int, 5, "thresholds"),
        new("blocked_attention_days", SettingKind.Int, 0, "thresholds"),
        new("health_overdue_task_pct_yellow", SettingKind.Int, 10, "health"),
        new("health_overdue_task_pct_red", SettingKind.Int, 25, "health"),
        new("health_overdue_task_min_yellow", SettingKind.Int, 3, "health"),
        new("health_blocked_days_yellow", SettingKind.Int, 5, "health"),
        new("health_override_expiry_days", SettingKind.Int, 14, "health"),
        new("chain_depth_limit", SettingKind.Int, 10, "thresholds"),
        new("allow_self_review", SettingKind.Bool, false, "work"),
        new("complete_project_edit_window_days", SettingKind.Int, 30, "work"),
        new("default_weekly_capacity_hours", SettingKind.Int, 40, "work"),
        new("coordination_lookahead_weeks", SettingKind.Int, 3, "work"),
        new("restricted_projects_enabled", SettingKind.Bool, false, "work"),
        new("viewer_comments_default", SettingKind.Bool, true, "work"),
        new("project_number_format", SettingKind.Regex, "^[A-Za-z0-9][A-Za-z0-9-]{0,31}$", "work"),
        new("working_days_enabled", SettingKind.Bool, false, "calendar"),
        new("org_time_zone", SettingKind.TimeZone, "America/Halifax", "calendar"),
        new("date_format", SettingKind.Text, "yyyy-MM-dd", "calendar"),
        new("digest_send_time_local", SettingKind.Time, "07:00", "notifications"),
        new("weekend_digests", SettingKind.Bool, false, "notifications"),
        new("idle_timeout_hours", SettingKind.Int, 8, "security"),
        .. Rules.Ids.Select(r => new SettingDef($"rule_enabled.{r}", SettingKind.Bool, true, "rules")),
        .. NotificationEvents.All.Select(e => new SettingDef($"notify_default.{e.Code}", SettingKind.Channels, new Channels(e.App, e.Email), "notifications")),
    ];

    public static SettingDef? Def(string key) => Defs.FirstOrDefault(d => d.Key == key);

    public static OrgSettings From(IReadOnlyDictionary<string, JsonElement> v)
    {
        int I(string k, int d) => v.TryGetValue(k, out var e) && e.ValueKind == JsonValueKind.Number ? e.GetInt32() : d;
        bool B(string k, bool d) => v.TryGetValue(k, out var e) && e.ValueKind is JsonValueKind.True or JsonValueKind.False ? e.GetBoolean() : d;
        string S(string k, string d) => v.TryGetValue(k, out var e) && e.ValueKind == JsonValueKind.String ? e.GetString()! : d;
        var s = new OrgSettings();
        return s with
        {
            TaskDueSoonDays = I("task_due_soon_days", s.TaskDueSoonDays),
            DeliverableDueSoonDays = I("deliverable_due_soon_days", s.DeliverableDueSoonDays),
            MilestoneApproachingDays = I("milestone_approaching_days", s.MilestoneApproachingDays),
            DecisionDueSoonDays = I("decision_due_soon_days", s.DecisionDueSoonDays),
            TaskStaleDays = I("task_stale_days", s.TaskStaleDays),
            ReviewStaleDays = I("review_stale_days", s.ReviewStaleDays),
            BlockedAttentionDays = I("blocked_attention_days", s.BlockedAttentionDays),
            HealthOverdueTaskPctYellow = I("health_overdue_task_pct_yellow", s.HealthOverdueTaskPctYellow),
            HealthOverdueTaskPctRed = I("health_overdue_task_pct_red", s.HealthOverdueTaskPctRed),
            HealthOverdueTaskMinYellow = I("health_overdue_task_min_yellow", s.HealthOverdueTaskMinYellow),
            HealthBlockedDaysYellow = I("health_blocked_days_yellow", s.HealthBlockedDaysYellow),
            HealthOverrideExpiryDays = I("health_override_expiry_days", s.HealthOverrideExpiryDays),
            ChainDepthLimit = I("chain_depth_limit", s.ChainDepthLimit),
            AllowSelfReview = B("allow_self_review", s.AllowSelfReview),
            CompleteProjectEditWindowDays = I("complete_project_edit_window_days", s.CompleteProjectEditWindowDays),
            DefaultWeeklyCapacityHours = I("default_weekly_capacity_hours", s.DefaultWeeklyCapacityHours),
            CoordinationLookaheadWeeks = I("coordination_lookahead_weeks", s.CoordinationLookaheadWeeks),
            RestrictedProjectsEnabled = B("restricted_projects_enabled", s.RestrictedProjectsEnabled),
            ViewerCommentsDefault = B("viewer_comments_default", s.ViewerCommentsDefault),
            ProjectNumberFormat = S("project_number_format", s.ProjectNumberFormat),
            WorkingDaysEnabled = B("working_days_enabled", s.WorkingDaysEnabled),
            OrgTimeZone = S("org_time_zone", s.OrgTimeZone),
            DateFormat = S("date_format", s.DateFormat),
            DigestSendTimeLocal = S("digest_send_time_local", s.DigestSendTimeLocal),
            WeekendDigests = B("weekend_digests", s.WeekendDigests),
            IdleTimeoutHours = I("idle_timeout_hours", s.IdleTimeoutHours),
            RuleEnabled = Rules.Ids.ToDictionary(r => r, r => B($"rule_enabled.{r}", true)),
            NotificationDefaults = NotificationEvents.All.ToDictionary(e => e.Code, e =>
                v.TryGetValue($"notify_default.{e.Code}", out var c) && c.ValueKind == JsonValueKind.Object
                    ? new Channels(c.TryGetProperty("app", out var a) && a.GetBoolean(), c.TryGetProperty("email", out var m) && m.GetBoolean())
                    : new Channels(e.App, e.Email)),
        };
    }

    /// Validates an Admin-entered value; returns an error message key or null.
    public static string? Validate(SettingDef def, JsonElement value) => def.Kind switch
    {
        SettingKind.Int => value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var n) &&
            (def.Key == "coordination_lookahead_weeks" ? n is >= 1 and <= 12 : n is >= 0 and <= 3650) ? null : "setting.int",
        SettingKind.Bool => value.ValueKind is JsonValueKind.True or JsonValueKind.False ? null : "setting.bool",
        SettingKind.Time => value.ValueKind == JsonValueKind.String && TimeOnly.TryParseExact(value.GetString(), "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) ? null : "setting.time",
        SettingKind.TimeZone => value.ValueKind == JsonValueKind.String && TryZone(value.GetString()!) ? null : "setting.timezone",
        SettingKind.Regex => value.ValueKind == JsonValueKind.String && TryRegex(value.GetString()!) ? null : "setting.regex",
        SettingKind.Channels => value.ValueKind == JsonValueKind.Object && value.TryGetProperty("app", out var a) && a.ValueKind is JsonValueKind.True or JsonValueKind.False
            && value.TryGetProperty("email", out var e) && e.ValueKind is JsonValueKind.True or JsonValueKind.False ? null : "setting.channels",
        _ => value.ValueKind == JsonValueKind.String && value.GetString()!.Length is > 0 and <= 200 ? null : "setting.text",
    };

    static bool TryZone(string id) { try { TimeZoneInfo.FindSystemTimeZoneById(id); return true; } catch { return false; } }
    static bool TryRegex(string p) { try { _ = new System.Text.RegularExpressions.Regex(p); return true; } catch { return false; } }
}

public sealed record Channels(bool App, bool Email);

public sealed record NotificationEventDef(string Code, bool App, bool Email, bool DirectAssignment = false);

/// Event catalogue and default channels (§17.2). Digest content is controlled separately (§17.3).
public static class NotificationEvents
{
    public const string TaskAssigned = "TaskAssigned", ReviewerSet = "ReviewerSet", ReviewRequested = "ReviewRequested",
        ReviewOutcome = "ReviewOutcome", TaskBlocked = "TaskBlocked", TaskUnblocked = "TaskUnblocked",
        BlockingOverdue = "BlockingOverdue", DueDateChanged = "DueDateChanged", CommentOnItem = "CommentOnItem",
        Mention = "Mention", DecisionAssigned = "DecisionAssigned", DecisionOverdue = "DecisionOverdue",
        DecisionRecorded = "DecisionRecorded", MilestoneStatus = "MilestoneStatus", MilestoneDateChanged = "MilestoneDateChanged",
        AddedToProject = "AddedToProject", BecameDisciplineLead = "BecameDisciplineLead", ProjectStatusChanged = "ProjectStatusChanged",
        AttentionCritical = "AttentionCritical", HealthOverride = "HealthOverride", WorkReassignedAway = "WorkReassignedAway",
        DependencyRemoved = "DependencyRemoved", StaffAssignment = "StaffAssignment", SupervisorStaffing = "SupervisorStaffing",
        MemberAutoAdded = "MemberAutoAdded", DeliverableOwned = "DeliverableOwned", ActionAssigned = "ActionAssigned",
        TaskChanged = "TaskChanged", HandoffChanged = "HandoffChanged", ReviewPackageChanged = "ReviewPackageChanged", ChangeImpact = "ChangeImpact",
        AllocationChanged = "AllocationChanged", SubmissionChanged = "SubmissionChanged",
        IssueVerifierAssigned = "IssueVerifierAssigned", IssueVerificationOutcome = "IssueVerificationOutcome",
        ConstraintAction = "ConstraintAction", ConstraintOutcome = "ConstraintOutcome",
        CommitmentProposed = "CommitmentProposed", CommitmentChanged = "CommitmentChanged",
        BasisImpactPending = "BasisImpactPending", BasisConflictRaised = "BasisConflictRaised";

    public static readonly NotificationEventDef[] All =
    [
        new(TaskAssigned, true, true, true), new(ReviewerSet, true, true, true), new(ReviewRequested, true, true, true),
        new(ReviewOutcome, true, true), new(TaskBlocked, true, false), new(TaskUnblocked, true, true),
        new(BlockingOverdue, true, true), new(DueDateChanged, true, false), new(CommentOnItem, true, false),
        new(Mention, true, true, true), new(DecisionAssigned, true, true, true), new(DecisionOverdue, true, false),
        new(DecisionRecorded, true, true), new(MilestoneStatus, true, false), new(MilestoneDateChanged, true, false),
        new(AddedToProject, true, true), new(BecameDisciplineLead, true, true), new(ProjectStatusChanged, true, false),
        new(AttentionCritical, true, false), new(HealthOverride, true, false), new(WorkReassignedAway, true, false),
        new(DependencyRemoved, true, false), new(StaffAssignment, true, false), new(SupervisorStaffing, true, false),
        new(MemberAutoAdded, true, false), new(DeliverableOwned, true, false, true), new(ActionAssigned, true, false, true),
        new(TaskChanged, true, false),
        new(HandoffChanged, true, false, true), new(ReviewPackageChanged, true, false, true), new(ChangeImpact, true, false, true),
        new(AllocationChanged, true, false), new(SubmissionChanged, true, false),
        new(IssueVerifierAssigned, true, false, true), new(IssueVerificationOutcome, true, false),
        new(ConstraintAction, true, false, true), new(ConstraintOutcome, true, false),
        new(CommitmentProposed, true, false, true), new(CommitmentChanged, true, false),
        new(BasisImpactPending, true, false, true), new(BasisConflictRaised, true, false),
    ];

    /// Coordination events whose recipients must hold current project access when the notice is composed and
    /// again when its email is delivered (FR-MDC-02).
    public static readonly HashSet<string> ProjectScoped = [HandoffChanged, ReviewPackageChanged, ChangeImpact, AllocationChanged,
        SubmissionChanged, IssueVerifierAssigned, IssueVerificationOutcome, ConstraintAction, ConstraintOutcome, CommitmentProposed, CommitmentChanged,
        BasisImpactPending, BasisConflictRaised];

    public static NotificationEventDef Get(string code) => All.First(e => e.Code == code);
}

/// Attention rule identifiers (§12.12). A-07 activates with the Phase 2 issue register.
public static class Rules
{
    public static readonly string[] Ids = Enumerable.Range(1, 20).Select(i => $"A-{i:00}").ToArray();
}
