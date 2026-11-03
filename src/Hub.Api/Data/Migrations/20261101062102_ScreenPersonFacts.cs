using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class ScreenPersonFacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "person_end_date",
                schema: "hub",
                table: "app_user",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "person_start_date",
                schema: "hub",
                table: "app_user",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "person_type",
                schema: "hub",
                table: "app_user",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "certification_tag",
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
                    table.PrimaryKey("pk_certification_tag", x => x.id);
                    table.CheckConstraint("ck_certification_tag_name", "name = btrim(name) AND char_length(name) BETWEEN 1 AND 120");
                });

            migrationBuilder.CreateTable(
                name: "user_certification",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    certification_tag_id = table.Column<Guid>(type: "uuid", nullable: false),
                    expires_on = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_certification", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_certification_app_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_user_certification_certification_tag_certification_tag_id",
                        column: x => x.certification_tag_id,
                        principalSchema: "hub",
                        principalTable: "certification_tag",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_app_user_person_dates",
                schema: "hub",
                table: "app_user",
                sql: "person_start_date IS NULL OR person_end_date IS NULL OR person_start_date <= person_end_date");

            migrationBuilder.AddCheckConstraint(
                name: "ck_app_user_person_type",
                schema: "hub",
                table: "app_user",
                sql: "person_type IS NULL OR person_type IN ('Employee', 'Co-op', 'Contractor')");

            migrationBuilder.CreateIndex(
                name: "ix_certification_tag_name",
                schema: "hub",
                table: "certification_tag",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_certification_certification_tag_id",
                schema: "hub",
                table: "user_certification",
                column: "certification_tag_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_certification_user_id_certification_tag_id",
                schema: "hub",
                table: "user_certification",
                columns: new[] { "user_id", "certification_tag_id" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_certification",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "certification_tag",
                schema: "hub");

            migrationBuilder.DropCheckConstraint(
                name: "ck_app_user_person_dates",
                schema: "hub",
                table: "app_user");

            migrationBuilder.DropCheckConstraint(
                name: "ck_app_user_person_type",
                schema: "hub",
                table: "app_user");

            migrationBuilder.DropColumn(
                name: "person_end_date",
                schema: "hub",
                table: "app_user");

            migrationBuilder.DropColumn(
                name: "person_start_date",
                schema: "hub",
                table: "app_user");

            migrationBuilder.DropColumn(
                name: "person_type",
                schema: "hub",
                table: "app_user");
        }
    }
}
