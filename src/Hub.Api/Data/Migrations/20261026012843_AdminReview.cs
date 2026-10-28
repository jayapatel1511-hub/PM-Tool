using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AdminReview : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "admin_review",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    reviewed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reviewer_id = table.Column<Guid>(type: "uuid", nullable: false),
                    note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    checks = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_admin_review", x => x.id);
                    table.ForeignKey(
                        name: "fk_admin_review_app_user_reviewer_id",
                        column: x => x.reviewer_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_admin_review_reviewed_at",
                schema: "hub",
                table: "admin_review",
                column: "reviewed_at");

            migrationBuilder.CreateIndex(
                name: "ix_admin_review_reviewer_id",
                schema: "hub",
                table: "admin_review",
                column: "reviewer_id");

            // FR-ADR-03: a recorded review is access-review evidence, so the database refuses edits and deletes too.
            migrationBuilder.Sql("""
CREATE FUNCTION hub.admin_review_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'admin_review is append-only'; END $$;
CREATE TRIGGER admin_review_no_update BEFORE UPDATE OR DELETE ON hub.admin_review FOR EACH ROW EXECUTE FUNCTION hub.admin_review_immutable();
CREATE TRIGGER admin_review_no_truncate BEFORE TRUNCATE ON hub.admin_review FOR EACH STATEMENT EXECUTE FUNCTION hub.admin_review_immutable();
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "admin_review",
                schema: "hub");
            migrationBuilder.Sql("DROP FUNCTION hub.admin_review_immutable();");
        }
    }
}
