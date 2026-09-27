using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ProjectLinksAsDocumentLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Project header links become document links on the project (§12.7), so they are soft-deleted and logged like
            // every other link (DOC-03). Existing rows are carried across, attributed to the project's PM.
            migrationBuilder.Sql("""
                INSERT INTO hub.document_link (id, project_id, item_type, item_id, title, url, link_type, added_by, added_at)
                SELECT pl.id, pl.project_id, 'Project', pl.project_id, pl.title, pl.url, pl.link_type, p.project_manager_id, now() + pl.sort_order * interval '1 millisecond'
                FROM hub.project_link pl JOIN hub.project p ON p.id = pl.project_id;
                """);
            migrationBuilder.DropTable(
                name: "project_link",
                schema: "hub");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "project_link",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    link_type = table.Column<string>(type: "text", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    title = table.Column<string>(type: "text", nullable: false),
                    url = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_project_link", x => x.id);
                    table.ForeignKey(
                        name: "fk_project_link_project_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_project_link_project_id",
                schema: "hub",
                table: "project_link",
                column: "project_id");
        }
    }
}
