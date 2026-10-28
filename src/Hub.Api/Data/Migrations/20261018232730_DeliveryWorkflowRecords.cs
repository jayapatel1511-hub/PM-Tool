using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DeliveryWorkflowRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_commitment_state",
                schema: "hub",
                table: "output_commitment");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "onboarding_hidden_at",
                schema: "hub",
                table: "user_setting",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "lead_unit_id",
                schema: "hub",
                table: "project",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "revision_convention",
                schema: "hub",
                table: "project",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "status_note",
                schema: "hub",
                table: "project",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "status_note_at",
                schema: "hub",
                table: "project",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "status_note_by",
                schema: "hub",
                table: "project",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<decimal[]>(
                name: "weekday_hours",
                schema: "hub",
                table: "person_schedule",
                type: "numeric[]",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "miss_category_id",
                schema: "hub",
                table: "output_commitment_event",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "baseline_date",
                schema: "hub",
                table: "milestone",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "baseline_due_date",
                schema: "hub",
                table: "deliverable",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<DateOnly>(
                name: "baseline_start_date",
                schema: "hub",
                table: "deliverable",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "current_baseline_id",
                schema: "hub",
                table: "deliverable",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "unit_id",
                schema: "hub",
                table: "app_user",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "chain_id",
                schema: "hub",
                table: "activity_log",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "chain_seq",
                schema: "hub",
                table: "activity_log",
                type: "bigint",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "prev_hash",
                schema: "hub",
                table: "activity_log",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "row_hash",
                schema: "hub",
                table: "activity_log",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "activity_chain_head",
                schema: "hub",
                columns: table => new
                {
                    chain_id = table.Column<Guid>(type: "uuid", nullable: false),
                    head_seq = table.Column<long>(type: "bigint", nullable: false),
                    head_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    last_verified_seq = table.Column<long>(type: "bigint", nullable: false),
                    last_verified_hash = table.Column<string>(type: "text", nullable: false),
                    last_verified_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    first_broken_seq = table.Column<long>(type: "bigint", nullable: true),
                    alerted_broken_seq = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_activity_chain_head", x => x.chain_id);
                });

            migrationBuilder.CreateTable(
                name: "commitment_miss_category",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_commitment_miss_category", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "deliverable_baseline",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    project_id = table.Column<Guid>(type: "uuid", nullable: false),
                    deliverable_id = table.Column<Guid>(type: "uuid", nullable: false),
                    number = table.Column<int>(type: "integer", nullable: false),
                    start_date = table.Column<DateOnly>(type: "date", nullable: true),
                    due_date = table.Column<DateOnly>(type: "date", nullable: true),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    estimated_hours_total = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    remaining_hours = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    reason = table.Column<string>(type: "text", nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recorded_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_deliverable_baseline", x => x.id);
                    table.ForeignKey(
                        name: "fk_deliverable_baseline_deliverable_deliverable_id",
                        column: x => x.deliverable_id,
                        principalSchema: "hub",
                        principalTable: "deliverable",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "org_level",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_org_level", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "route_count",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    day = table.Column<DateOnly>(type: "date", nullable: false),
                    route_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    opens = table.Column<int>(type: "integer", nullable: false),
                    under500ms = table.Column<int>(type: "integer", nullable: false),
                    from500to999ms = table.Column<int>(type: "integer", nullable: false),
                    from1000to1999ms = table.Column<int>(type: "integer", nullable: false),
                    from2000to4999ms = table.Column<int>(type: "integer", nullable: false),
                    over5000ms = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_route_count", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "task_pickup_offer",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false),
                    offered_by_id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignee_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    hours_remaining = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: true),
                    needed_by = table.Column<DateOnly>(type: "date", nullable: true),
                    note = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    opened_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    closed_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    close_cause = table.Column<string>(type: "text", nullable: true),
                    keep_watching = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_task_pickup_offer", x => x.id);
                    table.ForeignKey(
                        name: "fk_task_pickup_offer_app_user_assignee_id",
                        column: x => x.assignee_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_task_pickup_offer_app_user_offered_by_id",
                        column: x => x.offered_by_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_task_pickup_offer_task_task_id",
                        column: x => x.task_id,
                        principalSchema: "hub",
                        principalTable: "task",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "template_role",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_discipline_id = table.Column<Guid>(type: "uuid", nullable: true),
                    role_label = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    start_milestone_id = table.Column<Guid>(type: "uuid", nullable: true),
                    start_offset_days = table.Column<int>(type: "integer", nullable: false),
                    end_milestone_id = table.Column<Guid>(type: "uuid", nullable: true),
                    end_offset_days = table.Column<int>(type: "integer", nullable: false),
                    planned_hours = table.Column<decimal>(type: "numeric(9,3)", precision: 9, scale: 3, nullable: false),
                    note = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_template_role", x => x.id);
                    table.ForeignKey(
                        name: "fk_template_role_project_template_template_id",
                        column: x => x.template_id,
                        principalSchema: "hub",
                        principalTable: "project_template",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_template_role_template_discipline_template_discipline_id",
                        column: x => x.template_discipline_id,
                        principalSchema: "hub",
                        principalTable: "template_discipline",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_template_role_template_milestone_end_milestone_id",
                        column: x => x.end_milestone_id,
                        principalSchema: "hub",
                        principalTable: "template_milestone",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_template_role_template_milestone_start_milestone_id",
                        column: x => x.start_milestone_id,
                        principalSchema: "hub",
                        principalTable: "template_milestone",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "org_unit",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    level_id = table.Column<Guid>(type: "uuid", nullable: false),
                    leader_id = table.Column<Guid>(type: "uuid", nullable: true),
                    discipline_id = table.Column<Guid>(type: "uuid", nullable: true),
                    office_id = table.Column<Guid>(type: "uuid", nullable: true),
                    closed_on = table.Column<DateOnly>(type: "date", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    row_version = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_org_unit", x => x.id);
                    table.ForeignKey(
                        name: "fk_org_unit_app_user_leader_id",
                        column: x => x.leader_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_org_unit_discipline_discipline_id",
                        column: x => x.discipline_id,
                        principalSchema: "hub",
                        principalTable: "discipline",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_org_unit_office_office_id",
                        column: x => x.office_id,
                        principalSchema: "hub",
                        principalTable: "office",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_org_unit_org_level_level_id",
                        column: x => x.level_id,
                        principalSchema: "hub",
                        principalTable: "org_level",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_org_unit_org_unit_parent_id",
                        column: x => x.parent_id,
                        principalSchema: "hub",
                        principalTable: "org_unit",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_project_lead_unit_id",
                schema: "hub",
                table: "project",
                column: "lead_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_project_status_note_by",
                schema: "hub",
                table: "project",
                column: "status_note_by");

            migrationBuilder.CreateIndex(
                name: "ix_output_commitment_event_miss_category_id",
                schema: "hub",
                table: "output_commitment_event",
                column: "miss_category_id");

            migrationBuilder.AddCheckConstraint(
                name: "ck_commitment_state",
                schema: "hub",
                table: "output_commitment",
                sql: "state IN ('Proposed','Committed','Met','Met, awaiting confirmation','Not Met','Withdrawn')");

            migrationBuilder.CreateIndex(
                name: "ix_app_user_unit_id",
                schema: "hub",
                table: "app_user",
                column: "unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_activity_log_chain_id_chain_seq",
                schema: "hub",
                table: "activity_log",
                columns: new[] { "chain_id", "chain_seq" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_activity_chain_columns",
                schema: "hub",
                table: "activity_log",
                sql: "(chain_id IS NULL AND chain_seq IS NULL AND prev_hash IS NULL AND row_hash IS NULL) OR (chain_id IS NOT NULL AND chain_seq IS NOT NULL AND prev_hash IS NOT NULL AND row_hash IS NOT NULL AND chain_seq > 0 AND length(prev_hash) = 64 AND length(row_hash) = 64)");

            migrationBuilder.CreateIndex(
                name: "ix_deliverable_baseline_deliverable_id_number",
                schema: "hub",
                table: "deliverable_baseline",
                columns: new[] { "deliverable_id", "number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_org_level_sort_order",
                schema: "hub",
                table: "org_level",
                column: "sort_order",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_org_unit_discipline_id",
                schema: "hub",
                table: "org_unit",
                column: "discipline_id");

            migrationBuilder.CreateIndex(
                name: "ix_org_unit_leader_id",
                schema: "hub",
                table: "org_unit",
                column: "leader_id");

            migrationBuilder.CreateIndex(
                name: "ix_org_unit_level_id",
                schema: "hub",
                table: "org_unit",
                column: "level_id");

            migrationBuilder.CreateIndex(
                name: "ix_org_unit_office_id",
                schema: "hub",
                table: "org_unit",
                column: "office_id");

            migrationBuilder.CreateIndex(
                name: "ix_org_unit_parent_id_name",
                schema: "hub",
                table: "org_unit",
                columns: new[] { "parent_id", "name" },
                unique: true)
                .Annotation("Npgsql:NullsDistinct", false);

            migrationBuilder.CreateIndex(
                name: "ix_route_count_day_route_key",
                schema: "hub",
                table: "route_count",
                columns: new[] { "day", "route_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_task_pickup_offer_assignee_id",
                schema: "hub",
                table: "task_pickup_offer",
                column: "assignee_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_pickup_offer_closed_at_opened_at",
                schema: "hub",
                table: "task_pickup_offer",
                columns: new[] { "closed_at", "opened_at" });

            migrationBuilder.CreateIndex(
                name: "ix_task_pickup_offer_offered_by_id",
                schema: "hub",
                table: "task_pickup_offer",
                column: "offered_by_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_pickup_offer_task_id",
                schema: "hub",
                table: "task_pickup_offer",
                column: "task_id",
                unique: true,
                filter: "closed_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_template_role_end_milestone_id",
                schema: "hub",
                table: "template_role",
                column: "end_milestone_id");

            migrationBuilder.CreateIndex(
                name: "ix_template_role_start_milestone_id",
                schema: "hub",
                table: "template_role",
                column: "start_milestone_id");

            migrationBuilder.CreateIndex(
                name: "ix_template_role_template_discipline_id",
                schema: "hub",
                table: "template_role",
                column: "template_discipline_id");

            migrationBuilder.CreateIndex(
                name: "ix_template_role_template_id",
                schema: "hub",
                table: "template_role",
                column: "template_id");

            migrationBuilder.AddForeignKey(
                name: "fk_app_user_org_units_unit_id",
                schema: "hub",
                table: "app_user",
                column: "unit_id",
                principalSchema: "hub",
                principalTable: "org_unit",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_output_commitment_event_commitment_miss_category_miss_categ~",
                schema: "hub",
                table: "output_commitment_event",
                column: "miss_category_id",
                principalSchema: "hub",
                principalTable: "commitment_miss_category",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_project_app_user_status_note_by",
                schema: "hub",
                table: "project",
                column: "status_note_by",
                principalSchema: "hub",
                principalTable: "app_user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_project_org_unit_lead_unit_id",
                schema: "hub",
                table: "project",
                column: "lead_unit_id",
                principalSchema: "hub",
                principalTable: "org_unit",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            // Existing evidence stays immutable; the only update allowed is initial sealing of an unchanged legacy row.
            migrationBuilder.Sql("""
CREATE OR REPLACE FUNCTION hub.activity_log_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF TG_OP = 'UPDATE' AND OLD.chain_id IS NULL AND NEW.chain_id IS NOT NULL
     AND NEW.chain_seq IS NOT NULL AND NEW.prev_hash IS NOT NULL AND NEW.row_hash IS NOT NULL
     AND (to_jsonb(NEW) - ARRAY['chain_id','chain_seq','prev_hash','row_hash']) = (to_jsonb(OLD) - ARRAY['chain_id','chain_seq','prev_hash','row_hash']) THEN
    RETURN NEW;
  END IF;
  RAISE EXCEPTION 'activity_log is append-only';
END $$;
CREATE OR REPLACE FUNCTION hub.guard_commitment_history() RETURNS trigger LANGUAGE plpgsql AS $guard$
DECLARE snapshot_project uuid; snapshot_week date;
BEGIN
    IF TG_TABLE_NAME = 'output_commitment_event' THEN
        IF TG_OP <> 'INSERT' THEN RAISE EXCEPTION 'Commitment event history is immutable'; END IF;
        RETURN NEW;
    END IF;
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'Commitment history cannot be deleted';
    END IF;
    IF NEW.snapshot_id IS NOT NULL THEN
        SELECT project_id, week_start INTO snapshot_project, snapshot_week
            FROM hub.weekly_plan_snapshot WHERE id = NEW.snapshot_id;
        IF snapshot_project IS DISTINCT FROM NEW.project_id OR snapshot_week IS DISTINCT FROM NEW.week_start THEN
            RAISE EXCEPTION 'Commitment snapshot must belong to the same project and week';
        END IF;
    END IF;
    IF TG_OP = 'INSERT' THEN
        IF NEW.state NOT IN ('Proposed', 'Committed') THEN
            RAISE EXCEPTION 'New commitment must be proposed or signed';
        END IF;
        RETURN NEW;
    END IF;
    IF OLD.state = 'Proposed' AND NEW.state NOT IN ('Proposed', 'Committed', 'Withdrawn') THEN
        RAISE EXCEPTION 'A proposed commitment cannot close without performer commitment';
    END IF;
    IF OLD.state <> 'Proposed' THEN
        IF ROW(NEW.project_id, NEW.target_type, NEW.target_id, NEW.performer_id,
               NEW.intended_output, NEW.completion_criteria, NEW.target_date,
               NEW.week_start, NEW.readiness_at_commit)
           IS DISTINCT FROM
           ROW(OLD.project_id, OLD.target_type, OLD.target_id, OLD.performer_id,
               OLD.intended_output, OLD.completion_criteria, OLD.target_date,
               OLD.week_start, OLD.readiness_at_commit) THEN
            RAISE EXCEPTION 'Committed promise content is immutable';
        END IF;
        IF OLD.snapshot_id IS NOT NULL AND NEW.snapshot_id IS DISTINCT FROM OLD.snapshot_id THEN
            RAISE EXCEPTION 'Committed promise snapshot cannot change';
        END IF;
        IF OLD.snapshot_id IS NULL AND NEW.snapshot_id IS NOT NULL AND OLD.state <> 'Committed' THEN
            RAISE EXCEPTION 'A closed commitment cannot enter a snapshot';
        END IF;
        IF OLD.state = 'Committed' AND NEW.state NOT IN ('Committed', 'Met', 'Not Met', 'Withdrawn', 'Met, awaiting confirmation') THEN
            RAISE EXCEPTION 'Committed promise state cannot reverse';
        END IF;
        IF OLD.state = 'Met, awaiting confirmation' AND NEW.state NOT IN ('Met, awaiting confirmation', 'Met', 'Committed') THEN
            RAISE EXCEPTION 'Awaiting confirmation can only be confirmed or returned';
        END IF;
        IF OLD.state NOT IN ('Committed', 'Met, awaiting confirmation') AND NEW.state <> OLD.state THEN
            RAISE EXCEPTION 'Closed commitment outcome cannot change';
        END IF;
    END IF;
    RETURN NEW;
END $guard$;
CREATE FUNCTION hub.deliverable_baseline_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'deliverable_baseline is append-only'; END $$;
CREATE TRIGGER deliverable_baseline_no_update BEFORE UPDATE OR DELETE ON hub.deliverable_baseline FOR EACH ROW EXECUTE FUNCTION hub.deliverable_baseline_immutable();
CREATE TRIGGER deliverable_baseline_no_truncate BEFORE TRUNCATE ON hub.deliverable_baseline FOR EACH STATEMENT EXECUTE FUNCTION hub.deliverable_baseline_immutable();
INSERT INTO hub.org_level (id,name,sort_order,created_at,updated_at,row_version) VALUES
 ('00500000-0000-0000-0000-000000000001','Region',0,now(),now(),0),
 ('00500000-0000-0000-0000-000000000002','Business unit',1,now(),now(),0),
 ('00500000-0000-0000-0000-000000000003','Team',2,now(),now(),0);
INSERT INTO hub.commitment_miss_category (id,name,is_active,sort_order,created_at,updated_at,row_version) VALUES
 ('00480000-0000-0000-0000-000000000001','Missing input',true,0,now(),now(),0),
 ('00480000-0000-0000-0000-000000000002','Decision delay',true,1,now(),now(),0),
 ('00480000-0000-0000-0000-000000000003','Changed scope',true,2,now(),now(),0),
 ('00480000-0000-0000-0000-000000000004','Unavailable capacity',true,3,now(),now(),0),
 ('00480000-0000-0000-0000-000000000005','Technical problem',true,4,now(),now(),0),
 ('00480000-0000-0000-0000-000000000006','Other',true,5,now(),now(),0);
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
CREATE OR REPLACE FUNCTION hub.guard_commitment_history() RETURNS trigger LANGUAGE plpgsql AS $guard$
DECLARE snapshot_project uuid; snapshot_week date;
BEGIN
    IF TG_TABLE_NAME = 'output_commitment_event' THEN
        IF TG_OP <> 'INSERT' THEN RAISE EXCEPTION 'Commitment event history is immutable'; END IF;
        RETURN NEW;
    END IF;
    IF TG_OP = 'DELETE' THEN
        RAISE EXCEPTION 'Commitment history cannot be deleted';
    END IF;
    IF NEW.snapshot_id IS NOT NULL THEN
        SELECT project_id, week_start INTO snapshot_project, snapshot_week
            FROM hub.weekly_plan_snapshot WHERE id = NEW.snapshot_id;
        IF snapshot_project IS DISTINCT FROM NEW.project_id OR snapshot_week IS DISTINCT FROM NEW.week_start THEN
            RAISE EXCEPTION 'Commitment snapshot must belong to the same project and week';
        END IF;
    END IF;
    IF TG_OP = 'INSERT' THEN
        IF NEW.state NOT IN ('Proposed', 'Committed') THEN
            RAISE EXCEPTION 'New commitment must be proposed or signed';
        END IF;
        RETURN NEW;
    END IF;
    IF OLD.state = 'Proposed' AND NEW.state NOT IN ('Proposed', 'Committed', 'Withdrawn') THEN
        RAISE EXCEPTION 'A proposed commitment cannot close without performer commitment';
    END IF;
    IF OLD.state <> 'Proposed' THEN
        IF ROW(NEW.project_id, NEW.target_type, NEW.target_id, NEW.performer_id,
               NEW.intended_output, NEW.completion_criteria, NEW.target_date,
               NEW.week_start, NEW.readiness_at_commit)
           IS DISTINCT FROM
           ROW(OLD.project_id, OLD.target_type, OLD.target_id, OLD.performer_id,
               OLD.intended_output, OLD.completion_criteria, OLD.target_date,
               OLD.week_start, OLD.readiness_at_commit) THEN
            RAISE EXCEPTION 'Committed promise content is immutable';
        END IF;
        IF OLD.snapshot_id IS NOT NULL AND NEW.snapshot_id IS DISTINCT FROM OLD.snapshot_id THEN
            RAISE EXCEPTION 'Committed promise snapshot cannot change';
        END IF;
        IF OLD.snapshot_id IS NULL AND NEW.snapshot_id IS NOT NULL AND OLD.state <> 'Committed' THEN
            RAISE EXCEPTION 'A closed commitment cannot enter a snapshot';
        END IF;
        IF OLD.state = 'Committed' AND NEW.state NOT IN ('Committed', 'Met', 'Not Met', 'Withdrawn') THEN
            RAISE EXCEPTION 'Committed promise state cannot reverse';
        END IF;
        IF OLD.state <> 'Committed' AND NEW.state <> OLD.state THEN
            RAISE EXCEPTION 'Closed commitment outcome cannot change';
        END IF;
    END IF;
    RETURN NEW;
END $guard$;
""");
            migrationBuilder.Sql("""
CREATE OR REPLACE FUNCTION hub.activity_log_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'activity_log is append-only'; END $$;
""");
            migrationBuilder.DropForeignKey(
                name: "fk_app_user_org_units_unit_id",
                schema: "hub",
                table: "app_user");

            migrationBuilder.DropForeignKey(
                name: "fk_output_commitment_event_commitment_miss_category_miss_categ~",
                schema: "hub",
                table: "output_commitment_event");

            migrationBuilder.DropForeignKey(
                name: "fk_project_app_user_status_note_by",
                schema: "hub",
                table: "project");

            migrationBuilder.DropForeignKey(
                name: "fk_project_org_unit_lead_unit_id",
                schema: "hub",
                table: "project");

            migrationBuilder.DropTable(
                name: "activity_chain_head",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "commitment_miss_category",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "deliverable_baseline",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "org_unit",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "route_count",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "task_pickup_offer",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "template_role",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "org_level",
                schema: "hub");

            migrationBuilder.DropIndex(
                name: "ix_project_lead_unit_id",
                schema: "hub",
                table: "project");

            migrationBuilder.DropIndex(
                name: "ix_project_status_note_by",
                schema: "hub",
                table: "project");

            migrationBuilder.DropIndex(
                name: "ix_output_commitment_event_miss_category_id",
                schema: "hub",
                table: "output_commitment_event");

            migrationBuilder.DropCheckConstraint(
                name: "ck_commitment_state",
                schema: "hub",
                table: "output_commitment");

            migrationBuilder.DropIndex(
                name: "ix_app_user_unit_id",
                schema: "hub",
                table: "app_user");

            migrationBuilder.DropIndex(
                name: "ix_activity_log_chain_id_chain_seq",
                schema: "hub",
                table: "activity_log");

            migrationBuilder.DropCheckConstraint(
                name: "ck_activity_chain_columns",
                schema: "hub",
                table: "activity_log");

            migrationBuilder.DropColumn(
                name: "onboarding_hidden_at",
                schema: "hub",
                table: "user_setting");

            migrationBuilder.DropColumn(
                name: "lead_unit_id",
                schema: "hub",
                table: "project");

            migrationBuilder.DropColumn(
                name: "revision_convention",
                schema: "hub",
                table: "project");

            migrationBuilder.DropColumn(
                name: "status_note",
                schema: "hub",
                table: "project");

            migrationBuilder.DropColumn(
                name: "status_note_at",
                schema: "hub",
                table: "project");

            migrationBuilder.DropColumn(
                name: "status_note_by",
                schema: "hub",
                table: "project");

            migrationBuilder.DropColumn(
                name: "weekday_hours",
                schema: "hub",
                table: "person_schedule");

            migrationBuilder.DropColumn(
                name: "miss_category_id",
                schema: "hub",
                table: "output_commitment_event");

            migrationBuilder.DropColumn(
                name: "baseline_date",
                schema: "hub",
                table: "milestone");

            migrationBuilder.DropColumn(
                name: "baseline_due_date",
                schema: "hub",
                table: "deliverable");

            migrationBuilder.DropColumn(
                name: "baseline_start_date",
                schema: "hub",
                table: "deliverable");

            migrationBuilder.DropColumn(
                name: "current_baseline_id",
                schema: "hub",
                table: "deliverable");

            migrationBuilder.DropColumn(
                name: "unit_id",
                schema: "hub",
                table: "app_user");

            migrationBuilder.DropColumn(
                name: "chain_id",
                schema: "hub",
                table: "activity_log");

            migrationBuilder.DropColumn(
                name: "chain_seq",
                schema: "hub",
                table: "activity_log");

            migrationBuilder.DropColumn(
                name: "prev_hash",
                schema: "hub",
                table: "activity_log");

            migrationBuilder.DropColumn(
                name: "row_hash",
                schema: "hub",
                table: "activity_log");

            migrationBuilder.AddCheckConstraint(
                name: "ck_commitment_state",
                schema: "hub",
                table: "output_commitment",
                sql: "state IN ('Proposed','Committed','Met','Not Met','Withdrawn')");
            migrationBuilder.Sql("DROP FUNCTION hub.deliverable_baseline_immutable();");
        }
    }
}
