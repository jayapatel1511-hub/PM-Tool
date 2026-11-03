using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class RegisterMetadataAndTemplateLineage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "template_risk_id",
                schema: "hub",
                table: "risk",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "copied_from_snapshot",
                schema: "hub",
                table: "project_template",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "copied_from_template_id",
                schema: "hub",
                table: "project_template",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "root_cause_category_id",
                schema: "hub",
                table: "issue",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "root_cause_label_snapshot",
                schema: "hub",
                table: "issue",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "category_id",
                schema: "hub",
                table: "decision",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string[]>(
                name: "options_considered",
                schema: "hub",
                table: "decision",
                type: "character varying(500)[]",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "rationale",
                schema: "hub",
                table: "decision",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "decision_category",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "citext", maxLength: 120, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_decision_category", x => x.id);
                    table.CheckConstraint("ck_decision_category_name", "name = btrim(name) AND char_length(name) BETWEEN 1 AND 120 AND name !~ '[[:cntrl:]]'");
                });

            migrationBuilder.CreateTable(
                name: "issue_root_cause_category",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "citext", maxLength: 120, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_issue_root_cause_category", x => x.id);
                    table.CheckConstraint("ck_issue_root_cause_category_name", "name = btrim(name) AND char_length(name) BETWEEN 1 AND 120 AND name !~ '[[:cntrl:]]'");
                });

            migrationBuilder.CreateTable(
                name: "template_risk",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    probability = table.Column<int>(type: "integer", nullable: false),
                    impact = table.Column<int>(type: "integer", nullable: false),
                    mitigation = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    trigger_indicator = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_template_risk", x => x.id);
                    table.CheckConstraint("ck_template_risk_scores", "probability BETWEEN 1 AND 3 AND impact BETWEEN 1 AND 3");
                    table.CheckConstraint("ck_template_risk_title", "char_length(btrim(title)) BETWEEN 1 AND 300");
                    table.ForeignKey(
                        name: "fk_template_risk_project_template_template_id",
                        column: x => x.template_id,
                        principalSchema: "hub",
                        principalTable: "project_template",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_risk_template_risk_id",
                schema: "hub",
                table: "risk",
                column: "template_risk_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_template_copied_from_template_id",
                schema: "hub",
                table: "project_template",
                column: "copied_from_template_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_template_copy_baseline_pair",
                schema: "hub",
                table: "project_template",
                sql: "(copied_from_template_id IS NULL) = (copied_from_snapshot IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_issue_root_cause_category_id",
                schema: "hub",
                table: "issue",
                column: "root_cause_category_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_issue_root_cause_pair",
                schema: "hub",
                table: "issue",
                sql: "(root_cause_category_id IS NULL AND root_cause_label_snapshot IS NULL) OR (root_cause_category_id IS NOT NULL AND root_cause_label_snapshot IS NOT NULL AND btrim(root_cause_label_snapshot) <> '')");

            migrationBuilder.CreateIndex(
                name: "ix_decision_category_id",
                schema: "hub",
                table: "decision",
                column: "category_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_decision_options_considered",
                schema: "hub",
                table: "decision",
                sql: "options_considered IS NULL OR (cardinality(options_considered) <= 5 AND array_position(options_considered, NULL) IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_decision_category_name",
                schema: "hub",
                table: "decision_category",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_issue_root_cause_category_name",
                schema: "hub",
                table: "issue_root_cause_category",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_template_risk_template_id_sort_order",
                schema: "hub",
                table: "template_risk",
                columns: new[] { "template_id", "sort_order" });

            migrationBuilder.AddForeignKey(
                name: "fk_decision_decision_categories_category_id",
                schema: "hub",
                table: "decision",
                column: "category_id",
                principalSchema: "hub",
                principalTable: "decision_category",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_issue_issue_root_cause_categories_root_cause_category_id",
                schema: "hub",
                table: "issue",
                column: "root_cause_category_id",
                principalSchema: "hub",
                principalTable: "issue_root_cause_category",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_project_template_project_template_copied_from_template_id",
                schema: "hub",
                table: "project_template",
                column: "copied_from_template_id",
                principalSchema: "hub",
                principalTable: "project_template",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_risk_template_risks_template_risk_id",
                schema: "hub",
                table: "risk",
                column: "template_risk_id",
                principalSchema: "hub",
                principalTable: "template_risk",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_decision_decision_categories_category_id",
                schema: "hub",
                table: "decision");

            migrationBuilder.DropForeignKey(
                name: "fk_issue_issue_root_cause_categories_root_cause_category_id",
                schema: "hub",
                table: "issue");

            migrationBuilder.DropForeignKey(
                name: "fk_project_template_project_template_copied_from_template_id",
                schema: "hub",
                table: "project_template");

            migrationBuilder.DropForeignKey(
                name: "fk_risk_template_risks_template_risk_id",
                schema: "hub",
                table: "risk");

            migrationBuilder.DropTable(
                name: "decision_category",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "issue_root_cause_category",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "template_risk",
                schema: "hub");

            migrationBuilder.DropIndex(
                name: "ix_risk_template_risk_id",
                schema: "hub",
                table: "risk");

            migrationBuilder.DropIndex(
                name: "ix_project_template_copied_from_template_id",
                schema: "hub",
                table: "project_template");

            migrationBuilder.DropCheckConstraint(
                name: "ck_template_copy_baseline_pair",
                schema: "hub",
                table: "project_template");

            migrationBuilder.DropIndex(
                name: "ix_issue_root_cause_category_id",
                schema: "hub",
                table: "issue");

            migrationBuilder.DropCheckConstraint(
                name: "ck_issue_root_cause_pair",
                schema: "hub",
                table: "issue");

            migrationBuilder.DropIndex(
                name: "ix_decision_category_id",
                schema: "hub",
                table: "decision");

            migrationBuilder.DropCheckConstraint(
                name: "ck_decision_options_considered",
                schema: "hub",
                table: "decision");

            migrationBuilder.DropColumn(
                name: "template_risk_id",
                schema: "hub",
                table: "risk");

            migrationBuilder.DropColumn(
                name: "copied_from_snapshot",
                schema: "hub",
                table: "project_template");

            migrationBuilder.DropColumn(
                name: "copied_from_template_id",
                schema: "hub",
                table: "project_template");

            migrationBuilder.DropColumn(
                name: "root_cause_category_id",
                schema: "hub",
                table: "issue");

            migrationBuilder.DropColumn(
                name: "root_cause_label_snapshot",
                schema: "hub",
                table: "issue");

            migrationBuilder.DropColumn(
                name: "category_id",
                schema: "hub",
                table: "decision");

            migrationBuilder.DropColumn(
                name: "options_considered",
                schema: "hub",
                table: "decision");

            migrationBuilder.DropColumn(
                name: "rationale",
                schema: "hub",
                table: "decision");
        }
    }
}
