using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class SavedViewConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "created_by",
                schema: "hub",
                table: "saved_view",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "row_version",
                schema: "hub",
                table: "saved_view",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "updated_by",
                schema: "hub",
                table: "saved_view",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "created_by",
                schema: "hub",
                table: "saved_view");

            migrationBuilder.DropColumn(
                name: "row_version",
                schema: "hub",
                table: "saved_view");

            migrationBuilder.DropColumn(
                name: "updated_by",
                schema: "hub",
                table: "saved_view");
        }
    }
}
