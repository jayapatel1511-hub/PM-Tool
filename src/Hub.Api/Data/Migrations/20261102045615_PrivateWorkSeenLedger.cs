using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PrivateWorkSeenLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "my_work_first_visit_at",
                schema: "hub",
                table: "user_setting",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "my_work_last_mark_all_at",
                schema: "hub",
                table: "user_setting",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "my_work_last_visit_at",
                schema: "hub",
                table: "user_setting",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "my_work_previous_visit_at",
                schema: "hub",
                table: "user_setting",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "my_work_seen_revision",
                schema: "hub",
                table: "user_setting",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "work_assignment_occurrence",
                schema: "hub",
                columns: table => new
                {
                    token = table.Column<Guid>(type: "uuid", nullable: false),
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    item_type = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    source_version = table.Column<int>(type: "integer", nullable: false),
                    source_activity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_occurred_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    provenance = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_assignment_occurrence", x => x.token);
                    table.UniqueConstraint("ak_work_assignment_occurrence_token_recipient_id", x => new { x.token, x.recipient_id });
                    table.CheckConstraint("ck_work_assignment_provenance", "(provenance = 'Published' AND source_activity_id IS NOT NULL AND source_occurred_at IS NOT NULL) OR (provenance = 'BaselineSeed' AND source_activity_id IS NULL AND source_occurred_at IS NULL)");
                    table.CheckConstraint("ck_work_assignment_role", "(item_type = 'Task' AND role IN ('Assignee', 'Reviewer')) OR (item_type = 'Deliverable' AND role IN ('Owner', 'Reviewer')) OR (item_type IN ('Decision', 'Action') AND role = 'InternalOwner') OR (item_type IN ('Risk', 'Issue', 'ChangeNotice') AND role = 'Owner') OR (item_type = 'Handoff' AND role IN ('SendingOwner', 'ReceivingOwner')) OR (item_type = 'Project' AND role = 'ProjectManager')");
                    table.CheckConstraint("ck_work_assignment_source_version", "source_version >= 0");
                    table.ForeignKey(
                        name: "fk_work_assignment_occurrence_activity_log_source_activity_id",
                        column: x => x.source_activity_id,
                        principalSchema: "hub",
                        principalTable: "activity_log",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_assignment_occurrence_app_user_recipient_id",
                        column: x => x.recipient_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_work_assignment_occurrence_project_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "work_seen_command_receipt",
                schema: "hub",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    payload_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    response_json = table.Column<string>(type: "jsonb", nullable: false),
                    committed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_seen_command_receipt", x => new { x.user_id, x.request_id });
                    table.CheckConstraint("ck_work_seen_command_action", "action IN ('Visit', 'Opened', 'MarkAll')");
                    table.CheckConstraint("ck_work_seen_command_hash", "payload_hash ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_work_seen_command_response", "jsonb_typeof(response_json) = 'object'");
                    table.ForeignKey(
                        name: "fk_work_seen_command_receipt_app_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "work_seen_acknowledgement",
                schema: "hub",
                columns: table => new
                {
                    recipient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token = table.Column<Guid>(type: "uuid", nullable: false),
                    acknowledged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    provenance = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_work_seen_acknowledgement", x => new { x.recipient_id, x.token });
                    table.CheckConstraint("ck_work_seen_ack_provenance", "provenance IN ('InitialBaseline', 'Opened', 'MarkAll')");
                    table.ForeignKey(
                        name: "fk_work_seen_acknowledgement_work_assignment_occurrence_token_~",
                        columns: x => new { x.token, x.recipient_id },
                        principalSchema: "hub",
                        principalTable: "work_assignment_occurrence",
                        principalColumns: new[] { "token", "recipient_id" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_work_assignment_occurrence_item_type_item_id_role_source_ve~",
                schema: "hub",
                table: "work_assignment_occurrence",
                columns: new[] { "item_type", "item_id", "role", "source_version" },
                unique: true,
                descending: new[] { false, false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_work_assignment_occurrence_project_id",
                schema: "hub",
                table: "work_assignment_occurrence",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_work_assignment_occurrence_recipient_id_token",
                schema: "hub",
                table: "work_assignment_occurrence",
                columns: new[] { "recipient_id", "token" });

            migrationBuilder.CreateIndex(
                name: "ix_work_assignment_occurrence_source_activity_id_role",
                schema: "hub",
                table: "work_assignment_occurrence",
                columns: new[] { "source_activity_id", "role" },
                unique: true,
                filter: "source_activity_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_work_seen_acknowledgement_token_recipient_id",
                schema: "hub",
                table: "work_seen_acknowledgement",
                columns: new[] { "token", "recipient_id" });

            // Quiescent cutover only. Preserve unknown legacy source events/times and every current role,
            // including hidden/closed/deleted history, without changing any canonical row or audit event.
            migrationBuilder.Sql("""
                -- Apply only as the ordinary migration within a quiescent application cutover: no old/new application writers.
                -- The normal migration transaction holds these source locks through the complete baseline insertion.
                -- Include hidden, archived, closed and soft-deleted canonical rows; first visit baselines history without exposing it.
                -- No canonical update, version increment, activity insertion, inferred assignment time or private acknowledgement.
                LOCK TABLE hub.task, hub.deliverable, hub.decision, hub.meeting_action, hub.risk,
                    hub.issue, hub.handoff, hub.change_notice, hub.project IN SHARE MODE;
                WITH legacy_roles AS (
                    SELECT 'Task'::text item_type, id item_id, project_id, 'Assignee'::text role, assignee_id recipient_id, row_version source_version FROM hub.task
                    UNION ALL SELECT 'Task', id, project_id, 'Reviewer', reviewer_id, row_version FROM hub.task
                    UNION ALL SELECT 'Deliverable', id, project_id, 'Owner', owner_id, row_version FROM hub.deliverable
                    UNION ALL SELECT 'Deliverable', id, project_id, 'Reviewer', reviewer_id, row_version FROM hub.deliverable
                    UNION ALL SELECT 'Decision', id, project_id, 'InternalOwner', owner_user_id, row_version FROM hub.decision WHERE owner_external_party_id IS NULL
                    UNION ALL SELECT 'Action', id, project_id, 'InternalOwner', owner_user_id, row_version FROM hub.meeting_action WHERE owner_type = 'User'
                    UNION ALL SELECT 'Risk', id, project_id, 'Owner', owner_id, row_version FROM hub.risk
                    UNION ALL SELECT 'Issue', id, project_id, 'Owner', owner_id, row_version FROM hub.issue
                    UNION ALL SELECT 'ChangeNotice', id, project_id, 'Owner', owner_id, row_version FROM hub.change_notice
                    UNION ALL SELECT 'Handoff', id, project_id, 'SendingOwner', sending_owner_id, row_version FROM hub.handoff
                    UNION ALL SELECT 'Handoff', id, project_id, 'ReceivingOwner', receiving_owner_id, row_version FROM hub.handoff
                    UNION ALL SELECT 'Project', id, id, 'ProjectManager', project_manager_id, row_version FROM hub.project
                )
                INSERT INTO hub.work_assignment_occurrence
                    (token, recipient_id, item_type, item_id, project_id, role, source_version,
                     source_activity_id, source_occurred_at, provenance, recorded_at)
                SELECT gen_random_uuid(), recipient_id, item_type, item_id, project_id, role, source_version,
                    NULL, NULL, 'BaselineSeed', clock_timestamp()
                FROM legacy_roles
                WHERE recipient_id IS NOT NULL AND recipient_id <> '00000000-0000-0000-0000-000000000000'::uuid;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "work_seen_acknowledgement",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "work_seen_command_receipt",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "work_assignment_occurrence",
                schema: "hub");

            migrationBuilder.DropColumn(
                name: "my_work_first_visit_at",
                schema: "hub",
                table: "user_setting");

            migrationBuilder.DropColumn(
                name: "my_work_last_mark_all_at",
                schema: "hub",
                table: "user_setting");

            migrationBuilder.DropColumn(
                name: "my_work_last_visit_at",
                schema: "hub",
                table: "user_setting");

            migrationBuilder.DropColumn(
                name: "my_work_previous_visit_at",
                schema: "hub",
                table: "user_setting");

            migrationBuilder.DropColumn(
                name: "my_work_seen_revision",
                schema: "hub",
                table: "user_setting");
        }
    }
}
