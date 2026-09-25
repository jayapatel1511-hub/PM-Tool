using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SearchTextIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_task_description_trgm",
                schema: "hub",
                table: "task",
                column: "description")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_deliverable_description_trgm",
                schema: "hub",
                table: "deliverable",
                column: "description")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_comment_body_trgm",
                schema: "hub",
                table: "comment",
                column: "body",
                filter: "deleted_at IS NULL")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_task_description_trgm",
                schema: "hub",
                table: "task");

            migrationBuilder.DropIndex(
                name: "ix_deliverable_description_trgm",
                schema: "hub",
                table: "deliverable");

            migrationBuilder.DropIndex(
                name: "ix_comment_body_trgm",
                schema: "hub",
                table: "comment");
        }
    }
}
