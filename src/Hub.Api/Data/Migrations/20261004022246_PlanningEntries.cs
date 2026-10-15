using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class PlanningEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "planning_entry",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hours_per_week = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: false),
                    start_week = table.Column<DateOnly>(type: "date", nullable: false),
                    end_week = table.Column<DateOnly>(type: "date", nullable: false),
                    label = table.Column<string>(type: "text", nullable: false),
                    source_category = table.Column<string>(type: "text", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    project_discipline_id = table.Column<Guid>(type: "uuid", nullable: true),
                    confidence = table.Column<string>(type: "text", nullable: false),
                    visibility = table.Column<string>(type: "text", nullable: false),
                    notes = table.Column<string>(type: "text", nullable: true),
                    last_validated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("pk_planning_entry", x => x.id);
                    table.CheckConstraint("ck_planning_confidence", "confidence IN ('Confirmed','Expected','Possible')");
                    table.CheckConstraint("ck_planning_discipline", "project_discipline_id IS NULL OR project_id IS NOT NULL");
                    table.CheckConstraint("ck_planning_hours", "hours_per_week > 0 AND hours_per_week <= 168 AND hours_per_week * 2 = trunc(hours_per_week * 2)");
                    table.CheckConstraint("ck_planning_label", "char_length(btrim(label)) BETWEEN 1 AND 120");
                    table.CheckConstraint("ck_planning_notes", "notes IS NULL OR char_length(notes) <= 2000");
                    table.CheckConstraint("ck_planning_owner", "created_by IS NOT NULL");
                    table.CheckConstraint("ck_planning_project_source", "(source_category = 'MajorProject') = (project_id IS NOT NULL)");
                    table.CheckConstraint("ck_planning_self_visible", "created_by <> person_id OR visibility = 'Confirmed'");
                    table.CheckConstraint("ck_planning_source", "source_category IN ('MajorProject','OtherProject','Proposal','BusinessDevelopment','Training','Admin','Supervision','InternalInitiative','FieldWork','Other')");
                    table.CheckConstraint("ck_planning_visibility", "visibility IN ('Draft','Published','Confirmed')");
                    table.CheckConstraint("ck_planning_weeks", "extract(isodow FROM start_week) = 1 AND extract(isodow FROM end_week) = 1 AND end_week >= start_week AND end_week - start_week <= 721");
                    table.ForeignKey(
                        name: "fk_planning_entry_app_user_created_by",
                        column: x => x.created_by,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_planning_entry_app_user_deleted_by",
                        column: x => x.deleted_by,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_planning_entry_app_user_person_id",
                        column: x => x.person_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_planning_entry_app_user_updated_by",
                        column: x => x.updated_by,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_planning_entry_project_disciplines_project_discipline_id",
                        column: x => x.project_discipline_id,
                        principalSchema: "hub",
                        principalTable: "project_discipline",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_planning_entry_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_planning_entry_created_by_end_week",
                schema: "hub",
                table: "planning_entry",
                columns: new[] { "created_by", "end_week" },
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_planning_entry_deleted_by",
                schema: "hub",
                table: "planning_entry",
                column: "deleted_by");

            migrationBuilder.CreateIndex(
                name: "ix_planning_entry_person_id_start_week_end_week",
                schema: "hub",
                table: "planning_entry",
                columns: new[] { "person_id", "start_week", "end_week" },
                filter: "deleted_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_planning_entry_project_discipline_id",
                schema: "hub",
                table: "planning_entry",
                column: "project_discipline_id");

            migrationBuilder.CreateIndex(
                name: "ix_planning_entry_project_id",
                schema: "hub",
                table: "planning_entry",
                column: "project_id",
                filter: "project_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_planning_entry_updated_by",
                schema: "hub",
                table: "planning_entry",
                column: "updated_by");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "planning_entry",
                schema: "hub");
        }
    }
}
