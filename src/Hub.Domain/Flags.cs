namespace Hub.Domain;

/// A release toggle (§43.4 FR-DLV-07): its name and the packet that declares it, for example "048".
public sealed record FlagDef(string Name, string Packet);

/// Feature flags (§43.4). A flag gates only an addition that has never been on in pilot (FR-DLV-09); no existing
/// feature is ever flagged. Each packet that adds a flag ends with the cleanup slice that removes it.
public static class Flags
{
    public static readonly FlagDef[] All =
    [
        new("verifiable_log", "048"),
        new("commitment_reliability", "048"),
        new("allocation_preview", "048"),
        new("baseline_history", "048"),
        new("revision_check", "048"),
        new("task_pickup", "049"),
        new("org_structure", "050"),
        new("figure_names", "041"), // §45.2 and §45.3: the Uncommitted name, the basis lines, the Partial view row label and "at most"
        new("planner_kept_rows", "041"), // §45.7: retain edited planner rows while filters stay active
        new("show_deleted", "045"), // §49.2: the deleted-items list, export and deliverable/milestone restore
        new("template_working_days", "045"), // §49.4: office-calendar offsets in templates
        new("status_rollups", "045"), // §49.5 and §49.6: discipline status segments and the Portfolio overdue deliverables count
        new("picker_project_rank", "046"),
        new("row_quick_actions", "046"),
        new("date_context", "046"),
        new("schedule_baseline", "046"),
        new("small_ux", "046"),
        new("timeline_handoffs_chain", "046"),
        new("onboarding_card", "047"),
        new("capacity_reports", "047"),
        new("route_counter", "047"),
        new("template_roles", "047"),
        new("unfilled_beside_capacity", "047"),
        new("project_status_note", "047"),
        new("my_work_follow_up", "047"),
        new("project_import", "047"),
        new("capacity_polish", "047"),
        new("record_engine_additions", "043"), // §47.5 FR-REC-12: Risks saved views; open-role sorting, read-only panel and export
        new("live_project_stamp", "042"), // §46.5 project stamp in the notification pulse
        new("live_events", "042"), // §46.6 change-event stream
        new("admin_review", "051"), // §55.4: the Admin review page, its record, the digest line and the Portfolio strip
        new("data_entry_safety", "051"), // §55.2 Part A: text autosave after a pause, drafts, leave prompt, Retry control
        new(TaskRules.BacklogFlag, "051"), // §55.3: the Backlog filter, row field, counts, export column and FR-BKL-05 exclusions
        new("visual_system", "052"), // §56: visual system and navigation amendment
        new("screen_patterns", "053"), // §57.2: cross-screen record-context patterns
        new("home_overview", "053"), // §57.3: Home overview completeness
        new("work_screens", "053"), // §57.4: My Work and task-screen completeness
        new("project_screens", "053"), // §57.5: project, portfolio and dashboard completeness
        new("capacity_screens", "053"), // §57.6: reports, planning and capacity-screen completeness
        new("register_screens", "053"), // §57.7: deliverable, review, milestone and register completeness
        new("people_screens", "053"), // §57.8: people, files, notification, search, preference and admin completeness
    ];

    /// `flag.<name>` as a Bool setting, default off, group `flags`, generated like `rule_enabled.*`.
    public static IEnumerable<SettingDef> Defs(IEnumerable<FlagDef> flags) =>
        flags.Select(f => new SettingDef($"flag.{f.Name}", SettingKind.Bool, false, "flags"));

    public static FlagDef? Find(string settingKey) =>
        settingKey.StartsWith("flag.", StringComparison.Ordinal) ? All.FirstOrDefault(f => $"flag.{f.Name}" == settingKey) : null;
}
