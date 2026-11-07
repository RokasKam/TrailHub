using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrailHub.Routes.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RouteSharingAndPublishing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "PublishedAtUtc",
                schema: "routes",
                table: "Routes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<List<Guid>>(
                name: "SharedWithGroups",
                schema: "routes",
                table: "Routes",
                type: "uuid[]",
                nullable: false,
                defaultValueSql: "'{}'");

            migrationBuilder.CreateTable(
                name: "KnownGroups",
                schema: "routes",
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
                schema: "routes",
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
                        principalSchema: "routes",
                        principalTable: "KnownGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Routes_SharedWithGroups",
                schema: "routes",
                table: "Routes",
                column: "SharedWithGroups")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_KnownGroupMembers_UserId",
                schema: "routes",
                table: "KnownGroupMembers",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KnownGroupMembers",
                schema: "routes");

            migrationBuilder.DropTable(
                name: "KnownGroups",
                schema: "routes");

            migrationBuilder.DropIndex(
                name: "IX_Routes_SharedWithGroups",
                schema: "routes",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "PublishedAtUtc",
                schema: "routes",
                table: "Routes");

            migrationBuilder.DropColumn(
                name: "SharedWithGroups",
                schema: "routes",
                table: "Routes");
        }
    }
}
