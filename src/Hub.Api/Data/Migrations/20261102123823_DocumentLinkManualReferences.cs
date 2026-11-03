using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class DocumentLinkManualReferences : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "declared_revision",
                schema: "hub",
                table: "document_link",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reference_deliverable_id",
                schema: "hub",
                table: "document_link",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "reference_registered_at",
                schema: "hub",
                table: "document_link",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "reference_registered_by",
                schema: "hub",
                table: "document_link",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "source_system",
                schema: "hub",
                table: "document_link",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "stable_source_id",
                schema: "hub",
                table: "document_link",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_link_reference_deliverable_id",
                schema: "hub",
                table: "document_link",
                column: "reference_deliverable_id");

            migrationBuilder.CreateIndex(
                name: "ix_document_link_reference_registered_by",
                schema: "hub",
                table: "document_link",
                column: "reference_registered_by");

            migrationBuilder.AddCheckConstraint(
                name: "ck_document_link_reference_registration",
                schema: "hub",
                table: "document_link",
                sql: "(source_system IS NULL AND stable_source_id IS NULL AND declared_revision IS NULL AND reference_registered_by IS NULL AND reference_registered_at IS NULL AND reference_deliverable_id IS NULL) OR (source_system IS NOT NULL AND btrim(source_system) <> '' AND reference_registered_by IS NOT NULL AND reference_registered_at IS NOT NULL AND (stable_source_id IS NULL OR btrim(stable_source_id) <> '') AND (declared_revision IS NULL OR btrim(declared_revision) <> ''))");

            migrationBuilder.AddForeignKey(
                name: "fk_document_link_app_user_reference_registered_by",
                schema: "hub",
                table: "document_link",
                column: "reference_registered_by",
                principalSchema: "hub",
                principalTable: "app_user",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "fk_document_link_deliverable_reference_deliverable_id",
                schema: "hub",
                table: "document_link",
                column: "reference_deliverable_id",
                principalSchema: "hub",
                principalTable: "deliverable",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_document_link_app_user_reference_registered_by",
                schema: "hub",
                table: "document_link");

            migrationBuilder.DropForeignKey(
                name: "fk_document_link_deliverable_reference_deliverable_id",
                schema: "hub",
                table: "document_link");

            migrationBuilder.DropIndex(
                name: "ix_document_link_reference_deliverable_id",
                schema: "hub",
                table: "document_link");

            migrationBuilder.DropIndex(
                name: "ix_document_link_reference_registered_by",
                schema: "hub",
                table: "document_link");

            migrationBuilder.DropCheckConstraint(
                name: "ck_document_link_reference_registration",
                schema: "hub",
                table: "document_link");

            migrationBuilder.DropColumn(
                name: "declared_revision",
                schema: "hub",
                table: "document_link");

            migrationBuilder.DropColumn(
                name: "reference_deliverable_id",
                schema: "hub",
                table: "document_link");

            migrationBuilder.DropColumn(
                name: "reference_registered_at",
                schema: "hub",
                table: "document_link");

            migrationBuilder.DropColumn(
                name: "reference_registered_by",
                schema: "hub",
                table: "document_link");

            migrationBuilder.DropColumn(
                name: "source_system",
                schema: "hub",
                table: "document_link");

            migrationBuilder.DropColumn(
                name: "stable_source_id",
                schema: "hub",
                table: "document_link");
        }
    }
}
