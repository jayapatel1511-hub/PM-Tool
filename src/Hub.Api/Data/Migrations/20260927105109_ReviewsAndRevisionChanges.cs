using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReviewsAndRevisionChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "deliverable_id",
                schema: "hub",
                table: "source_revision",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid[]>(
                name: "author_ids",
                schema: "hub",
                table: "source_revision",
                type: "uuid[]",
                nullable: false,
                defaultValue: new Guid[0]);

            migrationBuilder.AddColumn<string>(
                name: "external_identifier",
                schema: "hub",
                table: "source_revision",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "issuer",
                schema: "hub",
                table: "source_revision",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "scope",
                schema: "hub",
                table: "source_revision",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "source_checked_at",
                schema: "hub",
                table: "source_revision",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_identity",
                schema: "hub",
                table: "source_revision",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "source_system",
                schema: "hub",
                table: "source_revision",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "supersedes_id",
                schema: "hub",
                table: "source_revision",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "next_change_seq",
                schema: "hub",
                table: "project",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "next_review_seq",
                schema: "hub",
                table: "project",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "required_review_package_id",
                schema: "hub",
                table: "deliverable",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "change_notice",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_discipline_id = table.Column<Guid>(type: "uuid", nullable: false),
                    old_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    new_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "text", nullable: false),
                    scope = table.Column<string>(type: "text", nullable: false),
                    effective_date = table.Column<DateOnly>(type: "date", nullable: false),
                    assessment_due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    published_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("pk_change_notice", x => x.id);
                    table.CheckConstraint("ck_change_notice_status", "status IN ('Draft','Open','Closed','Cancelled')");
                    table.ForeignKey(
                        name: "fk_change_notice_app_user_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_change_notice_project_disciplines_project_discipline_id",
                        column: x => x.project_discipline_id,
                        principalSchema: "hub",
                        principalTable: "project_discipline",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_change_notice_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_change_notice_source_revisions_new_revision_id",
                        column: x => x.new_revision_id,
                        principalSchema: "hub",
                        principalTable: "source_revision",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_change_notice_source_revisions_old_revision_id",
                        column: x => x.old_revision_id,
                        principalSchema: "hub",
                        principalTable: "source_revision",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "coordination_command",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payload_hash = table.Column<string>(type: "text", nullable: false),
                    result_id = table.Column<Guid>(type: "uuid", nullable: false),
                    result_version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_coordination_command", x => x.id);
                    table.ForeignKey(
                        name: "fk_coordination_command_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "input_use",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "text", nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_identity = table.Column<string>(type: "text", nullable: false),
                    source_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    intended_use = table.Column<string>(type: "text", nullable: false),
                    adopted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    adopted_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_input_use", x => x.id);
                    table.CheckConstraint("ck_input_use_type", "target_type IN ('Task', 'Deliverable')");
                    table.ForeignKey(
                        name: "fk_input_use_app_user_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_input_use_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_input_use_source_revisions_source_revision_id",
                        column: x => x.source_revision_id,
                        principalSchema: "hub",
                        principalTable: "source_revision",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "source_head",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    identity = table.Column<string>(type: "text", nullable: false),
                    current_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_discipline_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_source_head", x => x.id);
                    table.ForeignKey(
                        name: "fk_source_head_app_user_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_source_head_project_discipline_project_discipline_id",
                        column: x => x.project_discipline_id,
                        principalSchema: "hub",
                        principalTable: "project_discipline",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_source_head_project_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_source_head_source_revisions_current_revision_id",
                        column: x => x.current_revision_id,
                        principalSchema: "hub",
                        principalTable: "source_revision",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "change_assessment",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    change_notice_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<string>(type: "text", nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewer_id = table.Column<Guid>(type: "uuid", nullable: true),
                    input_use_id = table.Column<Guid>(type: "uuid", nullable: true),
                    handoff_id = table.Column<Guid>(type: "uuid", nullable: true),
                    revision_used_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    acknowledged_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    rationale = table.Column<string>(type: "text", nullable: true),
                    evidence_url = table.Column<string>(type: "text", nullable: true),
                    correction_task_id = table.Column<Guid>(type: "uuid", nullable: true),
                    effort_impact_hours = table.Column<decimal>(type: "numeric", nullable: true),
                    date_impact_days = table.Column<int>(type: "integer", nullable: true),
                    retain_old_revision = table.Column<bool>(type: "boolean", nullable: false),
                    retention_approved_by = table.Column<Guid>(type: "uuid", nullable: true),
                    retention_reason = table.Column<string>(type: "text", nullable: true),
                    verified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_change_assessment", x => x.id);
                    table.CheckConstraint("ck_change_assessment_status", "status IN ('Pending Assessment','Unaffected','Update Required','Clarification Needed','Resolved')");
                    table.ForeignKey(
                        name: "fk_change_assessment_app_user_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_change_assessment_app_user_reviewer_id",
                        column: x => x.reviewer_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_change_assessment_change_notices_change_notice_id",
                        column: x => x.change_notice_id,
                        principalSchema: "hub",
                        principalTable: "change_notice",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_change_assessment_handoffs_handoff_id",
                        column: x => x.handoff_id,
                        principalSchema: "hub",
                        principalTable: "handoff",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_change_assessment_input_uses_input_use_id",
                        column: x => x.input_use_id,
                        principalSchema: "hub",
                        principalTable: "input_use",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_change_assessment_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_change_assessment_source_revisions_revision_used_id",
                        column: x => x.revision_used_id,
                        principalSchema: "hub",
                        principalTable: "source_revision",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_change_assessment_task_correction_task_id",
                        column: x => x.correction_task_id,
                        principalSchema: "hub",
                        principalTable: "task",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "input_adoption",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    input_use_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    intended_use = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_input_adoption", x => x.id);
                    table.ForeignKey(
                        name: "fk_input_adoption_input_uses_input_use_id",
                        column: x => x.input_use_id,
                        principalSchema: "hub",
                        principalTable: "input_use",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_input_adoption_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_input_adoption_source_revisions_source_revision_id",
                        column: x => x.source_revision_id,
                        principalSchema: "hub",
                        principalTable: "source_revision",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "discipline_review",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    round_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_discipline_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    due_date = table.Column<DateOnly>(type: "date", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    rationale = table.Column<string>(type: "text", nullable: true),
                    decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_discipline_review", x => x.id);
                    table.CheckConstraint("ck_discipline_review_status", "status IN ('Pending','In Review','Changes Required','Approved')");
                    table.ForeignKey(
                        name: "fk_discipline_review_app_user_reviewer_id",
                        column: x => x.reviewer_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_discipline_review_project_disciplines_project_discipline_id",
                        column: x => x.project_discipline_id,
                        principalSchema: "hub",
                        principalTable: "project_discipline",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_discipline_review_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "finding_event",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    finding_id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    evidence_url = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_finding_event", x => x.id);
                    table.ForeignKey(
                        name: "fk_finding_event_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "review_finding",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    package_id = table.Column<Guid>(type: "uuid", nullable: false),
                    round_id = table.Column<Guid>(type: "uuid", nullable: false),
                    carried_from_id = table.Column<Guid>(type: "uuid", nullable: true),
                    source_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_discipline_id = table.Column<Guid>(type: "uuid", nullable: false),
                    originator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    resolver_id = table.Column<Guid>(type: "uuid", nullable: false),
                    verifier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "text", nullable: false),
                    severity = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    response = table.Column<string>(type: "text", nullable: true),
                    evidence_url = table.Column<string>(type: "text", nullable: true),
                    withdrawal_acknowledged_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_review_finding", x => x.id);
                    table.CheckConstraint("ck_review_finding_severity", "severity IN ('Blocking', 'Advisory')");
                    table.CheckConstraint("ck_review_finding_status", "status IN ('Open','Responded','Verified Closed','Withdrawn')");
                    table.ForeignKey(
                        name: "fk_review_finding_app_user_originator_id",
                        column: x => x.originator_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_review_finding_app_user_resolver_id",
                        column: x => x.resolver_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_review_finding_app_user_verifier_id",
                        column: x => x.verifier_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_review_finding_project_discipline_project_discipline_id",
                        column: x => x.project_discipline_id,
                        principalSchema: "hub",
                        principalTable: "project_discipline",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_review_finding_project_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_review_finding_review_finding_carried_from_id",
                        column: x => x.carried_from_id,
                        principalSchema: "hub",
                        principalTable: "review_finding",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_review_finding_source_revisions_source_revision_id",
                        column: x => x.source_revision_id,
                        principalSchema: "hub",
                        principalTable: "source_revision",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "review_manifest_item",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    round_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deliverable_id = table.Column<Guid>(type: "uuid", nullable: false),
                    author_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_review_manifest_item", x => x.id);
                    table.ForeignKey(
                        name: "fk_review_manifest_item_deliverable_deliverable_id",
                        column: x => x.deliverable_id,
                        principalSchema: "hub",
                        principalTable: "deliverable",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_review_manifest_item_project_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_review_manifest_item_source_revisions_source_revision_id",
                        column: x => x.source_revision_id,
                        principalSchema: "hub",
                        principalTable: "source_revision",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "review_package",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    purpose = table.Column<string>(type: "text", nullable: false),
                    coordinator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_discipline_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    current_round_id = table.Column<Guid>(type: "uuid", nullable: true),
                    round_number = table.Column<int>(type: "integer", nullable: false),
                    required_for_issue = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_review_package", x => x.id);
                    table.CheckConstraint("ck_review_package_status", "status IN ('Draft','In Review','Changes Required','Approved','Superseded','Cancelled')");
                    table.ForeignKey(
                        name: "fk_review_package_app_user_coordinator_id",
                        column: x => x.coordinator_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_review_package_project_discipline_project_discipline_id",
                        column: x => x.project_discipline_id,
                        principalSchema: "hub",
                        principalTable: "project_discipline",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_review_package_project_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "review_round",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    package_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    purpose = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    removal_impact = table.Column<string>(type: "text", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_review_round", x => x.id);
                    table.ForeignKey(
                        name: "fk_review_round_project_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_review_round_review_package_package_id",
                        column: x => x.package_id,
                        principalSchema: "hub",
                        principalTable: "review_package",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_source_revision_supersedes_id",
                schema: "hub",
                table: "source_revision",
                column: "supersedes_id");

            migrationBuilder.CreateIndex(
                name: "ix_deliverable_required_review_package_id",
                schema: "hub",
                table: "deliverable",
                column: "required_review_package_id");

            migrationBuilder.CreateIndex(
                name: "ix_change_assessment_change_notice_id_target_type_target_id",
                schema: "hub",
                table: "change_assessment",
                columns: new[] { "change_notice_id", "target_type", "target_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_change_assessment_correction_task_id",
                schema: "hub",
                table: "change_assessment",
                column: "correction_task_id");

            migrationBuilder.CreateIndex(
                name: "ix_change_assessment_handoff_id",
                schema: "hub",
                table: "change_assessment",
                column: "handoff_id");

            migrationBuilder.CreateIndex(
                name: "ix_change_assessment_input_use_id",
                schema: "hub",
                table: "change_assessment",
                column: "input_use_id");

            migrationBuilder.CreateIndex(
                name: "ix_change_assessment_owner_id",
                schema: "hub",
                table: "change_assessment",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_change_assessment_project_id",
                schema: "hub",
                table: "change_assessment",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_change_assessment_reviewer_id",
                schema: "hub",
                table: "change_assessment",
                column: "reviewer_id");

            migrationBuilder.CreateIndex(
                name: "ix_change_assessment_revision_used_id",
                schema: "hub",
                table: "change_assessment",
                column: "revision_used_id");

            migrationBuilder.CreateIndex(
                name: "ix_change_notice_key",
                schema: "hub",
                table: "change_notice",
                column: "key");

            migrationBuilder.CreateIndex(
                name: "ix_change_notice_new_revision_id",
                schema: "hub",
                table: "change_notice",
                column: "new_revision_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_change_notice_old_revision_id",
                schema: "hub",
                table: "change_notice",
                column: "old_revision_id");

            migrationBuilder.CreateIndex(
                name: "ix_change_notice_owner_id",
                schema: "hub",
                table: "change_notice",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_change_notice_project_discipline_id",
                schema: "hub",
                table: "change_notice",
                column: "project_discipline_id");

            migrationBuilder.CreateIndex(
                name: "ix_change_notice_project_id_seq",
                schema: "hub",
                table: "change_notice",
                columns: new[] { "project_id", "seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_change_notice_project_id_status",
                schema: "hub",
                table: "change_notice",
                columns: new[] { "project_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_coordination_command_project_id_actor_id_request_id",
                schema: "hub",
                table: "coordination_command",
                columns: new[] { "project_id", "actor_id", "request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_discipline_review_project_discipline_id",
                schema: "hub",
                table: "discipline_review",
                column: "project_discipline_id");

            migrationBuilder.CreateIndex(
                name: "ix_discipline_review_project_id",
                schema: "hub",
                table: "discipline_review",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_discipline_review_reviewer_id",
                schema: "hub",
                table: "discipline_review",
                column: "reviewer_id");

            migrationBuilder.CreateIndex(
                name: "ix_discipline_review_round_id_project_discipline_id",
                schema: "hub",
                table: "discipline_review",
                columns: new[] { "round_id", "project_discipline_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_finding_event_finding_id",
                schema: "hub",
                table: "finding_event",
                column: "finding_id");

            migrationBuilder.CreateIndex(
                name: "ix_finding_event_project_id",
                schema: "hub",
                table: "finding_event",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_input_adoption_input_use_id",
                schema: "hub",
                table: "input_adoption",
                column: "input_use_id");

            migrationBuilder.CreateIndex(
                name: "ix_input_adoption_project_id",
                schema: "hub",
                table: "input_adoption",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_input_adoption_source_revision_id",
                schema: "hub",
                table: "input_adoption",
                column: "source_revision_id");

            migrationBuilder.CreateIndex(
                name: "ix_input_use_owner_id",
                schema: "hub",
                table: "input_use",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_input_use_project_id_target_type_target_id_source_identity",
                schema: "hub",
                table: "input_use",
                columns: new[] { "project_id", "target_type", "target_id", "source_identity" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_input_use_source_revision_id",
                schema: "hub",
                table: "input_use",
                column: "source_revision_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_finding_carried_from_id",
                schema: "hub",
                table: "review_finding",
                column: "carried_from_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_finding_originator_id",
                schema: "hub",
                table: "review_finding",
                column: "originator_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_finding_package_id",
                schema: "hub",
                table: "review_finding",
                column: "package_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_finding_project_discipline_id",
                schema: "hub",
                table: "review_finding",
                column: "project_discipline_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_finding_project_id",
                schema: "hub",
                table: "review_finding",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_finding_resolver_id",
                schema: "hub",
                table: "review_finding",
                column: "resolver_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_finding_round_id_status",
                schema: "hub",
                table: "review_finding",
                columns: new[] { "round_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_review_finding_source_revision_id",
                schema: "hub",
                table: "review_finding",
                column: "source_revision_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_finding_verifier_id",
                schema: "hub",
                table: "review_finding",
                column: "verifier_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_manifest_item_deliverable_id",
                schema: "hub",
                table: "review_manifest_item",
                column: "deliverable_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_manifest_item_project_id",
                schema: "hub",
                table: "review_manifest_item",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_manifest_item_round_id_deliverable_id",
                schema: "hub",
                table: "review_manifest_item",
                columns: new[] { "round_id", "deliverable_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_review_manifest_item_source_revision_id",
                schema: "hub",
                table: "review_manifest_item",
                column: "source_revision_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_package_coordinator_id",
                schema: "hub",
                table: "review_package",
                column: "coordinator_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_package_current_round_id",
                schema: "hub",
                table: "review_package",
                column: "current_round_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_package_key",
                schema: "hub",
                table: "review_package",
                column: "key");

            migrationBuilder.CreateIndex(
                name: "ix_review_package_project_discipline_id",
                schema: "hub",
                table: "review_package",
                column: "project_discipline_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_package_project_id_seq",
                schema: "hub",
                table: "review_package",
                columns: new[] { "project_id", "seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_review_package_project_id_status",
                schema: "hub",
                table: "review_package",
                columns: new[] { "project_id", "status" });

            migrationBuilder.CreateIndex(
                name: "ix_review_round_package_id_number",
                schema: "hub",
                table: "review_round",
                columns: new[] { "package_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_review_round_project_id",
                schema: "hub",
                table: "review_round",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_source_head_current_revision_id",
                schema: "hub",
                table: "source_head",
                column: "current_revision_id");

            migrationBuilder.CreateIndex(
                name: "ix_source_head_owner_id",
                schema: "hub",
                table: "source_head",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_source_head_project_discipline_id",
                schema: "hub",
                table: "source_head",
                column: "project_discipline_id");

            migrationBuilder.CreateIndex(
                name: "ix_source_head_project_id_identity",
                schema: "hub",
                table: "source_head",
                columns: new[] { "project_id", "identity" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_deliverable_review_packages_required_review_package_id",
                schema: "hub",
                table: "deliverable",
                column: "required_review_package_id",
                principalSchema: "hub",
                principalTable: "review_package",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_source_revision_source_revision_supersedes_id",
                schema: "hub",
                table: "source_revision",
                column: "supersedes_id",
                principalSchema: "hub",
                principalTable: "source_revision",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_discipline_review_review_rounds_round_id",
                schema: "hub",
                table: "discipline_review",
                column: "round_id",
                principalSchema: "hub",
                principalTable: "review_round",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_finding_event_review_findings_finding_id",
                schema: "hub",
                table: "finding_event",
                column: "finding_id",
                principalSchema: "hub",
                principalTable: "review_finding",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_review_finding_review_packages_package_id",
                schema: "hub",
                table: "review_finding",
                column: "package_id",
                principalSchema: "hub",
                principalTable: "review_package",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_review_finding_review_rounds_round_id",
                schema: "hub",
                table: "review_finding",
                column: "round_id",
                principalSchema: "hub",
                principalTable: "review_round",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_review_manifest_item_review_rounds_round_id",
                schema: "hub",
                table: "review_manifest_item",
                column: "round_id",
                principalSchema: "hub",
                principalTable: "review_round",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_review_package_review_rounds_current_round_id",
                schema: "hub",
                table: "review_package",
                column: "current_round_id",
                principalSchema: "hub",
                principalTable: "review_round",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_deliverable_review_packages_required_review_package_id",
                schema: "hub",
                table: "deliverable");

            migrationBuilder.DropForeignKey(
                name: "fk_source_revision_source_revision_supersedes_id",
                schema: "hub",
                table: "source_revision");

            migrationBuilder.DropForeignKey(
                name: "fk_review_package_review_rounds_current_round_id",
                schema: "hub",
                table: "review_package");

            migrationBuilder.DropTable(
                name: "change_assessment",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "coordination_command",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "discipline_review",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "finding_event",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "input_adoption",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "review_manifest_item",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "source_head",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "change_notice",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "review_finding",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "input_use",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "review_round",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "review_package",
                schema: "hub");

            migrationBuilder.DropIndex(
                name: "ix_source_revision_supersedes_id",
                schema: "hub",
                table: "source_revision");

            migrationBuilder.DropIndex(
                name: "ix_deliverable_required_review_package_id",
                schema: "hub",
                table: "deliverable");

            migrationBuilder.DropColumn(
                name: "author_ids",
                schema: "hub",
                table: "source_revision");

            migrationBuilder.DropColumn(
                name: "external_identifier",
                schema: "hub",
                table: "source_revision");

            migrationBuilder.DropColumn(
                name: "issuer",
                schema: "hub",
                table: "source_revision");

            migrationBuilder.DropColumn(
                name: "scope",
                schema: "hub",
                table: "source_revision");

            migrationBuilder.DropColumn(
                name: "source_checked_at",
                schema: "hub",
                table: "source_revision");

            migrationBuilder.DropColumn(
                name: "source_identity",
                schema: "hub",
                table: "source_revision");

            migrationBuilder.DropColumn(
                name: "source_system",
                schema: "hub",
                table: "source_revision");

            migrationBuilder.DropColumn(
                name: "supersedes_id",
                schema: "hub",
                table: "source_revision");

            migrationBuilder.DropColumn(
                name: "next_change_seq",
                schema: "hub",
                table: "project");

            migrationBuilder.DropColumn(
                name: "next_review_seq",
                schema: "hub",
                table: "project");

            migrationBuilder.DropColumn(
                name: "required_review_package_id",
                schema: "hub",
                table: "deliverable");

            migrationBuilder.AlterColumn<Guid>(
                name: "deliverable_id",
                schema: "hub",
                table: "source_revision",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
