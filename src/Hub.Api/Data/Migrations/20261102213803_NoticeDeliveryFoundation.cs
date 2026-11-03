using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class NoticeDeliveryFoundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "hold_immediate_email_when_away",
                schema: "hub",
                table: "user_setting",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "notice_digest_frequency",
                schema: "hub",
                table: "user_setting",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "notice_preferences_revision",
                schema: "hub",
                table: "user_setting",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "time_reminder_enabled",
                schema: "hub",
                table: "user_setting",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "time_reminder_time_local",
                schema: "hub",
                table: "user_setting",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "time_reminder_weekday",
                schema: "hub",
                table: "user_setting",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "acceptance_uncertain_at",
                schema: "hub",
                table: "email_message",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "bulk_rendered_membership_json",
                schema: "hub",
                table: "email_message",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "notice_claim_token",
                schema: "hub",
                table: "email_message",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "notice_claim_version",
                schema: "hub",
                table: "email_message",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "notice_claimed_at",
                schema: "hub",
                table: "email_message",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "notice_composition_version",
                schema: "hub",
                table: "email_message",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "notice_protocol_version",
                schema: "hub",
                table: "email_message",
                type: "smallint",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "notice_slot_id",
                schema: "hub",
                table: "email_message",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "provider_accepted_at",
                schema: "hub",
                table: "email_message",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid[]>(
                name: "rendered_occurrence_ids",
                schema: "hub",
                table: "email_message",
                type: "uuid[]",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "transport_kind",
                schema: "hub",
                table: "email_message",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "transport_observed_at",
                schema: "hub",
                table: "email_message",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "transport_outcome",
                schema: "hub",
                table: "email_message",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "notice_delivery_intent",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurrence_key = table.Column<string>(type: "text", nullable: false),
                    event_type = table.Column<string>(type: "text", nullable: false),
                    item_type = table.Column<string>(type: "text", nullable: true),
                    item_id = table.Column<Guid>(type: "uuid", nullable: true),
                    item_key = table.Column<string>(type: "text", nullable: true),
                    project_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_direct_recipient = table.Column<bool>(type: "boolean", nullable: false),
                    required_project_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    required_planning_entry_ids = table.Column<Guid[]>(type: "uuid[]", nullable: false),
                    notification_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notification_count_at_capture = table.Column<int>(type: "integer", nullable: true),
                    original_subject = table.Column<string>(type: "text", nullable: false),
                    original_body_text = table.Column<string>(type: "text", nullable: false),
                    original_link_path = table.Column<string>(type: "text", nullable: true),
                    suppression_key = table.Column<string>(type: "text", nullable: false),
                    suppression_anchor_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    disposition = table.Column<string>(type: "text", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    delivery_email_id = table.Column<Guid>(type: "uuid", nullable: true),
                    bulk_policy_json = table.Column<string>(type: "jsonb", nullable: true),
                    next_policy_check_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    policy_check_local_date = table.Column<DateOnly>(type: "date", nullable: true),
                    policy_checks_in_day = table.Column<short>(type: "smallint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notice_delivery_intent", x => x.id);
                    table.CheckConstraint("ck_notice_intent", "disposition IN ('Accepted','AccessRemoved','ChannelOff','Duplicate','Exhausted','HeldAway','HeldNoDigest','Mixed','Muted','NoAddress','Pending','PendingAvailability','Queued','RecipientInactive','ReservedForDigest','Simulated','Uncertain','Unknown') AND btrim(occurrence_key) <> '' AND btrim(event_type) <> '' AND btrim(suppression_key) <> '' AND ((item_type IS NULL) = (item_id IS NULL)) AND (notification_count_at_capture IS NULL OR notification_count_at_capture > 0) AND (notification_id IS NULL OR notification_count_at_capture IS NOT NULL) AND (policy_checks_in_day IS NULL OR policy_checks_in_day BETWEEN 0 AND 8) AND ((policy_check_local_date IS NULL) = (policy_checks_in_day IS NULL)) AND (next_policy_check_at IS NULL OR policy_check_local_date IS NOT NULL) AND (disposition <> 'Mixed' OR bulk_policy_json IS NOT NULL) AND (bulk_policy_json IS NULL OR (jsonb_typeof(bulk_policy_json) = 'object' AND bulk_policy_json ? 'origin' AND bulk_policy_json ? 'progress' AND jsonb_typeof(bulk_policy_json->'origin') = 'object' AND jsonb_typeof(bulk_policy_json->'progress') = 'array')) AND (disposition NOT IN ('HeldAway','HeldNoDigest','Queued','ReservedForDigest','Accepted','Simulated','Unknown','Uncertain','Exhausted') OR suppression_anchor_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_notice_delivery_intent_app_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notice_delivery_intent_email_message_delivery_email_id",
                        column: x => x.delivery_email_id,
                        principalSchema: "hub",
                        principalTable: "email_message",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notice_delivery_intent_notifications_notification_id",
                        column: x => x.notification_id,
                        principalSchema: "hub",
                        principalTable: "notification",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "notice_delivery_slot",
                schema: "hub",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "text", nullable: false),
                    local_date = table.Column<DateOnly>(type: "date", nullable: false),
                    org_time_zone_id = table.Column<string>(type: "text", nullable: false),
                    scheduled_local_date = table.Column<DateOnly>(type: "date", nullable: false),
                    scheduled_local_time = table.Column<string>(type: "text", nullable: false),
                    scheduled_at_utc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    frequency_at_creation = table.Column<string>(type: "text", nullable: true),
                    window_from = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    window_through = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    state = table.Column<string>(type: "text", nullable: false),
                    composition_version = table.Column<short>(type: "smallint", nullable: false),
                    external_attempts = table.Column<short>(type: "smallint", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    reminder_notification_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notice_delivery_slot", x => x.id);
                    table.CheckConstraint("ck_notice_slot", "kind IN ('Digest','TimeReminder') AND state IN ('Accepted','Emitted','Empty','Exhausted','Paused','Queued','Ready','Simulated','Uncertain','Unknown') AND btrim(org_time_zone_id) <> '' AND scheduled_local_time ~ '^([01][0-9]|2[0-3]):[0-5][0-9]$' AND ((kind = 'Digest' AND frequency_at_creation IS NOT NULL AND frequency_at_creation IN ('Daily','Weekly') AND window_from IS NOT NULL AND window_through IS NOT NULL AND window_from <= window_through AND reminder_notification_id IS NULL AND composition_version BETWEEN 0 AND 3 AND external_attempts BETWEEN 0 AND 8 AND state <> 'Emitted') OR (kind = 'TimeReminder' AND frequency_at_creation IS NULL AND window_from IS NULL AND window_through IS NULL AND composition_version = 0 AND external_attempts = 0 AND state IN ('Ready','Emitted')))");
                    table.ForeignKey(
                        name: "fk_notice_delivery_slot_app_user_user_id",
                        column: x => x.user_id,
                        principalSchema: "hub",
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_notice_delivery_slot_notifications_reminder_notification_id",
                        column: x => x.reminder_notification_id,
                        principalSchema: "hub",
                        principalTable: "notification",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_notice_preferences",
                schema: "hub",
                table: "user_setting",
                sql: "(notice_digest_frequency IS NULL OR notice_digest_frequency IN ('Daily','Weekly')) AND (notice_preferences_revision IS NULL OR notice_preferences_revision >= 0) AND (time_reminder_weekday IS NULL OR time_reminder_weekday BETWEEN 1 AND 7) AND (time_reminder_time_local IS NULL OR time_reminder_time_local ~ '^([01][0-9]|2[0-3]):[0-5][0-9]$') AND (NOT coalesce(time_reminder_enabled, false) OR (time_reminder_weekday IS NOT NULL AND time_reminder_time_local IS NOT NULL))");

            migrationBuilder.CreateIndex(
                name: "ix_email_message_notice_slot_id_notice_composition_version",
                schema: "hub",
                table: "email_message",
                columns: new[] { "notice_slot_id", "notice_composition_version" },
                unique: true,
                filter: "notice_slot_id IS NOT NULL AND notice_composition_version IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notice_email_protocol",
                schema: "hub",
                table: "email_message",
                sql: "(notice_protocol_version IS NULL AND notice_slot_id IS NULL AND notice_composition_version IS NULL AND rendered_occurrence_ids IS NULL AND bulk_rendered_membership_json IS NULL AND notice_claim_token IS NULL AND notice_claim_version IS NULL AND notice_claimed_at IS NULL AND transport_kind IS NULL AND transport_outcome IS NULL AND transport_observed_at IS NULL AND provider_accepted_at IS NULL AND acceptance_uncertain_at IS NULL) OR (notice_protocol_version IS NOT NULL AND notice_protocol_version = 1 AND user_id IS NOT NULL AND rendered_occurrence_ids IS NOT NULL AND attempts BETWEEN 0 AND 8 AND ((kind IN ('Immediate','PlanningEntryChanged','PlanningWithdrawal') AND notice_slot_id IS NULL AND notice_composition_version IS NULL AND cardinality(rendered_occurrence_ids) = 1) OR (kind = 'Digest' AND notice_slot_id IS NOT NULL AND notice_composition_version IS NOT NULL AND notice_composition_version BETWEEN 1 AND 3)) AND ((notice_claim_token IS NULL AND notice_claim_version IS NULL AND notice_claimed_at IS NULL) OR (notice_claim_token IS NOT NULL AND notice_claim_version IS NOT NULL AND notice_claim_version >= 1 AND notice_claimed_at IS NOT NULL)) AND (transport_kind IS NULL OR transport_kind IN ('Graph','Log','Smtp','Unknown')) AND (transport_outcome IS NULL OR transport_outcome IN ('Accepted','Exhausted','Failed','InFlight','Queued','Simulated','SuppressedBeforeSend','Uncertain','Unknown')) AND (notice_claim_token IS NULL OR (transport_kind IS NOT NULL AND transport_outcome IS NOT NULL AND transport_observed_at IS NOT NULL)) AND (provider_accepted_at IS NULL OR (notice_claim_token IS NOT NULL AND transport_kind IS NOT NULL AND transport_kind IN ('Smtp','Graph') AND transport_outcome IS NOT NULL AND transport_outcome = 'Accepted' AND transport_observed_at IS NOT NULL)) AND (transport_outcome IS DISTINCT FROM 'Accepted' OR provider_accepted_at IS NOT NULL) AND (bulk_rendered_membership_json IS NULL OR jsonb_typeof(bulk_rendered_membership_json) = 'array'))");

            migrationBuilder.CreateIndex(
                name: "ix_notice_delivery_intent_delivery_email_id",
                schema: "hub",
                table: "notice_delivery_intent",
                column: "delivery_email_id");

            migrationBuilder.CreateIndex(
                name: "ix_notice_delivery_intent_notification_id_notification_count_a~",
                schema: "hub",
                table: "notice_delivery_intent",
                columns: new[] { "notification_id", "notification_count_at_capture" },
                unique: true,
                filter: "notification_id IS NOT NULL AND notification_count_at_capture IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_notice_delivery_intent_user_id_created_at",
                schema: "hub",
                table: "notice_delivery_intent",
                columns: new[] { "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_notice_delivery_intent_user_id_disposition_created_at",
                schema: "hub",
                table: "notice_delivery_intent",
                columns: new[] { "user_id", "disposition", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_notice_delivery_intent_user_id_occurrence_key",
                schema: "hub",
                table: "notice_delivery_intent",
                columns: new[] { "user_id", "occurrence_key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notice_delivery_intent_user_id_suppression_key_suppression_~",
                schema: "hub",
                table: "notice_delivery_intent",
                columns: new[] { "user_id", "suppression_key", "suppression_anchor_at" });

            migrationBuilder.CreateIndex(
                name: "ix_notice_delivery_slot_reminder_notification_id",
                schema: "hub",
                table: "notice_delivery_slot",
                column: "reminder_notification_id");

            migrationBuilder.CreateIndex(
                name: "ix_notice_delivery_slot_user_id_kind_local_date",
                schema: "hub",
                table: "notice_delivery_slot",
                columns: new[] { "user_id", "kind", "local_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_notice_delivery_slot_user_id_state_local_date",
                schema: "hub",
                table: "notice_delivery_slot",
                columns: new[] { "user_id", "state", "local_date" });

            migrationBuilder.AddForeignKey(
                name: "fk_email_message_notice_delivery_slots_notice_slot_id",
                schema: "hub",
                table: "email_message",
                column: "notice_slot_id",
                principalSchema: "hub",
                principalTable: "notice_delivery_slot",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_email_message_notice_delivery_slots_notice_slot_id",
                schema: "hub",
                table: "email_message");

            migrationBuilder.DropTable(
                name: "notice_delivery_intent",
                schema: "hub");

            migrationBuilder.DropTable(
                name: "notice_delivery_slot",
                schema: "hub");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notice_preferences",
                schema: "hub",
                table: "user_setting");

            migrationBuilder.DropIndex(
                name: "ix_email_message_notice_slot_id_notice_composition_version",
                schema: "hub",
                table: "email_message");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notice_email_protocol",
                schema: "hub",
                table: "email_message");

            migrationBuilder.DropColumn(
                name: "hold_immediate_email_when_away",
                schema: "hub",
                table: "user_setting");

            migrationBuilder.DropColumn(
                name: "notice_digest_frequency",
                schema: "hub",
                table: "user_setting");

            migrationBuilder.DropColumn(
                name: "notice_preferences_revision",
                schema: "hub",
                table: "user_setting");

            migrationBuilder.DropColumn(
                name: "time_reminder_enabled",
                schema: "hub",
                table: "user_setting");

            migrationBuilder.DropColumn(
                name: "time_reminder_time_local",
                schema: "hub",
                table: "user_setting");

            migrationBuilder.DropColumn(
                name: "time_reminder_weekday",
                schema: "hub",
                table: "user_setting");

            migrationBuilder.DropColumn(
                name: "acceptance_uncertain_at",
                schema: "hub",
                table: "email_message");

            migrationBuilder.DropColumn(
                name: "bulk_rendered_membership_json",
                schema: "hub",
                table: "email_message");

            migrationBuilder.DropColumn(
                name: "notice_claim_token",
                schema: "hub",
                table: "email_message");

            migrationBuilder.DropColumn(
                name: "notice_claim_version",
                schema: "hub",
                table: "email_message");

            migrationBuilder.DropColumn(
                name: "notice_claimed_at",
                schema: "hub",
                table: "email_message");

            migrationBuilder.DropColumn(
                name: "notice_composition_version",
                schema: "hub",
                table: "email_message");

            migrationBuilder.DropColumn(
                name: "notice_protocol_version",
                schema: "hub",
                table: "email_message");

            migrationBuilder.DropColumn(
                name: "notice_slot_id",
                schema: "hub",
                table: "email_message");

            migrationBuilder.DropColumn(
                name: "provider_accepted_at",
                schema: "hub",
                table: "email_message");

            migrationBuilder.DropColumn(
                name: "rendered_occurrence_ids",
                schema: "hub",
                table: "email_message");

            migrationBuilder.DropColumn(
                name: "transport_kind",
                schema: "hub",
                table: "email_message");

            migrationBuilder.DropColumn(
                name: "transport_observed_at",
                schema: "hub",
                table: "email_message");

            migrationBuilder.DropColumn(
                name: "transport_outcome",
                schema: "hub",
                table: "email_message");
        }
    }
}
