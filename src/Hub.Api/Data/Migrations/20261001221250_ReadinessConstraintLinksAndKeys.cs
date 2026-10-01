using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ReadinessConstraintLinksAndKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "key",
                schema: "hub",
                table: "work_constraint",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "linked_id",
                schema: "hub",
                table: "work_constraint",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "linked_type",
                schema: "hub",
                table: "work_constraint",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "seq",
                schema: "hub",
                table: "work_constraint",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "next_commitment_seq",
                schema: "hub",
                table: "project",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "next_constraint_seq",
                schema: "hub",
                table: "project",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "key",
                schema: "hub",
                table: "output_commitment",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<int>(
                name: "seq",
                schema: "hub",
                table: "output_commitment",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // §10.8 keys for rows recorded before keys existed: numbered per project in creation order (Keys.Format pads to
            // three digits and never truncates), then each project's counter continues after the highest number.
            migrationBuilder.Sql("""
                UPDATE hub.work_constraint c SET seq = n.rn,
                    key = p.project_number || '-CT' || CASE WHEN n.rn < 1000 THEN lpad(n.rn::text, 3, '0') ELSE n.rn::text END
                FROM (SELECT id, row_number() OVER (PARTITION BY project_id ORDER BY created_at, id) AS rn FROM hub.work_constraint) n, hub.project p
                WHERE n.id = c.id AND p.id = c.project_id;
                UPDATE hub.output_commitment c SET seq = n.rn,
                    key = p.project_number || '-WC' || CASE WHEN n.rn < 1000 THEN lpad(n.rn::text, 3, '0') ELSE n.rn::text END
                FROM (SELECT id, row_number() OVER (PARTITION BY project_id ORDER BY created_at, id) AS rn FROM hub.output_commitment) n, hub.project p
                WHERE n.id = c.id AND p.id = c.project_id;
                UPDATE hub.project p SET
                    next_constraint_seq = 1 + coalesce((SELECT max(seq) FROM hub.work_constraint c WHERE c.project_id = p.id), 0),
                    next_commitment_seq = 1 + coalesce((SELECT max(seq) FROM hub.output_commitment c WHERE c.project_id = p.id), 0);
                """);

            migrationBuilder.CreateIndex(
                name: "ix_work_constraint_key",
                schema: "hub",
                table: "work_constraint",
                column: "key");

            migrationBuilder.CreateIndex(
                name: "ix_work_constraint_project_id_seq",
                schema: "hub",
                table: "work_constraint",
                columns: new[] { "project_id", "seq" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_work_constraint_link",
                schema: "hub",
                table: "work_constraint",
                sql: "(linked_type IS NULL) = (linked_id IS NULL) AND (linked_type IS NULL OR linked_type IN ('Decision', 'Issue', 'Handoff'))");

            migrationBuilder.CreateIndex(
                name: "ix_output_commitment_key",
                schema: "hub",
                table: "output_commitment",
                column: "key");

            migrationBuilder.CreateIndex(
                name: "ix_output_commitment_project_id_seq",
                schema: "hub",
                table: "output_commitment",
                columns: new[] { "project_id", "seq" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_work_constraint_key",
                schema: "hub",
                table: "work_constraint");

            migrationBuilder.DropIndex(
                name: "ix_work_constraint_project_id_seq",
                schema: "hub",
                table: "work_constraint");

            migrationBuilder.DropCheckConstraint(
                name: "ck_work_constraint_link",
                schema: "hub",
                table: "work_constraint");

            migrationBuilder.DropIndex(
                name: "ix_output_commitment_key",
                schema: "hub",
                table: "output_commitment");

            migrationBuilder.DropIndex(
                name: "ix_output_commitment_project_id_seq",
                schema: "hub",
                table: "output_commitment");

            migrationBuilder.DropColumn(
                name: "key",
                schema: "hub",
                table: "work_constraint");

            migrationBuilder.DropColumn(
                name: "linked_id",
                schema: "hub",
                table: "work_constraint");

            migrationBuilder.DropColumn(
                name: "linked_type",
                schema: "hub",
                table: "work_constraint");

            migrationBuilder.DropColumn(
                name: "seq",
                schema: "hub",
                table: "work_constraint");

            migrationBuilder.DropColumn(
                name: "next_commitment_seq",
                schema: "hub",
                table: "project");

            migrationBuilder.DropColumn(
                name: "next_constraint_seq",
                schema: "hub",
                table: "project");

            migrationBuilder.DropColumn(
                name: "key",
                schema: "hub",
                table: "output_commitment");

            migrationBuilder.DropColumn(
                name: "seq",
                schema: "hub",
                table: "output_commitment");
        }
    }
}
