using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrailHub.Trips.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SharedGroupTrips : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                schema: "trips",
                table: "Trips",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AddedBy",
                schema: "trips",
                table: "ChecklistItems",
                type: "character varying(128)",
                maxLength: 128,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ClaimedBy",
                schema: "trips",
                table: "ChecklistItems",
                type: "character varying(128)",
                maxLength: 128,
                nullable: true);

            migrationBuilder.AddColumn<List<string>>(
                name: "DoneBy",
                schema: "trips",
                table: "ChecklistItems",
                type: "text[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.AddColumn<string>(
                name: "Kind",
                schema: "trips",
                table: "ChecklistItems",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Personal");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                schema: "trips",
                table: "ChecklistItems",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            // Existing items were the owner's own: they added them, and a tick meant the owner had packed it.
            migrationBuilder.Sql("""
                UPDATE trips."ChecklistItems" AS i
                SET "AddedBy" = t."OwnerId",
                    "DoneBy" = CASE WHEN i."IsDone" THEN ARRAY[t."OwnerId"]::text[] ELSE '{}' END,
                    "IsDone" = false
                FROM trips."Trips" AS t
                WHERE t."Id" = i."TripId";
                """);

            migrationBuilder.CreateTable(
                name: "KnownGroups",
                schema: "trips",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Deleted = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnownGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KnownGroupMembers",
                schema: "trips",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnownGroupMembers", x => new { x.GroupId, x.UserId });
                    table.ForeignKey(
                        name: "FK_KnownGroupMembers_KnownGroups_GroupId",
                        column: x => x.GroupId,
                        principalSchema: "trips",
                        principalTable: "KnownGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Trips_GroupId",
                schema: "trips",
                table: "Trips",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_KnownGroupMembers_UserId",
                schema: "trips",
                table: "KnownGroupMembers",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KnownGroupMembers",
                schema: "trips");

            migrationBuilder.DropTable(
                name: "KnownGroups",
                schema: "trips");

            migrationBuilder.DropIndex(
                name: "IX_Trips_GroupId",
                schema: "trips",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "GroupId",
                schema: "trips",
                table: "Trips");

            migrationBuilder.DropColumn(
                name: "AddedBy",
                schema: "trips",
                table: "ChecklistItems");

            migrationBuilder.DropColumn(
                name: "ClaimedBy",
                schema: "trips",
                table: "ChecklistItems");

            migrationBuilder.DropColumn(
                name: "DoneBy",
                schema: "trips",
                table: "ChecklistItems");

            migrationBuilder.DropColumn(
                name: "Kind",
                schema: "trips",
                table: "ChecklistItems");

            migrationBuilder.DropColumn(
                name: "xmin",
                schema: "trips",
                table: "ChecklistItems");
        }
    }
}
