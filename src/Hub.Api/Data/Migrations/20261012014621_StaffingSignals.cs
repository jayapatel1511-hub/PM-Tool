using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class StaffingSignals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "started_at",
                schema: "hub",
                table: "task",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "dependency_type",
                schema: "hub",
                table: "deliverable_dependency",
                type: "text",
                nullable: false,
                defaultValue: "FinishToStart");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "started_at",
                schema: "hub",
                table: "deliverable",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "home_discipline_id",
                schema: "hub",
                table: "app_user",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "open_role",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_label = table.Column<string>(type: "text", nullable: false),
                    project_discipline_id = table.Column<Guid>(type: "uuid", nullable: true),
                    from_date = table.Column<DateOnly>(type: "date", nullable: false),
                    through_date = table.Column<DateOnly>(type: "date", nullable: false),
                    planned_hours = table.Column<decimal>(type: "numeric(9,3)", precision: 9, scale: 3, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    filled_allocation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    note = table.Column<string>(type: "text", nullable: true),
                    deleted_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    deleted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_open_role", x => x.id);
                    table.CheckConstraint("ck_open_role_dates", "through_date >= from_date");
                    table.CheckConstraint("ck_open_role_hours", "planned_hours > 0");
                    table.CheckConstraint("ck_open_role_label", "char_length(btrim(role_label)) BETWEEN 1 AND 80");
                    table.CheckConstraint("ck_open_role_status", "status IN ('Open','Filled','Cancelled')");
                    table.ForeignKey(
                        name: "fk_open_role_allocations_filled_allocation_id",
                        column: x => x.filled_allocation_id,
                        principalSchema: "hub",
                        principalTable: "resource_allocation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_open_role_project_disciplines_project_discipline_id",
                        column: x => x.project_discipline_id,
                        principalSchema: "hub",
                        principalTable: "project_discipline",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_open_role_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Every existing link behaved as Finish-to-Start: the column was never read before packet 036 (AC-DEPT-01a).
            migrationBuilder.Sql("UPDATE hub.task_dependency SET dependency_type = 'FinishToStart' WHERE dependency_type <> 'FinishToStart';");

            // D4 backfill for items that have left Not Started: the first logged Status change from Not Started to a
            // started or finished status (On Hold and Cancelled are not a start), else StatusChangedAt, else CreatedAt.
            // Items still Not Started, or held or cancelled straight from Not Started, get no fallback.
            foreach (var (table, itemType) in new[] { ("task", "Task"), ("deliverable", "Deliverable") })
                migrationBuilder.Sql($$"""
                    UPDATE hub.{{table}} AS x SET started_at = COALESCE(
                        (SELECT min(a.occurred_at) FROM hub.activity_log a
                          WHERE a.item_type = '{{itemType}}' AND a.item_id = x.id
                            AND jsonb_path_exists(a.changes, '$[*] ? (@.field == "Status" && @.old == "Not Started" && @.new != "On Hold" && @.new != "Cancelled")')),
                        CASE WHEN x.status IN ('On Hold', 'Cancelled') AND x.previous_status = 'Not Started' THEN NULL
                             ELSE COALESCE(x.status_changed_at, x.created_at) END)
                    WHERE x.status <> 'Not Started';
                    """);

            migrationBuilder.AddCheckConstraint(
                name: "ck_task_dependency_type",
                schema: "hub",
                table: "task_dependency",
                sql: "dependency_type IN ('FinishToStart','StartToStart','FinishToFinish')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_deliverable_dependency_type",
                schema: "hub",
                table: "deliverable_dependency",
                sql: "dependency_type IN ('FinishToStart','StartToStart','FinishToFinish')");

            migrationBuilder.CreateIndex(
                name: "ix_app_user_home_discipline_id",
                schema: "hub",
                table: "app_user",
                column: "home_discipline_id");

            migrationBuilder.CreateIndex(
                name: "ix_open_role_filled_allocation_id",
                schema: "hub",
                table: "open_role",
                column: "filled_allocation_id",
                unique: true,
                filter: "filled_allocation_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_open_role_project_discipline_id",
                schema: "hub",
                table: "open_role",
                column: "project_discipline_id");

            migrationBuilder.CreateIndex(
                name: "ix_open_role_project_id_status",
                schema: "hub",
                table: "open_role",
                columns: new[] { "project_id", "status" },
                filter: "deleted_at IS NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_app_user_disciplines_home_discipline_id",
                schema: "hub",
                table: "app_user",
                column: "home_discipline_id",
                principalSchema: "hub",
                principalTable: "discipline",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_app_user_disciplines_home_discipline_id",
                schema: "hub",
                table: "app_user");

            migrationBuilder.DropTable(
                name: "open_role",
                schema: "hub");

            migrationBuilder.DropCheckConstraint(
                name: "ck_task_dependency_type",
                schema: "hub",
                table: "task_dependency");

            migrationBuilder.DropCheckConstraint(
                name: "ck_deliverable_dependency_type",
                schema: "hub",
                table: "deliverable_dependency");

            migrationBuilder.DropIndex(
                name: "ix_app_user_home_discipline_id",
                schema: "hub",
                table: "app_user");

            migrationBuilder.DropColumn(
                name: "started_at",
                schema: "hub",
                table: "task");

            migrationBuilder.DropColumn(
                name: "dependency_type",
                schema: "hub",
                table: "deliverable_dependency");

            migrationBuilder.DropColumn(
                name: "started_at",
                schema: "hub",
                table: "deliverable");

            migrationBuilder.DropColumn(
                name: "home_discipline_id",
                schema: "hub",
                table: "app_user");
        }
    }
}
