using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DisciplineHandoffs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "next_handoff_seq",
                schema: "hub",
                table: "project",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid[]>(
                name: "required_project_ids",
                schema: "hub",
                table: "email_message",
                type: "uuid[]",
                nullable: false,
                defaultValue: new Guid[0]);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "suppressed_at",
                schema: "hub",
                table: "email_message",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "source_revision",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deliverable_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_row_version = table.Column<int>(type: "integer", nullable: false),
                    identity_hash = table.Column<string>(type: "text", nullable: false),
                    source_key = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    revision = table.Column<string>(type: "text", nullable: false),
                    url = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_source_revision", x => x.id);
                    table.ForeignKey(
                        name: "fk_source_revision_deliverable_deliverable_id",
                        column: x => x.deliverable_id,
                        principalSchema: "hub",
                        principalTable: "deliverable",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_source_revision_project_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "handoff",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    source_deliverable_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_row_version = table.Column<int>(type: "integer", nullable: false),
                    declared_revision = table.Column<string>(type: "text", nullable: false),
                    source_url = table.Column<string>(type: "text", nullable: false),
                    sending_discipline_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receiving_discipline_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sending_owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receiving_owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_task_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target_deliverable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    intended_use = table.Column<string>(type: "text", nullable: false),
                    acceptance_criteria = table.Column<string>(type: "text", nullable: false),
                    needed_by = table.Column<DateOnly>(type: "date", nullable: false),
                    promised_by = table.Column<DateOnly>(type: "date", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    current_revision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    incorporated_revision_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_handoff", x => x.id);
                    table.CheckConstraint("ck_handoff_status", "status IN ('Draft','Submitted','Clarification Requested','Returned','Accepted','Incorporated','Cancelled')");
                    table.CheckConstraint("ck_handoff_target", "(target_task_id IS NULL) <> (target_deliverable_id IS NULL)");
                    table.ForeignKey(
                        name: "fk_handoff_app_user_receiving_owner_id",
                        column: x => x.receiving_owner_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_handoff_app_user_sending_owner_id",
                        column: x => x.sending_owner_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_handoff_deliverable_source_deliverable_id",
                        column: x => x.source_deliverable_id,
                        principalSchema: "hub",
                        principalTable: "deliverable",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_handoff_deliverable_target_deliverable_id",
                        column: x => x.target_deliverable_id,
                        principalSchema: "hub",
                        principalTable: "deliverable",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_handoff_project_disciplines_receiving_discipline_id",
                        column: x => x.receiving_discipline_id,
                        principalSchema: "hub",
                        principalTable: "project_discipline",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_handoff_project_disciplines_sending_discipline_id",
                        column: x => x.sending_discipline_id,
                        principalSchema: "hub",
                        principalTable: "project_discipline",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_handoff_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_handoff_task_target_task_id",
                        column: x => x.target_task_id,
                        principalSchema: "hub",
                        principalTable: "task",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "handoff_command",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    payload_hash = table.Column<string>(type: "text", nullable: false),
                    handoff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    result_version = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_handoff_command", x => x.id);
                    table.ForeignKey(
                        name: "fk_handoff_command_handoff_handoff_id",
                        column: x => x.handoff_id,
                        principalSchema: "hub",
                        principalTable: "handoff",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "handoff_revision",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    handoff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    previous_revision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sending_owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    receiving_owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    intended_use = table.Column<string>(type: "text", nullable: false),
                    acceptance_criteria = table.Column<string>(type: "text", nullable: false),
                    needed_by = table.Column<DateOnly>(type: "date", nullable: false),
                    promised_by = table.Column<DateOnly>(type: "date", nullable: false),
                    target_task_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target_deliverable_id = table.Column<Guid>(type: "uuid", nullable: true),
                    response = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_handoff_revision", x => x.id);
                    table.ForeignKey(
                        name: "fk_handoff_revision_handoff_handoff_id",
                        column: x => x.handoff_id,
                        principalSchema: "hub",
                        principalTable: "handoff",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_handoff_revision_handoff_revision_previous_revision_id",
                        column: x => x.previous_revision_id,
                        principalSchema: "hub",
                        principalTable: "handoff_revision",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_handoff_revision_source_revisions_source_revision_id",
                        column: x => x.source_revision_id,
                        principalSchema: "hub",
                        principalTable: "source_revision",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "handoff_receipt_event",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    handoff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    revision_id = table.Column<Guid>(type: "uuid", nullable: true),
                    from_status = table.Column<string>(type: "text", nullable: false),
                    to_status = table.Column<string>(type: "text", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: true),
                    criteria_outcome = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_handoff_receipt_event", x => x.id);
                    table.ForeignKey(
                        name: "fk_handoff_receipt_event_handoff_handoff_id",
                        column: x => x.handoff_id,
                        principalSchema: "hub",
                        principalTable: "handoff",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_handoff_receipt_event_handoff_revisions_revision_id",
                        column: x => x.revision_id,
                        principalSchema: "hub",
                        principalTable: "handoff_revision",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_handoff_current_revision_id",
                schema: "hub",
                table: "handoff",
                column: "current_revision_id");

            migrationBuilder.CreateIndex(
                name: "ix_handoff_incorporated_revision_id",
                schema: "hub",
                table: "handoff",
                column: "incorporated_revision_id");

            migrationBuilder.CreateIndex(
                name: "ix_handoff_key",
                schema: "hub",
                table: "handoff",
                column: "key");

            migrationBuilder.CreateIndex(
                name: "ix_handoff_project_id_seq",
                schema: "hub",
                table: "handoff",
                columns: new[] { "project_id", "seq" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_handoff_project_id_status_needed_by",
                schema: "hub",
                table: "handoff",
                columns: new[] { "project_id", "status", "needed_by" },
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_handoff_receiving_discipline_id",
                schema: "hub",
                table: "handoff",
                column: "receiving_discipline_id");

            migrationBuilder.CreateIndex(
                name: "ix_handoff_receiving_owner_id",
                schema: "hub",
                table: "handoff",
                column: "receiving_owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_handoff_sending_discipline_id",
                schema: "hub",
                table: "handoff",
                column: "sending_discipline_id");

            migrationBuilder.CreateIndex(
                name: "ix_handoff_sending_owner_id",
                schema: "hub",
                table: "handoff",
                column: "sending_owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_handoff_source_deliverable_id",
                schema: "hub",
                table: "handoff",
                column: "source_deliverable_id");

            migrationBuilder.CreateIndex(
                name: "ix_handoff_target_deliverable_id",
                schema: "hub",
                table: "handoff",
                column: "target_deliverable_id");

            migrationBuilder.CreateIndex(
                name: "ix_handoff_target_task_id",
                schema: "hub",
                table: "handoff",
                column: "target_task_id");

            migrationBuilder.CreateIndex(
                name: "ix_handoff_command_handoff_id",
                schema: "hub",
                table: "handoff_command",
                column: "handoff_id");

            migrationBuilder.CreateIndex(
                name: "ix_handoff_command_project_id_actor_id_request_id",
                schema: "hub",
                table: "handoff_command",
                columns: new[] { "project_id", "actor_id", "request_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_handoff_receipt_event_handoff_id_created_at",
                schema: "hub",
                table: "handoff_receipt_event",
                columns: new[] { "handoff_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_handoff_receipt_event_revision_id",
                schema: "hub",
                table: "handoff_receipt_event",
                column: "revision_id");

            migrationBuilder.CreateIndex(
                name: "ix_handoff_revision_handoff_id",
                schema: "hub",
                table: "handoff_revision",
                column: "handoff_id");

            migrationBuilder.CreateIndex(
                name: "ix_handoff_revision_previous_revision_id",
                schema: "hub",
                table: "handoff_revision",
                column: "previous_revision_id");

            migrationBuilder.CreateIndex(
                name: "ix_handoff_revision_source_revision_id",
                schema: "hub",
                table: "handoff_revision",
                column: "source_revision_id");

            migrationBuilder.CreateIndex(
                name: "ix_source_revision_deliverable_id",
                schema: "hub",
                table: "source_revision",
                column: "deliverable_id");

            migrationBuilder.CreateIndex(
                name: "ix_source_revision_project_id_identity_hash",
                schema: "hub",
                table: "source_revision",
                columns: new[] { "project_id", "identity_hash" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_handoff_handoff_revisions_current_revision_id",
                schema: "hub",
                table: "handoff",
                column: "current_revision_id",
                principalSchema: "hub",
                principalTable: "handoff_revision",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_handoff_handoff_revisions_incorporated_revision_id",
                schema: "hub",
                table: "handoff",
                column: "incorporated_revision_id",
                principalSchema: "hub",
                principalTable: "handoff_revision",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_handoff_handoff_revisions_current_revision_id",
                schema: "hub",
                table: "handoff");

            migrationBuilder.DropForeignKey(
                name: "fk_handoff_handoff_revisions_incorporated_revision_id",
                schema: "hub",
                table: "handoff");

            migrationBuilder.DropTable(
                name: "handoff_command",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "handoff_receipt_event",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "handoff_revision",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "handoff",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "source_revision",
                schema: "hub");

            migrationBuilder.DropColumn(
                name: "next_handoff_seq",
                schema: "hub",
                table: "project");

            migrationBuilder.DropColumn(
                name: "required_project_ids",
                schema: "hub",
                table: "email_message");

            migrationBuilder.DropColumn(
                name: "suppressed_at",
                schema: "hub",
                table: "email_message");
        }
    }
}
