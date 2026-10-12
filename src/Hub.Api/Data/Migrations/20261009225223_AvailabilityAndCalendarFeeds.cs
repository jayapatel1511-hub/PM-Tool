using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AvailabilityAndCalendarFeeds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "reconciled_leave_id",
                schema: "hub",
                table: "person_availability_override",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "hr_person_key",
                schema: "hub",
                table: "app_user",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "hr_source_system",
                schema: "hub",
                table: "app_user",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "approved_leave",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_system = table.Column<string>(type: "text", nullable: false),
                    external_id = table.Column<string>(type: "text", nullable: false),
                    source_kind = table.Column<string>(type: "text", nullable: false),
                    status = table.Column<string>(type: "text", nullable: false),
                    from_date = table.Column<DateOnly>(type: "date", nullable: false),
                    through_date = table.Column<DateOnly>(type: "date", nullable: false),
                    hours_per_day = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    source_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    imported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_approved_leave", x => x.id);
                    table.CheckConstraint("ck_leave_dates", "through_date >= from_date AND through_date - from_date <= 365");
                    table.CheckConstraint("ck_leave_hours", "hours_per_day IS NULL OR hours_per_day > 0 AND hours_per_day <= 24");
                    table.CheckConstraint("ck_leave_source", "source_kind IN ('HRImport','ManualHR')");
                    table.CheckConstraint("ck_leave_status", "status IN ('Approved','Cancelled','Unverified')");
                    table.ForeignKey(
                        name: "fk_approved_leave_app_user_person_id",
                        column: x => x.person_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "calendar_feed_item",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_key = table.Column<string>(type: "text", nullable: false),
                    fingerprint = table.Column<string>(type: "text", nullable: false),
                    from_date = table.Column<DateOnly>(type: "date", nullable: false),
                    through_date = table.Column<DateOnly>(type: "date", nullable: false),
                    sequence = table.Column<int>(type: "integer", nullable: false),
                    active = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calendar_feed_item", x => x.id);
                    table.ForeignKey(
                        name: "fk_calendar_feed_item_app_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "calendar_subscription",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "text", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_calendar_subscription", x => x.id);
                    table.ForeignKey(
                        name: "fk_calendar_subscription_app_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "leave_import_preview",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    rows_hash = table.Column<string>(type: "text", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_leave_import_preview", x => x.id);
                    table.ForeignKey(
                        name: "fk_leave_import_preview_app_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "leave_import_state",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_system = table.Column<string>(type: "text", nullable: false),
                    imported_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    source_updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_leave_import_state", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "person_schedule",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    from_date = table.Column<DateOnly>(type: "date", nullable: false),
                    weekly_hours = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: false),
                    working_days = table.Column<int[]>(type: "integer[]", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_person_schedule", x => x.id);
                    table.CheckConstraint("ck_person_schedule", "weekly_hours >= 0 AND weekly_hours <= 168 AND cardinality(working_days) BETWEEN 1 AND 7 AND working_days <@ ARRAY[0,1,2,3,4,5,6]");
                    table.ForeignKey(
                        name: "fk_person_schedule_app_user_person_id",
                        column: x => x.person_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_person_availability_override_reconciled_leave_id",
                schema: "hub",
                table: "person_availability_override",
                column: "reconciled_leave_id");

            migrationBuilder.CreateIndex(
                name: "ix_app_user_hr_source_system_hr_person_key",
                schema: "hub",
                table: "app_user",
                columns: new[] { "hr_source_system", "hr_person_key" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_hr_identity_pair",
                schema: "hub",
                table: "app_user",
                sql: "(hr_source_system IS NULL) = (hr_person_key IS NULL)");

            migrationBuilder.CreateIndex(
                name: "ix_approved_leave_person_id_from_date_through_date",
                schema: "hub",
                table: "approved_leave",
                columns: new[] { "person_id", "from_date", "through_date" });

            migrationBuilder.CreateIndex(
                name: "ix_approved_leave_source_system_external_id",
                schema: "hub",
                table: "approved_leave",
                columns: new[] { "source_system", "external_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_calendar_feed_item_user_id_source_key",
                schema: "hub",
                table: "calendar_feed_item",
                columns: new[] { "user_id", "source_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_calendar_subscription_token_hash",
                schema: "hub",
                table: "calendar_subscription",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_calendar_subscription_user_id",
                schema: "hub",
                table: "calendar_subscription",
                column: "user_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_leave_import_preview_user_id",
                schema: "hub",
                table: "leave_import_preview",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_leave_import_state_source_system",
                schema: "hub",
                table: "leave_import_state",
                column: "source_system",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_person_schedule_person_id_from_date",
                schema: "hub",
                table: "person_schedule",
                columns: new[] { "person_id", "from_date" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "fk_person_availability_override_approved_leave_reconciled_leav~",
                schema: "hub",
                table: "person_availability_override",
                column: "reconciled_leave_id",
                principalSchema: "hub",
                principalTable: "approved_leave",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_person_availability_override_approved_leave_reconciled_leav~",
                schema: "hub",
                table: "person_availability_override");

            migrationBuilder.DropTable(
                name: "approved_leave",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "calendar_feed_item",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "calendar_subscription",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "leave_import_preview",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "leave_import_state",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "person_schedule",
                schema: "hub");

            migrationBuilder.DropIndex(
                name: "ix_person_availability_override_reconciled_leave_id",
                schema: "hub",
                table: "person_availability_override");

            migrationBuilder.DropIndex(
                name: "ix_app_user_hr_source_system_hr_person_key",
                schema: "hub",
                table: "app_user");

            migrationBuilder.DropCheckConstraint(
                name: "ck_hr_identity_pair",
                schema: "hub",
                table: "app_user");

            migrationBuilder.DropColumn(
                name: "reconciled_leave_id",
                schema: "hub",
                table: "person_availability_override");

            migrationBuilder.DropColumn(
                name: "hr_person_key",
                schema: "hub",
                table: "app_user");

            migrationBuilder.DropColumn(
                name: "hr_source_system",
                schema: "hub",
                table: "app_user");
        }
    }
}
