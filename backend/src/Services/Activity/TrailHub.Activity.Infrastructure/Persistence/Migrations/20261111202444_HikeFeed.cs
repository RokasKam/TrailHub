using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TrailHub.Activity.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HikeFeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Caption",
                schema: "activity",
                table: "Activities",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Photos",
                schema: "activity",
                table: "Activities",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]"); // existing hikes have no photos

            migrationBuilder.AddColumn<string>(
                name: "Privacy",
                schema: "activity",
                table: "Activities",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Full");

            migrationBuilder.AddColumn<DateTime>(
                name: "SharedAtUtc",
                schema: "activity",
                table: "Activities",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid[]>(
                name: "SharedGroupIds",
                schema: "activity",
                table: "Activities",
                type: "uuid[]",
                nullable: false,
                defaultValue: new Guid[0]);

            migrationBuilder.CreateTable(
                name: "HikeComments",
                schema: "activity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Text = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HikeComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HikeComments_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalSchema: "activity",
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HikeLikes",
                schema: "activity",
                columns: table => new
                {
                    ActivityId = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HikeLikes", x => new { x.ActivityId, x.UserId });
                    table.ForeignKey(
                        name: "FK_HikeLikes_Activities_ActivityId",
                        column: x => x.ActivityId,
                        principalSchema: "activity",
                        principalTable: "Activities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KnownGroups",
                schema: "activity",
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
                schema: "activity",
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
                        principalSchema: "activity",
                        principalTable: "KnownGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Activities_SharedAtUtc",
                schema: "activity",
                table: "Activities",
                column: "SharedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Activities_SharedGroupIds",
                schema: "activity",
                table: "Activities",
                column: "SharedGroupIds")
                .Annotation("Npgsql:IndexMethod", "gin");

            migrationBuilder.CreateIndex(
                name: "IX_HikeComments_ActivityId",
                schema: "activity",
                table: "HikeComments",
                column: "ActivityId");

            migrationBuilder.CreateIndex(
                name: "IX_KnownGroupMembers_UserId",
                schema: "activity",
                table: "KnownGroupMembers",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HikeComments",
                schema: "activity");

            migrationBuilder.DropTable(
                name: "HikeLikes",
                schema: "activity");

            migrationBuilder.DropTable(
                name: "KnownGroupMembers",
                schema: "activity");

            migrationBuilder.DropTable(
                name: "KnownGroups",
                schema: "activity");

            migrationBuilder.DropIndex(
                name: "IX_Activities_SharedAtUtc",
                schema: "activity",
                table: "Activities");

            migrationBuilder.DropIndex(
                name: "IX_Activities_SharedGroupIds",
                schema: "activity",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "Caption",
                schema: "activity",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "Photos",
                schema: "activity",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "Privacy",
                schema: "activity",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "SharedAtUtc",
                schema: "activity",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "SharedGroupIds",
                schema: "activity",
                table: "Activities");
        }
    }
}
