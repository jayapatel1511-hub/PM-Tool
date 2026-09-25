using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "hub");

            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:citext", ",,")
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "activity_log",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_type = table.Column<string>(type: "text", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    project_discipline_id = table.Column<Guid>(type: "uuid", nullable: true),
                    item_type = table.Column<string>(type: "text", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    item_key = table.Column<string>(type: "text", nullable: true),
                    item_name = table.Column<string>(type: "text", nullable: true),
                    action = table.Column<string>(type: "text", nullable: false),
                    categories = table.Column<List<string>>(type: "text[]", nullable: false),
                    changes = table.Column<string>(type: "jsonb", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source = table.Column<string>(type: "text", nullable: false),
                    snapshot = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_activity_log", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "attention_snooze",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_id = table.Column<string>(type: "text", nullable: false),
                    item_type = table.Column<string>(type: "text", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snoozed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    snoozed_until = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    note = table.Column<string>(type: "text", nullable: false),
                    severity_at_snooze = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ended_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attention_snooze", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "board_order",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: true),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    position = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_board_order", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "calendar_event",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    type = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    start_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    end_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    time_zone = table.Column<string>(type: "text", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    location = table.Column<string>(type: "text", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    visibility = table.Column<string>(type: "text", nullable: false),
                    cancelled_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calendar_event", x => x.id);
                    table.CheckConstraint("ck_event_times", "end_at > start_at");
                });

            migrationBuilder.CreateTable(
                name: "client",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    short_name = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_client", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dashboard_layout",
                schema: "hub",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    widgets = table.Column<string>(type: "jsonb", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dashboard_layout", x => x.user_id);
                });

            migrationBuilder.CreateTable(
                name: "discipline",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    colour = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_discipline", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "document_link",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_type = table.Column<string>(type: "text", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    url = table.Column<string>(type: "text", nullable: false),
                    link_type = table.Column<string>(type: "text", nullable: false),
                    added_by = table.Column<Guid>(type: "uuid", nullable: false),
                    added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_document_link", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "email_message",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    to_address = table.Column<string>(type: "text", nullable: false),
                    subject = table.Column<string>(type: "text", nullable: false),
                    body_text = table.Column<string>(type: "text", nullable: false),
                    body_html = table.Column<string>(type: "text", nullable: true),
                    kind = table.Column<string>(type: "text", nullable: false),
                    dedup_key = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "text", nullable: true),
                    next_attempt_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_email_message", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "item_link",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_type = table.Column<string>(type: "text", nullable: false),
                    source_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "text", nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    relation = table.Column<string>(type: "text", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_item_link", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "item_watcher",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_type = table.Column<string>(type: "text", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_item_watcher", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "job_run",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_name = table.Column<string>(type: "text", nullable: false),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    finished_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    details = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_run", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "meeting",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    meeting_date = table.Column<DateOnly>(type: "date", nullable: false),
                    meeting_type = table.Column<string>(type: "text", nullable: false),
                    notes_link = table.Column<string>(type: "text", nullable: true),
                    calendar_event_id = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meeting", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    item_type = table.Column<string>(type: "text", nullable: true),
                    item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    item_key = table.Column<string>(type: "text", nullable: true),
                    title = table.Column<string>(type: "text", nullable: false),
                    body = table.Column<string>(type: "text", nullable: true),
                    link_path = table.Column<string>(type: "text", nullable: true),
                    collapse_key = table.Column<string>(type: "text", nullable: true),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    count = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    emailed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    digest_included_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "notification_preference",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    in_app = table.Column<bool>(type: "boolean", nullable: false),
                    email = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_preference", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "office",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "text", nullable: false),
                    time_zone = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_office", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "org_setting",
                schema: "hub",
                columns: table => new
                {
                    key = table.Column<string>(type: "text", nullable: false),
                    value = table.Column<string>(type: "jsonb", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_org_setting", x => x.key);
                });

            migrationBuilder.CreateTable(
                name: "outbox_event",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    payload = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false),
                    last_error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_outbox_event", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "phase",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_phase", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "project_star",
                schema: "hub",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_star", x => new { x.user_id, x.project_id });
                });

            migrationBuilder.CreateTable(
                name: "project_template",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    project_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                    version = table.Column<int>(type: "integer", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_template", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "project_type",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_type", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "saved_view",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope = table.Column<string>(type: "text", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    list_type = table.Column<string>(type: "text", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    filters = table.Column<string>(type: "jsonb", nullable: false),
                    sort = table.Column<string>(type: "text", nullable: true),
                    columns = table.Column<string>(type: "jsonb", nullable: false),
                    group_by = table.Column<string>(type: "text", nullable: true),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_saved_view", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "template_deliverable",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_discipline_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    deliverable_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_milestone_id = table.Column<Guid>(type: "uuid", nullable: true),
                    due_offset_days = table.Column<int>(type: "integer", nullable: true),
                    requires_review = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_template_deliverable", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "template_dependency",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    predecessor_template_task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    successor_template_task_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_template_dependency", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "template_discipline",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    discipline_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_default_included = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_template_discipline", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "template_milestone",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    milestone_type = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    anchor = table.Column<string>(type: "text", nullable: false),
                    offset_days_from_anchor = table.Column<int>(type: "integer", nullable: true),
                    completes_phase_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_client_facing = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_template_milestone", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "template_task",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_deliverable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    template_discipline_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    requires_review = table.Column<bool>(type: "boolean", nullable: false),
                    priority = table.Column<string>(type: "text", nullable: false),
                    estimated_hours = table.Column<decimal>(type: "numeric", nullable: true),
                    due_offset_days = table.Column<int>(type: "integer", nullable: true),
                    assign_to_role = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_template_task", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_setting",
                schema: "hub",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    digest_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    digest_time_local = table.Column<string>(type: "text", nullable: true),
                    dense_rows = table.Column<bool>(type: "boolean", nullable: false),
                    weekly_summary_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    digest_sections_off = table.Column<string>(type: "jsonb", nullable: false),
                    last_digest_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_weekly_summary_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_setting", x => x.user_id);
                });

            migrationBuilder.CreateTable(
                name: "workspace",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workspace", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "deliverable_type",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    default_discipline_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_deliverable_type", x => x.id);
                    table.ForeignKey(
                        name: "fk_deliverable_type_disciplines_default_discipline_id",
                        column: x => x.default_discipline_id,
                        principalSchema: "hub",
                        principalTable: "discipline",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "app_user",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entra_object_id = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "citext", nullable: false),
                    display_name = table.Column<string>(type: "text", nullable: false),
                    job_title = table.Column<string>(type: "text", nullable: true),
                    office_id = table.Column<Guid>(type: "uuid", nullable: true),
                    supervisor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    weekly_capacity_hours = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true),
                    last_sign_in_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    is_template_editor = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_app_user", x => x.id);
                    table.CheckConstraint("ck_app_user_not_own_supervisor", "supervisor_id IS NULL OR supervisor_id <> id");
                    table.ForeignKey(
                        name: "fk_app_user_app_user_supervisor_id",
                        column: x => x.supervisor_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_app_user_offices_office_id",
                        column: x => x.office_id,
                        principalSchema: "hub",
                        principalTable: "office",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "holiday",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: true),
                    date = table.Column<DateOnly>(type: "date", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_holiday", x => x.id);
                    table.ForeignKey(
                        name: "fk_holiday_offices_office_id",
                        column: x => x.office_id,
                        principalSchema: "hub",
                        principalTable: "office",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "comment",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_type = table.Column<string>(type: "text", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_id = table.Column<Guid>(type: "uuid", nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    comment_kind = table.Column<string>(type: "text", nullable: false),
                    review_round = table.Column<int>(type: "integer", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    edited_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    deleted_by_pm = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_comment", x => x.id);
                    table.ForeignKey(
                        name: "fk_comment_app_user_author_id",
                        column: x => x.author_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_number = table.Column<string>(type: "citext", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_reference = table.Column<string>(type: "text", nullable: true),
                    project_manager_id = table.Column<Guid>(type: "uuid", nullable: false),
                    office_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_type_id = table.Column<Guid>(type: "uuid", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    location = table.Column<string>(type: "text", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    phase_id = table.Column<Guid>(type: "uuid", nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    target_completion_date = table.Column<DateOnly>(type: "date", nullable: true),
                    priority = table.Column<string>(type: "text", nullable: false),
                    visibility = table.Column<string>(type: "text", nullable: false),
                    internal_notes = table.Column<string>(type: "text", nullable: true),
                    coordination_day = table.Column<string>(type: "text", nullable: true),
                    allow_viewer_comments = table.Column<bool>(type: "boolean", nullable: false),
                    health_override = table.Column<string>(type: "text", nullable: true),
                    health_override_note = table.Column<string>(type: "text", nullable: true),
                    health_override_by = table.Column<Guid>(type: "uuid", nullable: true),
                    health_override_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    health_override_expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_from_template_id = table.Column<Guid>(type: "uuid", nullable: true),
                    template_version = table.Column<int>(type: "integer", nullable: true),
                    last_coordination_reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_coordination_reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    status_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    activated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    archived_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    next_task_seq = table.Column<int>(type: "integer", nullable: false),
                    next_deliverable_seq = table.Column<int>(type: "integer", nullable: false),
                    next_milestone_seq = table.Column<int>(type: "integer", nullable: false),
                    next_decision_seq = table.Column<int>(type: "integer", nullable: false),
                    next_risk_seq = table.Column<int>(type: "integer", nullable: false),
                    next_issue_seq = table.Column<int>(type: "integer", nullable: false),
                    next_action_seq = table.Column<int>(type: "integer", nullable: false),
                    external_source = table.Column<string>(type: "text", nullable: true),
                    external_id = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project", x => x.id);
                    table.CheckConstraint("ck_project_override", "health_override IS NULL OR health_override IN ('Green','Yellow','Red')");
                    table.CheckConstraint("ck_project_priority", "priority IN ('Low','Medium','High','Critical')");
                    table.CheckConstraint("ck_project_status", "status IN ('Setup','Active','On Hold','Complete','Archived','Cancelled')");
                    table.CheckConstraint("ck_project_visibility", "visibility IN ('Open','Restricted')");
                    table.ForeignKey(
                        name: "fk_project_app_user_project_manager_id",
                        column: x => x.project_manager_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_project_client_client_id",
                        column: x => x.client_id,
                        principalSchema: "hub",
                        principalTable: "client",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_project_office_office_id",
                        column: x => x.office_id,
                        principalSchema: "hub",
                        principalTable: "office",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_project_phase_phase_id",
                        column: x => x.phase_id,
                        principalSchema: "hub",
                        principalTable: "phase",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_project_project_types_project_type_id",
                        column: x => x.project_type_id,
                        principalSchema: "hub",
                        principalTable: "project_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_project_templates_created_from_template_id",
                        column: x => x.created_from_template_id,
                        principalSchema: "hub",
                        principalTable: "project_template",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "user_system_role",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "text", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    granted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    granted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_system_role", x => x.id);
                    table.CheckConstraint("ck_role", "role IN ('Admin','Executive','Supervisor','ProjectManager','ReadOnly')");
                    table.ForeignKey(
                        name: "fk_user_system_role_app_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "comment_mention",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    comment_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_comment_mention", x => x.id);
                    table.ForeignKey(
                        name: "fk_comment_mention_comment_comment_id",
                        column: x => x.comment_id,
                        principalSchema: "hub",
                        principalTable: "comment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "attention_item",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rule_id = table.Column<string>(type: "text", nullable: false),
                    item_type = table.Column<string>(type: "text", nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_key = table.Column<string>(type: "text", nullable: true),
                    item_name = table.Column<string>(type: "text", nullable: true),
                    severity = table.Column<string>(type: "text", nullable: false),
                    message = table.Column<string>(type: "text", nullable: false),
                    why = table.Column<string>(type: "jsonb", nullable: false),
                    route_to_user_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    project_discipline_id = table.Column<Guid>(type: "uuid", nullable: true),
                    days_overdue_or_blocked = table.Column<int>(type: "integer", nullable: false),
                    priority = table.Column<string>(type: "text", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    first_detected_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_evaluated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_attention_item", x => x.id);
                    table.ForeignKey(
                        name: "fk_attention_item_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "external_party",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    organisation = table.Column<string>(type: "text", nullable: true),
                    email = table.Column<string>(type: "text", nullable: true),
                    role = table.Column<string>(type: "text", nullable: true),
                    is_client = table.Column<bool>(type: "boolean", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_external_party", x => x.id);
                    table.ForeignKey(
                        name: "fk_external_party_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "issue",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    raised_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    severity = table.Column<string>(type: "text", nullable: false),
                    date_raised = table.Column<DateOnly>(type: "date", nullable: false),
                    target_resolution_date = table.Column<DateOnly>(type: "date", nullable: true),
                    resolution = table.Column<string>(type: "text", nullable: true),
                    resolved_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    origin_risk_id = table.Column<Guid>(type: "uuid", nullable: true),
                    project_discipline_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_activity_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "text", nullable: false),
                    seq = table.Column<int>(type: "integer", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_issue", x => x.id);
                    table.CheckConstraint("ck_issue_status", "status IN ('Open','In Progress','Resolved','Cancelled')");
                    table.ForeignKey(
                        name: "fk_issue_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "meeting_action",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    meeting_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    owner_type = table.Column<string>(type: "text", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    owner_discipline_id = table.Column<Guid>(type: "uuid", nullable: true),
                    owner_external_party_id = table.Column<Guid>(type: "uuid", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    related_task_id = table.Column<Guid>(type: "uuid", nullable: true),
                    related_decision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_activity_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "text", nullable: false),
                    seq = table.Column<int>(type: "integer", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meeting_action", x => x.id);
                    table.CheckConstraint("ck_action_status", "status IN ('Open','In Progress','Complete','Cancelled')");
                    table.ForeignKey(
                        name: "fk_meeting_action_meeting_meeting_id",
                        column: x => x.meeting_id,
                        principalSchema: "hub",
                        principalTable: "meeting",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_meeting_action_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_discipline",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    discipline_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lead_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_discipline", x => x.id);
                    table.ForeignKey(
                        name: "fk_project_discipline_app_user_lead_user_id",
                        column: x => x.lead_user_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_project_discipline_discipline_discipline_id",
                        column: x => x.discipline_id,
                        principalSchema: "hub",
                        principalTable: "discipline",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_project_discipline_project_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_follow",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    level = table.Column<string>(type: "text", nullable: false),
                    source = table.Column<string>(type: "text", nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_follow", x => x.id);
                    table.ForeignKey(
                        name: "fk_project_follow_project_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_health_snapshot",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    snapshot_date = table.Column<DateOnly>(type: "date", nullable: false),
                    computed_health = table.Column<string>(type: "text", nullable: false),
                    reported_health = table.Column<string>(type: "text", nullable: false),
                    inputs = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_health_snapshot", x => x.id);
                    table.ForeignKey(
                        name: "fk_project_health_snapshot_project_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_link",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    url = table.Column<string>(type: "text", nullable: false),
                    link_type = table.Column<string>(type: "text", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_link", x => x.id);
                    table.ForeignKey(
                        name: "fk_project_link_project_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "project_state",
                schema: "hub",
                columns: table => new
                {
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    computed_health = table.Column<string>(type: "text", nullable: false),
                    health_reasons = table.Column<string>(type: "jsonb", nullable: false),
                    inputs = table.Column<string>(type: "jsonb", nullable: false),
                    counts = table.Column<string>(type: "jsonb", nullable: false),
                    discipline_states = table.Column<string>(type: "jsonb", nullable: false),
                    next_milestone_id = table.Column<Guid>(type: "uuid", nullable: true),
                    next_submission_milestone_id = table.Column<Guid>(type: "uuid", nullable: true),
                    progress_pct = table.Column<int>(type: "integer", nullable: true),
                    overdue_tasks = table.Column<int>(type: "integer", nullable: false),
                    blocked_tasks = table.Column<int>(type: "integer", nullable: false),
                    overdue_decisions = table.Column<int>(type: "integer", nullable: false),
                    high_issues = table.Column<int>(type: "integer", nullable: false),
                    attention_critical = table.Column<int>(type: "integer", nullable: false),
                    attention_warning = table.Column<int>(type: "integer", nullable: false),
                    evaluated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_state", x => x.project_id);
                    table.ForeignKey(
                        name: "fk_project_state_project_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "risk",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    probability = table.Column<int>(type: "integer", nullable: false),
                    impact = table.Column<int>(type: "integer", nullable: false),
                    mitigation = table.Column<string>(type: "text", nullable: true),
                    trigger_indicator = table.Column<string>(type: "text", nullable: true),
                    review_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    realised_issue_id = table.Column<Guid>(type: "uuid", nullable: true),
                    project_discipline_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_activity_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "text", nullable: false),
                    seq = table.Column<int>(type: "integer", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_risk", x => x.id);
                    table.CheckConstraint("ck_risk_scores", "probability BETWEEN 1 AND 3 AND impact BETWEEN 1 AND 3");
                    table.CheckConstraint("ck_risk_status", "status IN ('Open','Monitoring','Closed','Realised')");
                    table.ForeignKey(
                        name: "fk_risk_project_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "workspace_project",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    workspace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_workspace_project", x => x.id);
                    table.ForeignKey(
                        name: "fk_workspace_project_project_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_workspace_project_workspace_workspace_id",
                        column: x => x.workspace_id,
                        principalSchema: "hub",
                        principalTable: "workspace",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "decision",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    requested_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    owner_external_party_id = table.Column<Guid>(type: "uuid", nullable: true),
                    date_requested = table.Column<DateOnly>(type: "date", nullable: false),
                    required_by_date = table.Column<DateOnly>(type: "date", nullable: false),
                    original_required_by_date = table.Column<DateOnly>(type: "date", nullable: false),
                    impact_level = table.Column<string>(type: "text", nullable: false),
                    impact_description = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    decision_text = table.Column<string>(type: "text", nullable: true),
                    decision_date = table.Column<DateOnly>(type: "date", nullable: true),
                    decided_by_id = table.Column<Guid>(type: "uuid", nullable: true),
                    deferral_reason = table.Column<string>(type: "text", nullable: true),
                    cancelled_reason = table.Column<string>(type: "text", nullable: true),
                    status_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    last_activity_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "text", nullable: false),
                    seq = table.Column<int>(type: "integer", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_decision", x => x.id);
                    table.CheckConstraint("ck_decision_owner", "num_nonnulls(owner_user_id, owner_external_party_id) = 1");
                    table.CheckConstraint("ck_decision_status", "status IN ('Pending','Under Review','Decided','Deferred','Cancelled')");
                    table.ForeignKey(
                        name: "fk_decision_app_user_owner_user_id",
                        column: x => x.owner_user_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_decision_app_user_requested_by_id",
                        column: x => x.requested_by_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_decision_external_parties_owner_external_party_id",
                        column: x => x.owner_external_party_id,
                        principalSchema: "hub",
                        principalTable: "external_party",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_decision_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "milestone",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    milestone_type = table.Column<string>(type: "text", nullable: false),
                    date = table.Column<DateOnly>(type: "date", nullable: true),
                    original_date = table.Column<DateOnly>(type: "date", nullable: true),
                    description = table.Column<string>(type: "text", nullable: true),
                    project_discipline_id = table.Column<Guid>(type: "uuid", nullable: true),
                    completes_phase_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_client_facing = table.Column<bool>(type: "boolean", nullable: false),
                    is_complete = table.Column<bool>(type: "boolean", nullable: false),
                    completed_date = table.Column<DateOnly>(type: "date", nullable: true),
                    is_cancelled = table.Column<bool>(type: "boolean", nullable: false),
                    cancelled_reason = table.Column<string>(type: "text", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    template_milestone_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "text", nullable: false),
                    seq = table.Column<int>(type: "integer", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_milestone", x => x.id);
                    table.CheckConstraint("ck_milestone_type", "milestone_type IN ('Kickoff','Field Work','Design Submission','Client Workshop','Permit Submission','Tender','Construction','IFC','Record Drawings','Closeout','Other')");
                    table.ForeignKey(
                        name: "fk_milestone_phases_completes_phase_id",
                        column: x => x.completes_phase_id,
                        principalSchema: "hub",
                        principalTable: "phase",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_milestone_project_disciplines_project_discipline_id",
                        column: x => x.project_discipline_id,
                        principalSchema: "hub",
                        principalTable: "project_discipline",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_milestone_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "project_member",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    roles = table.Column<List<string>>(type: "text[]", nullable: false),
                    primary_discipline_id = table.Column<Guid>(type: "uuid", nullable: true),
                    added_by = table.Column<Guid>(type: "uuid", nullable: true),
                    added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    removed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    removed_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_member", x => x.id);
                    table.CheckConstraint("ck_member_roles", "cardinality(roles) >= 1 AND roles <@ ARRAY['PM','TeamMember','Reviewer','Viewer']::text[]");
                    table.ForeignKey(
                        name: "fk_project_member_app_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_project_member_project_discipline_primary_discipline_id",
                        column: x => x.primary_discipline_id,
                        principalSchema: "hub",
                        principalTable: "project_discipline",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_project_member_project_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "decision_state",
                schema: "hub",
                columns: table => new
                {
                    decision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_overdue = table.Column<bool>(type: "boolean", nullable: false),
                    days_overdue = table.Column<int>(type: "integer", nullable: false),
                    is_due_soon = table.Column<bool>(type: "boolean", nullable: false),
                    blocking_count = table.Column<int>(type: "integer", nullable: false),
                    blocking_task_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    is_inactive_owner = table.Column<bool>(type: "boolean", nullable: false),
                    evaluated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_decision_state", x => x.decision_id);
                    table.ForeignKey(
                        name: "fk_decision_state_decision_decision_id",
                        column: x => x.decision_id,
                        principalSchema: "hub",
                        principalTable: "decision",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "deliverable",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    project_discipline_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deliverable_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    milestone_id = table.Column<Guid>(type: "uuid", nullable: true),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    original_start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    original_due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    priority = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    previous_status = table.Column<string>(type: "text", nullable: true),
                    revision = table.Column<string>(type: "text", nullable: true),
                    issued_date = table.Column<DateOnly>(type: "date", nullable: true),
                    issued_to = table.Column<string>(type: "text", nullable: true),
                    transmittal_url = table.Column<string>(type: "text", nullable: true),
                    issue_note = table.Column<string>(type: "text", nullable: true),
                    accepted_date = table.Column<DateOnly>(type: "date", nullable: true),
                    on_hold_reason = table.Column<string>(type: "text", nullable: true),
                    cancelled_reason = table.Column<string>(type: "text", nullable: true),
                    requires_review = table.Column<bool>(type: "boolean", nullable: false),
                    template_deliverable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_activity_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    status_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "text", nullable: false),
                    seq = table.Column<int>(type: "integer", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_deliverable", x => x.id);
                    table.CheckConstraint("ck_deliverable_dates", "start_date IS NULL OR due_date IS NULL OR start_date <= due_date");
                    table.CheckConstraint("ck_deliverable_status", "status IN ('Not Started','In Progress','In Review','Revision Required','Ready to Issue','Issued','Accepted','On Hold','Cancelled')");
                    table.ForeignKey(
                        name: "fk_deliverable_app_user_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_deliverable_app_user_reviewer_id",
                        column: x => x.reviewer_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_deliverable_deliverable_types_deliverable_type_id",
                        column: x => x.deliverable_type_id,
                        principalSchema: "hub",
                        principalTable: "deliverable_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_deliverable_milestones_milestone_id",
                        column: x => x.milestone_id,
                        principalSchema: "hub",
                        principalTable: "milestone",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_deliverable_project_disciplines_project_discipline_id",
                        column: x => x.project_discipline_id,
                        principalSchema: "hub",
                        principalTable: "project_discipline",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_deliverable_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "milestone_state",
                schema: "hub",
                columns: table => new
                {
                    milestone_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: true),
                    status_reasons = table.Column<string>(type: "jsonb", nullable: false),
                    days_remaining = table.Column<int>(type: "integer", nullable: true),
                    slip_days = table.Column<int>(type: "integer", nullable: false),
                    deliverable_total = table.Column<int>(type: "integer", nullable: false),
                    deliverable_issued = table.Column<int>(type: "integer", nullable: false),
                    task_total = table.Column<int>(type: "integer", nullable: false),
                    task_complete = table.Column<int>(type: "integer", nullable: false),
                    task_open = table.Column<int>(type: "integer", nullable: false),
                    task_overdue = table.Column<int>(type: "integer", nullable: false),
                    task_blocked = table.Column<int>(type: "integer", nullable: false),
                    evaluated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_milestone_state", x => x.milestone_id);
                    table.ForeignKey(
                        name: "fk_milestone_state_milestone_milestone_id",
                        column: x => x.milestone_id,
                        principalSchema: "hub",
                        principalTable: "milestone",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "deliverable_dependency",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    predecessor_deliverable_id = table.Column<Guid>(type: "uuid", nullable: false),
                    successor_deliverable_id = table.Column<Guid>(type: "uuid", nullable: false),
                    lag_days = table.Column<int>(type: "integer", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_deliverable_dependency", x => x.id);
                    table.CheckConstraint("ck_deliverable_dependency_self", "predecessor_deliverable_id <> successor_deliverable_id");
                    table.ForeignKey(
                        name: "fk_deliverable_dependency_deliverable_predecessor_deliverable_~",
                        column: x => x.predecessor_deliverable_id,
                        principalSchema: "hub",
                        principalTable: "deliverable",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_deliverable_dependency_deliverable_successor_deliverable_id",
                        column: x => x.successor_deliverable_id,
                        principalSchema: "hub",
                        principalTable: "deliverable",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "deliverable_issue",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deliverable_id = table.Column<Guid>(type: "uuid", nullable: false),
                    issued_date = table.Column<DateOnly>(type: "date", nullable: false),
                    revision = table.Column<string>(type: "text", nullable: true),
                    issued_to = table.Column<string>(type: "text", nullable: true),
                    transmittal_url = table.Column<string>(type: "text", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    issued_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_deliverable_issue", x => x.id);
                    table.ForeignKey(
                        name: "fk_deliverable_issue_deliverable_deliverable_id",
                        column: x => x.deliverable_id,
                        principalSchema: "hub",
                        principalTable: "deliverable",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "deliverable_state",
                schema: "hub",
                columns: table => new
                {
                    deliverable_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    progress_pct = table.Column<int>(type: "integer", nullable: true),
                    task_total = table.Column<int>(type: "integer", nullable: false),
                    task_complete = table.Column<int>(type: "integer", nullable: false),
                    task_cancelled = table.Column<int>(type: "integer", nullable: false),
                    task_open = table.Column<int>(type: "integer", nullable: false),
                    task_overdue = table.Column<int>(type: "integer", nullable: false),
                    task_blocked = table.Column<int>(type: "integer", nullable: false),
                    estimated_hours_total = table.Column<decimal>(type: "numeric", nullable: false),
                    remaining_hours = table.Column<decimal>(type: "numeric", nullable: false),
                    is_overdue = table.Column<bool>(type: "boolean", nullable: false),
                    days_overdue = table.Column<int>(type: "integer", nullable: false),
                    is_due_soon = table.Column<bool>(type: "boolean", nullable: false),
                    is_at_risk = table.Column<bool>(type: "boolean", nullable: false),
                    at_risk_reasons = table.Column<string>(type: "jsonb", nullable: false),
                    is_unassigned = table.Column<bool>(type: "boolean", nullable: false),
                    is_stale = table.Column<bool>(type: "boolean", nullable: false),
                    is_date_inconsistent = table.Column<bool>(type: "boolean", nullable: false),
                    inconsistency_detail = table.Column<string>(type: "jsonb", nullable: false),
                    slip_days = table.Column<int>(type: "integer", nullable: false),
                    is_inactive_owner = table.Column<bool>(type: "boolean", nullable: false),
                    issued_with_open_work = table.Column<bool>(type: "boolean", nullable: false),
                    milestone_cancelled = table.Column<bool>(type: "boolean", nullable: false),
                    is_waiting = table.Column<bool>(type: "boolean", nullable: false),
                    is_blocked = table.Column<bool>(type: "boolean", nullable: false),
                    blocked_by = table.Column<string>(type: "jsonb", nullable: false),
                    derived_predecessor_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    derived_successor_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    evaluated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_deliverable_state", x => x.deliverable_id);
                    table.ForeignKey(
                        name: "fk_deliverable_state_deliverable_deliverable_id",
                        column: x => x.deliverable_id,
                        principalSchema: "hub",
                        principalTable: "deliverable",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    description = table.Column<string>(type: "text", nullable: true),
                    project_discipline_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deliverable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    milestone_id = table.Column<Guid>(type: "uuid", nullable: true),
                    assignee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    requires_review = table.Column<bool>(type: "boolean", nullable: false),
                    priority = table.Column<string>(type: "text", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    original_start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    original_due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    previous_status = table.Column<string>(type: "text", nullable: true),
                    progress_pct = table.Column<int>(type: "integer", nullable: false),
                    estimated_hours = table.Column<decimal>(type: "numeric(7,1)", precision: 7, scale: 1, nullable: true),
                    manual_block_type = table.Column<string>(type: "text", nullable: true),
                    manual_block_reason = table.Column<string>(type: "text", nullable: true),
                    manual_block_set_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    manual_block_set_by = table.Column<Guid>(type: "uuid", nullable: true),
                    on_hold_reason = table.Column<string>(type: "text", nullable: true),
                    cancelled_reason = table.Column<string>(type: "text", nullable: true),
                    review_round = table.Column<int>(type: "integer", nullable: false),
                    due_date_change_count = table.Column<int>(type: "integer", nullable: false),
                    last_activity_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status_changed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    review_requested_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    template_task_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "text", nullable: false),
                    seq = table.Column<int>(type: "integer", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task", x => x.id);
                    table.CheckConstraint("ck_task_dates", "start_date IS NULL OR due_date IS NULL OR start_date <= due_date");
                    table.CheckConstraint("ck_task_parent", "milestone_id IS NULL OR deliverable_id IS NULL");
                    table.CheckConstraint("ck_task_progress", "progress_pct BETWEEN 0 AND 100 AND progress_pct % 10 = 0");
                    table.CheckConstraint("ck_task_status", "status IN ('Not Started','In Progress','Ready for Review','In Review','Revision Required','Complete','On Hold','Cancelled')");
                    table.ForeignKey(
                        name: "fk_task_app_user_assignee_id",
                        column: x => x.assignee_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_task_app_user_reviewer_id",
                        column: x => x.reviewer_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_task_deliverable_deliverable_id",
                        column: x => x.deliverable_id,
                        principalSchema: "hub",
                        principalTable: "deliverable",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_task_milestone_milestone_id",
                        column: x => x.milestone_id,
                        principalSchema: "hub",
                        principalTable: "milestone",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_task_project_discipline_project_discipline_id",
                        column: x => x.project_discipline_id,
                        principalSchema: "hub",
                        principalTable: "project_discipline",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_task_project_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "task_collaborator",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    added_by = table.Column<Guid>(type: "uuid", nullable: true),
                    added_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_collaborator", x => x.id);
                    table.ForeignKey(
                        name: "fk_task_collaborator_app_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_task_collaborator_task_task_id",
                        column: x => x.task_id,
                        principalSchema: "hub",
                        principalTable: "task",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task_dependency",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    predecessor_task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    successor_task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dependency_type = table.Column<string>(type: "text", nullable: false),
                    lag_days = table.Column<int>(type: "integer", nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_dependency", x => x.id);
                    table.CheckConstraint("ck_dependency_self", "predecessor_task_id <> successor_task_id");
                    table.ForeignKey(
                        name: "fk_task_dependency_task_predecessor_task_id",
                        column: x => x.predecessor_task_id,
                        principalSchema: "hub",
                        principalTable: "task",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_task_dependency_task_successor_task_id",
                        column: x => x.successor_task_id,
                        principalSchema: "hub",
                        principalTable: "task",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "task_state",
                schema: "hub",
                columns: table => new
                {
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    is_overdue = table.Column<bool>(type: "boolean", nullable: false),
                    days_overdue = table.Column<int>(type: "integer", nullable: false),
                    is_due_soon = table.Column<bool>(type: "boolean", nullable: false),
                    is_waiting = table.Column<bool>(type: "boolean", nullable: false),
                    is_blocked = table.Column<bool>(type: "boolean", nullable: false),
                    blocked_since = table.Column<DateOnly>(type: "date", nullable: true),
                    days_blocked = table.Column<int>(type: "integer", nullable: false),
                    blocked_by = table.Column<string>(type: "jsonb", nullable: false),
                    is_blocking = table.Column<bool>(type: "boolean", nullable: false),
                    blocking_count = table.Column<int>(type: "integer", nullable: false),
                    blocking_task_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    is_stale = table.Column<bool>(type: "boolean", nullable: false),
                    stale_days = table.Column<int>(type: "integer", nullable: false),
                    is_unassigned = table.Column<bool>(type: "boolean", nullable: false),
                    is_missing_due_date = table.Column<bool>(type: "boolean", nullable: false),
                    is_date_inconsistent = table.Column<bool>(type: "boolean", nullable: false),
                    inconsistency_detail = table.Column<string>(type: "jsonb", nullable: false),
                    is_inactive_owner = table.Column<bool>(type: "boolean", nullable: false),
                    is_held_past_due = table.Column<bool>(type: "boolean", nullable: false),
                    is_review_stalled = table.Column<bool>(type: "boolean", nullable: false),
                    affected_milestone_ids = table.Column<List<Guid>>(type: "uuid[]", nullable: false),
                    notes = table.Column<List<string>>(type: "text[]", nullable: false),
                    evaluated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_state", x => x.task_id);
                    table.ForeignKey(
                        name: "fk_task_state_task_task_id",
                        column: x => x.task_id,
                        principalSchema: "hub",
                        principalTable: "task",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task_time_entry",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_date = table.Column<DateOnly>(type: "date", nullable: false),
                    hours = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    note = table.Column<string>(type: "text", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_time_entry", x => x.id);
                    table.CheckConstraint("ck_time_hours", "hours > 0 AND hours <= 24");
                    table.ForeignKey(
                        name: "fk_task_time_entry_app_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_task_time_entry_task_task_id",
                        column: x => x.task_id,
                        principalSchema: "hub",
                        principalTable: "task",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_activity_log_actor_user_id_occurred_at",
                schema: "hub",
                table: "activity_log",
                columns: new[] { "actor_user_id", "occurred_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_activity_log_correlation_id",
                schema: "hub",
                table: "activity_log",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "ix_activity_log_item_type_item_id_occurred_at",
                schema: "hub",
                table: "activity_log",
                columns: new[] { "item_type", "item_id", "occurred_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_activity_log_project_id_occurred_at",
                schema: "hub",
                table: "activity_log",
                columns: new[] { "project_id", "occurred_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_app_user_display_name",
                schema: "hub",
                table: "app_user",
                column: "display_name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_app_user_email",
                schema: "hub",
                table: "app_user",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_app_user_entra_object_id",
                schema: "hub",
                table: "app_user",
                column: "entra_object_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_app_user_office_id",
                schema: "hub",
                table: "app_user",
                column: "office_id");

            migrationBuilder.CreateIndex(
                name: "ix_app_user_supervisor_id",
                schema: "hub",
                table: "app_user",
                column: "supervisor_id");

            migrationBuilder.CreateIndex(
                name: "ix_attention_item_project_id",
                schema: "hub",
                table: "attention_item",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_attention_item_route_to_user_ids",
                schema: "hub",
                table: "attention_item",
                column: "route_to_user_ids")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "ix_attention_item_rule_id_item_type_item_id",
                schema: "hub",
                table: "attention_item",
                columns: new[] { "rule_id", "item_type", "item_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_attention_snooze_rule_id_item_type_item_id",
                schema: "hub",
                table: "attention_snooze",
                columns: new[] { "rule_id", "item_type", "item_id" });

            migrationBuilder.CreateIndex(
                name: "ix_board_order_project_id_task_id",
                schema: "hub",
                table: "board_order",
                columns: new[] { "project_id", "task_id" },
                unique: true,
                filter: "project_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_board_order_workspace_id_task_id",
                schema: "hub",
                table: "board_order",
                columns: new[] { "workspace_id", "task_id" },
                unique: true,
                filter: "workspace_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_calendar_event_owner_id_start_at",
                schema: "hub",
                table: "calendar_event",
                columns: new[] { "owner_id", "start_at" });

            migrationBuilder.CreateIndex(
                name: "ix_calendar_event_project_id_start_at",
                schema: "hub",
                table: "calendar_event",
                columns: new[] { "project_id", "start_at" });

            migrationBuilder.CreateIndex(
                name: "ix_client_name",
                schema: "hub",
                table: "client",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_comment_author_id",
                schema: "hub",
                table: "comment",
                column: "author_id");

            migrationBuilder.CreateIndex(
                name: "ix_comment_item_type_item_id_created_at",
                schema: "hub",
                table: "comment",
                columns: new[] { "item_type", "item_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_comment_mention_comment_id",
                schema: "hub",
                table: "comment_mention",
                column: "comment_id");

            migrationBuilder.CreateIndex(
                name: "ix_decision_key",
                schema: "hub",
                table: "decision",
                column: "key");

            migrationBuilder.CreateIndex(
                name: "ix_decision_owner_external_party_id",
                schema: "hub",
                table: "decision",
                column: "owner_external_party_id");

            migrationBuilder.CreateIndex(
                name: "ix_decision_owner_user_id",
                schema: "hub",
                table: "decision",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_decision_project_id_seq",
                schema: "hub",
                table: "decision",
                columns: new[] { "project_id", "seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_decision_project_id_status",
                schema: "hub",
                table: "decision",
                columns: new[] { "project_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_decision_requested_by_id",
                schema: "hub",
                table: "decision",
                column: "requested_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_decision_subject",
                schema: "hub",
                table: "decision",
                column: "subject")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_decision_state_project_id",
                schema: "hub",
                table: "decision_state",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_deliverable_deliverable_type_id",
                schema: "hub",
                table: "deliverable",
                column: "deliverable_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_deliverable_key",
                schema: "hub",
                table: "deliverable",
                column: "key");

            migrationBuilder.CreateIndex(
                name: "ix_deliverable_milestone_id",
                schema: "hub",
                table: "deliverable",
                column: "milestone_id");

            migrationBuilder.CreateIndex(
                name: "ix_deliverable_owner_id_status",
                schema: "hub",
                table: "deliverable",
                columns: new[] { "owner_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_deliverable_project_discipline_id",
                schema: "hub",
                table: "deliverable",
                column: "project_discipline_id");

            migrationBuilder.CreateIndex(
                name: "ix_deliverable_project_id_seq",
                schema: "hub",
                table: "deliverable",
                columns: new[] { "project_id", "seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_deliverable_project_id_status",
                schema: "hub",
                table: "deliverable",
                columns: new[] { "project_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_deliverable_reviewer_id",
                schema: "hub",
                table: "deliverable",
                column: "reviewer_id");

            migrationBuilder.CreateIndex(
                name: "ix_deliverable_dependency_predecessor_deliverable_id_successor~",
                schema: "hub",
                table: "deliverable_dependency",
                columns: new[] { "predecessor_deliverable_id", "successor_deliverable_id" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_deliverable_dependency_successor_deliverable_id",
                schema: "hub",
                table: "deliverable_dependency",
                column: "successor_deliverable_id");

            migrationBuilder.CreateIndex(
                name: "ix_deliverable_issue_deliverable_id",
                schema: "hub",
                table: "deliverable_issue",
                column: "deliverable_id");

            migrationBuilder.CreateIndex(
                name: "ix_deliverable_state_project_id",
                schema: "hub",
                table: "deliverable_state",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_deliverable_type_default_discipline_id",
                schema: "hub",
                table: "deliverable_type",
                column: "default_discipline_id");

            migrationBuilder.CreateIndex(
                name: "ix_deliverable_type_name",
                schema: "hub",
                table: "deliverable_type",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_discipline_name",
                schema: "hub",
                table: "discipline",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_document_link_item_type_item_id",
                schema: "hub",
                table: "document_link",
                columns: new[] { "item_type", "item_id" });

            migrationBuilder.CreateIndex(
                name: "ix_email_message_sent_at",
                schema: "hub",
                table: "email_message",
                column: "sent_at");

            migrationBuilder.CreateIndex(
                name: "ix_external_party_project_id",
                schema: "hub",
                table: "external_party",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_holiday_office_id",
                schema: "hub",
                table: "holiday",
                column: "office_id");

            migrationBuilder.CreateIndex(
                name: "ix_issue_key",
                schema: "hub",
                table: "issue",
                column: "key");

            migrationBuilder.CreateIndex(
                name: "ix_issue_project_id_seq",
                schema: "hub",
                table: "issue",
                columns: new[] { "project_id", "seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_item_link_source_type_source_id",
                schema: "hub",
                table: "item_link",
                columns: new[] { "source_type", "source_id" });

            migrationBuilder.CreateIndex(
                name: "ix_item_link_source_type_source_id_target_type_target_id_relat~",
                schema: "hub",
                table: "item_link",
                columns: new[] { "source_type", "source_id", "target_type", "target_id", "relation" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_item_link_target_type_target_id",
                schema: "hub",
                table: "item_link",
                columns: new[] { "target_type", "target_id" });

            migrationBuilder.CreateIndex(
                name: "ix_item_watcher_item_type_item_id_user_id",
                schema: "hub",
                table: "item_watcher",
                columns: new[] { "item_type", "item_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_job_run_job_name_started_at",
                schema: "hub",
                table: "job_run",
                columns: new[] { "job_name", "started_at" });

            migrationBuilder.CreateIndex(
                name: "ix_meeting_project_id_meeting_date",
                schema: "hub",
                table: "meeting",
                columns: new[] { "project_id", "meeting_date" });

            migrationBuilder.CreateIndex(
                name: "ix_meeting_action_key",
                schema: "hub",
                table: "meeting_action",
                column: "key");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_action_meeting_id",
                schema: "hub",
                table: "meeting_action",
                column: "meeting_id");

            migrationBuilder.CreateIndex(
                name: "ix_meeting_action_project_id_seq",
                schema: "hub",
                table: "meeting_action",
                columns: new[] { "project_id", "seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_milestone_completes_phase_id",
                schema: "hub",
                table: "milestone",
                column: "completes_phase_id");

            migrationBuilder.CreateIndex(
                name: "ix_milestone_key",
                schema: "hub",
                table: "milestone",
                column: "key");

            migrationBuilder.CreateIndex(
                name: "ix_milestone_project_discipline_id",
                schema: "hub",
                table: "milestone",
                column: "project_discipline_id");

            migrationBuilder.CreateIndex(
                name: "ix_milestone_project_id_date",
                schema: "hub",
                table: "milestone",
                columns: new[] { "project_id", "date" });

            migrationBuilder.CreateIndex(
                name: "ix_milestone_project_id_seq",
                schema: "hub",
                table: "milestone",
                columns: new[] { "project_id", "seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_milestone_state_project_id",
                schema: "hub",
                table: "milestone_state",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_notification_user_id_collapse_key",
                schema: "hub",
                table: "notification",
                columns: new[] { "user_id", "collapse_key" });

            migrationBuilder.CreateIndex(
                name: "ix_notification_user_id_read_at_created_at",
                schema: "hub",
                table: "notification",
                columns: new[] { "user_id", "read_at", "created_at" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_notification_preference_user_id_event_type",
                schema: "hub",
                table: "notification_preference",
                columns: new[] { "user_id", "event_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_office_name",
                schema: "hub",
                table: "office",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_outbox_event_processed_at",
                schema: "hub",
                table: "outbox_event",
                column: "processed_at",
                filter: "processed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_phase_name",
                schema: "hub",
                table: "phase",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_project_client_id",
                schema: "hub",
                table: "project",
                column: "client_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_created_from_template_id",
                schema: "hub",
                table: "project",
                column: "created_from_template_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_name",
                schema: "hub",
                table: "project",
                column: "name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_project_number_trgm",
                schema: "hub",
                table: "project",
                column: "project_number")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_project_office_id",
                schema: "hub",
                table: "project",
                column: "office_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_phase_id",
                schema: "hub",
                table: "project",
                column: "phase_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_project_manager_id",
                schema: "hub",
                table: "project",
                column: "project_manager_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_project_number",
                schema: "hub",
                table: "project",
                column: "project_number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_project_project_type_id",
                schema: "hub",
                table: "project",
                column: "project_type_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_status",
                schema: "hub",
                table: "project",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_project_discipline_discipline_id",
                schema: "hub",
                table: "project_discipline",
                column: "discipline_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_discipline_lead_user_id",
                schema: "hub",
                table: "project_discipline",
                column: "lead_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_discipline_project_id_discipline_id",
                schema: "hub",
                table: "project_discipline",
                columns: new[] { "project_id", "discipline_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_project_follow_project_id_level",
                schema: "hub",
                table: "project_follow",
                columns: new[] { "project_id", "level" });

            migrationBuilder.CreateIndex(
                name: "ix_project_follow_user_id_project_id",
                schema: "hub",
                table: "project_follow",
                columns: new[] { "user_id", "project_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_project_health_snapshot_project_id_snapshot_date",
                schema: "hub",
                table: "project_health_snapshot",
                columns: new[] { "project_id", "snapshot_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_project_link_project_id",
                schema: "hub",
                table: "project_link",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_member_primary_discipline_id",
                schema: "hub",
                table: "project_member",
                column: "primary_discipline_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_member_project_id_user_id",
                schema: "hub",
                table: "project_member",
                columns: new[] { "project_id", "user_id" },
                unique: true,
                filter: "removed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_project_member_user_id",
                schema: "hub",
                table: "project_member",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_template_family_id",
                schema: "hub",
                table: "project_template",
                column: "family_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_type_name",
                schema: "hub",
                table: "project_type",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "ix_risk_key",
                schema: "hub",
                table: "risk",
                column: "key");

            migrationBuilder.CreateIndex(
                name: "ix_risk_project_id_seq",
                schema: "hub",
                table: "risk",
                columns: new[] { "project_id", "seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_saved_view_owner_id_list_type",
                schema: "hub",
                table: "saved_view",
                columns: new[] { "owner_id", "list_type" });

            migrationBuilder.CreateIndex(
                name: "ix_task_assignee_id_status",
                schema: "hub",
                table: "task",
                columns: new[] { "assignee_id", "status" },
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_task_created_by",
                schema: "hub",
                table: "task",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_task_deliverable_id",
                schema: "hub",
                table: "task",
                column: "deliverable_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_key",
                schema: "hub",
                table: "task",
                column: "key");

            migrationBuilder.CreateIndex(
                name: "ix_task_milestone_id",
                schema: "hub",
                table: "task",
                column: "milestone_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_project_discipline_id",
                schema: "hub",
                table: "task",
                column: "project_discipline_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_project_id_seq",
                schema: "hub",
                table: "task",
                columns: new[] { "project_id", "seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_task_project_id_status",
                schema: "hub",
                table: "task",
                columns: new[] { "project_id", "status" },
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_task_reviewer_id_status",
                schema: "hub",
                table: "task",
                columns: new[] { "reviewer_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_task_collaborator_task_id_user_id",
                schema: "hub",
                table: "task_collaborator",
                columns: new[] { "task_id", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_task_collaborator_user_id",
                schema: "hub",
                table: "task_collaborator",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_dependency_predecessor_task_id_successor_task_id",
                schema: "hub",
                table: "task_dependency",
                columns: new[] { "predecessor_task_id", "successor_task_id" },
                unique: true,
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_task_dependency_successor_task_id",
                schema: "hub",
                table: "task_dependency",
                column: "successor_task_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_state_is_blocking",
                schema: "hub",
                table: "task_state",
                column: "is_blocking");

            migrationBuilder.CreateIndex(
                name: "ix_task_state_project_id_is_blocked",
                schema: "hub",
                table: "task_state",
                columns: new[] { "project_id", "is_blocked" });

            migrationBuilder.CreateIndex(
                name: "ix_task_state_project_id_is_overdue",
                schema: "hub",
                table: "task_state",
                columns: new[] { "project_id", "is_overdue" });

            migrationBuilder.CreateIndex(
                name: "ix_task_time_entry_project_id_work_date",
                schema: "hub",
                table: "task_time_entry",
                columns: new[] { "project_id", "work_date" },
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_task_time_entry_task_id_work_date",
                schema: "hub",
                table: "task_time_entry",
                columns: new[] { "task_id", "work_date" },
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_task_time_entry_user_id_work_date",
                schema: "hub",
                table: "task_time_entry",
                columns: new[] { "user_id", "work_date" },
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_user_system_role_user_id_role_source",
                schema: "hub",
                table: "user_system_role",
                columns: new[] { "user_id", "role", "source" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_workspace_owner_id_name",
                schema: "hub",
                table: "workspace",
                columns: new[] { "owner_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_workspace_project_project_id",
                schema: "hub",
                table: "workspace_project",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_workspace_project_workspace_id_project_id",
                schema: "hub",
                table: "workspace_project",
                columns: new[] { "workspace_id", "project_id" },
                unique: true);

            // §20.3: the activity log is append-only; no code path can update or delete a row (AC-AUD-04).
            migrationBuilder.Sql(@"
CREATE FUNCTION hub.activity_log_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'activity_log is append-only'; END $$;
CREATE TRIGGER activity_log_no_update BEFORE UPDATE OR DELETE ON hub.activity_log FOR EACH ROW EXECUTE FUNCTION hub.activity_log_immutable();
CREATE TRIGGER activity_log_no_truncate BEFORE TRUNCATE ON hub.activity_log FOR EACH STATEMENT EXECUTE FUNCTION hub.activity_log_immutable();");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activity_log",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "attention_item",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "attention_snooze",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "board_order",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "calendar_event",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "comment_mention",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "dashboard_layout",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "decision_state",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "deliverable_dependency",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "deliverable_issue",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "deliverable_state",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "document_link",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "email_message",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "holiday",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "issue",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "item_link",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "item_watcher",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "job_run",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "meeting_action",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "milestone_state",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "notification",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "notification_preference",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "org_setting",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "outbox_event",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "project_follow",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "project_health_snapshot",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "project_link",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "project_member",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "project_star",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "project_state",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "risk",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "saved_view",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "task_collaborator",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "task_dependency",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "task_state",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "task_time_entry",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "template_deliverable",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "template_dependency",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "template_discipline",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "template_milestone",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "template_task",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "user_setting",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "user_system_role",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "workspace_project",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "comment",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "decision",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "meeting",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "task",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "workspace",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "external_party",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "deliverable",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "deliverable_type",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "milestone",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "project_discipline",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "discipline",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "project",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "app_user",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "client",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "phase",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "project_type",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "project_template",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "office",
                schema: "hub");
        }
    }
}
