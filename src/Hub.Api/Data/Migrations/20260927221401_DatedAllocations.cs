using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DatedAllocations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "person_availability_override",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_date = table.Column<DateOnly>(type: "date", nullable: false),
                    available_hours = table.Column<decimal>(type: "numeric(9,3)", precision: 9, scale: 3, nullable: false),
                    category = table.Column<string>(type: "text", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_person_availability_override", x => x.id);
                    table.CheckConstraint("ck_availability_category", "category IN ('Unavailable','Reduced','Additional')");
                    table.CheckConstraint("ck_availability_hours", "available_hours >= 0");
                    table.ForeignKey(
                        name: "fk_person_availability_override_app_user_person_id",
                        column: x => x.person_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "person_date_version",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_date = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_person_date_version", x => x.id);
                    table.ForeignKey(
                        name: "fk_person_date_version_app_user_person_id",
                        column: x => x.person_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "resource_allocation",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "text", nullable: false),
                    from_date = table.Column<DateOnly>(type: "date", nullable: false),
                    through_date = table.Column<DateOnly>(type: "date", nullable: false),
                    planned_hours = table.Column<decimal>(type: "numeric(9,3)", precision: 9, scale: 3, nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    confirmed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    confirmed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    over_capacity_reason = table.Column<string>(type: "text", nullable: true),
                    confirmation_snapshot = table.Column<string>(type: "jsonb", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_resource_allocation", x => x.id);
                    table.CheckConstraint("ck_allocation_dates", "through_date >= from_date");
                    table.CheckConstraint("ck_allocation_hours", "planned_hours > 0");
                    table.CheckConstraint("ck_allocation_purpose", "purpose IN ('Production','Review')");
                    table.CheckConstraint("ck_allocation_status", "status IN ('Proposed','Confirmed','Declined','Cancelled','Completed')");
                    table.ForeignKey(
                        name: "fk_resource_allocation_app_user_confirmed_by",
                        column: x => x.confirmed_by,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_resource_allocation_app_user_person_id",
                        column: x => x.person_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_resource_allocation_project_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "allocation_day_override",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    allocation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_date = table.Column<DateOnly>(type: "date", nullable: false),
                    hours = table.Column<decimal>(type: "numeric(9,3)", precision: 9, scale: 3, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_allocation_day_override", x => x.id);
                    table.CheckConstraint("ck_allocation_day_hours", "hours >= 0");
                    table.ForeignKey(
                        name: "fk_allocation_day_override_allocations_allocation_id",
                        column: x => x.allocation_id,
                        principalSchema: "hub",
                        principalTable: "resource_allocation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "allocation_work_link",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    allocation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_type = table.Column<string>(type: "text", nullable: false),
                    work_id = table.Column<Guid>(type: "uuid", nullable: false),
                    work_date = table.Column<DateOnly>(type: "date", nullable: false),
                    released_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_allocation_work_link", x => x.id);
                    table.CheckConstraint("ck_allocation_work_type", "work_type IN ('Task', 'Review')");
                    table.ForeignKey(
                        name: "fk_allocation_work_link_allocations_allocation_id",
                        column: x => x.allocation_id,
                        principalSchema: "hub",
                        principalTable: "resource_allocation",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_allocation_work_link_app_user_person_id",
                        column: x => x.person_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_allocation_day_override_allocation_id_work_date",
                schema: "hub",
                table: "allocation_day_override",
                columns: new[] { "allocation_id", "work_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_allocation_work_link_allocation_id",
                schema: "hub",
                table: "allocation_work_link",
                column: "allocation_id");

            migrationBuilder.CreateIndex(
                name: "ix_allocation_work_link_person_id_work_type_work_id_work_date",
                schema: "hub",
                table: "allocation_work_link",
                columns: new[] { "person_id", "work_type", "work_id", "work_date" },
                unique: true,
                filter: "released_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_person_availability_override_person_id_work_date",
                schema: "hub",
                table: "person_availability_override",
                columns: new[] { "person_id", "work_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_person_date_version_person_id_work_date",
                schema: "hub",
                table: "person_date_version",
                columns: new[] { "person_id", "work_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_resource_allocation_confirmed_by",
                schema: "hub",
                table: "resource_allocation",
                column: "confirmed_by");

            migrationBuilder.CreateIndex(
                name: "ix_resource_allocation_person_id_from_date_through_date",
                schema: "hub",
                table: "resource_allocation",
                columns: new[] { "person_id", "from_date", "through_date" });

            migrationBuilder.CreateIndex(
                name: "ix_resource_allocation_project_id_status",
                schema: "hub",
                table: "resource_allocation",
                columns: new[] { "project_id", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "allocation_day_override",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "allocation_work_link",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "person_availability_override",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "person_date_version",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "resource_allocation",
                schema: "hub");
        }
    }
}
