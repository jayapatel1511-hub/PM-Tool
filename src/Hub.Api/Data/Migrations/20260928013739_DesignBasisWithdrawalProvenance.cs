using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DesignBasisWithdrawalProvenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "withdrawal_version_id",
                schema: "hub",
                table: "basis_impact_assessment",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "template_design_basis",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_discipline_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    scope = table.Column<string>(type: "text", nullable: false),
                    statement = table.Column<string>(type: "text", nullable: false),
                    numeric_value = table.Column<decimal>(type: "numeric", nullable: true),
                    units = table.Column<string>(type: "text", nullable: true),
                    source_system = table.Column<string>(type: "text", nullable: true),
                    stable_source_id = table.Column<string>(type: "text", nullable: true),
                    source_url = table.Column<string>(type: "text", nullable: true),
                    declared_revision = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_template_design_basis", x => x.id);
                    table.CheckConstraint("ck_template_basis_kind", "kind IN ('Criterion','Assumption')");
                    table.CheckConstraint("ck_template_basis_numeric_units", "numeric_value IS NULL OR (units IS NOT NULL AND length(trim(units)) > 0)");
                    table.ForeignKey(
                        name: "fk_template_design_basis_project_template_template_id",
                        column: x => x.template_id,
                        principalSchema: "hub",
                        principalTable: "project_template",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_template_design_basis_template_disciplines_template_discipli~",
                        column: x => x.template_discipline_id,
                        principalSchema: "hub",
                        principalTable: "template_discipline",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_template_design_basis_template_discipline_id",
                schema: "hub",
                table: "template_design_basis",
                column: "template_discipline_id");

            migrationBuilder.CreateIndex(
                name: "ix_template_design_basis_template_id_template_discipline_id",
                schema: "hub",
                table: "template_design_basis",
                columns: new[] { "template_id", "template_discipline_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "template_design_basis",
                schema: "hub");

            migrationBuilder.DropColumn(
                name: "withdrawal_version_id",
                schema: "hub",
                table: "basis_impact_assessment");
        }
    }
}
