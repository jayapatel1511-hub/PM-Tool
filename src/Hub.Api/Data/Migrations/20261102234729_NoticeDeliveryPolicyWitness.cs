using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class NoticeDeliveryPolicyWitness : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_notice_slot",
                schema: "hub",
                table: "notice_delivery_slot");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notice_email_protocol",
                schema: "hub",
                table: "email_message");

            migrationBuilder.AddColumn<Guid>(
                name: "carry_forward_to_slot_id",
                schema: "hub",
                table: "notice_delivery_slot",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "notice_digest_policy_json",
                schema: "hub",
                table: "email_message",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "ak_notice_delivery_slot_id_user_id_kind",
                schema: "hub",
                table: "notice_delivery_slot",
                columns: new[] { "id", "user_id", "kind" });

            migrationBuilder.CreateIndex(
                name: "ix_notice_delivery_slot_carry_forward_to_slot_id_user_id_kind",
                schema: "hub",
                table: "notice_delivery_slot",
                columns: new[] { "carry_forward_to_slot_id", "user_id", "kind" });

            migrationBuilder.AddCheckConstraint(
                name: "ck_notice_slot",
                schema: "hub",
                table: "notice_delivery_slot",
                sql: "kind IN ('Digest','TimeReminder') AND state IN ('Accepted','Emitted','Empty','Exhausted','Paused','Queued','Ready','Simulated','Uncertain','Unknown') AND btrim(org_time_zone_id) <> '' AND scheduled_local_time ~ '^([01][0-9]|2[0-3]):[0-5][0-9]$' AND ((kind = 'Digest' AND frequency_at_creation IS NOT NULL AND frequency_at_creation IN ('Daily','Weekly') AND window_from IS NOT NULL AND window_through IS NOT NULL AND window_from <= window_through AND reminder_notification_id IS NULL AND composition_version BETWEEN 0 AND 3 AND external_attempts BETWEEN 0 AND 8 AND state <> 'Emitted') OR (kind = 'TimeReminder' AND frequency_at_creation IS NULL AND window_from IS NULL AND window_through IS NULL AND composition_version = 0 AND external_attempts = 0 AND state IN ('Ready','Emitted'))) AND (carry_forward_to_slot_id IS NULL OR (kind = 'Digest' AND carry_forward_to_slot_id <> id))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notice_email_protocol",
                schema: "hub",
                table: "email_message",
                sql: "(notice_protocol_version IS NULL AND notice_slot_id IS NULL AND notice_composition_version IS NULL AND rendered_occurrence_ids IS NULL AND bulk_rendered_membership_json IS NULL AND notice_digest_policy_json IS NULL AND notice_claim_token IS NULL AND notice_claim_version IS NULL AND notice_claimed_at IS NULL AND transport_kind IS NULL AND transport_outcome IS NULL AND transport_observed_at IS NULL AND provider_accepted_at IS NULL AND acceptance_uncertain_at IS NULL) OR (notice_protocol_version IS NOT NULL AND notice_protocol_version = 1 AND user_id IS NOT NULL AND rendered_occurrence_ids IS NOT NULL AND attempts BETWEEN 0 AND 8 AND ((kind IN ('Immediate','PlanningEntryChanged','PlanningWithdrawal') AND notice_slot_id IS NULL AND notice_composition_version IS NULL AND notice_digest_policy_json IS NULL AND cardinality(rendered_occurrence_ids) = 1) OR (kind = 'Digest' AND notice_slot_id IS NOT NULL AND notice_composition_version IS NOT NULL AND notice_composition_version BETWEEN 1 AND 3)) AND ((notice_claim_token IS NULL AND notice_claim_version IS NULL AND notice_claimed_at IS NULL) OR (notice_claim_token IS NOT NULL AND notice_claim_version IS NOT NULL AND notice_claim_version >= 1 AND notice_claimed_at IS NOT NULL)) AND (transport_kind IS NULL OR transport_kind IN ('Graph','Log','Smtp','Unknown')) AND (transport_outcome IS NULL OR transport_outcome IN ('Accepted','Exhausted','Failed','InFlight','Queued','Simulated','SuppressedBeforeSend','Uncertain','Unknown')) AND (notice_claim_token IS NULL OR (transport_kind IS NOT NULL AND transport_outcome IS NOT NULL AND transport_observed_at IS NOT NULL)) AND (provider_accepted_at IS NULL OR (notice_claim_token IS NOT NULL AND transport_kind IS NOT NULL AND transport_kind IN ('Smtp','Graph') AND transport_outcome IS NOT NULL AND transport_outcome = 'Accepted' AND transport_observed_at IS NOT NULL)) AND (transport_outcome IS DISTINCT FROM 'Accepted' OR provider_accepted_at IS NOT NULL) AND (bulk_rendered_membership_json IS NULL OR jsonb_typeof(bulk_rendered_membership_json) = 'array') AND (notice_digest_policy_json IS NULL OR (kind = 'Digest' AND jsonb_typeof(notice_digest_policy_json) = 'object')))");

            migrationBuilder.AddForeignKey(
                name: "fk_notice_delivery_slot_notice_delivery_slot_carry_forward_to_~",
                schema: "hub",
                table: "notice_delivery_slot",
                columns: new[] { "carry_forward_to_slot_id", "user_id", "kind" },
                principalSchema: "hub",
                principalTable: "notice_delivery_slot",
                principalColumns: new[] { "id", "user_id", "kind" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_notice_delivery_slot_notice_delivery_slot_carry_forward_to_~",
                schema: "hub",
                table: "notice_delivery_slot");

            migrationBuilder.DropUniqueConstraint(
                name: "ak_notice_delivery_slot_id_user_id_kind",
                schema: "hub",
                table: "notice_delivery_slot");

            migrationBuilder.DropIndex(
                name: "ix_notice_delivery_slot_carry_forward_to_slot_id_user_id_kind",
                schema: "hub",
                table: "notice_delivery_slot");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notice_slot",
                schema: "hub",
                table: "notice_delivery_slot");

            migrationBuilder.DropCheckConstraint(
                name: "ck_notice_email_protocol",
                schema: "hub",
                table: "email_message");

            migrationBuilder.DropColumn(
                name: "carry_forward_to_slot_id",
                schema: "hub",
                table: "notice_delivery_slot");

            migrationBuilder.DropColumn(
                name: "notice_digest_policy_json",
                schema: "hub",
                table: "email_message");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notice_slot",
                schema: "hub",
                table: "notice_delivery_slot",
                sql: "kind IN ('Digest','TimeReminder') AND state IN ('Accepted','Emitted','Empty','Exhausted','Paused','Queued','Ready','Simulated','Uncertain','Unknown') AND btrim(org_time_zone_id) <> '' AND scheduled_local_time ~ '^([01][0-9]|2[0-3]):[0-5][0-9]$' AND ((kind = 'Digest' AND frequency_at_creation IS NOT NULL AND frequency_at_creation IN ('Daily','Weekly') AND window_from IS NOT NULL AND window_through IS NOT NULL AND window_from <= window_through AND reminder_notification_id IS NULL AND composition_version BETWEEN 0 AND 3 AND external_attempts BETWEEN 0 AND 8 AND state <> 'Emitted') OR (kind = 'TimeReminder' AND frequency_at_creation IS NULL AND window_from IS NULL AND window_through IS NULL AND composition_version = 0 AND external_attempts = 0 AND state IN ('Ready','Emitted')))");

            migrationBuilder.AddCheckConstraint(
                name: "ck_notice_email_protocol",
                schema: "hub",
                table: "email_message",
                sql: "(notice_protocol_version IS NULL AND notice_slot_id IS NULL AND notice_composition_version IS NULL AND rendered_occurrence_ids IS NULL AND bulk_rendered_membership_json IS NULL AND notice_claim_token IS NULL AND notice_claim_version IS NULL AND notice_claimed_at IS NULL AND transport_kind IS NULL AND transport_outcome IS NULL AND transport_observed_at IS NULL AND provider_accepted_at IS NULL AND acceptance_uncertain_at IS NULL) OR (notice_protocol_version IS NOT NULL AND notice_protocol_version = 1 AND user_id IS NOT NULL AND rendered_occurrence_ids IS NOT NULL AND attempts BETWEEN 0 AND 8 AND ((kind IN ('Immediate','PlanningEntryChanged','PlanningWithdrawal') AND notice_slot_id IS NULL AND notice_composition_version IS NULL AND cardinality(rendered_occurrence_ids) = 1) OR (kind = 'Digest' AND notice_slot_id IS NOT NULL AND notice_composition_version IS NOT NULL AND notice_composition_version BETWEEN 1 AND 3)) AND ((notice_claim_token IS NULL AND notice_claim_version IS NULL AND notice_claimed_at IS NULL) OR (notice_claim_token IS NOT NULL AND notice_claim_version IS NOT NULL AND notice_claim_version >= 1 AND notice_claimed_at IS NOT NULL)) AND (transport_kind IS NULL OR transport_kind IN ('Graph','Log','Smtp','Unknown')) AND (transport_outcome IS NULL OR transport_outcome IN ('Accepted','Exhausted','Failed','InFlight','Queued','Simulated','SuppressedBeforeSend','Uncertain','Unknown')) AND (notice_claim_token IS NULL OR (transport_kind IS NOT NULL AND transport_outcome IS NOT NULL AND transport_observed_at IS NOT NULL)) AND (provider_accepted_at IS NULL OR (notice_claim_token IS NOT NULL AND transport_kind IS NOT NULL AND transport_kind IN ('Smtp','Graph') AND transport_outcome IS NOT NULL AND transport_outcome = 'Accepted' AND transport_observed_at IS NOT NULL)) AND (transport_outcome IS DISTINCT FROM 'Accepted' OR provider_accepted_at IS NOT NULL) AND (bulk_rendered_membership_json IS NULL OR jsonb_typeof(bulk_rendered_membership_json) = 'array'))");
        }
    }
}
