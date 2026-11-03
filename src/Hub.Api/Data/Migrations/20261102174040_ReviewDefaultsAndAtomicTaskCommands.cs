using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReviewDefaultsAndAtomicTaskCommands : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid[]>(
                name: "required_project_ids",
                schema: "hub",
                table: "notification",
                type: "uuid[]",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "calendar_view_project_id",
                schema: "hub",
                table: "idempotency_record",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "calendar_view_scope",
                schema: "hub",
                table: "idempotency_record",
                type: "character varying(8)",
                maxLength: 8,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "deliverable_review_default",
                schema: "hub",
                columns: table => new
                {
                    deliverable_type_id = table.Column<Guid>(type: "uuid", nullable: false),
                    discipline_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_deliverable_review_default", x => new { x.deliverable_type_id, x.discipline_id });
                    table.ForeignKey(
                        name: "fk_deliverable_review_default_deliverable_types_deliverable_typ~",
                        column: x => x.deliverable_type_id,
                        principalSchema: "hub",
                        principalTable: "deliverable_type",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_deliverable_review_default_disciplines_discipline_id",
                        column: x => x.discipline_id,
                        principalSchema: "hub",
                        principalTable: "discipline",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "task_bulk_command_receipt",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    request_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hash_version = table.Column<int>(type: "integer", nullable: false),
                    intent_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    intent_json = table.Column<string>(type: "jsonb", nullable: false),
                    response_json = table.Column<string>(type: "jsonb", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    completed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_bulk_command_receipt", x => x.id);
                    table.CheckConstraint("ck_task_bulk_command_completed", "completed_at >= created_at");
                    table.CheckConstraint("ck_task_bulk_command_hash", "hash_version = 1 AND intent_hash ~ '^[0-9a-f]{64}$'");
                    table.CheckConstraint("ck_task_bulk_command_json", "jsonb_typeof(intent_json) = 'object' AND jsonb_typeof(response_json) = 'object'");
                    table.ForeignKey(
                        name: "fk_task_bulk_command_receipt_app_user_actor_id",
                        column: x => x.actor_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_idempotency_calendar_context",
                schema: "hub",
                table: "idempotency_record",
                sql: "(calendar_view_scope IS NULL AND calendar_view_project_id IS NULL) OR (calendar_view_scope IS NOT NULL AND calendar_view_scope IN ('Personal', 'Project') AND (calendar_view_scope = 'Personal' OR calendar_view_project_id IS NOT NULL))");

            migrationBuilder.CreateIndex(
                name: "ix_deliverable_review_default_discipline_id",
                schema: "hub",
                table: "deliverable_review_default",
                column: "discipline_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_bulk_command_receipt_actor_id_request_id",
                schema: "hub",
                table: "task_bulk_command_receipt",
                columns: new[] { "actor_id", "request_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "deliverable_review_default",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "task_bulk_command_receipt",
                schema: "hub");

            migrationBuilder.DropCheckConstraint(
                name: "ck_idempotency_calendar_context",
                schema: "hub",
                table: "idempotency_record");

            migrationBuilder.DropColumn(
                name: "required_project_ids",
                schema: "hub",
                table: "notification");

            migrationBuilder.DropColumn(
                name: "calendar_view_project_id",
                schema: "hub",
                table: "idempotency_record");

            migrationBuilder.DropColumn(
                name: "calendar_view_scope",
                schema: "hub",
                table: "idempotency_record");
        }
    }
}
