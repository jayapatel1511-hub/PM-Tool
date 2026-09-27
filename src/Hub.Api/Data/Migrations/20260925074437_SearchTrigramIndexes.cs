using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SearchTrigramIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "ix_task_key_trgm",
                schema: "hub",
                table: "task",
                column: "key")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_task_name_trgm",
                schema: "hub",
                table: "task",
                column: "name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_milestone_name_trgm",
                schema: "hub",
                table: "milestone",
                column: "name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_deliverable_key_trgm",
                schema: "hub",
                table: "deliverable",
                column: "key")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_deliverable_name_trgm",
                schema: "hub",
                table: "deliverable",
                column: "name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_task_key_trgm",
                schema: "hub",
                table: "task");

            migrationBuilder.DropIndex(
                name: "ix_task_name_trgm",
                schema: "hub",
                table: "task");

            migrationBuilder.DropIndex(
                name: "ix_milestone_name_trgm",
                schema: "hub",
                table: "milestone");

            migrationBuilder.DropIndex(
                name: "ix_deliverable_key_trgm",
                schema: "hub",
                table: "deliverable");

            migrationBuilder.DropIndex(
                name: "ix_deliverable_name_trgm",
                schema: "hub",
                table: "deliverable");
        }
    }
}
