using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ExternalIssuedReviewSources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "source_revision_id",
                schema: "hub",
                table: "review_manifest_item",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "deliverable_issue_id",
                schema: "hub",
                table: "review_manifest_item",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "verifier_id",
                schema: "hub",
                table: "review_finding",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "source_revision_id",
                schema: "hub",
                table: "review_finding",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AlterColumn<Guid>(
                name: "originator_id",
                schema: "hub",
                table: "review_finding",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<Guid>(
                name: "deliverable_issue_id",
                schema: "hub",
                table: "review_finding",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "external_review_id",
                schema: "hub",
                table: "review_finding",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid[]>(
                name: "issued_author_ids",
                schema: "hub",
                table: "deliverable_issue",
                type: "uuid[]",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "external_review",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    package_id = table.Column<Guid>(type: "uuid", nullable: false),
                    round_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deliverable_issue_id = table.Column<Guid>(type: "uuid", nullable: false),
                    external_party_id = table.Column<Guid>(type: "uuid", nullable: false),
                    received_date = table.Column<DateOnly>(type: "date", nullable: false),
                    return_code_id = table.Column<Guid>(type: "uuid", nullable: false),
                    return_code_snapshot = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    return_label_snapshot = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    party_name_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    party_organisation_snapshot = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_external_review", x => x.id);
                    table.ForeignKey(
                        name: "fk_external_review_app_user_created_by",
                        column: x => x.created_by,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_external_review_deliverable_issue_deliverable_issue_id",
                        column: x => x.deliverable_issue_id,
                        principalSchema: "hub",
                        principalTable: "deliverable_issue",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_external_review_external_party_external_party_id",
                        column: x => x.external_party_id,
                        principalSchema: "hub",
                        principalTable: "external_party",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_external_review_issue_return_codes_return_code_id",
                        column: x => x.return_code_id,
                        principalSchema: "hub",
                        principalTable: "issue_return_code",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_external_review_projects_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_external_review_review_packages_package_id",
                        column: x => x.package_id,
                        principalSchema: "hub",
                        principalTable: "review_package",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_external_review_review_rounds_round_id",
                        column: x => x.round_id,
                        principalSchema: "hub",
                        principalTable: "review_round",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_review_manifest_item_deliverable_issue_id",
                schema: "hub",
                table: "review_manifest_item",
                column: "deliverable_issue_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_review_manifest_source",
                schema: "hub",
                table: "review_manifest_item",
                sql: "(source_revision_id IS NULL) <> (deliverable_issue_id IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_review_finding_deliverable_issue_id",
                schema: "hub",
                table: "review_finding",
                column: "deliverable_issue_id");

            migrationBuilder.CreateIndex(
                name: "ix_review_finding_external_review_id",
                schema: "hub",
                table: "review_finding",
                column: "external_review_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_review_finding_source_origin",
                schema: "hub",
                table: "review_finding",
                sql: "(source_revision_id IS NOT NULL AND deliverable_issue_id IS NULL AND originator_id IS NOT NULL AND external_review_id IS NULL AND verifier_id IS NOT NULL) OR (source_revision_id IS NULL AND deliverable_issue_id IS NOT NULL AND originator_id IS NULL AND external_review_id IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_deliverable_issue_authors",
                schema: "hub",
                table: "deliverable_issue",
                sql: "issued_author_ids IS NULL OR (array_ndims(issued_author_ids) = 1 AND cardinality(issued_author_ids) > 0 AND array_position(issued_author_ids, NULL) IS NULL AND NOT ('00000000-0000-0000-0000-000000000000'::uuid = ANY(issued_author_ids)))");

            migrationBuilder.CreateIndex(
                name: "ix_external_review_created_by",
                schema: "hub",
                table: "external_review",
                column: "created_by");

            migrationBuilder.CreateIndex(
                name: "ix_external_review_deliverable_issue_id",
                schema: "hub",
                table: "external_review",
                column: "deliverable_issue_id");

            migrationBuilder.CreateIndex(
                name: "ix_external_review_external_party_id",
                schema: "hub",
                table: "external_review",
                column: "external_party_id");

            migrationBuilder.CreateIndex(
                name: "ix_external_review_package_id_round_id",
                schema: "hub",
                table: "external_review",
                columns: new[] { "package_id", "round_id" });

            migrationBuilder.CreateIndex(
                name: "ix_external_review_project_id",
                schema: "hub",
                table: "external_review",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_external_review_return_code_id",
                schema: "hub",
                table: "external_review",
                column: "return_code_id");

            migrationBuilder.CreateIndex(
                name: "ix_external_review_round_id",
                schema: "hub",
                table: "external_review",
                column: "round_id");

            migrationBuilder.AddForeignKey(
                name: "fk_review_finding_deliverable_issue_deliverable_issue_id",
                schema: "hub",
                table: "review_finding",
                column: "deliverable_issue_id",
                principalSchema: "hub",
                principalTable: "deliverable_issue",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_review_finding_external_review_external_review_id",
                schema: "hub",
                table: "review_finding",
                column: "external_review_id",
                principalSchema: "hub",
                principalTable: "external_review",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_review_manifest_item_deliverable_issue_deliverable_issue_id",
                schema: "hub",
                table: "review_manifest_item",
                column: "deliverable_issue_id",
                principalSchema: "hub",
                principalTable: "deliverable_issue",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // This downgrade cannot discard captured authorship or issued-review history.
            migrationBuilder.Sql("""
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM hub.external_review)
                        OR EXISTS (SELECT 1 FROM hub.review_manifest_item WHERE deliverable_issue_id IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM hub.review_finding WHERE deliverable_issue_id IS NOT NULL OR external_review_id IS NOT NULL)
                        OR EXISTS (SELECT 1 FROM hub.deliverable_issue WHERE issued_author_ids IS NOT NULL)
                    THEN
                        RAISE EXCEPTION 'Cannot downgrade issued-review schema while captured authorship or issued-review records remain.'
                            USING ERRCODE = '55000';
                    END IF;
                END $$;
                """);

            migrationBuilder.DropForeignKey(
                name: "fk_review_finding_deliverable_issue_deliverable_issue_id",
                schema: "hub",
                table: "review_finding");

            migrationBuilder.DropForeignKey(
                name: "fk_review_finding_external_review_external_review_id",
                schema: "hub",
                table: "review_finding");

            migrationBuilder.DropForeignKey(
                name: "fk_review_manifest_item_deliverable_issue_deliverable_issue_id",
                schema: "hub",
                table: "review_manifest_item");

            migrationBuilder.DropTable(
                name: "external_review",
                schema: "hub");

            migrationBuilder.DropIndex(
                name: "ix_review_manifest_item_deliverable_issue_id",
                schema: "hub",
                table: "review_manifest_item");

            migrationBuilder.DropCheckConstraint(
                name: "ck_review_manifest_source",
                schema: "hub",
                table: "review_manifest_item");

            migrationBuilder.DropIndex(
                name: "ix_review_finding_deliverable_issue_id",
                schema: "hub",
                table: "review_finding");

            migrationBuilder.DropIndex(
                name: "ix_review_finding_external_review_id",
                schema: "hub",
                table: "review_finding");

            migrationBuilder.DropCheckConstraint(
                name: "ck_review_finding_source_origin",
                schema: "hub",
                table: "review_finding");

            migrationBuilder.DropCheckConstraint(
                name: "ck_deliverable_issue_authors",
                schema: "hub",
                table: "deliverable_issue");

            migrationBuilder.DropColumn(
                name: "deliverable_issue_id",
                schema: "hub",
                table: "review_manifest_item");

            migrationBuilder.DropColumn(
                name: "deliverable_issue_id",
                schema: "hub",
                table: "review_finding");

            migrationBuilder.DropColumn(
                name: "external_review_id",
                schema: "hub",
                table: "review_finding");

            migrationBuilder.DropColumn(
                name: "issued_author_ids",
                schema: "hub",
                table: "deliverable_issue");

            migrationBuilder.AlterColumn<Guid>(
                name: "source_revision_id",
                schema: "hub",
                table: "review_manifest_item",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "verifier_id",
                schema: "hub",
                table: "review_finding",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "source_revision_id",
                schema: "hub",
                table: "review_finding",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "originator_id",
                schema: "hub",
                table: "review_finding",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
