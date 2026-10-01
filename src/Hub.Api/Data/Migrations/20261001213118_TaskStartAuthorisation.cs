using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class TaskStartAuthorisation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "task_start_authorisation",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    authorised_by = table.Column<Guid>(type: "uuid", nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    readiness_at_authorisation = table.Column<string>(type: "text", nullable: false),
                    unknown = table.Column<string[]>(type: "text[]", nullable: false),
                    blocked = table.Column<string[]>(type: "text[]", nullable: false),
                    started_by = table.Column<Guid>(type: "uuid", nullable: true),
                    started_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_start_authorisation", x => x.id);
                    table.CheckConstraint("ck_task_start_authorisation_readiness", "readiness_at_authorisation IN ('Not Ready','Needs Assessment')");
                    table.CheckConstraint("ck_task_start_authorisation_start", "(started_by IS NULL) = (started_at IS NULL)");
                    table.ForeignKey(
                        name: "fk_task_start_authorisation_app_user_authorised_by",
                        column: x => x.authorised_by,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_task_start_authorisation_app_user_started_by",
                        column: x => x.started_by,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_task_start_authorisation_project_project_id",
                        column: x => x.project_id,
                        principalSchema: "hub",
                        principalTable: "project",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_task_start_authorisation_task_task_id",
                        column: x => x.task_id,
                        principalSchema: "hub",
                        principalTable: "task",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_task_start_authorisation_authorised_by",
                schema: "hub",
                table: "task_start_authorisation",
                column: "authorised_by");

            migrationBuilder.CreateIndex(
                name: "ix_task_start_authorisation_project_id",
                schema: "hub",
                table: "task_start_authorisation",
                column: "project_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_start_authorisation_started_by",
                schema: "hub",
                table: "task_start_authorisation",
                column: "started_by");

            migrationBuilder.CreateIndex(
                name: "ix_task_start_authorisation_task_id_created_at",
                schema: "hub",
                table: "task_start_authorisation",
                columns: new[] { "task_id", "created_at" });

            // An authorisation is evidence: its content never changes and it is used by at most one start.
            migrationBuilder.Sql("""
                CREATE FUNCTION hub.guard_task_start_authorisation() RETURNS trigger LANGUAGE plpgsql AS $guard$
                BEGIN
                    IF TG_OP = 'DELETE' THEN RAISE EXCEPTION 'Task start authorisations cannot be deleted'; END IF;
                    IF ROW(NEW.project_id, NEW.task_id, NEW.authorised_by, NEW.reason, NEW.readiness_at_authorisation,
                           NEW.unknown, NEW.blocked, NEW.created_at, NEW.created_by)
                       IS DISTINCT FROM
                       ROW(OLD.project_id, OLD.task_id, OLD.authorised_by, OLD.reason, OLD.readiness_at_authorisation,
                           OLD.unknown, OLD.blocked, OLD.created_at, OLD.created_by) THEN
                        RAISE EXCEPTION 'Task start authorisation content is immutable';
                    END IF;
                    IF OLD.started_at IS NOT NULL AND ROW(NEW.started_by, NEW.started_at) IS DISTINCT FROM ROW(OLD.started_by, OLD.started_at) THEN
                        RAISE EXCEPTION 'A used task start authorisation cannot change';
                    END IF;
                    RETURN NEW;
                END $guard$;
                CREATE TRIGGER guard_task_start_authorisation BEFORE UPDATE OR DELETE ON hub.task_start_authorisation
                    FOR EACH ROW EXECUTE FUNCTION hub.guard_task_start_authorisation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS guard_task_start_authorisation ON hub.task_start_authorisation;
                DROP FUNCTION IF EXISTS hub.guard_task_start_authorisation();
                """);
            migrationBuilder.DropTable(
                name: "task_start_authorisation",
                schema: "hub");
        }
    }
}
