using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DateReasonCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "date_reason_category",
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
                    table.PrimaryKey("pk_date_reason_category", x => x.id);
                    table.CheckConstraint("ck_date_reason_category_name", "name = btrim(name) AND char_length(name) BETWEEN 1 AND 120 AND name !~ '[[:cntrl:]]'");
                });

            migrationBuilder.CreateIndex(
                name: "ix_date_reason_category_name",
                schema: "hub",
                table: "date_reason_category",
                column: "name",
                unique: true);

            // 053-D13 default list; organisation-managed from here on (Admin → Date move reasons).
            migrationBuilder.Sql("""
                INSERT INTO hub.date_reason_category (id, created_at, updated_at, row_version, name, is_active, sort_order)
                SELECT gen_random_uuid(), now(), now(), 0, name, true, sort_order
                FROM (VALUES ('Client request', 1), ('Waiting on input', 2), ('Scope change', 3), ('Resource availability', 4),
                             ('Technical problem', 5), ('Correction', 6), ('Other', 7)) AS defaults(name, sort_order);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "date_reason_category",
                schema: "hub");
        }
    }
}
