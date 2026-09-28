using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class IssueReferenceImpactAssessment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "issue_reference_impact_assessment",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    issue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_reference_id = table.Column<Guid>(type: "uuid", nullable: false),
                    previous_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    current_revision_id = table.Column<Guid>(type: "uuid", nullable: false),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    verifier_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "text", nullable: false),
                    owner_disposition = table.Column<string>(type: "text", nullable: true),
                    owner_reason = table.Column<string>(type: "text", nullable: true),
                    owner_decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    owner_decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    verifier_disposition = table.Column<string>(type: "text", nullable: true),
                    verifier_reason = table.Column<string>(type: "text", nullable: true),
                    verifier_decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    verifier_decided_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_issue_reference_impact_assessment", x => x.id);
                    table.CheckConstraint("ck_issue_reference_impact_status", "status IN ('Pending','Unaffected','ReopenRequested')");
                    table.ForeignKey(
                        name: "fk_issue_reference_impact_assessment_app_user_owner_decided_by",
                        column: x => x.owner_decided_by,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_issue_reference_impact_assessment_app_user_owner_id",
                        column: x => x.owner_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_issue_reference_impact_assessment_app_user_verifier_decided~",
                        column: x => x.verifier_decided_by,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_issue_reference_impact_assessment_app_user_verifier_id",
                        column: x => x.verifier_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_issue_reference_impact_assessment_issue_document_reference_~",
                        column: x => x.document_reference_id,
                        principalSchema: "hub",
                        principalTable: "issue_document_reference",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_issue_reference_impact_assessment_issue_issue_id",
                        column: x => x.issue_id,
                        principalSchema: "hub",
                        principalTable: "issue",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_issue_reference_impact_assessment_source_revisions_current_r~",
                        column: x => x.current_revision_id,
                        principalSchema: "hub",
                        principalTable: "source_revision",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_issue_reference_impact_assessment_source_revisions_previous_~",
                        column: x => x.previous_revision_id,
                        principalSchema: "hub",
                        principalTable: "source_revision",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_issue_reference_impact_assessment_current_revision_id",
                schema: "hub",
                table: "issue_reference_impact_assessment",
                column: "current_revision_id");

            migrationBuilder.CreateIndex(
                name: "ix_issue_reference_impact_assessment_document_reference_id",
                schema: "hub",
                table: "issue_reference_impact_assessment",
                column: "document_reference_id");

            migrationBuilder.CreateIndex(
                name: "ix_issue_reference_impact_assessment_issue_id_document_referen~",
                schema: "hub",
                table: "issue_reference_impact_assessment",
                columns: new[] { "issue_id", "document_reference_id", "previous_revision_id", "current_revision_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_issue_reference_impact_assessment_owner_decided_by",
                schema: "hub",
                table: "issue_reference_impact_assessment",
                column: "owner_decided_by");

            migrationBuilder.CreateIndex(
                name: "ix_issue_reference_impact_assessment_owner_id",
                schema: "hub",
                table: "issue_reference_impact_assessment",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "ix_issue_reference_impact_assessment_previous_revision_id",
                schema: "hub",
                table: "issue_reference_impact_assessment",
                column: "previous_revision_id");

            migrationBuilder.CreateIndex(
                name: "ix_issue_reference_impact_assessment_verifier_decided_by",
                schema: "hub",
                table: "issue_reference_impact_assessment",
                column: "verifier_decided_by");

            migrationBuilder.CreateIndex(
                name: "ix_issue_reference_impact_assessment_verifier_id",
                schema: "hub",
                table: "issue_reference_impact_assessment",
                column: "verifier_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "issue_reference_impact_assessment",
                schema: "hub");
        }
    }
}
