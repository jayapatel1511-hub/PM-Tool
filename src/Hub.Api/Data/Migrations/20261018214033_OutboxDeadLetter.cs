using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Hub.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class OutboxDeadLetter : Migration
    {
        /// Marks events already stranded at the worker's limit (FR-OPS-06); safe to re-run.
        public const string Backfill =
            "UPDATE hub.outbox_event SET dead_lettered_at = now() WHERE processed_at IS NULL AND attempts >= 10 AND dead_lettered_at IS NULL";

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "dead_lettered_at",
                schema: "hub",
                table: "outbox_event",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.Sql(Backfill);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "dead_lettered_at",
                schema: "hub",
                table: "outbox_event");
        }
    }
}
