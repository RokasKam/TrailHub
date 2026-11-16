using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrailHub.Trips.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class GearReminders : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "GearReminderSentAtUtc",
                schema: "trips",
                table: "Trips",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Trips_StartDate",
                schema: "trips",
                table: "Trips",
                column: "StartDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Trips_StartDate",
                schema: "trips",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "GearReminderSentAtUtc",
                schema: "trips",
                table: "Trips");
        }
    }
}
